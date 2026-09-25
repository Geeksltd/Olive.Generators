using Olive.GeneratorHarness.Running;
using Olive.GeneratorHarness.Tools;

namespace Olive.GeneratorHarness.UI;

/// <summary>
/// The switches every tool shares, plus the ones only some accept. Fields that do not apply to
/// the selected tool are disabled rather than hidden, so the shape of the command line stays
/// visible.
/// </summary>
public sealed class CommonOptionsPanel : UserControl
{
    public event EventHandler? Changed;

    readonly TextBox OutputBox = new();
    readonly CheckBox CreateOutput = new() { Text = "create if missing", Checked = true, AutoSize = true };
    readonly TextBox PushBox = new();
    readonly TextBox ApiKeyBox = new() { UseSystemPasswordChar = true };
    readonly CheckBox ShowApiKey = new() { Text = "show", AutoSize = true };
    readonly TextBox ServiceNameBox = new();
    readonly CheckBox HashOnly = new() { Text = "/hashonly (report hashes, build nothing)", AutoSize = true };

    readonly ComboBox VerbosityBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    readonly ComboBox LogFormatBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    readonly NumericUpDown HeartbeatBox = new() { Minimum = 0, Maximum = 600, Value = 15 };
    readonly CheckBox Timing = new() { Text = "/timing", AutoSize = true };
    readonly CheckBox NoColor = new() { Text = "/nocolor", AutoSize = true };
    readonly CheckBox Debugger = new() { Text = "/debug", AutoSize = true };
    readonly TextBox LogFileBox = new();
    readonly TextBox JsonLogBox = new();
    readonly TextBox ExtraBox = new();
    readonly Label Notes = new() { AutoSize = false, Dock = DockStyle.Fill, ForeColor = Color.DarkOrange };

    GeneratorTool Tool = GeneratorTool.DataEndpoint;

    public CommonOptionsPanel()
    {
        Dock = DockStyle.Fill;
        AutoScroll = true;

        VerbosityBox.Items.AddRange(["quiet", "minimal", "normal", "detailed", "diagnostic"]);
        VerbosityBox.SelectedItem = "normal";

        LogFormatBox.Items.AddRange(["json", "text"]);
        LogFormatBox.SelectedItem = "json";

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 3,
            Padding = new Padding(8),
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));

        Row(layout, "Output folder", OutputBox, Browse("Browse...", () => PickFolder(OutputBox)));
        Row(layout, "", CreateOutput, null);
        Row(layout, "Push url", PushBox, null);
        Row(layout, "Api key", ApiKeyBox, ShowApiKey);
        Row(layout, "Service name", ServiceNameBox, null);
        Row(layout, "", HashOnly, null);
        Row(layout, "Verbosity", VerbosityBox, null);
        Row(layout, "Log format", LogFormatBox, null);
        Row(layout, "Heartbeat", HeartbeatBox, Flags());
        Row(layout, "/log: file", LogFileBox, Browse("Browse...", () => PickFile(LogFileBox)));
        Row(layout, "/logjson: file", JsonLogBox, Browse("Browse...", () => PickFile(JsonLogBox)));
        Row(layout, "Extra arguments", ExtraBox, null);

        Notes.Height = 54;
        layout.Controls.Add(new Label { Text = "", AutoSize = true }, 0, layout.RowCount);
        layout.Controls.Add(Notes, 1, layout.RowCount);
        layout.SetColumnSpan(Notes, 2);
        layout.RowCount++;

        Controls.Add(layout);

        foreach (var box in new[] { OutputBox, PushBox, ApiKeyBox, ServiceNameBox, LogFileBox, JsonLogBox, ExtraBox })
            box.TextChanged += Raise;

        foreach (var check in new[] { CreateOutput, HashOnly, Timing, NoColor, Debugger })
            check.CheckedChanged += Raise;

        VerbosityBox.SelectedIndexChanged += Raise;
        LogFormatBox.SelectedIndexChanged += Raise;
        HeartbeatBox.ValueChanged += Raise;

        ShowApiKey.CheckedChanged += (_, _) => ApiKeyBox.UseSystemPasswordChar = !ShowApiKey.Checked;
    }

    Control Flags()
    {
        var flow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0) };
        flow.Controls.AddRange([Timing, NoColor, Debugger]);
        return flow;
    }

    static Button Browse(string text, Action click)
    {
        var button = new Button { Text = text, AutoSize = true, Anchor = AnchorStyles.Left };
        button.Click += (_, _) => click();
        return button;
    }

    static void Row(TableLayoutPanel layout, string label, Control field, Control? trailing)
    {
        var row = layout.RowCount;

        layout.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 3, 3) }, 0, row);

        field.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        if (field is TextBox or ComboBox) field.Width = 320;
        layout.Controls.Add(field, 1, row);

        if (trailing != null) layout.Controls.Add(trailing, 2, row);
        else layout.SetColumnSpan(field, 2);

        layout.RowCount = row + 1;
    }

    void Raise(object? sender, EventArgs args) => Changed?.Invoke(this, EventArgs.Empty);

    void PickFolder(TextBox box)
    {
        using var dialog = new FolderBrowserDialog { SelectedPath = box.Text.Or("") };
        if (dialog.ShowDialog(this) == DialogResult.OK) box.Text = dialog.SelectedPath;
    }

    void PickFile(TextBox box)
    {
        using var dialog = new SaveFileDialog { FileName = box.Text.Or(""), OverwritePrompt = false };
        if (dialog.ShowDialog(this) == DialogResult.OK) box.Text = dialog.FileName;
    }

    /// <summary>Greys out what the selected tool does not accept, and says why where it matters.</summary>
    public void Apply(GeneratorTool tool)
    {
        Tool = tool;

        var publishes = tool.Kind != ToolKind.AcceleratePackage;

        OutputBox.Enabled = CreateOutput.Enabled = PushBox.Enabled = ApiKeyBox.Enabled =
            ShowApiKey.Enabled = ServiceNameBox.Enabled = publishes;

        Debugger.Enabled = publishes;
        HashOnly.Enabled = tool.SupportsHashOnly;

        if (!tool.SupportsHashOnly) HashOnly.Checked = false;

        Notes.Text = NoteFor(tool);
        Raise(this, EventArgs.Empty);
    }

    string NoteFor(GeneratorTool tool)
    {
        var notes = new List<string>();

        if (!tool.SupportsHashOnly && tool.Kind == ToolKind.ApiProxy)
            notes.Add("The api proxy generator has no /hashonly mode.");

        if (tool.Kind == ToolKind.ApiProxy)
            notes.Add("It reads only /serviceName: for the publisher, so leaving it empty generates a proxy without one.");

        // Only what is true of the tool itself. Everything about the run as configured is said
        // once, beneath the command line, by CommandLineBuilder.Warnings.
        return notes.Join(" ");
    }

    /// <summary>Copies the fields onto the options a run will be built from.</summary>
    public void Fill(RunOptions options)
    {
        options.Output = OutputBox.Text.OrNull();
        options.CreateOutputIfMissing = CreateOutput.Checked;
        options.PushUrl = PushBox.Text.OrNull();
        options.ApiKey = ApiKeyBox.Text.OrNull();
        options.ServiceName = ServiceNameBox.Text.OrNull();
        options.HashOnly = HashOnly.Checked && Tool.SupportsHashOnly;

        options.Verbosity = (string)VerbosityBox.SelectedItem!;
        options.LogFormat = (string)LogFormatBox.SelectedItem!;
        options.Heartbeat = (int)HeartbeatBox.Value;
        options.Timing = Timing.Checked;
        options.NoColor = NoColor.Checked;
        options.WaitForDebugger = Debugger.Checked && Tool.Kind != ToolKind.AcceleratePackage;
        options.LogFile = LogFileBox.Text.OrNull();
        options.JsonLogFile = JsonLogBox.Text.OrNull();
        options.ExtraArguments = ExtraBox.Text.OrNull();
    }

    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string? ServiceName
    {
        get => ServiceNameBox.Text.OrNull();
        set { if (ServiceNameBox.Text.IsBlank() && value.HasContent()) ServiceNameBox.Text = value; }
    }

    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool RevealApiKey => ShowApiKey.Checked;

    public void LoadFrom(Settings.HarnessSettings settings)
    {
        OutputBox.Text = settings.Output.Or("");
        PushBox.Text = settings.PushUrl.Or("");
        ServiceNameBox.Text = settings.ServiceName.Or("");
        VerbosityBox.SelectedItem = settings.Verbosity.Or("normal");
        LogFormatBox.SelectedItem = settings.LogFormat.Or("json");
        HeartbeatBox.Value = Math.Clamp(settings.Heartbeat, 0, 600);
        Timing.Checked = settings.Timing;
        NoColor.Checked = settings.NoColor;
        LogFileBox.Text = settings.LogFile.Or("");
        JsonLogBox.Text = settings.JsonLogFile.Or("");
        ExtraBox.Text = settings.ExtraArguments.Or("");
    }

    public void SaveTo(Settings.HarnessSettings settings)
    {
        settings.Output = OutputBox.Text.OrNull();
        settings.PushUrl = PushBox.Text.OrNull();
        settings.ServiceName = ServiceNameBox.Text.OrNull();
        settings.Verbosity = (string)VerbosityBox.SelectedItem!;
        settings.LogFormat = (string)LogFormatBox.SelectedItem!;
        settings.Heartbeat = (int)HeartbeatBox.Value;
        settings.Timing = Timing.Checked;
        settings.NoColor = NoColor.Checked;
        settings.LogFile = LogFileBox.Text.OrNull();
        settings.JsonLogFile = JsonLogBox.Text.OrNull();
        settings.ExtraArguments = ExtraBox.Text.OrNull();

        // The api key is never written. It is typed per session and lives only on the command
        // line of the run it is for.
    }
}
