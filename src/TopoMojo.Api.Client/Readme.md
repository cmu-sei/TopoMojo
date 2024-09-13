Note to self:

```bash

# install nswag
npm install -g nswag

cd src/TopoMojo.Api

# ensure latest is built
dotnet build

# generate openapi.json in the api project folder
dotnet swagger tofile --output ../TopoMojo.Api.Client/openapi.json ./bin/Debug/net6.0/TopoMojo.Api.dll v1

cd ../TopoMojo.Api.Client

# run generator
nswag run /runtime:Net60

# bundle nuget package
dotnet pack -o ~/dev/nuget-local

```
