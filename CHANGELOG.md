# Changelog

## [1.1.0] - 2026-10-17

### Added
- Charging indicator. While the headset is connected to the PC by USB-C cable, the tray icon shows a ⚡ next to the number and the tooltip says "(charging)", or "(full, plugged in)" at 100 %. Plugging in or unplugging updates the icon within about 2 seconds. Charging from a wall charger or power bank is not detected (the headset does not report it).
- No low-battery warning while charging.

### Fixed
- "Start with Windows" still did not start the app at login on some systems: Windows skipped the startup entry even when it was enabled. Autostart now uses a per-user Task Scheduler logon task (no admin needed). Existing installs are moved over automatically on first start, and the task follows the exe if it is moved.
- Shutting down or logging off was logged as a crash (and shown as one in dev mode).

### Upgrading
Right-click the tray icon → **Exit** before running the new version, otherwise the old one keeps running.

## [1.0.2] - 2026-10-10

### Fixed
- "Start with Windows" did not start the app at login. Windows skips startup entries that are not marked enabled in its startup-app list (Task Manager > Startup apps). The app now marks itself enabled, and the menu checkmark follows the Task Manager toggle.

### Added
- Dev mode: create `%LOCALAPPDATA%\MaxwellBattery\settings.json` with `{ "devMode": true }` to get a notification after a crash (exception and method, click to open the log) plus "Open crash log" and "Simulate crash" menu items.
- Crash log entries now include version, a one-line summary and the last battery state.

## [1.0.1] - 2026-10-09

### Fixed
- Battery often showed as `–` (headset off) or reported a wrong value. Replies from the headset arrive late, so the app read stale or unrelated replies. It now clears old replies, sends the battery request, and waits for the matching answer.
- Reading the battery is much faster (~250 ms instead of ~1.2 s). The init sequence is only sent as a fallback when the headset does not answer.

### Added
- Automatic restart after a crash. A hidden supervisor process restarts the tray app if it exits unexpectedly (gives up after 5 crashes within 10 minutes). Choosing **Exit** still closes it.
- Crash log at `%LOCALAPPDATA%\MaxwellBattery\crash.log`.

## [1.0.0] - 2026-10-09

### Added
- First release: tray icon showing the Audeze Maxwell battery percentage, colour-coded, with tooltip, low-battery notification at 15 % and "Start with Windows" option.
- Works with the wireless dongle (`3329:4B18`) and USB-C cable (`3329:4B1E`).

[1.1.0]: https://github.com/FafaWanji/MaxwellBattery/releases/tag/v1.1.0
[1.0.2]: https://github.com/FafaWanji/MaxwellBattery/releases/tag/v1.0.2
[1.0.1]: https://github.com/FafaWanji/MaxwellBattery/releases/tag/v1.0.1
[1.0.0]: https://github.com/FafaWanji/MaxwellBattery/releases/tag/v1.0.0
