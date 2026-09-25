using Olive.GeneratorHarness.Running;

namespace Olive.GeneratorHarness.UI;

/// <summary>
/// What a run produced, from the summary records every generator writes in its finally block.
/// They are written at minimal verbosity, so they survive everything but /verbosity:quiet, and
/// they arrive after a failure too: a run that produced half of what was asked for is exactly the
/// one whose summary is worth reading.
/// </summary>
public sealed class SummaryView : UserControl
{
    readonly Label Totals = new();
    readonly DataGridView Grid = new();
    readonly DataGridView Values = new();

    public SummaryView()
    {
        Dock = DockStyle.Fill;

        Totals.Dock = DockStyle.Top;
        Totals.Height = 28;
        Totals.TextAlign = ContentAlignment.MiddleLeft;
        Totals.Padding = new Padding(6, 0, 0, 0);
        Totals.Font = new Font(Font, FontStyle.Bold);
        Totals.Text = "No run yet.";

        Configure(Grid);
        Grid.Columns.Add("what", "Item");
        Grid.Columns.Add("status", "Status");
        Grid.Columns.Add("notes", "Notes");
        Grid.Columns.Add("failure", "Failure");
        Grid.Columns[0].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        Grid.Columns[3].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

        Configure(Values);
        Values.Columns.Add("name", "Result");
        Values.Columns.Add("value", "Value");
        Values.Columns[1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };

        split.Panel1.Controls.Add(Grid);
        split.Panel2.Controls.Add(Values);

        // Only once it has a size of its own: setting it against the default one throws.
        split.HandleCreated += (_, _) => Place(split, 0.7);

        Controls.Add(split);
        Controls.Add(Totals);
    }

    /// <summary>Puts the splitter at a fraction of the control, within what it will accept.</summary>
    static void Place(SplitContainer split, double fraction)
    {
        try
        {
            var span = split.Orientation == Orientation.Horizontal ? split.Height : split.Width;
            var room = span - split.SplitterWidth;

            if (room <= split.Panel1MinSize + split.Panel2MinSize) return;

            split.SplitterDistance = Math.Clamp((int)(span * fraction),
                split.Panel1MinSize, room - split.Panel2MinSize);
        }
        catch (InvalidOperationException) { /* Too small to place; its default will do. */ }
    }

    static void Configure(DataGridView grid)
    {
        grid.Dock = DockStyle.Fill;
        grid.ReadOnly = true;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false;
        grid.RowHeadersVisible = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
        grid.EditMode = DataGridViewEditMode.EditProgrammatically;
    }

    public void Clear()
    {
        Totals.Text = "Running...";
        Grid.Rows.Clear();
        Values.Rows.Clear();
    }

    /// <summary>Fills the grids from everything the run said.</summary>
    public void Show(IReadOnlyList<LogRecord> records, RunResult result)
    {
        Grid.Rows.Clear();
        Values.Rows.Clear();

        var totals = records.LastOrDefault(x => x.Kind == "summary-totals");

        Totals.Text = totals?.Message.Or(Describe(result)) ?? Describe(result);
        Totals.ForeColor = result.Succeeded ? Color.DarkGreen : Color.Firebrick;

        foreach (var record in records)
        {
            if (record.Kind == "summary-item")
            {
                var row = Grid.Rows[Grid.Rows.Add(
                    record.Item.Or("(unnamed)"),
                    record.Field("status").Or(""),
                    record.Field("notes").Or(""),
                    record.Field("failure").Or(""))];

                if (record.Field("status") == "FAILED") row.DefaultCellStyle.BackColor = Color.MistyRose;
            }
            else if (record.Kind == "summary-package")
            {
                var status = record.Field("status").Or("");

                var row = Grid.Rows[Grid.Rows.Add(
                    "    " + record.Field("package").Or("") + " " + record.Field("packageVersion").Or(""),
                    status,
                    record.Field("destination").Or("(not published)"),
                    record.Field("failure").Or(""))];

                row.DefaultCellStyle.ForeColor = Color.DimGray;
                if (status.Contains("FAIL", StringComparison.OrdinalIgnoreCase))
                    row.DefaultCellStyle.BackColor = Color.MistyRose;
            }
        }

        foreach (var (name, value) in result.Results) Values.Rows.Add(name, value);

        if (Grid.Rows.Count == 0)
            Grid.Rows.Add("(the run reported no items)", "", "", "");
    }

    static string Describe(RunResult result)
    {
        if (result.StartFailure.HasContent()) return result.StartFailure;
        if (result.Cancelled) return "Cancelled after " + result.Duration.TotalSeconds.ToString("0.0") + "s.";

        return $"Exit code {result.ExitCode} after {result.Duration.TotalSeconds:0.0}s, " +
            $"{result.Errors} error(s) and {result.Warnings} warning(s).";
    }
}
