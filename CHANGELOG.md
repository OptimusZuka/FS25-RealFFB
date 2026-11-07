# Changelog

## [0.6.0] - 2026-11-07 (beta)
- Everything in English by default, with Portuguese (Brazil), Spanish and French available in the app (System → Language) and in the installer (language chosen in the first window).
- New project page (README), guides in English, and English images.
- Installer and program icons now show in Windows Explorer (BMP images in the small icon sizes).
- `instalar.bat` renamed to `install.bat`; the "build on your PC" package is now `FS25_RealFFB_BuildOnPC_x.y.z.zip`.

## [0.5.1] - 2026-11-07 (beta)
- Wheel selection ("Wheel" section): automatic or a specific wheel.
- Header buttons are no longer cut off.
- Pedals unplugged from the base (all axes at 0) are no longer sent to the game as half-pressed.
- Update check now finds beta releases.
- Logo in the local installer window.

## [0.5.0] - 2026-11-07 (beta)
First public version, in testing.

- Official installer (Inno Setup) with 7 steps:
  - system check;
  - FS25 detection in every Steam library;
  - wheel detection;
  - official HidHide installation with signature check;
  - HidHide setup for the chosen wheel only;
  - `game.xml` adjustment with a backup;
  - final check.
- Uninstall from Windows Settings restores HidHide and `game.xml` as they were. HidHide is not uninstalled, and profiles are kept if you want.
- Settings live in `%APPDATA%\FS25 RealFFB` and survive updates.
- The app checks GitHub for new versions and installs them only with permission, after checking the SHA-256.
- New interface in the OptimusZuka colours (black, yellow and white):
  - header with status;
  - live panel with commands and forces;
  - settings in sections.
- "Pin on top": a compact window that always stays visible.
- The mouse wheel no longer changes sliders; it scrolls the page.
- Per-vehicle FFB profiles.
- Logitech wheel support (G29, G920, G923, G PRO, G27, G25), with peak torque per model.
- "Create diagnostic report" button (anonymised).
- Force feedback engine unchanged from v0.17.
