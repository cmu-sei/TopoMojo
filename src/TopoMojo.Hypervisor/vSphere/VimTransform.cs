// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using VimClient;
using TopoMojo.Hypervisor.Extensions;

namespace TopoMojo.Hypervisor.vSphere
{
    public static class Transform
    {

        public static VirtualMachineConfigSpec TemplateToVmSpec(VmTemplate template, string datastore, string dvsuuid)
        {
            int key = -101, idekey = 200;
            VirtualMachineConfigSpec vmcs = new();
            List<VirtualDeviceConfigSpec> devices = [];

            vmcs.name = template.Name;
            vmcs.extraConfig = GetExtraConfig(template);
            vmcs.AddRam(template.Ram);
            vmcs.AddCpu(template.Cpu);
            vmcs.AddBootOption(Math.Max(template.Delay, 10));
            vmcs.version = template.Version.HasValue() ? template.Version : null;
            vmcs.guestId = template.Guest.HasValue() ? template.Guest : "other";
            if (!vmcs.guestId.EndsWith("Guest")) vmcs.guestId += "Guest";
            if (datastore.HasValue())
            {
                vmcs.files = new VirtualMachineFileInfo { vmPathName = $"{datastore}/{template.Name}/{template.Name}.vmx" };
            }

            //can't actually be applied via ExtraConfig on 6.7
            if (template.GuestSettings.Any(c => c.Key == "firmware" && c.Value == "efi"))
                vmcs.firmware = "efi";

            //can't actually be applied via ExtraConfig
            if (template.GuestSettings.Length > 0
                && template.GuestSettings.Any(s => s.Key.Contains("vhv.enable") && s.Value.Equals("true", StringComparison.CurrentCultureIgnoreCase)))
            {
                vmcs.nestedHVEnabled = true;
                vmcs.nestedHVEnabledSpecified = true;
            }

            //video card
            devices.Add(GetVideoController(ref key, template.VideoRam));

            //floppy disk
            if (template.Floppy.HasValue())
                devices.Add(GetFloppy(ref key, template.Floppy));

            //nics
            foreach (VmNet nic in template.Eth)
                devices.Add(GetEthernetAdapter(ref key, nic, dvsuuid));

            // //network serial port
            // if (!String.IsNullOrEmpty(template.FindOne("nsp").Value()))
            //     devices.Add(GetNetworkSerialPort(ref key, template.FindOne("nsp").Value()));

            //controller
            int controllerKey = 0, count = 0;
            foreach (VmDisk disk in template.Disks)
            {
                if (controllerKey == 0)
                {
                    if (disk.Controller == "ide")
                    {
                        controllerKey = idekey;
                    }
                    else
                    {
                        VirtualDeviceConfigSpec controller = GetDiskController(ref key, disk.Controller);
                        controllerKey = controller.device.key;
                        devices.Add(controller);
                    }
                }
                devices.Add(GetDisk(ref key, disk.Path, controllerKey, count++));
            }


            //iso
            devices.Add(GetCdrom(ref key, idekey, template.Iso.HasValue() ? template.Iso : "[iso] null.iso"));

            //add all devices to spec
            vmcs.deviceChange = [.. devices];

            return vmcs;
        }

        private static VirtualDeviceConfigSpec GetCdrom(ref int key, int controllerkey, string iso)
        {
            // CD ROM
            VirtualCdrom cdrom = new()
            {
                key = key--
            };

            VirtualCdromIsoBackingInfo isobacking = new()
            {
                fileName = iso
            };
            cdrom.backing = isobacking;

            cdrom.controllerKey = controllerkey;
            cdrom.controllerKeySpecified = true;
            cdrom.connectable = new()
            {
                startConnected = true
            };

            return new()
            {
                device = cdrom,
                operation = VirtualDeviceConfigSpecOperation.add,
                operationSpecified = true
            };
        }

        private static VirtualDeviceConfigSpec GetEthernetAdapter(ref int key, VmNet nic, string dvsuuid)
        {
            VirtualEthernetCard eth = new VirtualE1000();

            if (nic.Type == "pcnet32")
                eth = new VirtualPCNet32();

            if (nic.Type == "vmx3")
                eth = new VirtualVmxnet3();

            if (nic.Type == "e1000e")
                eth = new VirtualE1000e();

            eth.key = key--;

            if (nic.Mac.HasValue())
            {
                eth.addressType = "Manual";
                eth.macAddress = nic.Mac;
            }

            if (nic.Net.StartsWith("nsx."))
            {
                eth.backing = new VirtualEthernetCardOpaqueNetworkBackingInfo
                {
                    opaqueNetworkId = nic.Key.Tag(),
                    opaqueNetworkType = nic.Key.Untagged()
                };
            }
            else if (dvsuuid.HasValue())
            {
                eth.backing = new VirtualEthernetCardDistributedVirtualPortBackingInfo
                {
                    port = new DistributedVirtualSwitchPortConnection
                    {
                        switchUuid = dvsuuid,
                        portgroupKey = nic.Key.AsReference().Value
                    }
                };
            }
            else
            {
                eth.backing = new VirtualEthernetCardNetworkBackingInfo { deviceName = nic.Key };
            }

            return new()
            {
                device = eth,
                operation = VirtualDeviceConfigSpecOperation.add,
                operationSpecified = true
            };
        }

        private static VirtualDeviceConfigSpec GetDisk(ref int key, string path, int controllerKey, int unitnumber)
        {

            VirtualDiskFlatVer2BackingInfo diskbacking = new()
            {
                fileName = path,
                diskMode = "persistent"
            };

            VirtualDisk disk = new()
            {
                key = key--,
                backing = diskbacking,
                controllerKey = controllerKey,
                controllerKeySpecified = true,
                connectable = new()
            };
            disk.connectable.connected = true;
            disk.connectable.startConnected = true;
            disk.unitNumber = unitnumber;
            disk.unitNumberSpecified = true;

            return new()
            {
                device = disk,
                operation = VirtualDeviceConfigSpecOperation.add,
                operationSpecified = true
            };
        }

        private static VirtualDeviceConfigSpec GetDiskController(ref int key, string type)
        {
            VirtualDevice device = null;

            // DISK CONTROLLER
            if (type.ToLower().EndsWith("sas"))
            {
                device = new VirtualLsiLogicSASController
                {
                    busNumber = 0,
                    sharedBus = VirtualSCSISharing.noSharing,
                    key = key--
                };
            }

            if (type.Equals("lsilogic", StringComparison.CurrentCultureIgnoreCase))
            {
                device = new VirtualLsiLogicController()
                {
                    busNumber = 0,
                    sharedBus = VirtualSCSISharing.noSharing,
                    key = key--
                };
            }

            if (type.Equals("buslogic", StringComparison.CurrentCultureIgnoreCase))
            {
                device = new VirtualBusLogicController
                {
                    busNumber = 0,
                    sharedBus = VirtualSCSISharing.noSharing,
                    controllerKey = key--
                };
            }

            if (type.Equals("pvscsi", StringComparison.CurrentCultureIgnoreCase))
            {
                device = new ParaVirtualSCSIController
                {
                    busNumber = 0,
                    sharedBus = VirtualSCSISharing.noSharing,
                    controllerKey = key--
                };
            }

            if (type.Equals("nvme", StringComparison.CurrentCultureIgnoreCase))
            {
                device = new VirtualNVMEController
                {
                    busNumber = 0,
                    controllerKey = key--
                };
            }

            return new()
            {
                device = device,
                operation = VirtualDeviceConfigSpecOperation.add,
                operationSpecified = true
            };
        }

        private static VirtualDeviceConfigSpec GetFloppy(ref int key, string name)
        {

            VirtualFloppyImageBackingInfo diskbacking = new()
            {
                fileName = name
            };

            VirtualFloppy disk = new()
            {
                key = key--,
                backing = diskbacking,
                connectable = new VirtualDeviceConnectInfo()
            };
            disk.connectable.connected = true;
            disk.connectable.startConnected = true;
            //disk.unitNumber = unitnumber;
            //disk.unitNumberSpecified = true;

            return new()
            {
                device = disk,
                operation = VirtualDeviceConfigSpecOperation.add,
                operationSpecified = true
            };
        }

        public static OptionValue[] GetExtraConfig(VmTemplate template)
        {
            List<OptionValue> options =
            [
                new() { key = "snapshot.redoNotWithParent", value = "true" },
                new() { key = "isolation.tools.setGUIOptions.enable", value = "true" },
                new() { key = "isolation.tools.copy.disable", value = "false" },
                new() { key = "isolation.tools.paste.disable", value = "false" },
                new() { key = "keyboard.typematicMinDelay", value = "2000000" },
                new() { key = "guestinfo.isolationTag", value = template.IsolationTag },
                new() { key = "guestinfo.templateSource", value = template.Id },
                new() { key = "guestinfo.hostname", value = template.Name.Untagged() },
            ];

            foreach (var setting in template.GuestSettings)
            {
                // TODO: rework this quick fix for injecting isolation specific settings
                if (setting.Key.StartsWith("iftag.") && !setting.Value.Contains(template.IsolationTag))
                {
                    continue;
                }

                var option = new OptionValue { key = setting.Key, value = setting.Value };

                option.key = option.key.Replace("iftag.", "guestinfo.");

                options.Add(option);
            }

            return [.. options];
        }

        private static VirtualDeviceConfigSpec GetVideoController(ref int key, int ramKB)
        {
            VirtualMachineVideoCard card = new()
            {
                key = key--
            };
            if (ramKB > 0)
            {
                card.videoRamSizeInKB = ramKB * 1024;
                card.videoRamSizeInKBSpecified = true;
            }
            else
            {
                card.useAutoDetect = true;
                card.useAutoDetectSpecified = true;
            }

            return new()
            {
                device = card,
                operation = VirtualDeviceConfigSpecOperation.add,
                operationSpecified = true
            };
        }

        public static HostNetworkPolicy GetDefaultHostNetworkPolicy()
        {
            return new()
            {
                security = new HostNetworkSecurityPolicy
                {
                    allowPromiscuous = true,
                    allowPromiscuousSpecified = true,
                    macChanges = true,
                    macChangesSpecified = true,
                    forgedTransmits = true,
                    forgedTransmitsSpecified = true
                },

                nicTeaming = new HostNicTeamingPolicy
                {
                    policy = "loadbalance_srcid",
                    reversePolicy = true,
                    notifySwitches = false,
                    rollingOrder = false,
                    reversePolicySpecified = true,
                    notifySwitchesSpecified = true,
                    rollingOrderSpecified = true,
                    failureCriteria = new HostNicFailureCriteria
                    {
                        checkBeacon = false,
                        checkDuplex = false,
                        checkSpeed = "minimum",
                        speed = 10,
                        fullDuplex = false,
                        checkErrorPercent = false,
                        percentage = 0,
                        checkBeaconSpecified = true,
                        checkDuplexSpecified = true,
                        speedSpecified = true,
                        fullDuplexSpecified = true,
                        checkErrorPercentSpecified = true,
                        percentageSpecified = true
                    }
                },

                offloadPolicy = new HostNetOffloadCapabilities
                {
                    csumOffload = true,
                    tcpSegmentation = true,
                    zeroCopyXmit = true,
                    csumOffloadSpecified = true,
                    tcpSegmentationSpecified = true,
                    zeroCopyXmitSpecified = true
                },

                shapingPolicy = new HostNetworkTrafficShapingPolicy
                {
                    enabled = false,
                    enabledSpecified = true
                }
            };
        }

        internal static string[] OsMap = [
            "rhel6_64",
            "rhel6",
            "rhel7_64",
            "rhel7",
            "dos",
            "solaris9",
            "ubuntu64",
            "ubuntu",
            "vmkernel5",
            "vmkernel6",
            "windows7_64",
            "windows7",
            "windows7Server64",
            "windows8_64",
            "windows8Server64",
            "windows9_64",
            "windows9Server64",
            "windowsHyperV",
        ];

    }
}


// asianux3_64Guest	Asianux Server 3 (64 bit) Since vSphere API 4.0
// asianux3Guest	Asianux Server 3 Since vSphere API 4.0
// asianux4_64Guest	Asianux Server 4 (64 bit) Since vSphere API 4.0
// asianux4Guest	Asianux Server 4 Since vSphere API 4.0
// asianux5_64Guest	Asianux Server 5 (64 bit) Since vSphere API 6.0
// centos64Guest	CentOS 4/5 (64-bit) Since vSphere API 4.1
// centosGuest	CentOS 4/5 Since vSphere API 4.1
// coreos64Guest	CoreOS Linux (64 bit) Since vSphere API 6.0
// darwin10_64Guest	Mac OS 10.6 (64 bit) Since vSphere API 5.0
// darwin10Guest	Mac OS 10.6 Since vSphere API 5.0
// darwin11_64Guest	Mac OS 10.7 (64 bit) Since vSphere API 5.0
// darwin11Guest	Mac OS 10.7 Since vSphere API 5.0
// darwin12_64Guest	Mac OS 10.8 (64 bit) Since vSphere API 5.5
// darwin13_64Guest	Mac OS 10.9 (64 bit) Since vSphere API 5.5
// darwin14_64Guest	Mac OS 10.10 (64 bit) Since vSphere API 6.0
// darwin64Guest	Mac OS 10.5 (64 bit) Since vSphere API 4.0
// darwinGuest	Mac OS 10.5
// debian4_64Guest	Debian GNU/Linux 4 (64 bit) Since vSphere API 4.0
// debian4Guest	Debian GNU/Linux 4 Since vSphere API 4.0
// debian5_64Guest	Debian GNU/Linux 5 (64 bit) Since vSphere API 4.0
// debian5Guest	Debian GNU/Linux 5 Since vSphere API 4.0
// debian6_64Guest	Debian GNU/Linux 6 (64 bit) Since vSphere API 5.0
// debian6Guest	Debian GNU/Linux 6 Since vSphere API 5.0
// debian7_64Guest	Debian GNU/Linux 7 (64 bit) Since vSphere API 5.5
// debian7Guest	Debian GNU/Linux 7 Since vSphere API 5.5
// debian8_64Guest	Debian GNU/Linux 8 (64 bit) Since vSphere API 6.0
// debian8Guest	Debian GNU/Linux 8 Since vSphere API 6.0
// dosGuest	MS-DOS.
// eComStation2Guest	eComStation 2.0 Since vSphere API 5.0
// eComStationGuest	eComStation 1.x Since vSphere API 4.1
// fedora64Guest	Fedora Linux (64 bit) Since vSphere API 5.1
// fedoraGuest	Fedora Linux Since vSphere API 5.1
// freebsd64Guest	FreeBSD x64
// freebsdGuest	FreeBSD
// genericLinuxGuest	Other Linux Since vSphere API 5.5
// mandrakeGuest	Mandrake Linux Since vSphere API 5.5
// mandriva64Guest	Mandriva Linux (64 bit) Since vSphere API 4.0
// mandrivaGuest	Mandriva Linux Since vSphere API 4.0
// netware4Guest	Novell NetWare 4
// netware5Guest	Novell NetWare 5.1
// netware6Guest	Novell NetWare 6.x
// nld9Guest	Novell Linux Desktop 9
// oesGuest	Open Enterprise Server
// openServer5Guest	SCO OpenServer 5 Since vSphere API 4.0
// openServer6Guest	SCO OpenServer 6 Since vSphere API 4.0
// opensuse64Guest	OpenSUSE Linux (64 bit) Since vSphere API 5.1
// opensuseGuest	OpenSUSE Linux Since vSphere API 5.1
// oracleLinux64Guest	Oracle Linux 4/5 (64-bit) Since vSphere API 4.1
// oracleLinuxGuest	Oracle Linux 4/5 Since vSphere API 4.1
// os2Guest	OS/2
// other24xLinux64Guest	Linux 2.4x Kernel (64 bit) (experimental)
// other24xLinuxGuest	Linux 2.4x Kernel
// other26xLinux64Guest	Linux 2.6x Kernel (64 bit) (experimental)
// other26xLinuxGuest	Linux 2.6x Kernel
// other3xLinux64Guest	Linux 3.x Kernel (64 bit) Since vSphere API 5.5
// other3xLinuxGuest	Linux 3.x Kernel Since vSphere API 5.5
// otherGuest	Other Operating System
// otherGuest64	Other Operating System (64 bit) (experimental)
// otherLinux64Guest	Linux (64 bit) (experimental)
// otherLinuxGuest	Linux 2.2x Kernel
// redhatGuest	Red Hat Linux 2.1
// rhel2Guest	Red Hat Enterprise Linux 2
// rhel3_64Guest	Red Hat Enterprise Linux 3 (64 bit)
// rhel3Guest	Red Hat Enterprise Linux 3
// rhel4_64Guest	Red Hat Enterprise Linux 4 (64 bit)
// rhel4Guest	Red Hat Enterprise Linux 4
// rhel5_64Guest	Red Hat Enterprise Linux 5 (64 bit) (experimental) Since VI API 2.5
// rhel5Guest	Red Hat Enterprise Linux 5 Since VI API 2.5
// rhel6_64Guest	Red Hat Enterprise Linux 6 (64 bit) Since vSphere API 4.0
// rhel6Guest	Red Hat Enterprise Linux 6 Since vSphere API 4.0
// rhel7_64Guest	Red Hat Enterprise Linux 7 (64 bit) Since vSphere API 5.5
// rhel7Guest	Red Hat Enterprise Linux 7 Since vSphere API 5.5
// sjdsGuest	Sun Java Desktop System
// sles10_64Guest	Suse Linux Enterprise Server 10 (64 bit) (experimental) Since VI API 2.5
// sles10Guest	Suse linux Enterprise Server 10 Since VI API 2.5
// sles11_64Guest	Suse Linux Enterprise Server 11 (64 bit) Since vSphere API 4.0
// sles11Guest	Suse linux Enterprise Server 11 Since vSphere API 4.0
// sles12_64Guest	Suse Linux Enterprise Server 12 (64 bit) Since vSphere API 5.5
// sles12Guest	Suse linux Enterprise Server 12 Since vSphere API 5.5
// sles64Guest	Suse Linux Enterprise Server 9 (64 bit)
// slesGuest	Suse Linux Enterprise Server 9
// solaris10_64Guest	Solaris 10 (64 bit) (experimental)
// solaris10Guest	Solaris 10 (32 bit) (experimental)
// solaris11_64Guest	Solaris 11 (64 bit) Since vSphere API 5.0
// solaris6Guest	Solaris 6
// solaris7Guest	Solaris 7
// solaris8Guest	Solaris 8
// solaris9Guest	Solaris 9
// suse64Guest	Suse Linux (64 bit)
// suseGuest	Suse Linux
// turboLinux64Guest	Turbolinux (64 bit) Since vSphere API 4.0
// turboLinuxGuest	Turbolinux
// ubuntu64Guest	Ubuntu Linux (64 bit)
// ubuntuGuest	Ubuntu Linux
// unixWare7Guest	SCO UnixWare 7 Since vSphere API 4.0
// vmkernel5Guest	VMware ESX 5 Since vSphere API 5.0
// vmkernel6Guest	VMware ESX 6 Since vSphere API 6.0
// vmkernelGuest	VMware ESX 4 Since vSphere API 5.0
// win2000AdvServGuest	Windows 2000 Advanced Server
// win2000ProGuest	Windows 2000 Professional
// win2000ServGuest	Windows 2000 Server
// win31Guest	Windows 3.1
// win95Guest	Windows 95
// win98Guest	Windows 98
// windows7_64Guest	Windows 7 (64 bit) Since vSphere API 4.0
// windows7Guest	Windows 7 Since vSphere API 4.0
// windows7Server64Guest	Windows Server 2008 R2 (64 bit) Since vSphere API 4.0
// windows8_64Guest	Windows 8 (64 bit) Since vSphere API 5.0
// windows8Guest	Windows 8 Since vSphere API 5.0
// windows8Server64Guest	Windows 8 Server (64 bit) Since vSphere API 5.0
// windows9_64Guest	Windows 9 (64 bit) Since vSphere API 6.0
// windows9Guest	Windows 9 Since vSphere API 6.0
// windows9Server64Guest	Windows 9 Server (64 bit) Since vSphere API 6.0
// windowsHyperVGuest	Windows Hyper-V Since vSphere API 5.5
// winLonghorn64Guest	Windows Longhorn (64 bit) (experimental) Since VI API 2.5
// winLonghornGuest	Windows Longhorn (experimental) Since VI API 2.5
// winMeGuest	Windows Millenium Edition
// winNetBusinessGuest	Windows Small Business Server 2003
// winNetDatacenter64Guest	Windows Server 2003, Datacenter Edition (64 bit) (experimental) Since VI API 2.5
// winNetDatacenterGuest	Windows Server 2003, Datacenter Edition Since VI API 2.5
// winNetEnterprise64Guest	Windows Server 2003, Enterprise Edition (64 bit)
// winNetEnterpriseGuest	Windows Server 2003, Enterprise Edition
// winNetStandard64Guest	Windows Server 2003, Standard Edition (64 bit)
// winNetStandardGuest	Windows Server 2003, Standard Edition
// winNetWebGuest	Windows Server 2003, Web Edition
// winNTGuest	Windows NT 4
// winVista64Guest	Windows Vista (64 bit)
// winVistaGuest	Windows Vista
// winXPHomeGuest	Windows XP Home Edition
// winXPPro64Guest	Windows XP Professional Edition (64 bit)
// winXPProGuest	Windows XP Professional
