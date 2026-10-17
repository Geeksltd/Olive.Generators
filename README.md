# Olive Generators

This repository provides a list of command line utilities for accelerating app development using standard Olive components and services.

---

## generate-data-endpoint-proxy

```bat
dotnet tool install -g generate-data-endpoint-proxy
```

| Command      | Description |
| ----------- | ----------- |
| C:\MyProject\Website\bin\debug\net8.0\> `generate-data-endpoint-proxy /assembly:"website.dll" /dataEndpoint:MyNamespace.OrdersEndpoint /out:"C:\...\PrivatePackages"` | Generate the nuget packages locally [learn more](https://geeksltd.github.io/Olive/#/Api/Replication?id=generating-a-proxy) |
| C:\MyProject\Website\bin\debug\net8.0\> `generate-data-endpoint-proxy /assembly:"website.dll" /dataEndpoint:MyNamespace.OrdersEndpoint /push:http://my-nuget-server.com/nuget /apiKey:12345`  | Generate and publish [learn more](https://geeksltd.github.io/Olive/#/Api/Replication?id=generating-a-proxy)        |
| C:\MyProject\Website\bin\debug\net8.0\> `generate-data-endpoint-proxy /assembly:"website.dll" /dataEndpoint:"MyNamespace.OrdersEndpoint;MyNamespace.CustomersEndpoint" /out:"C:\...\PrivatePackages"` | Generate several at once. Prefer this in CI: one run loads the target application once and builds every generated project in a single MSBuild invocation, where a loop of single runs repeats all of that per endpoint. |

---

## generate-eventbus-command-proxy

```bat
dotnet tool install -g generate-eventbus-command-proxy
```

| Command      | Description |
| ----------- | ----------- |
| C:\MyProject\Website\bin\debug\net8.0\> `generate-eventbus-command-proxy /assembly:"website.dll" /command:MyNamespace.FooCommand /out:"c:\temp\generated-packages"` | Generate the nuget packages locally [learn more](https://geeksltd.github.io/Olive/#/Api/EventBusCommands) |
| C:\MyProject\Website\bin\debug\net8.0\> `generate-eventbus-command-proxy /assembly:"website.dll" /command:MyNamespace.FooCommand /push:http://my-nuget-server.com/nuget /apiKey:12345` | Generate the nuget packages locally [learn more](https://geeksltd.github.io/Olive/#/Api/EventBusCommands) |
| C:\MyProject\Website\bin\debug\net8.0\> `generate-eventbus-command-proxy /assembly:"website.dll" /command:"MyNamespace.FooCommand;MyNamespace.BarCommand" /out:"c:\temp\generated-packages"` | Generate several at once. Prefer this in CI, for the same reason as above. |

Both tools accept `/hashonly`, which prints a hash of each endpoint or command and does no building, so a pipeline can tell which ones changed. It takes a semicolon-separated list too.

They also accept `/maxCpu:N`, which limits how many of the generated projects MSBuild builds at once. A batch of sixty endpoints is a solution of well over a hundred projects, and left to itself MSBuild starts a worker node per processor, each holding well over a hundred megabytes. The node count follows the core count while the memory each node needs does not, so a build agent with more cores than memory is what this is for. Leave it out and the build runs exactly as it always has.

---

## Seeing what a run is doing

Every tool in this repository reports its work as timed, nested steps. Each line carries the elapsed time since the run started and its own severity, and in a batch it also carries the endpoint or command it belongs to. A step reports its duration and outcome when it closes, and the run ends with a summary of what was produced.

| Switch | What it does |
| ----------- | ----------- |
| `/verbosity:quiet` | Errors and results only. |
| `/verbosity:minimal` | Errors, warnings, the run summary, and the heartbeat. |
| `/verbosity:normal` | The default. Every step, plus what was published where. |
| `/verbosity:detailed` | Adds the live output of every `dotnet build` and `nuget pack` as it arrives, the package versions each generated project resolved, and the files it wrote. Reach for this when a run fails or stalls. |
| `/verbosity:diagnostic` | Adds assembly resolution, the nuspec and project files as written, and process ids. |
| `/logformat:text` | The default. Written for a person to read. |
| `/logformat:json` | The console becomes one self-describing record per line, for another program to read. |
| `/log:<path>` | Writes a full-detail text log to that file, whatever the console verbosity is. |
| `/logjson:<path>` | Writes the same records to that file, whatever the console format is. |
| `/heartbeat:<seconds>` | How long a step may stay silent before the run says which one it is still on. Defaults to 15 seconds, and `0` turns it off. |
| `/timing` | Prints a breakdown of where the run spent its time, on standard error. |
| `/nocolor` | Never colours the output. |

A full-detail log is written for every run whether or not `/log:` was given, beside the run's own temp folder, and its path is printed at the start. It is flushed line by line, so a run that a build agent kills still leaves a complete record. The nuget api key never reaches any of it.

Under GitHub Actions the top-level steps are emitted as collapsible groups, and failures are annotated so they appear in the job summary. Both are suppressed under `/logformat:json`.

---

## Reading a run from another program

Use `/logformat:json`. Standard output is then newline-delimited JSON, one object per line, with nothing else mixed in: the usage screen, the timing breakdown and the log's own complaints all go elsewhere. Parse it line by line rather than as one document.

Every record carries the same head:

| Field | Meaning |
| ----------- | ----------- |
| `seq` | Position in the run, counting from one. |
| `run` | Identifies the run, so records of concurrent runs can be told apart. |
| `time` | Absolute UTC time, ISO 8601. |
| `elapsedSeconds` | Seconds since the run started. |
| `level` | `ERROR`, `WARN`, `INFO`, `DEBUG` or `TRACE`. |
| `verbosity` | The verbosity a run needs for this line to be shown. |
| `kind` | What the record is, from the table below. |
| `depth` | How deeply nested in steps, counting from zero. |
| `item` | The endpoint, command or controller it is about, or `null`. |
| `message` | The same text the console shows in text format. |

Each kind adds its own fields:

| `kind` | Added fields |
| ----------- | ----------- |
| `run-start` | `tool`, `version`, `startedAt`, `processId`, `commandLine` |
| `run-end` | `durationSeconds`, `exitCode` |
| `step-start` | `step` |
| `step-end` | `step`, `outcome` (`done` or `failed`), `durationSeconds`, `note` |
| `child-output` | `source` (which child process), `output` (the line as the process wrote it) |
| `heartbeat` | `step`, `durationSeconds` |
| `field` | `name`, `value` |
| `result` | `name`, `value`. The values the tool was run for, such as an endpoint hash. |
| `error` | `exception` (the exception type) |
| `stack` | none |
| `summary-totals` | `items`, `packagesPublished`, `itemsFailed`, `durationSeconds` |
| `summary-item` | `status`, `notes`, `failure` |
| `summary-package` | `package`, `packageVersion`, `destination`, `status`, `failure` |
| `warning`, `message`, `summary` | none |

Records are filtered by `/verbosity:` in this format too, so pass `/verbosity:detailed` to receive the child process output.

In text format the same information is laid out as:

```
[+MM:SS.s] LEVEL [item] <indent><marker> message
```

The item appears only when a run covers more than one. The indent is two spaces per nesting level. The marker is `>` to open a step, `<` to close one, `|` for a line of child process output, `...` for a heartbeat, and nothing for a plain message. The first line of a run names the absolute start time, so every elapsed stamp below it can be turned back into a wall clock time.

The hash that `/hashonly` prints keeps its own long-standing shape in text format, unprefixed, so an existing pipeline reads it unchanged. Under `/logformat:json` it arrives as a `result` record instead.

---

## update-local-nuget-cache

TODO: ...

---

## msharp-build

TODO: msharp-build /update-nuget...


TODO: Move all microservice ops here.


---

## The test harness

`GeneratorHarness` is a Windows Forms application for exercising the tools in this repository. It replaces the debug-tab recipe at the bottom of this file: open a microservice assembly, tick the endpoints, commands or controllers you want, set any switch from the tables above, and watch the run.

```bat
dotnet build Olive.Generators.sln -c Debug
GeneratorHarness\bin\Debug\net10.0-windows\Olive.GeneratorHarness.exe
```

It runs each tool as a child process rather than calling into it, so a run is exactly what the command line would have done, and the command it is about to run is shown in a box you can copy. It always passes `/logformat:json` and reads the records back, so the log in the window is the same information the tables above describe, filterable by level, kind and item. The exit code comes from the process itself, because `run-start` and `run-end` are written at normal verbosity and so never arrive under `/verbosity:quiet` or `minimal`.

It prefers the generators built in this working copy over the installed global tools, and the toolbar says which of the two each one resolved to and when it was built. "Rebuild tools" rebuilds all four and re-resolves them.

Opening an assembly lists what the generators would find in it, read as metadata only, so nothing in the target runs and a rebuilt assembly can be reopened without restarting. Those predicates are an approximation of ones the generators evaluate with live reflection, so each list has a "show every public type" toggle and a box for typing a name the scan missed.

"Run all" drives the cheap checks against whatever is selected: a hash of each item, a hash of them all in one batch, one generate of each kind into a temporary folder, and the unknown-name failure path. It reports pass or fail per run. Nothing it does publishes anything.

The harness is a development aid and is not packaged as a dotnet tool. It only runs on Windows, though the solution still builds on a Linux SDK.

# Debugging the Generators
The test harness above is usually the quickest way in. To attach a debugger instead:

1. Pull the source code of this repository on your local machine
2. Open the solution in Visual Studio
3. Set the generator you're trying to use as the *StartUp project* (e.g. **DataEndPointGenerator**)
4. Right click no the project and select *Properties*
5. Go under the `Debug` tab.
6. Set the Working Directory to the folder of your test project for which you want to generate (e.g. `C:\MyProject\Website\bin\debug\net8.0`)
7. Set the other command line parameters
8. In Visual Studio, press F5 to run the generator on your target test project. Now you can see what is hapenning.
