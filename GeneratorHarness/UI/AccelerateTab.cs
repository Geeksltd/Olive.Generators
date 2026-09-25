using Olive.GeneratorHarness.Running;

namespace Olive.GeneratorHarness.UI;

/// <summary>
/// accelerate-package-restore, which is the odd one out: positional arguments, no target
/// assembly, and a working directory that is a solution root rather than a bin folder, because it
/// scans every csproj beneath it.
/// </summary>
public sealed class AccelerateTab : UserControl
{
    public event EventHandler? Changed;

    readonly TextBox FolderBox = new();
    readonly RadioButton Extract = new() { Text = "extract --out <file>", AutoSize = true, Checked = true };
    readonly RadioButton Restore = new() { Text = "restore <file>", AutoSize = true };
    readonly TextBox FileBox = new() { Text = "packages.json" };
    readonly CheckBox Parallel = new() { Text = "-parallel", AutoSize = true };
    readonly Label Counted = new() { AutoSize = true, ForeColor = Color.DimGray };

    public AccelerateTab()
    {
        Dock = DockStyle.Fill;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 3,
            Padding = new Padding(8),
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));

        var browse = new Button { Text = "Browse...", AutoSize = true };
        browse.Click += (_, _) => PickFolder();

        Add(layout, "Solution folder", FolderBox, browse);

        var commands = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0) };
        commands.Controls.AddRange([Extract, Restore, Parallel]);
        Add(layout, "Command", commands, null);

        Add(layout, "Packages file", FileBox, null);
        Add(layout, "", Counted, null);

        Controls.Add(layout);

        FolderBox.TextChanged += (_, _) => { Count(); Raise(); };
        FileBox.TextChanged += (_, _) => Raise();
        Extract.CheckedChanged += (_, _) => Raise();
        Restore.CheckedChanged += (_, _) => Raise();
        Parallel.CheckedChanged += (_, _) => Raise();
    }

    static void Add(TableLayoutPanel layout, string label, Control field, Control? trailing)
    {
        var row = layout.RowCount;

        layout.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 3, 3) }, 0, row);

        field.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        if (field is TextBox) field.Width = 380;
        layout.Controls.Add(field, 1, row);

        if (trailing != null) layout.Controls.Add(trailing, 2, row);
        else layout.SetColumnSpan(field, 2);

        layout.RowCount = row + 1;
    }

    void Raise() => Changed?.Invoke(this, EventArgs.Empty);

    void PickFolder()
    {
        using var dialog = new FolderBrowserDialog { SelectedPath = FolderBox.Text.Or("") };
        if (dialog.ShowDialog(this) == DialogResult.OK) FolderBox.Text = dialog.SelectedPath;
    }

    /// <summary>How many projects it would find, so an empty run is obvious before it starts.</summary>
    void Count()
    {
        try
        {
            Counted.Text = Directory.Exists(FolderBox.Text)
                ? Directory.GetFiles(FolderBox.Text, "*.csproj", SearchOption.AllDirectories).Length + " csproj files beneath it"
                : "";
        }
        catch (Exception ex) { Counted.Text = "Could not scan the folder: " + ex.Message; }
    }

    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string? SolutionFolder
    {
        get => FolderBox.Text.OrNull();
        set { if (FolderBox.Text.IsBlank() && value.HasContent()) FolderBox.Text = value; }
    }

    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string? PackagesFile
    {
        get => FileBox.Text.OrNull();
        set { if (value.HasContent()) FileBox.Text = value; }
    }

    public void Fill(RunOptions options)
    {
        options.SolutionFolder = FolderBox.Text.OrNull();
        options.Accelerate = Restore.Checked ? AccelerateCommand.Restore : AccelerateCommand.Extract;
        options.PackagesFile = FileBox.Text.OrNull();
        options.Parallel = Parallel.Checked;
    }
}
