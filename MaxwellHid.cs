using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace MaxwellBattery;

/// <summary>
/// Reads the battery level of an Audeze Maxwell over its vendor HID interface
/// (usage page 0xFF13). Protocol taken from HeadsetControl's audeze_maxwell.hpp.
/// </summary>
public static class MaxwellHid
{
    const ushort AudezeVid = 0x3329;
    const ushort VendorUsagePage = 0xFF13;
    const int MsgSize = 62;
    const int DelayMs = 60;

    static readonly byte[][] InitRequests =
    {
        new byte[] { 0x06, 0x08, 0x80, 0x05, 0x5A, 0x04, 0x00, 0x01, 0x09, 0x20 },
        new byte[] { 0x06, 0x08, 0x80, 0x05, 0x5A, 0x04, 0x00, 0x01, 0x09, 0x25 },
        new byte[] { 0x06, 0x07, 0x80, 0x05, 0x5A, 0x03, 0x00, 0x07, 0x1C },
        new byte[] { 0x06, 0x08, 0x80, 0x05, 0x5A, 0x04, 0x00, 0x01, 0x09, 0x28 },
        new byte[] { 0x06, 0x08, 0x80, 0x05, 0x5A, 0x04, 0x00, 0x83, 0x2C, 0x01 },
        new byte[] { 0x06, 0x08, 0x80, 0x05, 0x5A, 0x04, 0x00, 0x83, 0x2C, 0x07 },
        new byte[] { 0x06, 0x07, 0x00, 0x05, 0x5A, 0x03, 0x00, 0x07, 0x1C },
        new byte[] { 0x06, 0x08, 0x80, 0x05, 0x5A, 0x04, 0x00, 0x01, 0x09, 0x2D },
        new byte[] { 0x06, 0x08, 0x80, 0x05, 0x5A, 0x04, 0x00, 0x01, 0x09, 0x2C },
        new byte[] { 0x06, 0x08, 0x80, 0x05, 0x5A, 0x04, 0x00, 0x01, 0x09 },
        new byte[] { 0x06, 0x08, 0x80, 0x05, 0x5A, 0x04, 0x00, 0x83, 0x2C, 0x0B },
        new byte[] { 0x06, 0x08, 0x80, 0x05, 0x5A, 0x04, 0x00, 0x01, 0x09, 0x24 },
        new byte[] { 0x06, 0x08, 0x80, 0x05, 0x5A, 0x04, 0x00, 0x01, 0x09, 0x2F },
        new byte[] { 0x06, 0x07, 0x80, 0x05, 0x5A, 0x03, 0x00, 0xD6, 0x0C },
    };

    static readonly byte[] BatteryRequest = { 0x06, 0x08, 0x80, 0x05, 0x5A, 0x04, 0x00, 0x01, 0x09, 0x22 };

    /// <summary>Result of a battery query.</summary>
    public enum State { NoDongle, HeadsetOff, Ok }

    public static (State state, int percent) ReadBattery(Action<string>? log = null)
    {
        string? path = FindDevicePath();
        if (path == null) return (State.NoDongle, -1);
        log?.Invoke($"Device: {path}");

        using var h = CreateFile(path, GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE,
            IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
        if (h.IsInvalid) throw new IOException($"CreateFile failed: {Marshal.GetLastWin32Error()}");

        var (inLen, outLen) = GetReportLengths(h);
        log?.Invoke($"Report lengths: in={inLen} out={outLen}");

        foreach (var req in InitRequests)
            Exchange(h, req, inLen, outLen);

        byte[] resp = Exchange(h, BatteryRequest, inLen, outLen);
        log?.Invoke("Battery response: " + BitConverter.ToString(resp));

        if (resp[0] == 0x00) return (State.HeadsetOff, -1);
        for (int i = 0; i < resp.Length - 4; i++)
        {
            if (resp[i] == 0xD6 && resp[i + 1] == 0x0C && resp[i + 2] == 0x00 && resp[i + 3] == 0x00)
                return (State.Ok, resp[i + 4]);
        }
        return (State.HeadsetOff, -1);
    }

    static byte[] Exchange(SafeFileHandle h, byte[] request, int inLen, int outLen)
    {
        Thread.Sleep(DelayMs);

        var outBuf = new byte[Math.Max(outLen, MsgSize)];
        Array.Copy(request, outBuf, request.Length);
        if (!WriteFile(h, outBuf, (uint)outLen, out _, IntPtr.Zero))
            throw new IOException($"WriteFile failed: {Marshal.GetLastWin32Error()}");

        var inBuf = new byte[Math.Max(inLen, MsgSize)];
        inBuf[0] = 0x07;
        if (!HidD_GetInputReport(h, inBuf, (uint)inLen))
            throw new IOException($"HidD_GetInputReport failed: {Marshal.GetLastWin32Error()}");
        return inBuf;
    }

    static string? FindDevicePath()
    {
        HidD_GetHidGuid(out Guid hidGuid);
        IntPtr devInfo = SetupDiGetClassDevs(ref hidGuid, IntPtr.Zero, IntPtr.Zero, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
        if (devInfo == new IntPtr(-1)) return null;
        try
        {
            var ifData = new SP_DEVICE_INTERFACE_DATA { cbSize = Marshal.SizeOf<SP_DEVICE_INTERFACE_DATA>() };
            for (uint i = 0; SetupDiEnumDeviceInterfaces(devInfo, IntPtr.Zero, ref hidGuid, i, ref ifData); i++)
            {
                string? path = GetInterfacePath(devInfo, ref ifData);
                if (path == null || !path.Contains("vid_3329", StringComparison.OrdinalIgnoreCase)) continue;

                using var h = CreateFile(path, 0, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
                if (h.IsInvalid) continue;

                var attrs = new HIDD_ATTRIBUTES { Size = Marshal.SizeOf<HIDD_ATTRIBUTES>() };
                if (!HidD_GetAttributes(h, ref attrs) || attrs.VendorID != AudezeVid) continue;
                if (GetUsagePage(h) == VendorUsagePage) return path;
            }
        }
        finally { SetupDiDestroyDeviceInfoList(devInfo); }
        return null;
    }

    static unsafe string? GetInterfacePath(IntPtr devInfo, ref SP_DEVICE_INTERFACE_DATA ifData)
    {
        SetupDiGetDeviceInterfaceDetail(devInfo, ref ifData, IntPtr.Zero, 0, out uint size, IntPtr.Zero);
        if (size == 0) return null;
        IntPtr buf = Marshal.AllocHGlobal((int)size);
        try
        {
            Marshal.WriteInt32(buf, IntPtr.Size == 8 ? 8 : 6); // cbSize of SP_DEVICE_INTERFACE_DETAIL_DATA_W
            if (!SetupDiGetDeviceInterfaceDetail(devInfo, ref ifData, buf, size, out _, IntPtr.Zero)) return null;
            return Marshal.PtrToStringUni(buf + 4);
        }
        finally { Marshal.FreeHGlobal(buf); }
    }

    static ushort GetUsagePage(SafeFileHandle h)
    {
        if (!HidD_GetPreparsedData(h, out IntPtr pp)) return 0;
        try { return HidP_GetCaps(pp, out HIDP_CAPS caps) == HIDP_STATUS_SUCCESS ? caps.UsagePage : (ushort)0; }
        finally { HidD_FreePreparsedData(pp); }
    }

    static (int inLen, int outLen) GetReportLengths(SafeFileHandle h)
    {
        if (!HidD_GetPreparsedData(h, out IntPtr pp)) return (MsgSize, MsgSize);
        try
        {
            if (HidP_GetCaps(pp, out HIDP_CAPS caps) != HIDP_STATUS_SUCCESS) return (MsgSize, MsgSize);
            return (caps.InputReportByteLength, caps.OutputReportByteLength);
        }
        finally { HidD_FreePreparsedData(pp); }
    }

    // ---- Win32 interop ----

    const uint GENERIC_READ = 0x80000000, GENERIC_WRITE = 0x40000000;
    const uint FILE_SHARE_READ = 1, FILE_SHARE_WRITE = 2, OPEN_EXISTING = 3;
    const uint DIGCF_PRESENT = 0x2, DIGCF_DEVICEINTERFACE = 0x10;
    const int HIDP_STATUS_SUCCESS = 0x00110000;

    [StructLayout(LayoutKind.Sequential)]
    struct SP_DEVICE_INTERFACE_DATA { public int cbSize; public Guid InterfaceClassGuid; public int Flags; public IntPtr Reserved; }

    [StructLayout(LayoutKind.Sequential)]
    struct HIDD_ATTRIBUTES { public int Size; public ushort VendorID; public ushort ProductID; public ushort VersionNumber; }

    [StructLayout(LayoutKind.Sequential)]
    struct HIDP_CAPS
    {
        public ushort Usage, UsagePage, InputReportByteLength, OutputReportByteLength, FeatureReportByteLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)] public ushort[] Reserved;
        public ushort NumberLinkCollectionNodes, NumberInputButtonCaps, NumberInputValueCaps, NumberInputDataIndices,
            NumberOutputButtonCaps, NumberOutputValueCaps, NumberOutputDataIndices,
            NumberFeatureButtonCaps, NumberFeatureValueCaps, NumberFeatureDataIndices;
    }

    [DllImport("hid.dll")] static extern void HidD_GetHidGuid(out Guid guid);
    [DllImport("hid.dll", SetLastError = true)] static extern bool HidD_GetAttributes(SafeFileHandle h, ref HIDD_ATTRIBUTES a);
    [DllImport("hid.dll", SetLastError = true)] static extern bool HidD_GetPreparsedData(SafeFileHandle h, out IntPtr pp);
    [DllImport("hid.dll")] static extern bool HidD_FreePreparsedData(IntPtr pp);
    [DllImport("hid.dll")] static extern int HidP_GetCaps(IntPtr pp, out HIDP_CAPS caps);
    [DllImport("hid.dll", SetLastError = true)] static extern bool HidD_GetInputReport(SafeFileHandle h, byte[] buf, uint len);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr SetupDiGetClassDevs(ref Guid g, IntPtr enumerator, IntPtr parent, uint flags);
    [DllImport("setupapi.dll", SetLastError = true)]
    static extern bool SetupDiEnumDeviceInterfaces(IntPtr devInfo, IntPtr devData, ref Guid g, uint idx, ref SP_DEVICE_INTERFACE_DATA d);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr devInfo, ref SP_DEVICE_INTERFACE_DATA d, IntPtr detail, uint size, out uint required, IntPtr devData);
    [DllImport("setupapi.dll")] static extern bool SetupDiDestroyDeviceInfoList(IntPtr devInfo);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr sec, uint disp, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool WriteFile(SafeFileHandle h, byte[] buf, uint len, out uint written, IntPtr overlapped);
}
