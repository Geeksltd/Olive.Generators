using System.Text.Json;

namespace Olive.GeneratorHarness.Discovery;

/// <summary>
/// Finds the publishing microservice's name the way the api proxy generator does: walk up from
/// the parent of the source folder looking for appSettings.json, then read Microservice:Me:Name.
/// <para>
/// The harness pre-fills the field with it because the generator reads only /serviceName: for the
/// value it uses, so a run without the switch generates a proxy with no publisher.
/// </para>
/// </summary>
public static class AppSettingsLocator
{
    public static (string? File, string? ServiceName) Find(string? assemblyPath)
    {
        if (assemblyPath.IsBlank()) return (null, null);

        try
        {
            var folder = new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(assemblyPath))!).Parent;

            while (folder != null)
            {
                var file = Path.Combine(folder.FullName, "appSettings.json");

                if (File.Exists(file)) return (file, NameIn(file));

                folder = folder.Parent;
            }
        }
        catch { /* A name the user can type themselves is not worth an error for. */ }

        return (null, null);
    }

    static string? NameIn(string file)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(file),
                new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });

            if (document.RootElement.TryGetProperty("Microservice", out var microservice) &&
                microservice.TryGetProperty("Me", out var me) &&
                me.TryGetProperty("Name", out var name) &&
                name.ValueKind == JsonValueKind.String)
                return name.GetString();
        }
        catch { /* Malformed or unreadable: the field stays empty and the user types it. */ }

        return null;
    }
}
