# Kubernetes Considerations with TopoMojo

## Service port
TopoMojo uses a TCP 8080 as the default service port.

```yaml
  service: 
    type: ClusterIP
    port: 8080
```

This configuration ensures that the service is accessible within the Kubernetes cluster using port 8080.


## Console Proxy Ingress
To enable console proxying through Kubernetes ingress, especially useful when accessing TopoMojo over the internet without direct network access to vCenter or ESXi hosts, configure the ingress as follows:

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
