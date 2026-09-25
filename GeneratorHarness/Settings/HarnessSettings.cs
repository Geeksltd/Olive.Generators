using System.Text.Json;
using System.Text.Json.Serialization;
using Olive.GeneratorHarness.Tools;

namespace Olive.GeneratorHarness.Settings;

/// <summary>
/// What the harness remembers between sessions. The api key is deliberately not among it: it is
/// typed each time, and lives only in memory and on the command line of the run it is for.
/// </summary>
public sealed class HarnessSettings
{
    public List<string> RecentAssemblies { get; set; } = [];
    public Dictionary<string, string> ToolOverrides { get; set; } = [];
    public string Configuration { get; set; } = "Debug";

    public string? Output { get; set; }
    public string? PushUrl { get; set; }
    public string? ServiceName { get; set; }
    public string? SolutionFolder { get; set; }
    public string? PackagesFile { get; set; }

    public string Verbosity { get; set; } = "normal";
    public string LogFormat { get; set; } = "json";
    public int Heartbeat { get; set; } = 15;
    public bool Timing { get; set; }
    public bool NoColor { get; set; }
    public string? LogFile { get; set; }
    public string? JsonLogFile { get; set; }
    public string? ExtraArguments { get; set; }

    public int WindowWidth { get; set; }
    public int WindowHeight { get; set; }
    public bool Maximised { get; set; }

    [JsonIgnore]
    public static string Path { get; } = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Olive.GeneratorHarness", "settings.json");

    public Dictionary<ToolKind, string> Overrides()
    {
        var results = new Dictionary<ToolKind, string>();

        foreach (var pair in ToolOverrides)
            if (Enum.TryParse<ToolKind>(pair.Key, out var kind)) results[kind] = pair.Value;

        return results;
    }

    public void Remember(string assembly)
    {
        RecentAssemblies.RemoveAll(x => string.Equals(x, assembly, StringComparison.OrdinalIgnoreCase));
        RecentAssemblies.Insert(0, assembly);

        while (RecentAssemblies.Count > 10) RecentAssemblies.RemoveAt(RecentAssemblies.Count - 1);
    }

    public static HarnessSettings Load()
    {
        try
        {
            if (File.Exists(Path))
                return JsonSerializer.Deserialize<HarnessSettings>(File.ReadAllText(Path)) ?? new HarnessSettings();
        }
        catch { /* A corrupt settings file must never stop the harness starting. */ }

        return new HarnessSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);

            File.WriteAllText(Path,
                JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* Losing the last window size is not worth a dialog. */ }
    }
}
