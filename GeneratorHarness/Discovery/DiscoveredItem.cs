using Olive.GeneratorHarness.Tools;

namespace Olive.GeneratorHarness.Discovery;

/// <summary>One type the target assembly offers to one of the generators.</summary>
public sealed record DiscoveredItem
{
    public required ToolKind Kind { get; init; }
    public required string FullName { get; init; }
    public required string ShortName { get; init; }
    public string? Namespace { get; init; }

    /// <summary>What the row says about it, such as how many api actions it declares.</summary>
    public string Detail { get; init; } = "";

    /// <summary>Things that would make the generator throw, said before the run rather than after.</summary>
    public List<string> Warnings { get; init; } = [];

    /// <summary>True when the base chain could not be followed, so this is a guess.</summary>
    public bool Unresolved { get; init; }

    public override string ToString() => FullName;
}

/// <summary>Everything one scan of a target assembly found.</summary>
public sealed class DiscoveryResult
{
    public required string AssemblyPath { get; init; }
    public string? AssemblyFullName { get; set; }
    public DateTime ScannedAt { get; init; } = DateTime.Now;
    public DateTime? Built { get; init; }
    public int TypeCount { get; set; }

    public List<DiscoveredItem> Endpoints { get; } = [];
    public List<DiscoveredItem> Commands { get; } = [];
    public List<DiscoveredItem> Controllers { get; } = [];

    /// <summary>Every public type, for when a predicate missed the one the user wants.</summary>
    public List<DiscoveredItem> AllTypes { get; } = [];

    /// <summary>Types whose base chain could not be followed, and why that usually is.</summary>
    public List<DiscoveredItem> Unclassified { get; } = [];

    /// <summary>Assemblies the target references that are not beside it. Nearly always the reason
    /// a list came back empty.</summary>
    public SortedSet<string> UnresolvedAssemblies { get; } = new(StringComparer.OrdinalIgnoreCase);

    public string? Failure { get; set; }

    public List<DiscoveredItem> For(ToolKind kind) => kind switch
    {
        ToolKind.DataEndpoint => Endpoints,
        ToolKind.EventBusCommand => Commands,
        ToolKind.ApiProxy => Controllers,
        _ => [],
    };
}
