# MaxwellBattery

A tiny Windows tray app that shows the battery level of the **Audeze Maxwell** headset in the taskbar notification area.

- Battery percentage drawn directly as the tray icon (green > 50 %, yellow > 20 %, red ≤ 20 %)
- Grey `–` when the headset is off or not connected
- Tooltip with the exact value, left-click to refresh, updates every 60 s
- Low-battery notification at 15 %
- Optional "Start with Windows" (right-click menu)
- No Audeze HQ required

## Supported connections

| Connection | USB product ID | Tested |
|---|---|---|
| USB wireless dongle | `3329:4B18` | ✓ |
| USB-C cable | `3329:4B1E` | ✓ |

Any Audeze device with vendor ID `0x3329` exposing the vendor HID interface (usage page `0xFF13`) is tried, so other Maxwell dongle variants (e.g. `4B19`) will most likely work too.

## Download

Grab `MaxwellBattery.exe` from the [latest release](../../releases/latest) and run it. It is self-contained — no .NET install needed. Windows SmartScreen may warn because the exe is unsigned; choose *More info → Run anyway*.

New tray icons are often hidden behind the `^` arrow. Drag the icon onto the taskbar, or enable it under *Settings → Personalization → Taskbar → Other system tray icons*.

## Build from source

Requires the .NET 8 SDK.

```
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

Diagnostics (prints the raw HID response):

```
MaxwellBattery.exe --probe > probe.txt
```

## Credits

The Maxwell HID protocol (init sequence and battery request) comes from [HeadsetControl](https://github.com/Sapd/HeadsetControl) (`lib/devices/audeze_maxwell.hpp`), licensed GPL-3.0.

## License

GPL-3.0 — see [LICENSE](LICENSE).
