namespace ScreenSwitch;

internal static class Program
{
    private const string SingleInstanceMutexName = @"Local\ScreenSwitch.SingleInstance";

    [STAThread]
    private static int Main(string[] args)
    {
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
                "ScreenSwitch כבר פועל. חפש את האייקון באזור ההודעות (ליד השעון).",
                "ScreenSwitch",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information,
                MessageBoxDefaultButton.Button1,
                MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
            return 0;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayAppContext());
        return 0;
    }
}
