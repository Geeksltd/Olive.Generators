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


# Debugging the Generators
If you are experiencing a problem using the generators, try the following:

1. Pull the source code of this repository on your local machine
2. Open the solution in Visual Studio
3. Set the generator you're trying to use as the *StartUp project* (e.g. **DataEndPointGenerator**)
4. Right click no the project and select *Properties*
5. Go under the `Debug` tab.
6. Set the Working Directory to the folder of your test project for which you want to generate (e.g. `C:\MyProject\Website\bin\debug\netcoreapp3.1`)
7. Set the other command line parameters
8. In Visual Studio, press F5 to run the generator on your target test project. Now you can see what is hapenning.
