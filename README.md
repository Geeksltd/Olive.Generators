# Olive Generators

This repository provides a list of command line utilities for accelerating app development using standard Olive components and services.

---

## generate-data-endpoint-proxy

```bat
dotnet tool install -g generate-data-endpoint-proxy
```

| Command      | Description |
| ----------- | ----------- |
| C:\MyProject\Website\bin\debug\netcoreapp3.1\> `generate-data-endpoint-proxy /assembly:"website.dll" /dataEndpoint:MyNamespace.OrdersEndpoint /out:"C:\...\PrivatePackages"` | Generate the nuget packages locally [learn more](https://geeksltd.github.io/Olive/#/Api/Replication?id=generating-a-proxy) |
| C:\MyProject\Website\bin\debug\netcoreapp3.1\> `generate-data-endpoint-proxy /assembly:"website.dll" /dataEndpoint:MyNamespace.OrdersEndpoint /push:http://my-nuget-server.com/nuget /apiKey:12345`  | Generate and publish [learn more](https://geeksltd.github.io/Olive/#/Api/Replication?id=generating-a-proxy)        |

---

## generate-eventbus-command-proxy

```bat
dotnet tool install -g generate-eventbus-command-proxy
```

| Command      | Description |
| ----------- | ----------- |
| C:\MyProject\Website\bin\debug\netcoreapp3.1\> `generate-eventbus-command-proxy /assembly:"website.dll" /command:MyNamespace.FooCommand /out:"c:\temp\generated-packages"` | Generate the nuget packages locally [learn more](https://geeksltd.github.io/Olive/#/Api/EventBusCommands) |
| C:\MyProject\Website\bin\debug\netcoreapp3.1\> `generate-eventbus-command-proxy /assembly:"website.dll" /command:MyNamespace.FooCommand /push:http://my-nuget-server.com/nuget /apiKey:12345` | Generate the nuget packages locally [learn more](https://geeksltd.github.io/Olive/#/Api/EventBusCommands) |

---

## update-local-nuget-cache

TODO: ...

---

## msharp-build

TODO: msharp-build /update-nuget...


TODO: Move all microservice ops here.
