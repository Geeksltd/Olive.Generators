using Olive.GeneratorHarness.UI;

namespace Olive.GeneratorHarness;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        Application.ThreadException += (_, args) => Report(args.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, args) => Report(args.ExceptionObject as Exception);

        Application.Run(new MainForm());
    }

    /// <summary>
    /// A harness that dies silently is worse than useless, so anything that escapes is shown
    /// with enough detail to act on.
    /// </summary>
    static void Report(Exception? error)
    {
        if (error == null) return;

        MessageBox.Show(error.ToString(), "Olive Generator Harness",
            MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
