using Olive.GeneratorHarness.Tools;

namespace Olive.GeneratorHarness.Running;

/// <summary>One run of the suite, and what it is expected to amount to.</summary>
public sealed class SuiteStep
{
    public required string Name { get; init; }
    public required RunOptions Options { get; init; }

    /// <summary>Whether the step passes on a zero exit code or on a non-zero one.</summary>
    public bool ExpectSuccess { get; init; } = true;

    /// <summary>A further condition on what the run said, or null.</summary>
    public Func<RunResult, string?>? Expectation { get; init; }
}

/// <summary>What one step of the suite came to.</summary>
public sealed class SuiteOutcome
{
    public required string Name { get; init; }
    public required RunResult Result { get; init; }
    public string? Failure { get; init; }
    public bool Passed => Failure == null;
}

/// <summary>
/// The smoke test the harness exists for: after a change to a generator, drive the cheap
/// operations against the current selection and say which ones still work.
/// <para>
/// Only operations that publish nothing are included. Nothing here pushes to a nuget server, and
/// the one generate it does runs into a temporary folder, so the suite is safe to run on a whim.
/// </para>
/// </summary>
public static class RunAllSuite
{
    public static List<SuiteStep> Plan(RunOptions template, IReadOnlyList<string> endpoints,
        IReadOnlyList<string> commands, IReadOnlyList<string> controllers, string temporaryOutput)
    {
        var steps = new List<SuiteStep>();

        AddHashes(steps, template, GeneratorTool.DataEndpoint, endpoints);
        AddHashes(steps, template, GeneratorTool.EventBusCommand, commands);

        AddGenerate(steps, template, GeneratorTool.DataEndpoint, endpoints, temporaryOutput);
        AddGenerate(steps, template, GeneratorTool.EventBusCommand, commands, temporaryOutput);
        AddGenerate(steps, template, GeneratorTool.ApiProxy, controllers, temporaryOutput);

        AddFailurePaths(steps, template, endpoints);

        return steps;
    }

    static void AddHashes(List<SuiteStep> steps, RunOptions template, GeneratorTool tool,
        IReadOnlyList<string> items)
    {
        if (items.Count == 0) return;

        steps.Add(new SuiteStep
        {
            Name = $"{tool.Title}: hash one {tool.ItemNoun}",
            Options = With(template, tool, [items[0]], hashOnly: true),
            Expectation = x => x.Results.Count == 0 ? "no hash was reported" : null,
        });

        if (items.Count < 2) return;

        steps.Add(new SuiteStep
        {
            Name = $"{tool.Title}: hash {items.Count} {tool.ItemNoun}s in one run",
            Options = With(template, tool, items, hashOnly: true),

            // The batch names each hash after its item, where a single run calls it "Endpoint Hash".
            Expectation = x => x.Results.Count < items.Count
                ? $"expected {items.Count} hashes, got {x.Results.Count}"
                : null,
        });
    }

    static void AddGenerate(List<SuiteStep> steps, RunOptions template, GeneratorTool tool,
        IReadOnlyList<string> items, string output)
    {
        if (items.Count == 0) return;

        var options = With(template, tool, [items[0]], hashOnly: false);
        options.Output = output;
        options.CreateOutputIfMissing = true;

        // The one verbosity that carries the build's own output, which is what a generate is
        // worth watching for.
        options.Verbosity = "detailed";

        steps.Add(new SuiteStep
        {
            Name = $"{tool.Title}: generate {items[0]}",
            Options = options,
            Expectation = x => x.LogFiles.Count == 0 ? "the run reported no log file" : null,
        });
    }

    static void AddFailurePaths(List<SuiteStep> steps, RunOptions template, IReadOnlyList<string> endpoints)
    {
        if (endpoints.Count == 0) return;

        // A name no assembly has. The tools must report it and exit non-zero rather than
        // succeeding quietly, and in a batch the other items must still be attempted.
        var options = With(template, GeneratorTool.DataEndpoint,
            [endpoints[0], "NoSuchTypeInThisAssembly"], hashOnly: true);

        steps.Add(new SuiteStep
        {
            Name = "Data endpoint: an unknown name fails without costing the others their hash",
            Options = options,
            ExpectSuccess = false,
            Expectation = x => x.Results.Count == 0
                ? "the endpoint that does exist reported no hash"
                : null,
        });
    }

    static RunOptions With(RunOptions template, GeneratorTool tool, IReadOnlyList<string> items, bool hashOnly)
    {
        var options = template.Clone();

        options.Tool = tool;
        options.Items = [.. items];
        options.HashOnly = hashOnly && tool.SupportsHashOnly;

        // Never in the suite: it publishes, and it blocks.
        options.PushUrl = null;
        options.ApiKey = null;
        options.WaitForDebugger = false;

        // The harness has to read what comes back, whatever the form was set to.
        options.LogFormat = "json";

        if (hashOnly) options.Output = null;

        return options;
    }

    /// <summary>Whether a finished run met what its step asked of it.</summary>
    public static SuiteOutcome Judge(SuiteStep step, RunResult result)
    {
        string? failure = null;

        if (result.StartFailure.HasContent()) failure = result.StartFailure;
        else if (result.Cancelled) failure = "cancelled";

        // The code itself is not pinned: the generators return -1, which a shell reports as 255,
        // so what matters is whether the run failed, not how the platform spelled it.
        else if (step.ExpectSuccess && result.ExitCode != 0)
            failure = "expected it to succeed, but the exit code was " + result.ExitCode;
        else if (!step.ExpectSuccess && result.ExitCode == 0)
            failure = "expected it to fail, but it succeeded";
        else failure = step.Expectation?.Invoke(result);

        return new SuiteOutcome { Name = step.Name, Result = result, Failure = failure };
    }
}
