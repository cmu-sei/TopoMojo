Using vSphere with TopoMojo
---------------------------

vSphere is VMware's cloud computing virtualization platform, and TopoMojo can be configured to use a vSphere environment for deploying virtual labs, leveraging VMware ESXi hosts or vCenter

### vSphere Setup

There are a few steps you need to take to prepare vSphere for use with TopoMojo.

### Installation

1.  Ensure that you have installed VMware ESXi on your nodes.
2.  Add all ESXi hosts that you want to use in TopoMojo to a vSphere cluster managed by a vCenter Server.

### Create a service account

TODO: Identify topomojo's needed vSphere permissions. 

### Configure Networking

TopoMojo relies on vSphere's networking infrastructure to manage virtual lab networks.

1.  In the vSphere Client, configure a dedicated switch for TopoMojo labs.
2.  Note the network name, as you'll need it when configuring TopoMojo's `Pod__Network` setting.

### TopoMojo Setup

This section covers the appsettings you need to configure TopoMojo for vSphere.

#### Required Settings

```yaml

Pod__HypervisorType: Vsphere # (v2.2.7+) Set this to `Vsphere` to enable vSphere mode. Note that each TopoMojo instance supports either vSphere or Proxmox mode, not both simultaneously. 


Pod__Url: https://vc.topomojo.local # Set this to the URL of your vCenter instance (e.g., `https://vc.local`).
Pod__User: administrator@vsphere.local #set this to your topomojo service account or an a user in the `Administrators` group
Pod__Password: <password> # set this to the password of your user account
# Proxmox Only
Pod_Token: <token>

## name of switch that bridges hosts (standard or distributed)
Pod__Uplink = dvSwitch
Pod__IsNsxNetwork = false

# datastore path of running vm's (support host macro... ie. "[local-{host}]")
# Must be relative path, CANNOT be absolute (/topomojo). Folders will be created if they don't exist.
Pod__VmStore: "[datastore] topomojo/vmstore" 
Pod__IsoStore: "[datastore] topomojo/isos"
Pod__DiskStore: "[datastore] topomojo/diskstore"

# TicketUrlHandler: console url transform method
#     "none" : wss://<host>.<internal-domain>/ticket/123455
#     "querystring" : wss://<Core.ConsoleHost>/ticket/123455?vmhost=<internalHost>
Core__ConsoleHost: topomojo.local/console
Pod__TicketUrlHander: querystring

# No Leading or trailing slashes (/)
Pod__PoolPath: <datacenter>/<cluster>
 
#### Optional Settings

Pod__ConsoleUrl:
## range of vlan id's available to topomojo; i.e. [200-399]
# Pod__Vlan__Range =
Pod__Vlan__Range: [100-200]
## vlan registrations, for reserved nets within the above range
# Pod__Vlan__Reservations__0__Id = 200
# Pod__Vlan__Reservations__0__Name = bridge-net
# Can add multiple by changing the index
Pod__Pod Vlan_Reservations__0__Id: 0
Pod__Pod Vlan_Reservations__0__Name: bridge-net
Pod__Pod Vlan_Reservations__1__Id: 100
Pod__Pod Vlan_Reservations__1__Name: lan
```

#### ISOs

TopoMojo can optionally allow uploading of ISO files that can be mounted to virtual machines. Configure the following settings to enable this feature:

```yaml
-   Pod__IsoStore: Specify the vSphere datastore where ISOs are stored for use with TopoMojo (e.g., `[iso-datastore]`).
-   FileUpload_IsoRoot: Set this to the path where uploaded ISOs should be saved (e.g., `/vmfs/volumes/iso-datastore`).
```

### Guest Customization

TopoMojo templates have a Guest Settings section allowing the user to set key-value pairs that are injected into the virtual machine.

-   In vSphere, this is done by setting `guestinfo` values in the virtual machine's `ExtraConfig`, which can be retrieved inside the VM using VMware tools (`vmtoolsd --cmd "info-get guestinfo.variable"`).

### Using vSphere Templates

In vSphere, TopoMojo templates are linked to vSphere vmdk disks (virtual machine snapshots):


2.  In TopoMojo, create a template and set the **Template** value of its **Detail** property to the name of the vSphere template you created.
3.  When deploying this TopoMojo template, TopoMojo will create a linked clone from the vSphere template and apply values from the TopoMojo template configuration, such as memory, CPU, and network settings.
