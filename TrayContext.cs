using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace MaxwellBattery;

sealed class TrayContext : ApplicationContext
{
    const int PollMs = 60_000;
    const int LowBattery = 15;

    readonly NotifyIcon _tray;
    readonly System.Windows.Forms.Timer _timer;
    readonly ToolStripMenuItem _autostart;
    Icon? _currentIcon;
    bool _busy;
    bool _lowWarned;

    /// <summary>Last tooltip text, included in crash logs.</summary>
    public static string LastStatus { get; private set; } = "not read yet";

    public TrayContext()
    {
        _autostart = new ToolStripMenuItem("Start with Windows", null, (_, _) => ToggleAutostart())
        {
            Checked = Autostart.IsEnabled(),
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("Refresh", null, (_, _) => Poll());
        menu.Items.Add(_autostart);
        if (Settings.DevMode)
        {
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Dev: Open crash log", null, (_, _) => OpenCrashLog());
            menu.Items.Add("Dev: Simulate crash", null, (_, _) => throw new InvalidOperationException("Simulated crash from dev menu"));
        }
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitThread());

        _tray = new NotifyIcon { ContextMenuStrip = menu, Visible = true };
        _tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) Poll(); };
        _tray.BalloonTipClicked += (_, _) => { if (_crashBalloon) OpenCrashLog(); };
        _tray.BalloonTipClosed += (_, _) => _crashBalloon = false;
        Show("…", Color.Gray, "Audeze Maxwell: reading…");
        ReportPendingCrash();

        _timer = new System.Windows.Forms.Timer { Interval = PollMs };
        _timer.Tick += (_, _) => Poll();
        _timer.Start();
        Poll();
    }

    bool _crashBalloon;

    /// <summary>Shows the crash that caused the previous restart (dev mode only).</summary>
    void ReportPendingCrash()
    {
        string summary;
        try
        {
            if (!File.Exists(Settings.PendingCrashPath)) return;
            summary = File.ReadAllText(Settings.PendingCrashPath).Trim();
            File.Delete(Settings.PendingCrashPath);
        }
        catch { return; }

        if (!Settings.DevMode) return;
        _crashBalloon = true;
        _tray.ShowBalloonTip(10000, "MaxwellBattery crashed and restarted", summary + "\nClick to open crash log.", ToolTipIcon.Error);
    }

    static void OpenCrashLog()
    {
        if (File.Exists(Settings.CrashLogPath))
            Process.Start(new ProcessStartInfo("notepad.exe", $"\"{Settings.CrashLogPath}\""));
    }

    async void Poll()
    {
        if (_busy) return;
        _busy = true;
        try
        {
            var (state, pct) = await Task.Run(() =>
            {
                try { return MaxwellHid.ReadBattery(); }
                catch { return (MaxwellHid.State.HeadsetOff, -1); }
            });

            switch (state)
            {
                case MaxwellHid.State.NoDongle:
                    Show("–", Color.Gray, "Audeze Maxwell: dongle not connected");
                    break;
                case MaxwellHid.State.HeadsetOff:
                    Show("–", Color.Gray, "Audeze Maxwell: headset off");
                    break;
                default:
                    Show(pct.ToString(), ColorFor(pct), $"Audeze Maxwell: {pct}%");
                    if (pct <= LowBattery && !_lowWarned)
                    {
                        _tray.ShowBalloonTip(5000, "Audeze Maxwell", $"Battery low: {pct}%", ToolTipIcon.Warning);
                        _lowWarned = true;
                    }
                    else if (pct > LowBattery + 5) _lowWarned = false;
                    break;
            }
        }
        finally { _busy = false; }
    }

    static Color ColorFor(int pct) =>
        pct > 50 ? Color.FromArgb(80, 220, 100) :
        pct > 20 ? Color.FromArgb(255, 200, 40) :
                   Color.FromArgb(255, 70, 70);

    void Show(string text, Color color, string tooltip)
    {
        var old = _currentIcon;
        _currentIcon = RenderIcon(text, color);
        _tray.Icon = _currentIcon;
        _tray.Text = tooltip;
        LastStatus = tooltip;
        if (old != null) { DestroyIcon(old.Handle); old.Dispose(); }
    }

    static Icon RenderIcon(string text, Color color)
    {
        int size = SystemInformation.SmallIconSize.Width;
        using var bmp = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Transparent);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            // Shrink font until the text fits the icon width.
            float fontSize = size * (text.Length >= 3 ? 0.55f : 0.75f);
            Font font;
            SizeF measured;
            while (true)
            {
                font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
                measured = g.MeasureString(text, font, PointF.Empty, StringFormat.GenericTypographic);
                if (measured.Width <= size || fontSize <= 6) break;
                font.Dispose();
                fontSize -= 0.5f;
            }

            using (font)
            using (var brush = new SolidBrush(color))
            {
                float x = (size - measured.Width) / 2f;
                float y = (size - measured.Height) / 2f;
                g.DrawString(text, font, brush, x, y, StringFormat.GenericTypographic);
            }
        }
        return Icon.FromHandle(bmp.GetHicon());
    }

    void ToggleAutostart()
    {
        if (Autostart.IsEnabled()) Autostart.Disable();
        else Autostart.Enable();
        _autostart.Checked = Autostart.IsEnabled();
    }

    protected override void ExitThreadCore()
    {
        _timer.Stop();
        _tray.Visible = false;
        _tray.Dispose();
        base.ExitThreadCore();
    }

    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr handle);
}
