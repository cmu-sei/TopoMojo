// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
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
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using VimClient;
using TopoMojo.Hypervisor.Extensions;
using System.Text.Json.Serialization;

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
        private readonly string _apiSegments;

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
            )
            {
                InitClientWithCertificate();
                return;
            }

            throw new Exception("No NSX-T Auth mechanism configured.");
        }

        private void InitClientWithCertificate()
        {
            _logger.LogDebug("NSX auth with certificate {path}", _config.CertificatePath);

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
            _logger.LogDebug("NSX auth with rest to {url}", _config.AuthUrl);

            if (DateTimeOffset.UtcNow.CompareTo(authExpiration) < 0)
                return;

            _sddc = new HttpClient();

            var content = new FormUrlEncodedContent(
                new KeyValuePair<string, string>[] {
                    new("refresh_token",_config.ApiKey)
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

            authExpiration = DateTimeOffset.UtcNow.AddSeconds(auth.ExpiresIn);

            _sddc.DefaultRequestHeaders.Add(_config.AuthTokenHeader, auth.AccessToken);

            if (!string.IsNullOrEmpty(_config.MetadataUrl))
            {
                string meta = await _sddc.GetStringAsync(_config.MetadataUrl);

                var sddc = JsonSerializer.Deserialize<SddcResponse>(meta);

                _apiUrl = sddc.ResourceConfig.NsxApiPublicEndpointUrl;
            }
        }

        public override async Task<PortGroupAllocation> AddPortGroup(string sw, VmNet eth)
        {
            await InitClient();

            string url = $"{_apiUrl}/{_apiSegments}/{eth.Net.Replace("#", "%23")}";

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

            while (response.IsSuccessStatusCode && pga == null && count < 10)
            {
                // slight delay
                await Task.Delay(1500);

                count += 1;

                // TODO: fetch single portgroup
                pga = (await LoadPortGroups())
                    .FirstOrDefault(p => p.Net == eth.Net);

            }

            if (pga == null)
                throw new Exception($"Failed to create net {eth.Net}");

            return pga;

        }

        public override async Task<PortGroupAllocation[]> AddPortGroups(string sw, VmNet[] eths)
        {
            if (eths.Length == 0)
                return [];

            await InitClient();

            string tag = eths[0].Net.Tag();
            var names = new List<string>();

            foreach (var eth in eths)
            {
                string url = $"{_apiUrl}/{_apiSegments}/{eth.Net.Replace("#", "%23")}";

                var response = await _sddc.PutAsync(
                    url,
                    new StringContent(
                        "{\"advanced_config\": { \"connectivity\": \"OFF\" } }",
                        Encoding.UTF8,
                        "application/json"
                    )
                );

                if (response.IsSuccessStatusCode)
                    names.Add(eth.Net);
                else
                    _logger.LogDebug("Failed to add SDDC PortGroup {net} {reason}", eth.Net, response.ReasonPhrase);

                await Task.Delay(100);
            }

            int count = 10;
            bool complete = false;
            PortGroupAllocation[] pgas = [];
            do
            {
                await Task.Delay(1500);
                _logger.LogDebug("SDDC resolving new portgroups [{count}]", count);
                pgas = (await LoadPortGroups())
                    .Where(p => names.Contains(p.Net))
                    .ToArray()
                ;
                complete = pgas.Length == names.Count;
                count -= 1;
            } while (count > 0 && !complete);

            if (!complete)
                throw new Exception($"Failed to create net(s) for {tag}");

            return pgas;
        }

        public override Task AddSwitch(string sw)
        {
            return Task.FromResult(0);
        }

        public override async Task<VmNetwork[]> GetVmNetworks(ManagedObjectReference mor)
        {
            var result = new List<VmNetwork>();
            RetrievePropertiesResponse response = await _client.Vim.RetrievePropertiesAsync(
                _client.Props,
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

            return [.. result];
        }

        public override async Task<PortGroupAllocation[]> LoadPortGroups()
        {
            var list = new List<PortGroupAllocation>();

            RetrievePropertiesResponse response = await _client.Vim.RetrievePropertiesAsync(
                _client.Props,
                FilterFactory.DistributedPortgroupFilter(_client.Cluster));

            ObjectContent[] clunkyTree = response.returnval;
            foreach (var dvpg in clunkyTree.FindType("DistributedVirtualPortgroup"))
            {
                var config = (DVPortgroupConfigInfo)dvpg.GetProperty("config");
                if (config.distributedVirtualSwitch.Value == _client.Dvs.Value)
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

            string info = string.Join('\n', [.. list.Select(p => $"{p.Net}::{p.Key}")]);
            _logger.LogDebug("{info}", info);

            return [.. list];
        }

        public override async Task<PortGroupAllocation[]> RemovePortgroups(PortGroupAllocation[] pgs)
        {
            await InitClient();

            // remove all
            var tasks = pgs.Select(p => _sddc.DeleteAsync($"{_apiUrl}/{_apiSegments}/{p.Net.Replace("#", "%23")}")).ToArray();
            Task.WaitAll(tasks);

            // verify deletion
            await Task.Delay(2000);
            var existing = await LoadPortGroups();
            return pgs.ExceptBy(existing.Select(e => e.Net), p => p.Net).ToArray();
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
            [JsonPropertyName("access_token")] public string AccessToken { get; set; }
            [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
        }

        internal class SddcResponse
        {
            [JsonPropertyName("resource_config")] public SddcResourceConfig ResourceConfig { get; set; }
        }

        internal class SddcResourceConfig
        {
            [JsonPropertyName("nsx_api_public_endpoint_url")] public string NsxApiPublicEndpointUrl { get; set; }
        }
    }
}
