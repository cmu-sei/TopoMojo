# Kubernetes Considerations with Topomojo
In Topomojo version 2.3.0, the application transitioned to a rootless container model. This change introduces some considerations for configuring the values files based on your specific environment. Below are the key adjustments to ensure proper deployment and functionality. 

## Service port changed 
Topomojo now uses a different service port. Update your values file accordingly:
```yaml
  service: 
    type: ClusterIP
    port: 8080
```
This configuration ensures that the service is accessible within the Kubernetes cluster using port 8080.

## Adding your own root certificates. 
To include custom root certificates, you need to modify the values file and update the security context as shown below:
```yaml
securityContext:
    # capabilities:
    #   drop:
    #   - ALL
    readOnlyRootFilesystem: false
    runAsNonRoot: false
    runAsUser: 0
```
You must also add the `customStart` script making sure to change the dotnet execution location to /home/app
```yaml
customStart: 
    command: ['/bin/sh']
    args: ['/start/start.sh']
    binaryFiles: {}
    files: 
      start.sh: |
        #!/bin/sh
        cp /start/*.crt /usr/local/share/ca-certificates && update-ca-certificates
        cd /home/app && dotnet TopoMojo.Api.dll
      cacert.crt: |-
        <PEM Format Certs>
```
These changes disable the rootless container restrictions, allowing the container to modify its file system as needed to incorporate your certificates.

## Console Proxy Ingress
To enable console proxying through Kubernetes ingress, especially useful when accessing Topomojo over the internet without direct network access to vCenter or ESXi hosts, configure the ingress as follows:

**Note:** Ensure your Kubernetes cluster has the necessary routes, firewall rules, and DNS entries to access vCenter and ESXi hosts. 

```yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: console-ingress
  annotations:
    cert-manager.io/cluster-issuer: ca-issuer
    kubernetes.io/ingress.class: nginx
    nginx.ingress.kubernetes.io/server-snippet: |
      location ~ /console/ticket/(.*) {
        proxy_pass https://$arg_vmhost/ticket/$1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_request_buffering off;
        proxy_buffering off;
        proxy_read_timeout 86400s;
        proxy_send_timeout 86400s;
        proxy_ssl_session_reuse on;
      }
spec:
  rules:
  - host: topomojo.local
    http:
      paths:
      - path: /console
        pathType: Prefix
        backend:
          service:
            name: topomojo-api
            port:
              number: 80
  tls:
  - secretName: topomojo-tls
    hosts:
      - topomojo.local
```
Additionally, update the environment variables in the `topomojo-api` section of the values file:
```yaml
Core__ConsoleHost: https://topomojo.local/console
Pod__TicketUrlHandler: "querystring"
```