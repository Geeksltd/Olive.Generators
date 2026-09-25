namespace Olive.GeneratorHarness.Tools;

/// <summary>Which of the repository's four command line tools a run is for.</summary>
public enum ToolKind { DataEndpoint, EventBusCommand, ApiProxy, AcceleratePackage }

/// <summary>
/// One of the tools this repository ships, as data: where its build output lives, what the
/// installed global tool is called, and which switches it accepts.
/// <para>
/// The switch spellings live here and nowhere else. <c>ParametersParserBase.Param</c> matches
/// "/key:" case sensitively while the load context chooser and the log both match it case
/// insensitively, so a mis-cased switch would be honoured by one and silently dropped by the
/// other. Nothing in the UI is allowed to build a switch string of its own.
/// </para>
/// </summary>
public sealed class GeneratorTool
{
    public required ToolKind Kind { get; init; }

    /// <summary>What the tool is called in the UI.</summary>
    public required string Title { get; init; }

    /// <summary>The folder of its project inside this repository.</summary>
    public required string ProjectFolder { get; init; }

    /// <summary>Its project file name, so the harness can rebuild it.</summary>
    public required string ProjectFile { get; init; }

    /// <summary>The assembly its build produces, without an extension.</summary>
    public required string AssemblyName { get; init; }

    /// <summary>The command the installed global tool is invoked by.</summary>
    public required string ToolCommand { get; init; }

    /// <summary>The switch naming the item to generate, exactly as the parser spells it.</summary>
    public string? ItemSwitch { get; init; }

    /// <summary>The noun for one item of this tool's work, for labels and messages.</summary>
    public string ItemNoun { get; init; } = "item";

    /// <summary>Whether several items may be listed in one run, separated by a semicolon.</summary>
    public bool SupportsBatch { get; init; }

    /// <summary>Whether the tool implements /hashonly. The api proxy generator does not.</summary>
    public bool SupportsHashOnly { get; init; }

    /// <summary>Whether the tool reads a target application assembly at all.</summary>
    public bool UsesTargetAssembly { get; init; } = true;

    public override string ToString() => Title;

    public static readonly GeneratorTool DataEndpoint = new()
    {
        Kind = ToolKind.DataEndpoint,
        Title = "Data endpoint",
        ProjectFolder = "DataEndPointGenerator",
        ProjectFile = "Olive.DataEndpointGenerator.csproj",
        AssemblyName = "Olive.DataEndpointGenerator",
        ToolCommand = "generate-data-endpoint-proxy",
        ItemSwitch = "dataEndpoint",
        ItemNoun = "endpoint",
        SupportsBatch = true,
        SupportsHashOnly = true,
    };

    public static readonly GeneratorTool EventBusCommand = new()
    {
        Kind = ToolKind.EventBusCommand,
        Title = "EventBus command",
        ProjectFolder = "EventBusCommandGenerator",
        ProjectFile = "Olive.EventBusCommandGenerator.csproj",
        AssemblyName = "Olive.EventBusCommandGenerator",
        ToolCommand = "generate-eventbus-command-proxy",
        ItemSwitch = "command",
        ItemNoun = "command",
        SupportsBatch = true,
        SupportsHashOnly = true,
    };

    public static readonly GeneratorTool ApiProxy = new()
    {
        Kind = ToolKind.ApiProxy,
        Title = "Api proxy",
        ProjectFolder = "ApiProxyGenerator",
        ProjectFile = "Olive.ApiProxyGenerator.csproj",
        AssemblyName = "Olive.ApiProxyGenerator",
        ToolCommand = "generate-api-proxy",
        ItemSwitch = "controller",
        ItemNoun = "controller",

        // /controller: takes a single name, and Program.cs has no hash branch.
        SupportsBatch = false,
        SupportsHashOnly = false,
    };

    public static readonly GeneratorTool AcceleratePackage = new()
    {
        Kind = ToolKind.AcceleratePackage,
        Title = "Accelerate package",
        ProjectFolder = "AcceleratePackage",
        ProjectFile = "Olive.AcceleratePackage.csproj",
        AssemblyName = "Olive.AcceleratePackageRestore",
        ToolCommand = "accelerate-package-restore",
        ItemNoun = "command",
        UsesTargetAssembly = false,
    };

    public static readonly GeneratorTool[] All =
        [DataEndpoint, EventBusCommand, ApiProxy, AcceleratePackage];

    public static GeneratorTool Of(ToolKind kind) => All.First(x => x.Kind == kind);
}
