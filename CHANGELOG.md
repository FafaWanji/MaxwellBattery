# Changelog

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

[1.0.1]: https://github.com/FafaWanji/MaxwellBattery/releases/tag/v1.0.1
[1.0.0]: https://github.com/FafaWanji/MaxwellBattery/releases/tag/v1.0.0
