// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using VimClient;
using TopoMojo.Hypervisor.Extensions;

namespace TopoMojo.Hypervisor.vSphere
{
    public class NsxNetworkManager : NetworkManager
    {
        public NsxNetworkManager(
            ILogger logger,
            VimReferences settings,
            ConcurrentDictionary<string, Vm> vmCache,
            VlanManager vlanManager,
            SddcConfiguration sddcConfig
        ) : base(logger, settings, vmCache, vlanManager)
        {
            _config = sddcConfig;
            _apiUrl = _config.ApiUrl;
            _apiSegments = _config.SegmentApiPath;
        }

        private readonly SddcConfiguration _config;
        private HttpClient _sddc;
        private DateTimeOffset authExpiration = DateTimeOffset.MinValue;
        private string _apiUrl;
        private string _apiSegments;
        // private string authToken = "";

        private async Task InitClient()
        {

            if (DateTimeOffset.UtcNow.CompareTo(authExpiration) < 0)
                return;

            if (
                string.IsNullOrEmpty(_config.ApiKey).Equals(false) &&
                string.IsNullOrEmpty(_config.AuthUrl).Equals(false)
            )
            {
                await InitClientViaRest();
                return;
            }

            if (
                string.IsNullOrEmpty(_config.CertificatePath).Equals(false) &&
                File.Exists(_config.CertificatePath)
            ) {
                InitClientWithCertificate();
                return;
            }

            throw new Exception("No NSX-T Auth mechanism configured.");
        }

        private void InitClientWithCertificate()
        {
            _logger.LogDebug($"NSX auth with certificate {_config.CertificatePath}");

            var clientcert = new X509Certificate2(
                _config.CertificatePath,
                _config.CertificatePassword,
                X509KeyStorageFlags.MachineKeySet |
                X509KeyStorageFlags.PersistKeySet |
                X509KeyStorageFlags.Exportable
            );

            var handler = new HttpClientHandler();

            handler.ClientCertificates.Add(clientcert);

            _sddc = new HttpClient(handler);

            authExpiration = clientcert.NotAfter;

        }

        private async Task InitClientViaRest()
        {
            _logger.LogDebug($"NSX auth with rest to {_config.AuthUrl}");

            if (DateTimeOffset.UtcNow.CompareTo(authExpiration) < 0)
                return;

            _sddc = new HttpClient();

            var content = new FormUrlEncodedContent(
                new KeyValuePair<string,string>[] {
                    new KeyValuePair<string,string>(
                        "refresh_token",
                        _config.ApiKey
                    )
                }
            );

            var response = await _sddc.PostAsync(
                _config.AuthUrl,
                content
            );

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException("SDDC login failed.");

            string data = await response.Content.ReadAsStringAsync();
            var auth = JsonSerializer.Deserialize<AuthResponse>(data);

            authExpiration = DateTimeOffset.UtcNow.AddSeconds(auth.expires_in);

            _sddc.DefaultRequestHeaders.Add(_config.AuthTokenHeader, auth.access_token);

            if (!string.IsNullOrEmpty(_config.MetadataUrl))
            {
                string meta = await _sddc.GetStringAsync(_config.MetadataUrl);

                var sddc = JsonSerializer.Deserialize<SddcResponse>(meta);

                _apiUrl = sddc.resource_config.nsx_api_public_endpoint_url;
            }
        }

        public override async Task<PortGroupAllocation> AddPortGroup(string sw, VmNet eth)
        {
            int resolution_delay_ms = 1500;

            // ensure api client is ready
            await InitClient();

            string url = $"{_apiUrl}/{_apiSegments}/{eth.Net.Replace("#","%23")}";

            // fire api to create net
            var response = await _sddc.PutAsync(
                url,
                new StringContent(
                    "{\"advanced_config\": { \"connectivity\": \"OFF\" } }",
                    Encoding.UTF8,
                    "application/json"
                )
            );

            int count = 0;
            PortGroupAllocation pga = null;

            if (response.IsSuccessStatusCode)
            {
                // if create was successful, poll until we get the new network key
                while (pga == null && count < 15)
                {
                    // slight delay
                    await Task.Delay(resolution_delay_ms);

                    count += 1;

                    // TODO: fetch single portgroup
                    pga = (await LoadPortGroups())
                        .FirstOrDefault(p => p.Net == eth.Net);
                }

            }
            else
            {
                // create failed; see if the network exists
                // TODO: fetch single portgroup
                    pga = (await LoadPortGroups())
                        .FirstOrDefault(p => p.Net == eth.Net);
            }

            // without the portgroup the vm will fail so might as well throw here
            if (pga == null)
                throw new Exception($"Failed to create net {eth.Net}");

            // we created or found the network; track how long it took
            _logger.LogDebug("Resolving created network took {}s", count * resolution_delay_ms);

            return pga;

        }

        public override Task AddSwitch(string sw)
        {
            return Task.FromResult(0);
        }

        public override async Task<VmNetwork[]> GetVmNetworks(ManagedObjectReference mor)
        {
            var result = new List<VmNetwork>();
            RetrievePropertiesResponse response = await _client.vim.RetrievePropertiesAsync(
                _client.props,
                FilterFactory.VmFilter(mor, "name config"));
            ObjectContent[] oc = response.returnval;

            foreach (ObjectContent obj in oc)
            {
                string vmName = obj.GetProperty("name").ToString();

                if (!IsTenantVm(vmName))
                    continue;

                VirtualMachineConfigInfo config = obj.GetProperty("config") as VirtualMachineConfigInfo;

                foreach (VirtualEthernetCard card in config.hardware.device.OfType<VirtualEthernetCard>())
                {
                    if (card.backing is VirtualEthernetCardDistributedVirtualPortBackingInfo)
                    {
                        var back = card.backing as VirtualEthernetCardDistributedVirtualPortBackingInfo;

                        result.Add(new VmNetwork
                        {
                            NetworkMOR = $"DistributedVirtualPortgroup|{back.port.portgroupKey}",
                            VmName = vmName
                        });
                    }
                }

            }

            return result.ToArray();
        }

        public override async Task<PortGroupAllocation[]> LoadPortGroups()
        {
            var list = new List<PortGroupAllocation>();

            RetrievePropertiesResponse response = await _client.vim.RetrievePropertiesAsync(
                _client.props,
                FilterFactory.DistributedPortgroupFilter(_client.cluster));

            ObjectContent[] clunkyTree = response.returnval;
            foreach (var dvpg in clunkyTree.FindType("DistributedVirtualPortgroup"))
            {
                var config = (DVPortgroupConfigInfo)dvpg.GetProperty("config");
                if (config.distributedVirtualSwitch.Value == _client.dvs.Value)
                {
                    string net = dvpg.GetProperty("name") as string;

                    if (!IsTenantNet(net))
                        continue;

                    if (
                        config.defaultPortConfig is VMwareDVSPortSetting setting
                        && setting.vlan is VmwareDistributedVirtualSwitchVlanIdSpec
                    )
                    {
                        list.Add(
                            new PortGroupAllocation
                            {
                                Net = net,
                                Key = dvpg.obj.AsString(),
                                Switch = _client.UplinkSwitch
                            }
                        );
                    }
                }
            }

            return list.ToArray();
        }

        public override async Task<bool> RemovePortgroup(string pgReference)
        {
            try
            {
                var pga = _pgAllocation.Values.FirstOrDefault(v => v.Key == pgReference);

                if (pga == null || !pga.Net.Contains("#"))
                    return false;

                await InitClient();

                var response = await _sddc.DeleteAsync(
                    $"{_apiUrl}/{_apiSegments}/{pga.Net.Replace("#","%23")}"
                );

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"error removing net: {pga.Net} {response.StatusCode} {response.ReasonPhrase} {await response.Content.ReadAsStringAsync()}");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"error removing net: {ex.Message}");
            }
            return false;
        }

        public override Task RemoveSwitch(string sw)
        {
            return Task.FromResult(0);
        }

        public override void UpdateEthernetCardBacking(VirtualEthernetCard card, string portgroupName)
        {
            if (card != null)
            {
                if (card.backing is VirtualEthernetCardDistributedVirtualPortBackingInfo)
                {
                    string netMorName = this.Resolve(portgroupName);

                    card.backing = new VirtualEthernetCardDistributedVirtualPortBackingInfo
                    {
                        port = new DistributedVirtualSwitchPortConnection
                        {
                            switchUuid = _client.DvsUuid,
                            portgroupKey = netMorName.AsReference().Value
                        }
                    };
                }

                card.connectable = new VirtualDeviceConnectInfo()
                {
                    connected = true,
                    startConnected = true,
                };
            }
        }

        internal class AuthResponse
        {
            public string access_token { get; set; }
            public int expires_in { get; set; }
        }

        internal class SddcResponse
        {
            public SddcResourceConfig resource_config { get; set; }
        }

        internal class SddcResourceConfig
        {
            public string nsx_api_public_endpoint_url { get; set; }
        }
    }
}
