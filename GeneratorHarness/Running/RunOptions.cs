using Olive.GeneratorHarness.Tools;

namespace Olive.GeneratorHarness.Running;

public enum AccelerateCommand { Extract, Restore }

/// <summary>
/// Everything one run needs, as the form holds it. Turning this into a command line is
/// <see cref="CommandLineBuilder"/>'s job and nothing else's.
/// </summary>
public sealed class RunOptions
{
    public GeneratorTool Tool { get; set; } = GeneratorTool.DataEndpoint;

    // ---- The target application -------------------------------------------------------------

    /// <summary>The full path to the target assembly, e.g. ...\bin\Debug\net10.0\Website.dll.</summary>
    public string? AssemblyPath { get; set; }

    /// <summary>Overrides the folder passed as /source:. Defaults to the assembly's own folder.</summary>
    public string? SourceOverride { get; set; }

    /// <summary>The items to generate. Several are joined with a semicolon for one batch run.</summary>
    public List<string> Items { get; set; } = [];

    // ---- Publishing --------------------------------------------------------------------------

    public string? Output { get; set; }
    public bool CreateOutputIfMissing { get; set; } = true;
    public string? PushUrl { get; set; }
    public string? ApiKey { get; set; }
    public string? ServiceName { get; set; }
    public bool HashOnly { get; set; }

    // ---- Logging -----------------------------------------------------------------------------

    /// <summary>quiet, minimal, normal, detailed or diagnostic.</summary>
    public string Verbosity { get; set; } = "normal";

    /// <summary>text or json. The harness parses json; text arrives as raw lines.</summary>
    public string LogFormat { get; set; } = "json";

    public string? LogFile { get; set; }
    public string? JsonLogFile { get; set; }

    /// <summary>Seconds of silence before a run says which step it is on. Null leaves the default.</summary>
    public int? Heartbeat { get; set; }

    public bool Timing { get; set; }
    public bool NoColor { get; set; }

    /// <summary>Makes the tool block until a debugger attaches. Only ever on request.</summary>
    public bool WaitForDebugger { get; set; }

    /// <summary>Anything the form has no field for, passed through verbatim.</summary>
    public string? ExtraArguments { get; set; }

    // ---- Accelerate package ------------------------------------------------------------------

    /// <summary>The solution folder it scans for csproj files. Its working directory.</summary>
    public string? SolutionFolder { get; set; }

    public AccelerateCommand Accelerate { get; set; } = AccelerateCommand.Extract;

    /// <summary>The packages json file it writes or reads.</summary>
    public string? PackagesFile { get; set; }

    public bool Parallel { get; set; }

    public RunOptions Clone() => (RunOptions)MemberwiseClone();

    /// <summary>The folder the tool is started in. /source: defaults to it, so it matters.</summary>
    public string WorkingDirectory
    {
        get
        {
            if (Tool.Kind == ToolKind.AcceleratePackage)
                return SolutionFolder.Or(Environment.CurrentDirectory);

            if (SourceOverride.HasContent()) return SourceOverride;

            var folder = AssemblyPath.HasContent() ? Path.GetDirectoryName(AssemblyPath) : null;

            return folder.Or(Environment.CurrentDirectory);
        }
    }
}
