using System.Globalization;
using System.Text.Json;

namespace Olive.GeneratorHarness.Running;

/// <summary>
/// One line of a run. Under /logformat:json every line the tool writes to standard output is one
/// of these, with the head fields below and whatever extra fields its kind adds. The extras are
/// kept as a dictionary rather than a class per kind, so a kind added to the tools later still
/// arrives here intact.
/// <para>
/// Anything that is not valid json - the usage screen, a bare hash line in text format, a
/// ::error:: annotation under GITHUB_ACTIONS, output on standard error - becomes a record too,
/// of kind "raw" or "stderr", so nothing a run said is ever dropped.
/// </para>
/// </summary>
public sealed class LogRecord
{
    public long Seq { get; init; }
    public string? Run { get; init; }
    public DateTime? Time { get; init; }
    public double ElapsedSeconds { get; init; }

    /// <summary>ERROR, WARN, INFO, DEBUG or TRACE.</summary>
    public string Level { get; init; } = "INFO";

    public string? Verbosity { get; init; }
    public string Kind { get; init; } = "message";
    public int Depth { get; init; }

    /// <summary>The endpoint, command or controller the line is about, or null.</summary>
    public string? Item { get; init; }

    public string Message { get; init; } = "";

    /// <summary>Every field beyond the head, by name, rendered as text.</summary>
    public IReadOnlyDictionary<string, string> Extra { get; init; }
        = new Dictionary<string, string>();

    public string? Field(string name) => Extra.TryGetValue(name, out var value) ? value : null;

    public bool IsError => Level == "ERROR";
    public bool IsWarning => Level == "WARN";

    /// <summary>A step that closed having failed, which the tools do not mark as an error line.</summary>
    public bool IsFailedStep => Kind == "step-end" && Field("outcome") == "failed";

    /// <summary>The elapsed stamp the text format prints, so the view reads like the console.</summary>
    public string Elapsed
    {
        get
        {
            var span = TimeSpan.FromSeconds(Math.Max(0, ElapsedSeconds));
            return $"+{(int)span.TotalMinutes:00}:{span.Seconds:00}.{span.Milliseconds / 100}";
        }
    }

    /// <summary>The marker the text format uses: opening a step, closing one, child output, a beat.</summary>
    public string Marker => Kind switch
    {
        "step-start" => ">",
        "step-end" => "<",
        "child-output" => "|",
        "heartbeat" => "...",
        _ => "",
    };

    /// <summary>The message as the console would lay it out, indented two spaces per level.</summary>
    public string Indented
    {
        get
        {
            var body = Kind == "child-output" ? Field("output").Or(Message) : Message;
            var marker = Marker;

            return new string(' ', Math.Max(0, Depth) * 2) + (marker.IsBlank() ? "" : marker + " ") + body;
        }
    }
}

/// <summary>Turns the tools' newline delimited json into records, tolerating anything that is not.</summary>
public static class LogRecordParser
{
    static readonly HashSet<string> Head =
    [
        "seq", "run", "time", "elapsedSeconds", "level", "verbosity", "kind", "depth", "item", "message",
    ];

    /// <summary>Parses one line of standard output.</summary>
    public static LogRecord Parse(string line)
    {
        var trimmed = line.TrimStart('﻿').Trim();

        if (trimmed.Length == 0 || trimmed[0] != '{') return Raw(line);

        try
        {
            using var document = JsonDocument.Parse(trimmed);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object) return Raw(line);

            var extra = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var property in root.EnumerateObject())
            {
                if (Head.Contains(property.Name)) continue;

                var rendered = Render(property.Value);
                if (rendered != null) extra[property.Name] = rendered;
            }

            return new LogRecord
            {
                Seq = Long(root, "seq"),
                Run = String(root, "run"),
                Time = Moment(root, "time"),
                ElapsedSeconds = Double(root, "elapsedSeconds"),
                Level = String(root, "level").Or("INFO"),
                Verbosity = String(root, "verbosity"),
                Kind = String(root, "kind").Or("message"),
                Depth = (int)Long(root, "depth"),
                Item = String(root, "item"),
                Message = String(root, "message").Or(""),
                Extra = extra,
            };
        }
        catch (JsonException)
        {
            // A line that looked like json and was not. Showing it beats losing it.
            return Raw(line);
        }
    }

    /// <summary>A line of standard output that was not a record.</summary>
    public static LogRecord Raw(string line) => new() { Kind = "raw", Level = "INFO", Message = line };

    /// <summary>
    /// A line of standard error. The tools send the usage screen, the /timing breakdown and the
    /// log's own complaints there, so it is worth surfacing next to everything else.
    /// </summary>
    public static LogRecord Error(string line) => new() { Kind = "stderr", Level = "WARN", Message = line };

    /// <summary>A line the harness itself says about a run, so it appears in the same sequence.</summary>
    public static LogRecord Harness(string message, string level = "INFO")
        => new() { Kind = "harness", Level = level, Message = message };

    static string? String(JsonElement root, string name)
        => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    static long Long(JsonElement root, string name)
        => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt64(out var number) ? number : 0;

    static double Double(JsonElement root, string name)
        => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            && value.TryGetDouble(out var number) ? number : 0;

    static DateTime? Moment(JsonElement root, string name)
        => DateTime.TryParse(String(root, name), CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind, out var moment) ? moment : null;

    static string? Render(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        _ => value.ToString(),
    };
}
