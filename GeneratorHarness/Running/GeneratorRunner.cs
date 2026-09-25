using System.Diagnostics;
using System.Text;
using System.Threading.Channels;
using Olive.GeneratorHarness.Tools;

namespace Olive.GeneratorHarness.Running;

/// <summary>What a finished run amounted to.</summary>
public sealed class RunResult
{
    public required RunOptions Options { get; init; }
    public required string CommandLine { get; init; }
    public required string WorkingDirectory { get; init; }

    /// <summary>Taken from the process, never from the run-end record: that record is written at
    /// normal verbosity and so never arrives under /verbosity:quiet or minimal.</summary>
    public int ExitCode { get; set; } = -1;

    public TimeSpan Duration { get; set; }
    public bool Cancelled { get; set; }
    public string? StartFailure { get; set; }

    public int Errors { get; set; }
    public int Warnings { get; set; }
    public int Records { get; set; }

    /// <summary>The log files this run wrote, including the one it opens for itself.</summary>
    public List<string> LogFiles { get; } = [];

    /// <summary>The values the run was for, such as an endpoint hash.</summary>
    public List<(string Name, string Value)> Results { get; } = [];

    public bool Succeeded => StartFailure == null && !Cancelled && ExitCode == 0;
}

/// <summary>
/// Starts one tool and streams what it says. Both pipes are read on their own tasks and pushed
/// into a channel, so the UI drains them at its own pace and a run that fills a pipe never
/// deadlocks.
/// </summary>
public sealed class GeneratorRunner
{
    readonly ChannelWriter<LogRecord> Writer;

    public GeneratorRunner(ChannelWriter<LogRecord> writer) => Writer = writer;

    public async Task<RunResult> RunAsync(ToolResolution resolution, RunOptions options,
        CancellationToken cancellation)
    {
        var arguments = CommandLineBuilder.Build(options);

        var result = new RunResult
        {
            Options = options,
            CommandLine = CommandLineBuilder.Display(resolution, options, revealApiKey: false),
            WorkingDirectory = options.WorkingDirectory,
        };

        if (!resolution.Found || resolution.FileName.IsBlank())
        {
            result.StartFailure = resolution.Tool.ToolCommand + " could not be found. " +
                "Build it in this repository, install it as a global tool, or browse to it.";

            Say(LogRecordParser.Harness(result.StartFailure, "ERROR"), result);
            return result;
        }

        if (options.CreateOutputIfMissing && options.Output.HasContent() && !Directory.Exists(options.Output))
        {
            try { Directory.CreateDirectory(options.Output); }
            catch (Exception ex)
            {
                result.StartFailure = "Could not create the output folder: " + ex.Message;
                Say(LogRecordParser.Harness(result.StartFailure, "ERROR"), result);
                return result;
            }
        }

        using var process = new Process { StartInfo = Describe(resolution, arguments, options) };

        Say(LogRecordParser.Harness(result.CommandLine), result);
        Say(LogRecordParser.Harness("in " + result.WorkingDirectory), result);

        var clock = Stopwatch.StartNew();

        try { process.Start(); }
        catch (Exception ex)
        {
            result.StartFailure = "Could not start " + resolution.FileName + ": " + ex.Message;
            Say(LogRecordParser.Harness(result.StartFailure, "ERROR"), result);
            return result;
        }

        // Nothing is ever typed at these tools, and a child left waiting on a prompt would hang
        // a harness that shows no console.
        process.StandardInput.Close();

        var output = ReadAsync(process.StandardOutput, LogRecordParser.Parse, result);
        var errors = ReadAsync(process.StandardError, LogRecordParser.Error, result);

        try
        {
            await process.WaitForExitAsync(cancellation);
        }
        catch (OperationCanceledException)
        {
            result.Cancelled = true;

            // The generators spawn dotnet build. Killing only the parent would leave a long
            // MSBuild running with nothing to report to.
            try { process.Kill(entireProcessTree: true); }
            catch (Exception ex) { Say(LogRecordParser.Harness("Could not stop the run: " + ex.Message, "WARN"), result); }

            try { await process.WaitForExitAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10), CancellationToken.None); }
            catch { /* Reported below whatever happened. */ }
        }

        // After the exit, so nothing the tool wrote on its way out is lost.
        await Task.WhenAll(output, errors);

        clock.Stop();
        result.Duration = clock.Elapsed;
        result.ExitCode = SafeExitCode(process);

        if (result.Cancelled) Say(LogRecordParser.Harness("Cancelled.", "WARN"), result);

        Say(LogRecordParser.Harness(
            $"Exit code {result.ExitCode} after {result.Duration.TotalSeconds:0.0}s.",
            result.ExitCode == 0 ? "INFO" : "ERROR"), result);

        return result;
    }

    static int SafeExitCode(Process process)
    {
        try { return process.ExitCode; }
        catch { return -1; }
    }

    ProcessStartInfo Describe(ToolResolution resolution, List<string> arguments, RunOptions options)
    {
        var start = new ProcessStartInfo(resolution.FileName!)
        {
            WorkingDirectory = options.WorkingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            StandardOutputEncoding = new UTF8Encoding(false),
            StandardErrorEncoding = new UTF8Encoding(false),
        };

        foreach (var argument in resolution.Prefix) start.ArgumentList.Add(argument);
        foreach (var argument in arguments) start.ArgumentList.Add(argument);

        // The same settings the generators give their own children, so a build under the harness
        // behaves like one from a terminal.
        start.Environment["DOTNET_HOST_PATH"] = ToolLocator.DotNetHost;
        start.Environment["DOTNET_NOLOGO"] = "1";
        start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        start.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";
        start.Environment["NUGET_XMLDOC_MODE"] = "skip";
        start.Environment["MSBUILDTERMINALLOGGER"] = "off";

        // Log.Error and Log.Warn write ::error:: and ::warning:: straight to standard output when
        // this is set, without the json guard the group markers have. That would put lines that
        // are not records into a stream that promises nothing else.
        start.Environment.Remove("GITHUB_ACTIONS");

        return start;
    }

    async Task ReadAsync(StreamReader reader, Func<string, LogRecord> parse, RunResult result)
    {
        while (true)
        {
            string? line;

            // Never cancelled: the pipes are drained to the end even when the run was killed, so
            // whatever the tool managed to say still reaches the log.
            try { line = await reader.ReadLineAsync(); }
            catch (Exception ex)
            {
                Say(LogRecordParser.Harness("Stopped reading the tool's output: " + ex.Message, "WARN"), result);
                return;
            }

            if (line == null) return;

            Say(parse(line), result);
        }
    }

    void Say(LogRecord record, RunResult result)
    {
        result.Records++;

        if (record.IsError) result.Errors++;
        else if (record.IsWarning) result.Warnings++;

        if (record.Kind == "result")
        {
            var name = record.Field("name");
            var value = record.Field("value");

            if (name.HasContent() && value.HasContent()) result.Results.Add((name, value));
        }

        // Every generator's Describe() reports the log file it opened for itself, beside the run's
        // temp folder, whether or not /log: was given.
        if (record.Kind == "field" && record.Field("name") == "log file")
        {
            var path = record.Field("value");
            if (path.HasContent() && !result.LogFiles.Contains(path)) result.LogFiles.Add(path);
        }

        Writer.TryWrite(record);
    }
}
