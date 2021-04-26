# Olive Generators

This repository provides a list of command line utilities for accelerating app development using standard Olive components and services.

## generate-data-endpoint-proxy

A utility named generate-data-endpoint-proxy (distributed as a nuget global tool) will be used to generate private nuget packages for the data endpoint, to be used by the consumer service.
```bat
dotnet tool install -g generate-data-endpoint-proxy
```

| Command      | Description |
| ----------- | ----------- |
| C:\MyProject\Website\bin\debug\netcoreapp3.1\> `generate-data-endpoint-proxy /assembly:"website.dll" /dataEndpoint:MyNamespace.OrdersEndpoint /out:"c:\temp\generated-packages"` | Generate the nuget packages locally [learn more](https://geeksltd.github.io/Olive/#/Api/Replication?id=generating-a-proxy) |
| C:\MyProject\Website\bin\debug\netcoreapp3.1\> `generate-data-endpoint-proxy /assembly:"website.dll" /dataEndpoint:MyNamespace.OrdersEndpoint /push:http://my-nuget-server.com/nuget /apiKey:12345`  | Generate and publish [learn more](https://geeksltd.github.io/Olive/#/Api/Replication?id=generating-a-proxy)        |
