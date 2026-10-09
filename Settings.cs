using System.Text.Json;

namespace MaxwellBattery;

/// <summary>
/// Per-user files in %LOCALAPPDATA%\MaxwellBattery. Dev mode is enabled by creating
/// settings.json with { "devMode": true }.
/// </summary>
static class Settings
{
    public static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MaxwellBattery");

    public static readonly string CrashLogPath = Path.Combine(Dir, "crash.log");
    public static readonly string PendingCrashPath = Path.Combine(Dir, "pending-crash");
    static readonly string SettingsPath = Path.Combine(Dir, "settings.json");

    public static bool DevMode { get; } = LoadDevMode();

    static bool LoadDevMode()
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(SettingsPath));
            return doc.RootElement.TryGetProperty("devMode", out var v) && v.ValueKind == JsonValueKind.True;
        }
        catch { return false; }
    }
}
