using Olive.GeneratorHarness.Discovery;
using Olive.GeneratorHarness.Tools;

namespace Olive.GeneratorHarness.UI;

/// <summary>
/// The items one generator can be pointed at, as found in the target assembly. Never a dead end:
/// the predicates here read metadata while the generators reflect for real, so a name the scan
/// missed can still be typed in, and every public type can be listed instead.
/// </summary>
public sealed class DiscoveryTab : UserControl
{
    public event EventHandler? Changed;

    readonly GeneratorTool Tool;
    readonly ListView Grid = new();
    readonly TextBox Filter = new();
    readonly CheckBox ShowAll = new() { Text = "show every public type", AutoSize = true };
    readonly CheckBox OneEach = new() { Text = "run once per selected item", AutoSize = true };
    readonly TextBox Typed = new();
    readonly Label Status = new() { Dock = DockStyle.Bottom, AutoSize = false, Height = 46, ForeColor = Color.DimGray };

    DiscoveryResult? Result;
    List<DiscoveredItem> Available = [];

    public DiscoveryTab(GeneratorTool tool)
    {
        Tool = tool;
        Dock = DockStyle.Fill;

        Grid.Dock = DockStyle.Fill;
        Grid.View = View.Details;
        Grid.CheckBoxes = true;
        Grid.FullRowSelect = true;
        Grid.HideSelection = false;
        Grid.Columns.Add(Capitalise(tool.ItemNoun), 300);
        Grid.Columns.Add("Namespace", 200);
        Grid.Columns.Add("Detail", 120);
        Grid.Columns.Add("Warnings", 320);
        Grid.ItemChecked += (_, _) => Raise();

        Filter.PlaceholderText = "Filter";
        Filter.Width = 200;
        Filter.TextChanged += (_, _) => Fill();

        ShowAll.CheckedChanged += (_, _) => Fill();
        OneEach.CheckedChanged += (_, _) => Raise();

        // The generators resolve a name with GetType(name) falling back to a match on Type.Name,
        // so either the full name or the short one works here.
        Typed.PlaceholderText = "or type a name the scan missed, separated by ';'";
        Typed.TextChanged += (_, _) => Raise();

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(4) };
        top.Controls.Add(Filter);
        top.Controls.Add(Button("All", () => Check(true)));
        top.Controls.Add(Button("None", () => Check(false)));
        top.Controls.Add(ShowAll);
        if (tool.SupportsBatch) top.Controls.Add(OneEach);

        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 30, Padding = new Padding(4, 3, 4, 3) };
        Typed.Dock = DockStyle.Fill;
        bottom.Controls.Add(Typed);

        Controls.Add(Grid);
        Controls.Add(bottom);
        Controls.Add(Status);
        Controls.Add(top);

        // The api proxy generator takes one controller per run, so one run per item is the only
        // way to cover several and the choice is not the user's to make.
        if (!tool.SupportsBatch) OneEach.Checked = true;
    }

    static string Capitalise(string text) => char.ToUpperInvariant(text[0]) + text[1..];

    static Button Button(string text, Action click)
    {
        var button = new Button { Text = text, AutoSize = true };
        button.Click += (_, _) => click();
        return button;
    }

    void Raise() => Changed?.Invoke(this, EventArgs.Empty);

    /// <summary>Whether each selected item gets its own run, rather than one batch covering them all.</summary>
    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool RunOncePerItem => OneEach.Checked || !Tool.SupportsBatch;

    /// <summary>Everything the user chose, whether ticked in the list or typed underneath.</summary>
    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public List<string> Selected
    {
        get
        {
            var chosen = Grid.CheckedItems.Cast<ListViewItem>()
                .Select(x => (x.Tag as DiscoveredItem)?.FullName)
                .Where(x => x.HasContent())
                .Select(x => x!)
                .ToList();

            foreach (var typed in Typed.Text.Split(';', StringSplitOptions.RemoveEmptyEntries))
                if (typed.Trim().HasContent()) chosen.Add(typed.Trim());

            return chosen.Distinct().ToList();
        }
    }

    public void Show(DiscoveryResult? result)
    {
        Result = result;
        Fill();
    }

    void Fill()
    {
        Available = ShowAll.Checked
            ? Result?.AllTypes ?? []
            : Result?.For(Tool.Kind) ?? [];

        var filter = Filter.Text;

        Grid.BeginUpdate();
        Grid.Items.Clear();

        foreach (var item in Available)
        {
            if (filter.HasContent() && item.FullName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            var row = new ListViewItem(item.ShortName) { Tag = item };
            row.SubItems.Add(item.Namespace.Or(""));
            row.SubItems.Add(item.Detail);
            row.SubItems.Add(item.Warnings.Join("; "));

            if (item.Warnings.Count != 0) row.ForeColor = Color.DarkOrange;
            if (item.Unresolved) row.ForeColor = Color.DimGray;

            Grid.Items.Add(row);
        }

        Grid.EndUpdate();

        Status.Text = Describe();
        Raise();
    }

    string Describe()
    {
        if (Result == null) return "Choose a target assembly and scan it.";

        if (Result.Failure.HasContent())
            return "The assembly could not be read: " + Result.Failure;

        if (Grid.Items.Count != 0)
        {
            var scanned = $"{Grid.Items.Count} of {Available.Count} shown, from {Result.TypeCount} types in " +
                Path.GetFileName(Result.AssemblyPath) + ".";

            return Result.UnresolvedAssemblies.Count == 0
                ? scanned
                : scanned + " Could not resolve: " + Result.UnresolvedAssemblies.Take(6).Join(", ") + ".";
        }

        var expected = Tool.Kind switch
        {
            ToolKind.DataEndpoint => "a concrete class deriving from Olive.Entities.Replication.SourceEndpoint",
            ToolKind.EventBusCommand => "a concrete class deriving from Olive.EventBusMessage",
            _ => "a class declaring a method with a Route or Http verb attribute",
        };

        var reason = Result.UnresolvedAssemblies.Count == 0
            ? ""
            : " These assemblies are not beside the target and could not be resolved, which is usually why: " +
                Result.UnresolvedAssemblies.Take(6).Join(", ") + ".";

        return $"Scanned {Result.TypeCount} types in {Path.GetFileName(Result.AssemblyPath)} and found no " +
            $"{Tool.ItemNoun}. Expected {expected}.{reason} Tick \"show every public type\", or type a name below.";
    }

    void Check(bool state)
    {
        Grid.BeginUpdate();
        foreach (ListViewItem row in Grid.Items) row.Checked = state;
        Grid.EndUpdate();
    }
}
