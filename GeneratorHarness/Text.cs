using System.Diagnostics.CodeAnalysis;

namespace Olive.GeneratorHarness;

/// <summary>
/// The handful of string helpers this project uses. The generators get these from the Olive
/// package, which the harness deliberately does not reference.
/// </summary>
public static class Text
{
    public static bool IsBlank([NotNullWhen(false)] this string? text) => string.IsNullOrWhiteSpace(text);

    public static bool HasContent([NotNullWhen(true)] this string? text) => !string.IsNullOrWhiteSpace(text);

    public static string Or(this string? text, string fallback) => text.HasContent() ? text : fallback;

    /// <summary>The value, or null when it is blank, so an absent switch is easy to spot.</summary>
    public static string? OrNull(this string? text) => text.HasContent() ? text.Trim() : null;

    public static string Join(this IEnumerable<string?> items, string separator)
        => string.Join(separator, items.Where(x => x.HasContent()));

    /// <summary>The first line of a message, for a table cell that has room for one.</summary>
    public static string FirstLine(this string? text)
        => text.Or("").Replace("\r", "").Split('\n').FirstOrDefault(x => x.HasContent()).Or("");

    /// <summary>
    /// Quotes an argument the way a shell needs it, for the copyable command line only. What is
    /// actually run goes through ArgumentList, which needs no quoting at all.
    /// </summary>
    public static string Quoted(this string argument)
    {
        // A semicolon separates the items of a batch, and it ends a statement in PowerShell, so
        // an unquoted /dataEndpoint:A;B pastes as two commands. The rest are the characters a
        // shell would otherwise act on.
        const string NeedsQuoting = ";&|<>^()";

        if (argument.Length != 0 && !argument.Any(char.IsWhiteSpace)
            && !argument.Any(NeedsQuoting.Contains) && !argument.Contains('"'))
            return argument;

        return '"' + argument.Replace("\"", "\\\"") + '"';
    }

    /// <summary>
    /// Splits a line of free text into arguments, honouring double quotes, so the "extra
    /// arguments" box can carry a value containing a space.
    /// </summary>
    public static string[] SplitArguments(this string? line)
    {
        if (line.IsBlank()) return [];

        var results = new List<string>();
        var current = new System.Text.StringBuilder();
        var quoted = false;

        foreach (var character in line)
        {
            if (character == '"') { quoted = !quoted; continue; }

            if (!quoted && char.IsWhiteSpace(character))
            {
                if (current.Length != 0) { results.Add(current.ToString()); current.Clear(); }
                continue;
            }

            current.Append(character);
        }

        if (current.Length != 0) results.Add(current.ToString());

        return [.. results];
    }
}
