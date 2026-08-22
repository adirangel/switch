using ScreenSwitch.Core;

namespace ScreenSwitch;

internal static class Program
{
    private const string SingleInstanceMutexName = @"Local\ScreenSwitch.SingleInstance";

    [STAThread]
    private static int Main(string[] args)
    {
        // Before anything that can produce text: every string below is looked up by culture.
        UiCulture.ApplyFromConfig();

        // Command-line mode exists so the switch can be bound to anything that can run a program
        // (a Stream Deck key, a scheduled task, another launcher) and so problems can be
        // diagnosed without guessing at what the tray icon is doing.
        if (args.Length > 0)
        {
            return CommandLineRunner.Run(args);
        }

        using var mutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var isFirstInstance);
        if (!isFirstInstance)
        {
            MessageBox.Show(
                Strings.App_AlreadyRunning,
                "ScreenSwitch",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information,
                MessageBoxDefaultButton.Button1,
                UiCulture.MessageBoxOptions);
            return 0;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayAppContext());
        return 0;
    }
}
