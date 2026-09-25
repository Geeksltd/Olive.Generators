using System.Threading.Channels;
using Olive.GeneratorHarness.Running;

namespace Olive.GeneratorHarness.UI;

/// <summary>
/// The run's log. A virtual list, because a detailed run of a batch emits tens of thousands of
/// child-output records and a control that materialises a row each would stop being usable long
/// before the run finished. The nesting a tree would show is already carried by every record's
/// own depth, so the indent says it just as well.
/// </summary>
public sealed class LogView : UserControl
{
    readonly ListView Grid = new();
    readonly ToolStrip Filters = new();
    readonly ToolStripComboBox LevelFilter = new();
    readonly ToolStripComboBox KindFilter = new();
    readonly ToolStripComboBox ItemFilter = new();
    readonly ToolStripTextBox TextFilter = new();
    readonly ToolStripButton FollowButton = new();
    readonly ToolStripLabel Counts = new();

    readonly List<LogRecord> Records = [];
    readonly List<int> Shown = [];
    readonly SortedSet<string> Kinds = new(StringComparer.OrdinalIgnoreCase);
    readonly SortedSet<string> Items = new(StringComparer.OrdinalIgnoreCase);

    readonly System.Windows.Forms.Timer Flush = new() { Interval = 100 };

    Channel<LogRecord>? Source;

    public LogView()
    {
        Dock = DockStyle.Fill;

        Grid.Dock = DockStyle.Fill;
        Grid.View = View.Details;
        Grid.VirtualMode = true;
        Grid.FullRowSelect = true;
        Grid.GridLines = false;
        Grid.HeaderStyle = ColumnHeaderStyle.Nonclickable;
        Grid.Font = new Font("Consolas", 9f);
        Grid.RetrieveVirtualItem += Retrieve;

        Grid.Columns.Add("Elapsed", 70);
        Grid.Columns.Add("Level", 55);
        Grid.Columns.Add("Item", 150);
        Grid.Columns.Add("Message", 900);

        LevelFilter.DropDownStyle = ComboBoxStyle.DropDownList;
        LevelFilter.Items.AddRange(["All levels", "ERROR", "WARN", "INFO", "DEBUG", "TRACE"]);
        LevelFilter.SelectedIndex = 0;
        LevelFilter.SelectedIndexChanged += (_, _) => Requery();

        KindFilter.DropDownStyle = ComboBoxStyle.DropDownList;
        KindFilter.Items.Add("All kinds");
        KindFilter.SelectedIndex = 0;
        KindFilter.SelectedIndexChanged += (_, _) => Requery();

        ItemFilter.DropDownStyle = ComboBoxStyle.DropDownList;
        ItemFilter.Items.Add("All items");
        ItemFilter.SelectedIndex = 0;
        ItemFilter.SelectedIndexChanged += (_, _) => Requery();

        TextFilter.AutoSize = false;
        TextFilter.Width = 180;
        TextFilter.ToolTipText = "Show only lines containing this";
        TextFilter.TextChanged += (_, _) => Requery();

        FollowButton.Text = "Follow";
        FollowButton.CheckOnClick = true;
        FollowButton.Checked = true;

        var copy = new ToolStripButton("Copy") { ToolTipText = "Copy every shown line" };
        copy.Click += (_, _) => CopyShown();

        var clear = new ToolStripButton("Clear");
        clear.Click += (_, _) => Clear();

        Counts.Text = "0 lines";

        Filters.GripStyle = ToolStripGripStyle.Hidden;
        Filters.Items.AddRange([LevelFilter, KindFilter, ItemFilter, TextFilter,
            new ToolStripSeparator(), FollowButton, copy, clear, new ToolStripSeparator(), Counts]);

        Controls.Add(Grid);
        Controls.Add(Filters);

        Flush.Tick += (_, _) => Drain();
        Flush.Start();
    }

    /// <summary>Where new records arrive from while a run is in flight.</summary>
    public void ReadFrom(Channel<LogRecord> source) => Source = source;

    /// <summary>
    /// Takes everything waiting right now, rather than on the next tick. Called when a run ends,
    /// so the summary and the raw view are built from all of what it said and not most of it.
    /// </summary>
    public void DrainNow() => Drain();

    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<LogRecord> All => Records;

    public void Clear()
    {
        Records.Clear();
        Shown.Clear();
        Kinds.Clear();
        Items.Clear();

        KindFilter.Items.Clear();
        KindFilter.Items.Add("All kinds");
        KindFilter.SelectedIndex = 0;

        ItemFilter.Items.Clear();
        ItemFilter.Items.Add("All items");
        ItemFilter.SelectedIndex = 0;

        Grid.VirtualListSize = 0;
        Grid.Invalidate();
        Counts.Text = "0 lines";
    }

    /// <summary>
    /// Takes everything waiting and appends it in one go. One control update per tick rather than
    /// per line is what keeps a diagnostic run smooth.
    /// </summary>
    void Drain()
    {
        if (Source == null) return;

        var added = 0;
        var newKind = false;
        var newItem = false;

        while (Source.Reader.TryRead(out var record))
        {
            Records.Add(record);
            added++;

            if (Kinds.Add(record.Kind)) newKind = true;
            if (record.Item.HasContent() && Items.Add(record.Item)) newItem = true;

            if (Passes(record)) Shown.Add(Records.Count - 1);

            // A very long stall would otherwise let one tick do unbounded work.
            if (added >= 5000) break;
        }

        if (added == 0) return;

        if (newKind) Refill(KindFilter, "All kinds", Kinds);
        if (newItem) Refill(ItemFilter, "All items", Items);

        Grid.VirtualListSize = Shown.Count;
        Counts.Text = Shown.Count == Records.Count
            ? Records.Count + " lines"
            : $"{Shown.Count} of {Records.Count} lines";

        if (FollowButton.Checked && Shown.Count != 0)
            Grid.EnsureVisible(Shown.Count - 1);
    }

    static void Refill(ToolStripComboBox box, string all, IEnumerable<string> values)
    {
        var chosen = box.SelectedItem as string;

        box.Items.Clear();
        box.Items.Add(all);
        foreach (var value in values) box.Items.Add(value);

        var index = chosen == null ? 0 : box.Items.IndexOf(chosen);
        box.SelectedIndex = index < 0 ? 0 : index;
    }

    /// <summary>Recomputes which lines are shown. Never re-parses; the records are already here.</summary>
    void Requery()
    {
        Shown.Clear();

        for (var index = 0; index < Records.Count; index++)
            if (Passes(Records[index])) Shown.Add(index);

        Grid.VirtualListSize = Shown.Count;
        Grid.Invalidate();

        Counts.Text = Shown.Count == Records.Count
            ? Records.Count + " lines"
            : $"{Shown.Count} of {Records.Count} lines";
    }

    bool Passes(LogRecord record)
    {
        if (LevelFilter.SelectedIndex > 0 && record.Level != (string)LevelFilter.SelectedItem!) return false;
        if (KindFilter.SelectedIndex > 0 && record.Kind != (string)KindFilter.SelectedItem!) return false;
        if (ItemFilter.SelectedIndex > 0 && record.Item != (string)ItemFilter.SelectedItem!) return false;

        var text = TextFilter.Text;

        if (text.HasContent() && record.Message.IndexOf(text, StringComparison.OrdinalIgnoreCase) < 0
            && record.Indented.IndexOf(text, StringComparison.OrdinalIgnoreCase) < 0) return false;

        return true;
    }

    void Retrieve(object? sender, RetrieveVirtualItemEventArgs args)
    {
        if (args.ItemIndex < 0 || args.ItemIndex >= Shown.Count)
        {
            args.Item = new ListViewItem("");
            return;
        }

        var record = Records[Shown[args.ItemIndex]];

        var row = new ListViewItem(record.Elapsed);
        row.SubItems.Add(record.Level);
        row.SubItems.Add(record.Item.Or(""));
        row.SubItems.Add(record.Indented);

        row.ForeColor = record.Level switch
        {
            "ERROR" => Color.Firebrick,
            "WARN" => Color.DarkOrange,
            "DEBUG" => Color.DimGray,
            "TRACE" => Color.Silver,
            _ => Grid.ForeColor,
        };

        if (record.IsFailedStep) row.BackColor = Color.MistyRose;
        else if (record.Kind is "run-start" or "run-end" or "summary-totals") row.BackColor = Color.WhiteSmoke;

        args.Item = row;
    }

    void CopyShown()
    {
        if (Shown.Count == 0) return;

        var text = string.Join(Environment.NewLine, Shown.Select(index =>
        {
            var record = Records[index];
            return $"[{record.Elapsed}] {record.Level} {record.Indented}";
        }));

        try { Clipboard.SetText(text); } catch { /* Another process owning the clipboard is not an error. */ }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) Flush.Dispose();
        base.Dispose(disposing);
    }
}
