# Using Proxmox with TopoMojo

[Proxmox Virtual Environment (PVE)](https://pve.proxmox.com/wiki/Main_Page) is an open source server virtualization management solution based on QEMU/KVM and LXC. TopoMojo can be configured to use a PVE cluster to deploy QEMU virtual machines rather than it's traditional VMware based virtual machines.

## Proxmox Setup

There are a few things you wil need to do within Proxmox to prepare it for use with TopoMojo.

### Installation

- Install Proxmox on one or more nodes.
    - Add all of the nodes that you want to be used by TopoMojo to a single Proxmox cluster.

### Generate an Access Token

TopoMojo requires a Proxmox Access Token in order to authenticate with the Proxmox API.

- From the Proxmox Web UI, generate an API Token by clicking on Datacenter and navigating to Permissions -> API Tokens.
    - Ensure Privilege Separation is unchecked if you want to use the privileges of the token user. Otherwise, you will need to select individual permissions to give to the token.
    - Copy the Secret and the Token ID. This will need to be added to appsettings later.

### Create an SDN Zone

TopoMojo uses Proxmox's Software Defined Networking (SDN) feature to manage the networks of the virtual labs. You will need to create an SDN Zone in Proxmox and configure TopoMojo to use it.

- In the Proxmox Web UI navigate to Datacenter -> SDN -> Zones.
- Add a new VXLAN Zone.
    - VXLAN is the only type currently supported by TopoMojo.
    - The ID is the name you want to use for this Zone. You will need it when configuring TopoMojo's appsettings.
    - Under Peer Address List, enter a comma separated list of the IP Addresses of all of the nodes in your cluster.
        - If you add a new node to your cluster, you will need to add it to the SDN Zone as well.

### Configure NGINX

You will need to configure a reverse proxy on the node that TopoMojo will communicate with in order to access the API and allow viewing of consoles. This will allow the Proxmox API to be accessed over port 443 as well as provide the required authentication headers for accessing consoles through an external application. Instructions for doing this with NGINX are provided below.

- Install NGINX on your main Proxmox node and configure it to run on startup.
    - `sudo apt install nginx`
    - `sudo systemctl enable nginx`
- Use the following NGINX configuration as a reference. This will allow your Proxmox Web UI and API to be reached over port 443 as well as allow console access to work through TopoMojo.
    - Replace "pve.local" with your Node's hostname
    - Replace <api_token> with the API Token you generated earlier. This should be in the format `user@system!TokenId=Secret` e.g. `root@pam!Topo=4c4fbe1e-b31e-55a9-9fg0-2de4a411cd23`

```
upstream proxmox {
        server "pve.local";
}

server {
        listen 80 default_server;
        rewrite ^(.*) https://$host$1 permanent;
}

server {
        listen 443;
        server_name _;
        ssl on;
        ssl_certificate /etc/pve/local/pve-ssl.pem;
        ssl_certificate_key /etc/pve/local/pve-ssl.key;
        proxy_redirect off;

        location ~ /api2/json/nodes/.+/qemu/.+/vncwebsocket.* {
                proxy_set_header "Authorization" "PVEAPIToken=<api_token>";
                proxy_http_version 1.1;
                proxy_set_header Upgrade $http_upgrade;
                proxy_set_header Connection "upgrade";
                proxy_pass https://localhost:8006;
                proxy_buffering off;
                client_max_body_size 0;
                proxy_connect_timeout 3600s;
                proxy_read_timeout 3600s;
                proxy_send_timeout 3600s;
                send_timeout 3600s;
        }

        location / {
                proxy_http_version 1.1;
                proxy_set_header Upgrade $http_upgrade;
                proxy_set_header Connection "upgrade";
                proxy_pass https://localhost:8006;
                proxy_buffering off;
                client_max_body_size 0;
                proxy_connect_timeout 3600s;
                proxy_read_timeout 3600s;
                proxy_send_timeout 3600s;
                send_timeout 3600s;
        }
}
```

## TopoMojo Setup

This section describes the appsettings that will need to be set to configure TopoMojo to use Proxmox

### Required Settings

- Pod__HypervisorType
    - Set this to `Proxmox` to enable Proxmox mode.
    - Each TopoMojo instance currently operates either entirely in Vsphere or Proxmox mode.
- Pod__Host
    - Set this to the hostname of your main Proxmox node.
- Pod__AccessToken
    - Set this to the access token generated above. It should be the same one set in your NGINX config. 
    - E.g. `root@pam!Topo=4c4fbe1e-b31e-55a9-9fg0-2de4a411cd23`
- Pod__SDNZone
    - Set this to the name of the SDN Zone you created in Proxmox

### Optional Settings

- Pod_Password
    - Set this to the password of the **root** user account only to enable Guest Settings support (discussed in detail below).
    - If no password or an invalid root password is provided, Guest Settings will be disabled.
- Pod__Vlan__ResetDebounceDuration
  - The integer number of milliseconds TopoMojo will wait after a virtual network operation is initiated before reloading Proxmox's SDN. As reloading is a synchronous process that can take up 10 seconds, we offer this setting to reduce aggregate wait times by debouncing changes into batches.
- Pod__Vlan__ResetDebounceMaxDuration
  - The integer number of milliseconds that describes the maximum amount of time TopoMojo will debounce before it reloads Proxmox's SDN following a network operation.

#### ISOs

TopoMojo can optionally allow uploading of ISO files that can be mounted to virtual machines. You will need to set these settings to enable this feature.

- Pod__IsoStore
    - Set this to the name of the shared storage in your Proxmox cluster that ISOs will be sourced from for mounting to virtual machines.
    - e.g. `iso`
- FileUpload_IsoRoot
    - Set this to a path that is mounted to the TopoMojo API container that ISOs uploaded through TopoMojo will be saved to.
    - This should map to the same underlying storage as `Pod_IsoStore` above.
    - Proxmox creates a particular directory structure for ISO stores, so this path needs to end in /template/iso.
    - e.g. `/mnt/isos/template/iso`
- FileUpload_SupportsSubFolders
    - Set this to `false` for Proxmox since Proxmox does not allow sub folders in it's ISO stores

## Guest Settings

TopoMojo templates have a Guest Settings section, allowing the user to set key value pairs that are injected into the virtual machine.

In Vsphere, this is done by setting guestinfo values in the virtual machine's ExtraConfig that can be retrieved inside the virtual machine using open-vm-tools or vmware tools with the command `vmtoolsd --cmd "info-get guestinfo.variable"`.

In Proxmox, a similar functionality is achieved using the QEMU Firmware Configuration (fw_cfg) Device. Guest Settings are injected into the virtual machine and can be accessed with the command `sudo cat /sys/firmware/qemu_fw_cfg/by_name/opt/guestinfo.variable/raw`, where `variable` is the key of the Guest Setting.

Note: This currently only works on Linux Guests. There is an open source [Windows driver](https://github.com/virtio-win/kvm-guest-drivers-windows/tree/master/fwcfg64) that has some basic fw_cfg support, but does not support reading user-defined /opt values at this time.

As described in the Settings section, this currently requires the use of the root user and password. There is a [patch](https://bugzilla.proxmox.com/show_bug.cgi?id=4068) available for Proxmox that would make this no longer necessary, but it has not been merged into a release. Currently, if a root password is not provided, Guest Settings will be skipped when virtual machines are deployed.

## Using Proxmox Templates

In Vsphere, TopoMojo templates point to virtual disks. In Proxmox, TopoMojo templates point to Proxmox Templates. To get started with a new installation, create one or more Proxmox Templates manually by deploying a virtual machine and converting it into a template. Then create a TopoMojo template and set the `Template` value of it's Detail property to the name of the Proxmox template you created. When deplying this TopoMojo template, TopoMojo will create a linked clone of the Proxmox template to the same storage location that the template exists on and reconfigure appropriate values from the TopoMojo template such as memory, cpus, networks, etc.

### Windows Virtual Machines

Windows virtual machines require the installation of VirtIO drivers for compatibility with QEMU/KVM. Details can be found at https://pve.proxmox.com/wiki/Windows_VirtIO_Drivers.

### Clipboard Support

TopoMojo supports clipboard access to Proxmox Virtual machines, if the appropriate pre-requisites are set.

- On the Proxmox template, the VNC Clipboard must be enabled.
    - To do this, in the template's Hardware tab, Edit the Display and set Clipboard to VNC
        - There is currently a known QEMU limitation where a virtual machine with the Clipboard set to VNC cannot be migrated to another Node. You may need to temporarily disable the VNC clipboard, perform the migration, and re-enable it if you need to move a vm to another Node.
- The [SPICE Guest Tools](https://www.spice-space.org/download.html) must be installed in the virtual machines
    - This is installed by default on some Linux distributions.
    - This must be installed manually in Windows and has been tested and works the same as Linux clipboard support.