using Olive.GeneratorHarness.Tools;

namespace Olive.GeneratorHarness.Running;

/// <summary>
/// Turns options into the argument list a tool is started with, and into the quoted line a user
/// can paste into a terminal.
/// <para>
/// Every switch spelling in the harness lives here. ParametersParserBase.Param matches "/key:"
/// case sensitively, while the load context chooser and the log both match case insensitively,
/// so "/dataendpoint:" would pick the right target folder and then generate nothing. The
/// spellings come from GeneratorTool and are never assembled in the UI.
/// </para>
/// </summary>
public static class CommandLineBuilder
{
    /// <summary>The arguments the tool itself receives, ready for ProcessStartInfo.ArgumentList.</summary>
    public static List<string> Build(RunOptions options)
    {
        var arguments = new List<string>();

        if (options.Tool.Kind == ToolKind.AcceleratePackage) AddAccelerate(arguments, options);
        else AddGenerator(arguments, options);

        AddLogging(arguments, options);

        arguments.AddRange(options.ExtraArguments.SplitArguments());

        return arguments;
    }

    static void AddAccelerate(List<string> arguments, RunOptions options)
    {
        // Positional, unlike every other tool: "extract --out <file>" or "restore <file> [-parallel]".
        if (options.Accelerate == AccelerateCommand.Extract)
        {
            arguments.Add("extract");
            arguments.Add("--out");
            arguments.Add(options.PackagesFile.Or("packages.json"));
        }
        else
        {
            arguments.Add("restore");
            arguments.Add(options.PackagesFile.Or("packages.json"));

            if (options.Parallel) arguments.Add("-parallel");
        }
    }

    static void AddGenerator(List<string> arguments, RunOptions options)
    {
        // Both are passed explicitly even though each has a default, so the copied command line
        // reproduces the run from anywhere rather than only from the folder it was started in.
        if (options.AssemblyPath.HasContent())
            arguments.Add("/assembly:" + Path.GetFileName(options.AssemblyPath));

        arguments.Add("/source:" + options.WorkingDirectory);

        var items = options.Items.Where(x => x.HasContent()).Distinct().ToArray();

        if (options.Tool.ItemSwitch.HasContent() && items.Length != 0)
        {
            // One run covering several items, which is what the tools prefer: a loop of single
            // runs pays the process start, the load context handover and the assembly load again
            // every time.
            var value = options.Tool.SupportsBatch ? string.Join(";", items) : items[0];
            arguments.Add("/" + options.Tool.ItemSwitch + ":" + value);
        }

        if (options.Output.HasContent()) arguments.Add("/out:" + options.Output);
        if (options.PushUrl.HasContent()) arguments.Add("/push:" + options.PushUrl);
        if (options.ApiKey.HasContent()) arguments.Add("/apiKey:" + options.ApiKey);
        if (options.ServiceName.HasContent()) arguments.Add("/serviceName:" + options.ServiceName);

        if (options.HashOnly && options.Tool.SupportsHashOnly) arguments.Add("/hashonly");

        if (options.WaitForDebugger) arguments.Add("/debug");
    }

    static void AddLogging(List<string> arguments, RunOptions options)
    {
        if (options.Verbosity.HasContent()) arguments.Add("/verbosity:" + options.Verbosity);
        if (options.LogFormat.HasContent()) arguments.Add("/logformat:" + options.LogFormat);
        if (options.LogFile.HasContent()) arguments.Add("/log:" + options.LogFile);
        if (options.JsonLogFile.HasContent()) arguments.Add("/logjson:" + options.JsonLogFile);
        if (options.Heartbeat != null) arguments.Add("/heartbeat:" + options.Heartbeat);
        if (options.Timing) arguments.Add("/timing");
        if (options.NoColor) arguments.Add("/nocolor");
    }

    /// <summary>
    /// The whole thing as one line to copy. The api key is masked unless it is being revealed,
    /// because this control is the one a user screenshots.
    /// </summary>
    public static string Display(ToolResolution resolution, RunOptions options, bool revealApiKey)
    {
        var parts = new List<string>();

        if (resolution.FileName.HasContent()) parts.Add(resolution.FileName.Quoted());

        parts.AddRange(resolution.Prefix.Select(x => x.Quoted()));

        foreach (var argument in Build(options))
        {
            var shown = !revealApiKey && options.ApiKey.HasContent() && argument.StartsWith("/apiKey:")
                ? "/apiKey:***"
                : argument;

            parts.Add(shown.Quoted());
        }

        return string.Join(" ", parts);
    }

    /// <summary>
    /// Why this run cannot start, said before the tool would print a usage screen the user then
    /// has to interpret. Empty when it can.
    /// </summary>
    public static string[] Problems(RunOptions options)
    {
        var problems = new List<string>();

        if (options.Tool.Kind == ToolKind.AcceleratePackage)
        {
            if (options.SolutionFolder.IsBlank() || !Directory.Exists(options.SolutionFolder))
                problems.Add("Choose the solution folder to scan.");

            if (options.PackagesFile.IsBlank())
                problems.Add("Name the packages json file.");
            else if (options.Accelerate == AccelerateCommand.Restore && !ResolvesToAFile(options))
                problems.Add("The packages file to restore from does not exist.");

            return [.. problems];
        }

        if (options.AssemblyPath.IsBlank() || !File.Exists(options.AssemblyPath))
            problems.Add("Choose the target application assembly.");

        if (options.Items.Count(x => x.HasContent()) == 0)
            problems.Add($"Select at least one {options.Tool.ItemNoun}.");

        if (!options.Tool.SupportsBatch && options.Items.Count(x => x.HasContent()) > 1)
            problems.Add($"The {options.Tool.Title.ToLowerInvariant()} generator takes one " +
                $"{options.Tool.ItemNoun} per run. Use \"run once per selected item\".");

        // The parser throws on an /out: that does not exist, rather than creating it.
        if (options.Output.HasContent() && !Directory.Exists(options.Output) && !options.CreateOutputIfMissing)
            problems.Add("The output folder does not exist. Tick \"create if missing\", or choose another.");

        // The api proxy generator needs somewhere to publish before it will start at all, and it
        // does nothing with the service name it works out for itself, so pass one.
        if (options.Tool.Kind == ToolKind.ApiProxy)
        {
            if (options.Output.IsBlank() && options.PushUrl.IsBlank())
                problems.Add("The api proxy generator needs an output folder or a push url.");

            if (options.ServiceName.IsBlank())
                problems.Add("Set the service name. The generator reads only /serviceName:, so " +
                    "without it the generated proxy has no publisher.");
        }

        foreach (var file in new[] { options.LogFile, options.JsonLogFile })
        {
            if (file.IsBlank()) continue;

            var folder = Path.GetDirectoryName(Path.GetFullPath(file));
            if (folder.HasContent() && !Directory.Exists(folder))
                problems.Add("The log file folder does not exist: " + folder);
        }

        return [.. problems];
    }

    /// <summary>
    /// What is worth saying about a run that can still go ahead. Kept apart from
    /// <see cref="Problems"/> so an advisory never disables the Run button.
    /// </summary>
    public static string[] Warnings(RunOptions options)
    {
        var warnings = new List<string>();

        if (options.Tool.Kind == ToolKind.AcceleratePackage) return [];

        if (options.PushUrl.HasContent() && options.ApiKey.IsBlank())
            warnings.Add("Publishing to a nuget server usually needs an api key.");

        if (options.Verbosity is "quiet" or "minimal")
            warnings.Add("At this verbosity the run does not frame itself, so the exit code is the only outcome.");

        if (options.LogFormat == "text")
            warnings.Add("In text format nothing is a record, so every line arrives as raw output.");

        if (options.WaitForDebugger)
            warnings.Add("/debug makes the tool wait for a debugger before doing anything. Cancel to stop it.");

        return [.. warnings];
    }

    static bool ResolvesToAFile(RunOptions options)
    {
        try
        {
            var path = Path.IsPathRooted(options.PackagesFile!)
                ? options.PackagesFile!
                : Path.Combine(options.WorkingDirectory, options.PackagesFile!);

            return File.Exists(path);
        }
        catch { return false; }
    }
}
