// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
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
            VirtualMachineConfigSpec vmcs = new VirtualMachineConfigSpec();
            List<VirtualDeviceConfigSpec> devices = new List<VirtualDeviceConfigSpec>();

            vmcs.name = template.Name;
            vmcs.extraConfig = GetExtraConfig(template);
            vmcs.AddRam(template.Ram);
            vmcs.AddCpu(template.Cpu);
            vmcs.AddBootOption(Math.Max(template.Delay, 10));
            vmcs.version = (template.Version.HasValue()) ? template.Version : null;
            vmcs.guestId = (template.Guest.HasValue() ? template.Guest : "other");
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
                && template.GuestSettings.Any(s => s.Key == "vhv.enable" && s.Value == "true"))
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
                        VirtualDeviceConfigSpec controller = GetSCSIController(ref key, disk.Controller);
                        controllerKey = controller.device.key;
                        devices.Add(controller);
                    }
                }
                devices.Add(GetDisk(ref key, disk.Path, controllerKey, count++));
            }


            //iso
            devices.Add(GetCdrom(ref key, idekey, (template.Iso.HasValue() ? template.Iso : "[iso] null.iso")));

            //add all devices to spec
            vmcs.deviceChange = devices.ToArray();

            return vmcs;
        }

        private static VirtualDeviceConfigSpec GetCdrom(ref int key, int controllerkey, string iso)
        {
            VirtualDeviceConfigSpec devicespec = new VirtualDeviceConfigSpec();

            // CD ROM
            VirtualCdrom cdrom = new VirtualCdrom();
            cdrom.key = key--;

            VirtualCdromIsoBackingInfo isobacking = new VirtualCdromIsoBackingInfo();
            isobacking.fileName = iso;
            cdrom.backing = isobacking;

            cdrom.controllerKey = controllerkey;
            cdrom.controllerKeySpecified = true;
            cdrom.connectable = new VirtualDeviceConnectInfo();
            cdrom.connectable.startConnected = true;

            devicespec = new VirtualDeviceConfigSpec();
            devicespec.device = cdrom;
            devicespec.operation = VirtualDeviceConfigSpecOperation.add;
            devicespec.operationSpecified = true;

            return devicespec;

        }

        private static VirtualDeviceConfigSpec GetEthernetAdapter(ref int key, VmNet nic, string dvsuuid)
        {
            VirtualDeviceConfigSpec devicespec = new VirtualDeviceConfigSpec();
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
                eth.backing = new VirtualEthernetCardOpaqueNetworkBackingInfo {
                    opaqueNetworkId = nic.Key.Tag(),
                    opaqueNetworkType = nic.Key.Untagged()
                };
            }
            else if (dvsuuid.HasValue())
            {
                eth.backing = new VirtualEthernetCardDistributedVirtualPortBackingInfo {
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

            devicespec = new VirtualDeviceConfigSpec();
            devicespec.device = eth;
            devicespec.operation = VirtualDeviceConfigSpecOperation.add;
            devicespec.operationSpecified = true;

            return devicespec;
        }

        private static VirtualDeviceConfigSpec GetDisk(ref int key, string path, int controllerKey, int unitnumber)
        {
            VirtualDeviceConfigSpec devicespec = new VirtualDeviceConfigSpec();

            VirtualDiskFlatVer2BackingInfo diskbacking = new VirtualDiskFlatVer2BackingInfo();
            diskbacking.fileName = path;
            diskbacking.diskMode = "persistent";

            VirtualDisk disk = new VirtualDisk();
            disk.key = key--;
            disk.backing = diskbacking;
            disk.controllerKey = controllerKey;
            disk.controllerKeySpecified = true;
            disk.connectable = new VirtualDeviceConnectInfo();
            disk.connectable.connected = true;
            disk.connectable.startConnected = true;
            disk.unitNumber = unitnumber;
            disk.unitNumberSpecified = true;

            devicespec = new VirtualDeviceConfigSpec();
            devicespec.device = disk;
            devicespec.operation = VirtualDeviceConfigSpecOperation.add;
            devicespec.operationSpecified = true;

            return devicespec;
        }

        // private static VirtualDeviceConfigSpec GetIDEController(int key)
        // {
        //     VirtualDeviceConfigSpec devicespec = new VirtualDeviceConfigSpec();
        //     devicespec.device = new VirtualIDEController();
        //     devicespec.device.key = key;
        //     devicespec.operation = VirtualDeviceConfigSpecOperation.add;
        //     devicespec.operationSpecified = true;
        //     return devicespec;
        // }

        private static VirtualDeviceConfigSpec GetSCSIController(ref int key, string type)
        {
            VirtualDeviceConfigSpec devicespec = new VirtualDeviceConfigSpec();
            VirtualDevice device = null;

            // DISK CONTROLLER
            if (type.ToLower().EndsWith("sas"))
            {
                VirtualLsiLogicSASController sas = new VirtualLsiLogicSASController();
                sas.busNumber = 0;
                sas.sharedBus = VirtualSCSISharing.noSharing;
                sas.key = key--;
                device = sas;
            }

            if (type.ToLower() == "lsilogic")
            {
                VirtualLsiLogicController controller = new VirtualLsiLogicController();
                controller.busNumber = 0;
                controller.sharedBus = VirtualSCSISharing.noSharing;
                controller.key = key--;
                device = controller;
            }

            if (type.ToLower() == "buslogic")
            {
                VirtualBusLogicController bus = new VirtualBusLogicController();
                bus.busNumber = 0;
                bus.sharedBus = VirtualSCSISharing.noSharing;
                bus.controllerKey = key--;
                device = bus;
            }
            
            if (type.ToLower() == "pvscsi")
            {
                ParaVirtualSCSIController bus = new ParaVirtualSCSIController();
                bus.busNumber = 0;
                bus.sharedBus = VirtualSCSISharing.noSharing;
                bus.controllerKey = key--;
                device = bus;
            }

            devicespec.device = device;
            devicespec.operation = VirtualDeviceConfigSpecOperation.add;
            devicespec.operationSpecified = true;

            return devicespec;
        }

        private static VirtualDeviceConfigSpec GetFloppy(ref int key, string name)
        {
            VirtualDeviceConfigSpec devicespec = new VirtualDeviceConfigSpec();

            VirtualFloppyImageBackingInfo diskbacking = new VirtualFloppyImageBackingInfo();
            diskbacking.fileName = name;

            VirtualFloppy disk = new VirtualFloppy();
            disk.key = key--;
            disk.backing = diskbacking;

            disk.connectable = new VirtualDeviceConnectInfo();
            disk.connectable.connected = true;
            disk.connectable.startConnected = true;
            //disk.unitNumber = unitnumber;
            //disk.unitNumberSpecified = true;

            devicespec = new VirtualDeviceConfigSpec();
            devicespec.device = disk;
            devicespec.operation = VirtualDeviceConfigSpecOperation.add;
            devicespec.operationSpecified = true;

            return devicespec;
        }

        public static OptionValue[] GetExtraConfig(VmTemplate template)
        {
            List<OptionValue> options = new List<OptionValue>();
            options.Add(new OptionValue { key = "snapshot.redoNotWithParent", value = "true" });
            options.Add(new OptionValue { key = "isolation.tools.setGUIOptions.enable", value = "true" });
            options.Add(new OptionValue { key = "isolation.tools.copy.disable", value = "false" });
            options.Add(new OptionValue { key = "isolation.tools.paste.disable", value = "false" });
            options.Add(new OptionValue { key = "keyboard.typematicMinDelay", value = "2000000" });
            options.Add(new OptionValue { key = "guestinfo.isolationTag", value = template.IsolationTag });
            options.Add(new OptionValue { key = "guestinfo.templateSource", value = template.Id });
            options.Add(new OptionValue { key = "guestinfo.hostname", value = template.Name.Untagged() });

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

            return options.ToArray();
        }

        // private static VirtualDeviceConfigSpec GetNetworkSerialPort(ref int key, string name)
        // {
        //     VirtualDeviceConfigSpec devicespec = new VirtualDeviceConfigSpec();

        //     VirtualSerialPortURIBackingInfo backing = new VirtualSerialPortURIBackingInfo();
        //     backing.direction = "server";
        //     backing.serviceURI = "vSPC.py";
        //     backing.proxyURI = name;

        //     VirtualSerialPort device = new VirtualSerialPort();
        //     device.key = key--;
        //     device.backing = backing;

        //     device.connectable = new VirtualDeviceConnectInfo();
        //     device.connectable.connected = true;
        //     device.connectable.startConnected = true;
        //     //disk.unitNumber = unitnumber;
        //     //disk.unitNumberSpecified = true;

        //     devicespec = new VirtualDeviceConfigSpec();
        //     devicespec.device = device;
        //     devicespec.operation = VirtualDeviceConfigSpecOperation.add;
        //     devicespec.operationSpecified = true;

        //     return devicespec;
        // }

        private static VirtualDeviceConfigSpec GetVideoController(ref int key, int ramKB)
        {
            VirtualDeviceConfigSpec devicespec = new VirtualDeviceConfigSpec();

            VirtualMachineVideoCard card = new VirtualMachineVideoCard();
            card.key = key--;
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

            devicespec = new VirtualDeviceConfigSpec();
            devicespec.device = card;
            devicespec.operation = VirtualDeviceConfigSpecOperation.add;
            devicespec.operationSpecified = true;

            return devicespec;
        }

        // private static VirtualDeviceConfigSpec GetVMCIAdapter(ref int key, int cid)
        // {
        //     VirtualMachineVMCIDevice vmci = new VirtualMachineVMCIDevice();
        //     vmci.key = 12000;
        //     vmci.allowUnrestrictedCommunication = true;
        //     vmci.id = cid;
        //     vmci.controllerKey = 100;
        //     vmci.controllerKeySpecified = true;

        //     VirtualDeviceConfigSpec devicespec = new VirtualDeviceConfigSpec();
        //     devicespec = new VirtualDeviceConfigSpec();
        //     devicespec.device = vmci;
        //     devicespec.operation = VirtualDeviceConfigSpecOperation.add;
        //     devicespec.operationSpecified = true;
        //     return devicespec;
        // }


        // public static VirtualSwitch VimSwitchtoXnetSwitch(HostVirtualSwitch s)
        // {
        //     VirtualSwitch t = new VirtualSwitch();
        //     t.name = s.name;
        //     t.ports = s.numPorts;
        //     if (s.spec.policy.security != null)
        //         t.promiscuous = (s.spec.policy.security.allowPromiscuous && s.spec.policy.security.allowPromiscuousSpecified) ? 1 : 0;
        //     else
        //         t.promiscuous = -1;

        //     if (s.spec.bridge != null)
        //     {
        //         Type type = s.spec.bridge.GetType();
        //         t.bridgetype = type.Name;
        //         if (type == typeof(HostVirtualSwitchBondBridge))
        //             t.nic = ((HostVirtualSwitchBondBridge)s.spec.bridge).nicDevice[0];
        //         if (type == typeof(HostVirtualSwitchSimpleBridge))
        //             t.nic = ((HostVirtualSwitchSimpleBridge)s.spec.bridge).nicDevice;
        //     }
        //     return t;
        // }
        // public static VirtualLan VimPortGrouptoXnetLan(HostPortGroup pg)
        // {
        //     VirtualLan p = new VirtualLan();
        //     p.name = pg.spec.name;
        //     p.vlan = pg.spec.vlanId;
        //     p.switchname = pg.spec.vswitchName;
        //     if (pg.spec.policy.security != null)
        //         p.promiscuous = (pg.spec.policy.security.allowPromiscuous && pg.spec.policy.security.allowPromiscuousSpecified) ? 1 : 0;
        //     else
        //         p.promiscuous = -1;
        //     return p;
        // }

        public static HostNetworkPolicy GetDefaultHostNetworkPolicy()
        {
            HostNetworkPolicy policy = new HostNetworkPolicy();
            policy = new HostNetworkPolicy();
            policy.security = new HostNetworkSecurityPolicy();
            policy.security.allowPromiscuous = true;
            policy.security.allowPromiscuousSpecified = true;
            policy.security.macChanges = true;
            policy.security.macChangesSpecified = true;
            policy.security.forgedTransmits = true;
            policy.security.forgedTransmitsSpecified = true;

            policy.nicTeaming = new HostNicTeamingPolicy();
            policy.nicTeaming.policy = "loadbalance_srcid";
            policy.nicTeaming.reversePolicy = true;
            policy.nicTeaming.notifySwitches = false;
            policy.nicTeaming.rollingOrder = false;
            policy.nicTeaming.reversePolicySpecified = true;
            policy.nicTeaming.notifySwitchesSpecified = true;
            policy.nicTeaming.rollingOrderSpecified = true;

            policy.nicTeaming.failureCriteria = new HostNicFailureCriteria();
            policy.nicTeaming.failureCriteria.checkBeacon = false;
            policy.nicTeaming.failureCriteria.checkDuplex = false;
            policy.nicTeaming.failureCriteria.checkSpeed = "minimum";
            policy.nicTeaming.failureCriteria.speed = 10;
            policy.nicTeaming.failureCriteria.fullDuplex = false;
            policy.nicTeaming.failureCriteria.checkErrorPercent = false;
            policy.nicTeaming.failureCriteria.percentage = 0;

            policy.nicTeaming.failureCriteria.checkBeaconSpecified = true;
            policy.nicTeaming.failureCriteria.checkDuplexSpecified = true;
            policy.nicTeaming.failureCriteria.speedSpecified = true;
            policy.nicTeaming.failureCriteria.fullDuplexSpecified = true;
            policy.nicTeaming.failureCriteria.checkErrorPercentSpecified = true;
            policy.nicTeaming.failureCriteria.percentageSpecified = true;

            policy.offloadPolicy = new HostNetOffloadCapabilities();
            policy.offloadPolicy.csumOffload = true;
            policy.offloadPolicy.tcpSegmentation = true;
            policy.offloadPolicy.zeroCopyXmit = true;
            policy.offloadPolicy.csumOffloadSpecified = true;
            policy.offloadPolicy.tcpSegmentationSpecified = true;
            policy.offloadPolicy.zeroCopyXmitSpecified = true;

            policy.shapingPolicy = new HostNetworkTrafficShapingPolicy();
            policy.shapingPolicy.enabled = false;
            policy.shapingPolicy.enabledSpecified = true;

            return policy;
        }


        public static string[] OsMap = new string[] {
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
        };


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
