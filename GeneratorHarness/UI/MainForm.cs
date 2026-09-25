using System.Diagnostics;
using System.Threading.Channels;
using Olive.GeneratorHarness.Discovery;
using Olive.GeneratorHarness.Running;
using Olive.GeneratorHarness.Settings;
using Olive.GeneratorHarness.Tools;

namespace Olive.GeneratorHarness.UI;

/// <summary>
/// The harness window: open a target microservice assembly, see what the generators would find in
/// it, set any switch of any of the four tools, and watch the run.
/// </summary>
public sealed class MainForm : Form
{
    readonly HarnessSettings Settings = HarnessSettings.Load();
    readonly ToolLocator Locator;

    readonly ComboBox AssemblyBox = new() { DropDownStyle = ComboBoxStyle.DropDown };
    readonly ComboBox ConfigurationBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90 };
    readonly Label TargetDetail = new() { AutoSize = false, Dock = DockStyle.Fill, ForeColor = Color.DimGray };
    readonly ToolStrip ToolBar = new();

    readonly TabControl Tabs = new() { Dock = DockStyle.Fill };
    readonly Dictionary<ToolKind, DiscoveryTab> Discoveries = [];
    readonly AccelerateTab Accelerate = new();
    readonly CommonOptionsPanel Options = new();

    readonly TextBox Preview = new()
    {
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Vertical,
        Font = new Font("Consolas", 9f),
        Dock = DockStyle.Fill,
        BackColor = SystemColors.Window,
    };

    readonly Label Problems = new() { Dock = DockStyle.Bottom, AutoSize = false, Height = 40, ForeColor = Color.Firebrick };

    readonly Button RunButton = new() { Text = "Run", Width = 90 };
    readonly Button CancelRunButton = new() { Text = "Cancel", Width = 90, Enabled = false };
    readonly Button SuiteButton = new() { Text = "Run all", Width = 90 };
    readonly Button RebuildButton = new() { Text = "Rebuild tools", Width = 110 };
    readonly Label Elapsed = new() { AutoSize = true, Padding = new Padding(10, 8, 4, 0) };
    readonly Label ExitBadge = new() { AutoSize = true, Padding = new Padding(10, 8, 4, 0), Font = new Font(DefaultFont, FontStyle.Bold) };

    readonly LogView Log = new();
    readonly SummaryView Summary = new();
    readonly RichTextBox RawView = new() { Dock = DockStyle.Fill, ReadOnly = true, Font = new Font("Consolas", 9f), WordWrap = false };
    readonly DataGridView Runs = new() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, RowHeadersVisible = false };
    readonly FlowLayoutPanel Links = new() { Dock = DockStyle.Bottom, AutoSize = true, WrapContents = true };

    readonly System.Windows.Forms.Timer Ticker = new() { Interval = 250 };
    readonly Stopwatch Clock = new();

    Channel<LogRecord> Records = Channel.CreateUnbounded<LogRecord>();

    /// <summary>How much of the log the raw view has already been given.</summary>
    int RawShown;

    /// <summary>Where in the log the run now finishing began.</summary>
    int RunStart;
    CancellationTokenSource? Cancellation;
    DiscoveryResult? Discovered;
    bool Running;

    public MainForm()
    {
        Text = "Olive Generator Harness";
        Width = 1400;
        Height = 900;
        StartPosition = FormStartPosition.CenterScreen;

        Locator = new ToolLocator(Settings.Overrides()) { Configuration = Settings.Configuration.Or("Debug") };

        BuildLayout();
        LoadSettings();
        RefreshTools();
        Repreview();

        Ticker.Tick += (_, _) => { if (Running) Elapsed.Text = $"{Clock.Elapsed.TotalSeconds:0.0}s"; };
        Ticker.Start();

        Log.ReadFrom(Records);
    }

    // ------------------------------------------------------------------ layout

    void BuildLayout()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 45));

        root.Controls.Add(BuildTargetBar(), 0, 0);

        var upper = new SplitContainer { Dock = DockStyle.Fill };
        upper.Panel1.Controls.Add(Tabs);
        upper.Panel2.Controls.Add(BuildOptionsSide());

        // Once it has a size of its own: against the default one this throws.
        upper.HandleCreated += (_, _) =>
        {
            try
            {
                var room = upper.Width - upper.SplitterWidth;

                if (room > upper.Panel1MinSize + upper.Panel2MinSize)
                    upper.SplitterDistance = Math.Clamp((int)(upper.Width * 0.55),
                        upper.Panel1MinSize, room - upper.Panel2MinSize);
            }
            catch (InvalidOperationException) { /* Its default will do. */ }
        };
        root.Controls.Add(upper, 0, 1);

        root.Controls.Add(BuildRunSide(), 0, 2);

        Controls.Add(root);

        BuildTabs();
    }

    Control BuildTargetBar()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 5,
            Padding = new Padding(8, 6, 8, 2),
        };

        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        AssemblyBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        AssemblyBox.SelectedIndexChanged += (_, _) => Rescan();
        AssemblyBox.Leave += (_, _) => Rescan();

        var browse = new Button { Text = "Browse...", AutoSize = true };
        browse.Click += (_, _) => PickAssembly();

        var rescan = new Button { Text = "Rescan", AutoSize = true };
        rescan.Click += (_, _) => Rescan();

        ConfigurationBox.Items.AddRange(["Debug", "Release"]);
        ConfigurationBox.SelectedItem = Settings.Configuration.Or("Debug");
        ConfigurationBox.SelectedIndexChanged += (_, _) =>
        {
            Locator.Configuration = (string)ConfigurationBox.SelectedItem!;
            RefreshTools();
            Repreview();
        };

        panel.Controls.Add(new Label { Text = "Target assembly", AutoSize = true, Margin = new Padding(3, 7, 3, 3) }, 0, 0);
        panel.Controls.Add(AssemblyBox, 1, 0);
        panel.Controls.Add(browse, 2, 0);
        panel.Controls.Add(rescan, 3, 0);
        panel.Controls.Add(ConfigurationBox, 4, 0);

        TargetDetail.Height = 34;
        panel.Controls.Add(TargetDetail, 1, 1);
        panel.SetColumnSpan(TargetDetail, 4);

        ToolBar.GripStyle = ToolStripGripStyle.Hidden;
        ToolBar.Dock = DockStyle.Top;

        var host = new Panel { Dock = DockStyle.Top, AutoSize = true };
        host.Controls.Add(panel);
        host.Controls.Add(ToolBar);

        return host;
    }

    void BuildTabs()
    {
        foreach (var tool in GeneratorTool.All)
        {
            var page = new TabPage(tool.Title) { Tag = tool };

            if (tool.UsesTargetAssembly)
            {
                var discovery = new DiscoveryTab(tool);
                discovery.Changed += (_, _) => Repreview();
                Discoveries[tool.Kind] = discovery;
                page.Controls.Add(discovery);
            }
            else
            {
                Accelerate.Changed += (_, _) => Repreview();
                page.Controls.Add(Accelerate);
            }

            Tabs.TabPages.Add(page);
        }

        Tabs.SelectedIndexChanged += (_, _) =>
        {
            Options.Apply(Selected);
            Repreview();
        };
    }

    Control BuildOptionsSide()
    {
        var host = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        host.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        host.RowStyles.Add(new RowStyle(SizeType.Absolute, 160));

        Options.Changed += (_, _) => Repreview();
        host.Controls.Add(Options, 0, 0);

        var previewPanel = new Panel { Dock = DockStyle.Fill };

        var previewBar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
        previewBar.Controls.Add(new Label { Text = "Command line", AutoSize = true, Margin = new Padding(3, 6, 8, 3) });

        var copy = new Button { Text = "Copy", AutoSize = true };
        copy.Click += (_, _) => { try { Clipboard.SetText(Preview.Text); } catch { } };
        previewBar.Controls.Add(copy);

        previewPanel.Controls.Add(Preview);
        previewPanel.Controls.Add(Problems);
        previewPanel.Controls.Add(previewBar);

        host.Controls.Add(previewPanel, 0, 1);

        return host;
    }

    Control BuildRunSide()
    {
        var host = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        host.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        host.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        RunButton.Click += async (_, _) => await RunSelectedAsync();
        CancelRunButton.Click += (_, _) => Cancellation?.Cancel();
        SuiteButton.Click += async (_, _) => await RunSuiteAsync();
        RebuildButton.Click += async (_, _) => await RebuildAsync();

        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(6, 4, 6, 4) };
        bar.Controls.AddRange([RunButton, CancelRunButton, SuiteButton, RebuildButton, Elapsed, ExitBadge]);

        var pages = new TabControl { Dock = DockStyle.Fill };

        var logPage = new TabPage("Log");
        logPage.Controls.Add(Log);
        logPage.Controls.Add(Links);
        pages.TabPages.Add(logPage);

        var summaryPage = new TabPage("Summary");
        summaryPage.Controls.Add(Summary);
        pages.TabPages.Add(summaryPage);

        var rawPage = new TabPage("Raw");
        rawPage.Controls.Add(RawView);
        pages.TabPages.Add(rawPage);

        Runs.Columns.Add("step", "Step");
        Runs.Columns.Add("outcome", "Outcome");
        Runs.Columns.Add("exit", "Exit");
        Runs.Columns.Add("duration", "Duration");
        Runs.Columns.Add("detail", "Detail");
        Runs.Columns[0].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        Runs.Columns[4].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

        var runsPage = new TabPage("Runs");
        runsPage.Controls.Add(Runs);
        pages.TabPages.Add(runsPage);

        host.Controls.Add(bar, 0, 0);
        host.Controls.Add(pages, 0, 1);

        return host;
    }

    // ------------------------------------------------------------------ state

    GeneratorTool Selected => (Tabs.SelectedTab?.Tag as GeneratorTool) ?? GeneratorTool.DataEndpoint;

    void LoadSettings()
    {
        foreach (var recent in Settings.RecentAssemblies) AssemblyBox.Items.Add(recent);

        if (AssemblyBox.Items.Count != 0) AssemblyBox.SelectedIndex = 0;

        Options.LoadFrom(Settings);
        Options.Apply(Selected);

        Accelerate.SolutionFolder = Settings.SolutionFolder.Or(Locator.RepositoryRoot.Or(""));
        Accelerate.PackagesFile = Settings.PackagesFile.Or("packages.json");

        if (Settings.WindowWidth > 400 && Settings.WindowHeight > 300)
        {
            Width = Settings.WindowWidth;
            Height = Settings.WindowHeight;
        }

        if (Settings.Maximised) WindowState = FormWindowState.Maximized;
    }

    /// <summary>Which build of each tool is in play, said up front so a stale binary is visible.</summary>
    void RefreshTools()
    {
        ToolBar.Items.Clear();

        foreach (var tool in GeneratorTool.All)
        {
            var resolution = Locator.Resolve(tool);

            var button = new ToolStripDropDownButton(resolution.Describe())
            {
                ForeColor = resolution.Source switch
                {
                    ToolSource.RepoBuild => Color.DarkGreen,
                    ToolSource.GlobalTool => Color.DarkBlue,
                    ToolSource.Override => Color.DarkOrange,
                    _ => Color.Firebrick,
                },
                ToolTipText = resolution.Path.Or("not found"),
            };

            var current = tool;

            var browse = new ToolStripMenuItem("Browse to an override...");
            browse.Click += (_, _) => Override(current);
            button.DropDownItems.Add(browse);

            var clear = new ToolStripMenuItem("Clear the override");
            clear.Click += (_, _) =>
            {
                Settings.ToolOverrides.Remove(current.Kind.ToString());
                Rebind();
            };
            button.DropDownItems.Add(clear);

            if (resolution.Path.HasContent())
            {
                var open = new ToolStripMenuItem("Open its folder");
                open.Click += (_, _) => Reveal(Path.GetDirectoryName(resolution.Path)!);
                button.DropDownItems.Add(open);
            }

            ToolBar.Items.Add(button);
        }
    }

    void Rebind()
    {
        RefreshTools();
        Repreview();
    }

    ToolResolution Resolve(GeneratorTool tool)
    {
        var locator = new ToolLocator(Settings.Overrides()) { Configuration = Locator.Configuration };
        return locator.Resolve(tool);
    }

    void Override(GeneratorTool tool)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Choose the " + tool.ToolCommand + " executable",
            Filter = "Programs and assemblies (*.exe;*.dll)|*.exe;*.dll|All files (*.*)|*.*",
        };

        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        Settings.ToolOverrides[tool.Kind.ToString()] = dialog.FileName;
        Rebind();
    }

    // ------------------------------------------------------------------ discovery

    void PickAssembly()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Choose the target microservice assembly",
            Filter = "Website.dll|Website.dll|Assemblies (*.dll)|*.dll|All files (*.*)|*.*",
        };

        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        AssemblyBox.Text = dialog.FileName;
        Rescan();
    }

    async void Rescan()
    {
        var path = AssemblyBox.Text.OrNull();

        if (path.IsBlank() || !File.Exists(path))
        {
            TargetDetail.Text = path.IsBlank() ? "" : "That file does not exist.";
            return;
        }

        TargetDetail.Text = "Scanning " + Path.GetFileName(path) + "...";

        // Off the UI thread: a large Website.dll takes a moment, and the window must not freeze.
        var result = await Task.Run(() => AssemblyExplorer.Scan(path));

        Discovered = result;

        foreach (var discovery in Discoveries.Values) discovery.Show(result);

        Settings.Remember(path);

        if (!AssemblyBox.Items.Contains(path)) AssemblyBox.Items.Insert(0, path);

        var (file, service) = AppSettingsLocator.Find(path);
        Options.ServiceName = service;

        TargetDetail.Text = Describe(result, file, service);

        Repreview();
    }

    static string Describe(DiscoveryResult result, string? appSettings, string? service)
    {
        if (result.Failure.HasContent()) return "Could not read the assembly: " + result.Failure;

        var parts = new List<string>
        {
            $"{result.Endpoints.Count} endpoints, {result.Commands.Count} commands, " +
            $"{result.Controllers.Count} controllers, from {result.TypeCount} types",
        };

        if (result.Built != null) parts.Add("built " + result.Built.Value.ToString("yyyy-MM-dd HH:mm"));

        if (service.HasContent()) parts.Add("service " + service);
        else if (appSettings.HasContent()) parts.Add("appSettings.json has no Microservice:Me:Name");

        if (result.UnresolvedAssemblies.Count != 0)
            parts.Add("could not resolve " + result.UnresolvedAssemblies.Take(5).Join(", "));

        return parts.Join(". ") + ".";
    }

    // ------------------------------------------------------------------ the command line

    RunOptions Current()
    {
        var tool = Selected;

        var options = new RunOptions
        {
            Tool = tool,
            AssemblyPath = AssemblyBox.Text.OrNull(),
        };

        Options.Fill(options);

        if (tool.UsesTargetAssembly && Discoveries.TryGetValue(tool.Kind, out var discovery))
            options.Items = discovery.Selected;
        else if (!tool.UsesTargetAssembly)
            Accelerate.Fill(options);

        return options;
    }

    /// <summary>
    /// The runs the Run button will start. Several when the tab asks for one run per item, or
    /// when the tool takes a single item and more than one is selected, which is how the api
    /// proxy generator covers a batch at all.
    /// </summary>
    List<RunOptions> Planned()
    {
        var tool = Selected;

        if (tool.UsesTargetAssembly && Discoveries.TryGetValue(tool.Kind, out var discovery)
            && discovery.RunOncePerItem)
        {
            var runs = discovery.Selected.Select(item =>
            {
                var single = Current();
                single.Items = [item];
                return single;
            }).ToList();

            if (runs.Count != 0) return runs;
        }

        return [Current()];
    }

    void Repreview()
    {
        var runs = Planned();
        var options = runs[0];
        var resolution = Resolve(options.Tool);

        var heading = runs.Count == 1 ? "" : $"1 of {runs.Count} runs:" + Environment.NewLine;

        Preview.Text = heading
            + CommandLineBuilder.Display(resolution, options, Options.RevealApiKey)
            + Environment.NewLine + Environment.NewLine + "in " + options.WorkingDirectory;

        // Against each planned run, so a selection the harness will split is not judged as one.
        var problems = runs.SelectMany(CommandLineBuilder.Problems).Distinct().ToArray();

        if (!resolution.Found)
            problems = [.. problems.Append(options.Tool.ToolCommand + " was not found. Build it, install it, or browse to it.")];

        // Blockers in red and advisories in amber, so only the first kind stops a run.
        if (problems.Length != 0)
        {
            Problems.Text = problems.Join("  ");
            Problems.ForeColor = Color.Firebrick;
        }
        else
        {
            Problems.Text = runs.SelectMany(CommandLineBuilder.Warnings).Distinct().Join("  ");
            Problems.ForeColor = Color.DarkOrange;
        }

        RunButton.Enabled = !Running && problems.Length == 0;
        SuiteButton.Enabled = !Running;
    }

    // ------------------------------------------------------------------ running

    async Task RunSelectedAsync()
    {
        var runs = Planned();

        Reset();

        var outcomes = new List<SuiteOutcome>();

        await WhileRunning(async cancellation =>
        {
            foreach (var options in runs)
            {
                if (cancellation.IsCancellationRequested) break;

                var result = await new GeneratorRunner(Records.Writer).RunAsync(Resolve(options.Tool), options, cancellation);

                outcomes.Add(new SuiteOutcome
                {
                    Name = options.Tool.Title + ": " + options.Items.Join(", ").Or(options.Accelerate.ToString()),
                    Result = result,
                    Failure = result.Succeeded ? null : Why(result),
                });

                Finish(result);
            }
        });

        ShowRuns(outcomes);
    }

    async Task RunSuiteAsync()
    {
        if (Discovered == null)
        {
            MessageBox.Show(this, "Scan a target assembly first.", "Run all",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var template = Current();

        var output = Path.Combine(Path.GetTempPath(), "olive-generator-harness",
            DateTime.Now.ToString("yyyyMMdd-HHmmss"));

        Directory.CreateDirectory(output);

        var steps = RunAllSuite.Plan(template,
            Selection(ToolKind.DataEndpoint), Selection(ToolKind.EventBusCommand),
            Selection(ToolKind.ApiProxy), output);

        if (steps.Count == 0)
        {
            MessageBox.Show(this, "Select at least one endpoint, command or controller first.",
                "Run all", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var answer = MessageBox.Show(this,
            $"Run {steps.Count} checks against the current selection?\r\n\r\n" +
            $"Nothing is published. Generated packages go to {output}.",
            "Run all", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);

        if (answer != DialogResult.OK) return;

        Reset();

        var outcomes = new List<SuiteOutcome>();

        await WhileRunning(async cancellation =>
        {
            foreach (var step in steps)
            {
                if (cancellation.IsCancellationRequested) break;

                Records.Writer.TryWrite(LogRecordParser.Harness("== " + step.Name));

                var result = await new GeneratorRunner(Records.Writer)
                    .RunAsync(Resolve(step.Options.Tool), step.Options, cancellation);

                outcomes.Add(RunAllSuite.Judge(step, result));
                Finish(result);
            }
        });

        ShowRuns(outcomes);
    }

    List<string> Selection(ToolKind kind)
        => Discoveries.TryGetValue(kind, out var tab) ? tab.Selected : [];

    async Task RebuildAsync()
    {
        if (Locator.RepositoryRoot == null)
        {
            MessageBox.Show(this, "This harness is not running from inside the Olive.Generators repository.",
                "Rebuild tools", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Reset();

        await WhileRunning(async cancellation =>
        {
            foreach (var tool in GeneratorTool.All)
            {
                if (cancellation.IsCancellationRequested) break;

                var project = Locator.ProjectPathOf(tool);
                if (project == null) continue;

                Records.Writer.TryWrite(LogRecordParser.Harness("== build " + tool.ProjectFile));

                await BuildAsync(project, cancellation);
            }
        });

        RefreshTools();
        Repreview();
    }

    async Task BuildAsync(string project, CancellationToken cancellation)
    {
        var start = new ProcessStartInfo(ToolLocator.DotNetHost)
        {
            WorkingDirectory = Locator.RepositoryRoot!,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        start.ArgumentList.Add("build");
        start.ArgumentList.Add(project);
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add(Locator.Configuration);

        start.Environment["DOTNET_NOLOGO"] = "1";
        start.Environment["MSBUILDTERMINALLOGGER"] = "off";

        using var process = new Process { StartInfo = start };

        try { process.Start(); }
        catch (Exception ex)
        {
            Records.Writer.TryWrite(LogRecordParser.Harness("Could not start the build: " + ex.Message, "ERROR"));
            return;
        }

        var output = Pump(process.StandardOutput);
        var errors = Pump(process.StandardError);

        try { await process.WaitForExitAsync(cancellation); }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
        }

        await Task.WhenAll(output, errors);

        Records.Writer.TryWrite(LogRecordParser.Harness("Build exit code " + SafeExit(process),
            SafeExit(process) == 0 ? "INFO" : "ERROR"));
    }

    static int SafeExit(Process process)
    {
        try { return process.ExitCode; } catch { return -1; }
    }

    async Task Pump(StreamReader reader)
    {
        while (await reader.ReadLineAsync() is { } line)
            Records.Writer.TryWrite(LogRecordParser.Raw(line));
    }

    /// <summary>Runs the body with the window in its running state, whatever happens inside it.</summary>
    async Task WhileRunning(Func<CancellationToken, Task> body)
    {
        Running = true;
        RunButton.Enabled = SuiteButton.Enabled = RebuildButton.Enabled = false;
        CancelRunButton.Enabled = true;
        ExitBadge.Text = "";
        Clock.Restart();

        Cancellation = new CancellationTokenSource();

        try { await body(Cancellation.Token); }
        catch (Exception ex)
        {
            Records.Writer.TryWrite(LogRecordParser.Harness("The harness failed: " + ex.Message, "ERROR"));
        }
        finally
        {
            Clock.Stop();
            Cancellation.Dispose();
            Cancellation = null;

            Running = false;
            CancelRunButton.Enabled = false;
            RebuildButton.Enabled = true;
            Elapsed.Text = $"{Clock.Elapsed.TotalSeconds:0.0}s";

            Repreview();
        }
    }

    void Reset()
    {
        Log.Clear();
        Summary.Clear();
        RawView.Clear();
        Runs.Rows.Clear();
        Links.Controls.Clear();
        ExitBadge.Text = "";
        RawShown = 0;
        RunStart = 0;
    }

    /// <summary>Everything that only makes sense once a run has stopped.</summary>
    void Finish(RunResult result)
    {
        // The view drains on a timer, so without this the summary would be built from most of
        // what the run said rather than all of it.
        Log.DrainNow();

        ExitBadge.Text = result.Cancelled ? "cancelled" : "exit " + result.ExitCode;
        ExitBadge.ForeColor = result.Succeeded ? Color.DarkGreen : Color.Firebrick;

        // Only this run's records: in a sequence, an earlier run's summary is not this one's.
        Summary.Show([.. Log.All.Skip(RunStart)], result);

        // From where the raw view got to, so a sequence does not repeat every earlier run.
        for (; RawShown < Log.All.Count; RawShown++)
        {
            var line = Log.All[RawShown];
            if (line.Kind is "raw" or "stderr") RawView.AppendText(line.Message + Environment.NewLine);
        }

        RunStart = Log.All.Count;

        foreach (var file in result.LogFiles.Concat([result.Options.LogFile, result.Options.JsonLogFile]))
            AddLink(file);
    }

    void AddLink(string? path)
    {
        if (path.IsBlank()) return;
        if (Links.Controls.OfType<LinkLabel>().Any(x => (string?)x.Tag == path)) return;

        var link = new LinkLabel { Text = Path.GetFileName(path), AutoSize = true, Tag = path };
        link.LinkClicked += (_, _) => Open(path);

        var menu = new ContextMenuStrip();
        var reveal = new ToolStripMenuItem("Open its folder");
        reveal.Click += (_, _) => Reveal(Path.GetDirectoryName(path) ?? path);
        menu.Items.Add(reveal);
        link.ContextMenuStrip = menu;

        Links.Controls.Add(link);
    }

    static void Open(string path)
    {
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show("Could not open " + path + ": " + ex.Message); }
    }

    static void Reveal(string folder)
    {
        try { Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show("Could not open " + folder + ": " + ex.Message); }
    }

    static string Why(RunResult result)
    {
        if (result.StartFailure.HasContent()) return result.StartFailure;
        if (result.Cancelled) return "cancelled";

        return "exit code " + result.ExitCode;
    }

    void ShowRuns(List<SuiteOutcome> outcomes)
    {
        Runs.Rows.Clear();

        foreach (var outcome in outcomes)
        {
            var row = Runs.Rows[Runs.Rows.Add(
                outcome.Name,
                outcome.Passed ? "passed" : "FAILED",
                outcome.Result.ExitCode,
                $"{outcome.Result.Duration.TotalSeconds:0.0}s",
                outcome.Failure.Or(outcome.Result.CommandLine))];

            row.DefaultCellStyle.BackColor = outcome.Passed ? Color.Honeydew : Color.MistyRose;
        }

        if (outcomes.Count > 1 || outcomes.Any(x => !x.Passed))
            ExitBadge.Text = $"{outcomes.Count(x => x.Passed)} of {outcomes.Count} passed";
    }

    protected override void OnFormClosing(FormClosingEventArgs args)
    {
        if (Running)
        {
            var answer = MessageBox.Show(this,
                "A run is in progress. Close anyway and stop it?", "Olive Generator Harness",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (answer != DialogResult.Yes) { args.Cancel = true; return; }

            Cancellation?.Cancel();
        }

        Settings.Configuration = Locator.Configuration;
        Settings.SolutionFolder = Accelerate.SolutionFolder;
        Settings.PackagesFile = Accelerate.PackagesFile;
        Settings.Maximised = WindowState == FormWindowState.Maximized;

        if (WindowState == FormWindowState.Normal) { Settings.WindowWidth = Width; Settings.WindowHeight = Height; }

        Options.SaveTo(Settings);
        Settings.Save();

        base.OnFormClosing(args);
    }
}
