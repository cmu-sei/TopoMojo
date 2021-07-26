Note to self:

```bash

# generate openapi.json in the api project folder
dotnet swagger tofile --output ../TopoMojo.Api.Client/openapi.json ./bin/Debug/netcoreapp3.1/TopoMojo.Api.dll v1

# run generator
nswag run /runtime:NetCore31

# bundle nuget package
dotnet pack -o ~/dev/nuget-local

```
