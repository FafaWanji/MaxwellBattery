using System.Diagnostics;
using System.Security;
using System.Security.Principal;
using Microsoft.Win32;

namespace MaxwellBattery;

/// <summary>
/// "Start with Windows" via a per-user Task Scheduler logon task. The HKCU Run key
/// turned out to be skipped by Explorer at login on some systems, so older installs
/// that used it are migrated on startup.
/// </summary>
static class Autostart
{
    const string TaskName = "MaxwellBattery";
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string StartupApprovedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

    public static bool IsEnabled() => Schtasks($"/query /tn \"{TaskName}\"") == 0;

    public static void Enable()
    {
        string user = WindowsIdentity.GetCurrent().Name;
        string exe = Environment.ProcessPath!;
        string xml = $"""
            <?xml version="1.0" encoding="UTF-16"?>
            <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
              <RegistrationInfo><Description>Audeze Maxwell battery tray indicator</Description></RegistrationInfo>
              <Triggers><LogonTrigger><Enabled>true</Enabled><UserId>{SecurityElement.Escape(user)}</UserId></LogonTrigger></Triggers>
              <Principals><Principal id="Author"><UserId>{SecurityElement.Escape(user)}</UserId><LogonType>InteractiveToken</LogonType><RunLevel>LeastPrivilege</RunLevel></Principal></Principals>
              <Settings>
                <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
                <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
                <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
                <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
                <Enabled>true</Enabled>
              </Settings>
              <Actions Context="Author"><Exec><Command>{SecurityElement.Escape(exe)}</Command><WorkingDirectory>{SecurityElement.Escape(Path.GetDirectoryName(exe))}</WorkingDirectory></Exec></Actions>
            </Task>
            """;

        string file = Path.Combine(Path.GetTempPath(), "MaxwellBattery-task.xml");
        File.WriteAllText(file, xml, System.Text.Encoding.Unicode);
        try { Schtasks($"/create /tn \"{TaskName}\" /xml \"{file}\" /f"); }
        finally { File.Delete(file); }
    }

    public static void Disable() => Schtasks($"/delete /tn \"{TaskName}\" /f");

    /// <summary>
    /// Moves an old Run-key autostart to the logon task, and refreshes an existing task
    /// so it points at the current exe location.
    /// </summary>
    public static void Migrate()
    {
        bool hadRunKey;
        using (var run = Registry.CurrentUser.OpenSubKey(RunKey, writable: true))
        {
            hadRunKey = run?.GetValue(TaskName) != null;
            run?.DeleteValue(TaskName, false);
        }
        using (var approved = Registry.CurrentUser.OpenSubKey(StartupApprovedKey, writable: true))
            approved?.DeleteValue(TaskName, false);

        if (hadRunKey || (IsEnabled() && !TaskPointsTo(Environment.ProcessPath!))) Enable();
    }

    static bool TaskPointsTo(string exe)
    {
        var (code, output) = SchtasksOutput($"/query /tn \"{TaskName}\" /xml");
        return code == 0 && output.Contains(SecurityElement.Escape(exe), StringComparison.OrdinalIgnoreCase);
    }

    static int Schtasks(string args) => SchtasksOutput(args).code;

    static (int code, string output) SchtasksOutput(string args)
    {
        using var p = Process.Start(new ProcessStartInfo("schtasks.exe", args)
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        })!;
        string output = p.StandardOutput.ReadToEnd();
        p.StandardError.ReadToEnd();
        p.WaitForExit();
        return (p.ExitCode, output);
    }
}
