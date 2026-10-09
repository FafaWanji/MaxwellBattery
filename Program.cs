using System.Diagnostics;

namespace MaxwellBattery;

static class Program
{
    const string ChildArg = "--child";
    const int MaxCrashes = 5;
    static readonly TimeSpan CrashWindow = TimeSpan.FromMinutes(10);

    static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MaxwellBattery", "crash.log");

    [STAThread]
    static int Main(string[] args)
    {
        if (args.Contains("--probe"))
        {
            // Console diagnostics: MaxwellBattery.exe --probe > out.txt
            try
            {
                var (state, pct) = MaxwellHid.ReadBattery(Console.WriteLine);
                Console.WriteLine($"State={state} Percent={pct}");
                return 0;
            }
            catch (Exception ex) { Console.WriteLine(ex); return 1; }
        }

        if (args.Contains(ChildArg)) return RunTray();

        using var mutex = new Mutex(true, "MaxwellBattery.SingleInstance", out bool first);
        if (!first) return 0;
        return Supervise();
    }

    /// <summary>
    /// Runs the tray app as a child process and restarts it whenever it exits with a
    /// non-zero code (crash, killed). Exit code 0 means the user chose Exit.
    /// </summary>
    static int Supervise()
    {
        var crashes = new Queue<DateTime>();
        while (true)
        {
            int code;
            try
            {
                using var child = Process.Start(new ProcessStartInfo(Environment.ProcessPath!, ChildArg) { UseShellExecute = false })!;
                child.WaitForExit();
                code = child.ExitCode;
            }
            catch (Exception ex) { Log($"Failed to start tray process: {ex}"); code = -1; }

            if (code == 0) return 0;

            Log($"Tray process exited with code {code}, restarting");
            crashes.Enqueue(DateTime.Now);
            while (crashes.Count > 0 && DateTime.Now - crashes.Peek() > CrashWindow) crashes.Dequeue();
            if (crashes.Count >= MaxCrashes)
            {
                Log($"{MaxCrashes} crashes within {CrashWindow.TotalMinutes} minutes, giving up");
                return 1;
            }
            Thread.Sleep(TimeSpan.FromSeconds(5));
        }
    }

    static int RunTray()
    {
        // Crash hard instead of showing the WinForms error dialog, so the supervisor can restart us.
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            Log($"Unhandled exception: {e.ExceptionObject}");
            Environment.Exit(1);
        };

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayContext());
        return 0;
    }

    static void Log(string message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}");
        }
        catch { }
    }
}
