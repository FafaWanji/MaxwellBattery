using System.Diagnostics;
using System.Text.RegularExpressions;

namespace MaxwellBattery;

static class Program
{
    const string ChildArg = "--child";
    const int MaxCrashes = 5;
    const int SessionEndExitCode = 0x40010004;
    static readonly TimeSpan CrashWindow = TimeSpan.FromMinutes(10);

    static string Version => typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "?";

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

        try { Autostart.Migrate(); }
        catch (Exception ex) { Log($"Autostart migration failed: {ex.Message}"); }
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
            // Windows ends processes with DBG_TERMINATE_PROCESS at logoff/shutdown; not a crash.
            if (code == SessionEndExitCode) return 0;

            // The child writes pending-crash itself when it catches an exception.
            if (!File.Exists(Settings.PendingCrashPath))
            {
                string summary = $"Process exited with code {code} (no exception captured, likely killed)";
                Log(summary);
                WritePendingCrash(summary);
            }
            Log("Restarting tray process");

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
            var ex = e.ExceptionObject as Exception;
            string summary = ex == null ? "Unknown error" : Summarize(ex);
            Log($"CRASH v{Version}: {summary}{Environment.NewLine}" +
                $"  Last battery: {TrayContext.LastStatus}{Environment.NewLine}" +
                $"  {e.ExceptionObject}");
            WritePendingCrash(summary);
            Environment.Exit(1);
        };

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayContext());
        return 0;
    }

    /// <summary>"InvalidOperationException in TrayContext.Poll: message"</summary>
    static string Summarize(Exception ex)
    {
        // WinForms rethrows UI-thread exceptions wrapped; report the real one.
        while (ex is System.Reflection.TargetInvocationException or AggregateException && ex.InnerException != null)
            ex = ex.InnerException;

        var frame = Regex.Match(ex.StackTrace ?? "", @"at MaxwellBattery\.([\w.]+)\(");
        string where = frame.Success ? $" in {frame.Groups[1].Value}" : "";
        return $"{ex.GetType().Name}{where}: {ex.Message}";
    }

    static void WritePendingCrash(string summary)
    {
        try
        {
            Directory.CreateDirectory(Settings.Dir);
            File.WriteAllText(Settings.PendingCrashPath, summary);
        }
        catch { }
    }

    static void Log(string message)
    {
        try
        {
            Directory.CreateDirectory(Settings.Dir);
            File.AppendAllText(Settings.CrashLogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}");
        }
        catch { }
    }
}
