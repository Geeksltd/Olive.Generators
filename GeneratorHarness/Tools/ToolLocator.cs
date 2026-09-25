using System.Diagnostics;

namespace Olive.GeneratorHarness.Tools;

/// <summary>Where a tool was found, in the order the locator prefers.</summary>
public enum ToolSource
{
    /// <summary>A path the user browsed to.</summary>
    Override,

    /// <summary>This repository's own build output. The whole point of the harness.</summary>
    RepoBuild,

    /// <summary>The installed dotnet global tool.</summary>
    GlobalTool,

    NotFound,
}

/// <summary>What the harness will actually start for one tool.</summary>
public sealed class ToolResolution
{
    public required GeneratorTool Tool { get; init; }
    public required ToolSource Source { get; init; }

    /// <summary>The executable to start. The dotnet host when <see cref="Prefix"/> names a dll.</summary>
    public string? FileName { get; init; }

    /// <summary>Arguments that come before the tool's own, i.e. the dll path when going via dotnet.</summary>
    public IReadOnlyList<string> Prefix { get; init; } = [];

    /// <summary>The tool's own file, for display, whether it is the exe or the dll.</summary>
    public string? Path { get; init; }

    public DateTime? Built { get; init; }
    public string? Version { get; init; }

    public bool Found => Source != ToolSource.NotFound;

    public string Describe()
    {
        if (!Found) return Tool.ToolCommand + " - not found";

        var where = Source switch
        {
            ToolSource.Override => "manual override",
            ToolSource.RepoBuild => "repo build",
            _ => "global tool",
        };

        var built = Built == null ? "" : " - " + Built.Value.ToString("yyyy-MM-dd HH:mm");
        var version = Version.IsBlank() ? "" : " - " + Version;

        return $"{Tool.ToolCommand} - {where}{built}{version}";
    }
}

/// <summary>
/// Finds the four tools. The repository's own build output is preferred over the installed
/// global tool, because the harness exists to exercise the code in this working copy.
/// </summary>
public sealed class ToolLocator
{
    /// <summary>The framework the generators are built for. Pinned, because stale net8.0 output
    /// still sits in the same bin folders.</summary>
    const string TargetFramework = "net10.0";

    readonly Dictionary<ToolKind, string> Overrides;

    public ToolLocator(Dictionary<ToolKind, string>? overrides = null)
        => Overrides = overrides ?? [];

    /// <summary>Which build output to prefer.</summary>
    public string Configuration { get; set; } = "Debug";

    /// <summary>The root of this repository, or null when the harness was copied elsewhere.</summary>
    public string? RepositoryRoot { get; } = FindRepositoryRoot();

    /// <summary>
    /// The dotnet host to run a framework-dependent dll with, and to hand the child so its own
    /// builds use the same one. Mirrors ProcessRunner in SharedNew.
    /// </summary>
    public static string DotNetHost { get; } = FindDotNetHost();

    public ToolResolution Resolve(GeneratorTool tool)
    {
        if (Overrides.TryGetValue(tool.Kind, out var chosen) && File.Exists(chosen))
            return Describe(tool, ToolSource.Override, chosen);

        var fromRepo = FindInRepository(tool);
        if (fromRepo != null) return Describe(tool, ToolSource.RepoBuild, fromRepo);

        var installed = FindGlobalTool(tool);
        if (installed != null) return Describe(tool, ToolSource.GlobalTool, installed);

        return new ToolResolution { Tool = tool, Source = ToolSource.NotFound };
    }

    /// <summary>The project file of a tool inside this repository, for the rebuild button.</summary>
    public string? ProjectPathOf(GeneratorTool tool)
    {
        if (RepositoryRoot == null) return null;

        var path = System.IO.Path.Combine(RepositoryRoot, tool.ProjectFolder, tool.ProjectFile);
        return File.Exists(path) ? path : null;
    }

    string? FindInRepository(GeneratorTool tool)
    {
        if (RepositoryRoot == null) return null;

        // The preferred configuration first, then the other one, so a harness built in Debug
        // still finds generators that were only ever built in Release.
        foreach (var configuration in new[] { Configuration, Other(Configuration) })
        {
            var folder = System.IO.Path.Combine(
                RepositoryRoot, tool.ProjectFolder, "bin", configuration, TargetFramework);

            if (!Directory.Exists(folder)) continue;

            // The apphost when it exists, so no dotnet host has to be found at all.
            var exe = System.IO.Path.Combine(folder, tool.AssemblyName + ".exe");
            if (File.Exists(exe)) return exe;

            var dll = System.IO.Path.Combine(folder, tool.AssemblyName + ".dll");
            if (File.Exists(dll)) return dll;
        }

        return null;
    }

    static string Other(string configuration)
        => configuration.Equals("Debug", StringComparison.OrdinalIgnoreCase) ? "Release" : "Debug";

    static string? FindGlobalTool(GeneratorTool tool)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        var candidates = new List<string>
        {
            System.IO.Path.Combine(home, ".dotnet", "tools", tool.ToolCommand + ".exe"),
        };

        foreach (var folder in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(System.IO.Path.PathSeparator))
        {
            if (folder.IsBlank()) continue;

            candidates.Add(System.IO.Path.Combine(folder.Trim(), tool.ToolCommand + ".exe"));
            candidates.Add(System.IO.Path.Combine(folder.Trim(), tool.ToolCommand + ".cmd"));
        }

        return candidates.FirstOrDefault(SafeExists);
    }

    static bool SafeExists(string path)
    {
        try { return File.Exists(path); }
        catch { return false; }
    }

    static ToolResolution Describe(GeneratorTool tool, ToolSource source, string path)
    {
        var isDll = path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);

        return new ToolResolution
        {
            Tool = tool,
            Source = source,
            Path = path,
            FileName = isDll ? DotNetHost : path,
            Prefix = isDll ? [path] : [],
            Built = SafeWriteTime(path),
            Version = SafeVersion(path),
        };
    }

    static DateTime? SafeWriteTime(string path)
    {
        try { return File.GetLastWriteTime(path); }
        catch { return null; }
    }

    static string? SafeVersion(string path)
    {
        try { return FileVersionInfo.GetVersionInfo(path).FileVersion; }
        catch { return null; }
    }

    /// <summary>
    /// Walks up from the running harness looking for the solution, so the sibling generator
    /// projects can be found however deep the build output is.
    /// </summary>
    static string? FindRepositoryRoot()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);

        while (folder != null)
        {
            if (folder.GetFiles("Olive.Generators.sln").Length != 0) return folder.FullName;
            folder = folder.Parent;
        }

        return null;
    }

    static string FindDotNetHost()
    {
        var declared = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        if (declared.HasContent() && File.Exists(declared)) return declared;

        var installed = System.IO.Path.Combine(
            Environment.GetEnvironmentVariable("ProgramFiles") ?? @"C:\Program Files",
            "dotnet", "dotnet.exe");

        return File.Exists(installed) ? installed : "dotnet";
    }
}
