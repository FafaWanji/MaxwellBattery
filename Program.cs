namespace MaxwellBattery;

static class Program
{
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

        using var mutex = new Mutex(true, "MaxwellBattery.SingleInstance", out bool first);
        if (!first) return 0;

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayContext());
        return 0;
    }
}
