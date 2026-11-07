# Troubleshooting

First step, always: **Start → FS25 RealFFB - Diagnostic report**, or the button in the app. The report contains no user name, PC name or serial numbers. Attach it to an [issue](https://github.com/OptimusZuka/FS25-RealFFB/issues).

Logs are in `%APPDATA%\FS25 RealFFB\` (`RealFFB_log.txt`, `setup_log.txt`). Backups are in the `backup\` subfolder.

| Symptom / diagnostic line | Likely cause | What to do |
|---|---|---|
| `[ERROR] HidHide: not installed` | HidHide installation cancelled or failed | run the RealFFB installer again |
| `HidHide driver ... not responding` | the driver has not loaded yet, or the *HidHide Configuration Client* is open | restart Windows; close the Configuration Client |
| `RealFFB allowed in HidHide: ERROR` | the app was moved or reinstalled in another folder | Start → *Configure devices* |
| `Wheel hidden from the game: ERROR` | setup not applied or cancelled | Start → *Configure devices* |
| `Wheel: no supported wheel connected` | wheel off, in compatibility mode, or missing its driver | Fanatec: PC mode (red LED) + Fanatec driver. Logitech: G HUB installed |
| `Other controllers hidden: WARN` | HidHide entries that are not the wheel (RealFFB never creates them) | review them in the *HidHide Configuration Client* |
| `Farming Simulator 25: not found` | installed outside Steam in an unusual folder | run the installer and choose the folder |
| `Mod RealFFB: installed in the wrong folder` | the zip was copied by hand somewhere else | delete that copy; run the installer |
| `game.xml: invalid settings` | the game or another program reset `game.xml` | close FS25 and use *Configure devices* |
| `game.xml: does not exist yet` | FS25 was never opened on this account | open the game once, close it, use *Configure devices* |
| `Permissions (data folder): ERROR` | antivirus or a synced folder blocking `%APPDATA%` | allow the folder in your antivirus |

## Other cases

- **The app opens but there is no force in the game.** Check three things:
  - the *Real FFB Telemetry* mod is active in the savegame;
  - the app shows telemetry arriving;
  - FS25 was started **after** the app.
- **The wheel does not steer in the game.** Either the command bridge is off or FS25 still sees the wheel. Use *Configure devices* and restart the game.
- **The wheel does not work in other games.** Close RealFFB; hiding turns off when the app closes. If the app crashed, open and close it again.
- **Logitech wheel pulls to centre on its own.** Turn off the centering spring in Logitech G HUB.
- **Wrong force strength on a different wheel.** Set *Wheel torque (peak)* to the manufacturer's value.
- **The installer stopped half-way** (power cut, PC turned off). Run it again. Each step checks the current state first: it does not reinstall HidHide or duplicate entries.
- **"Windows protected your PC" / Smart App Control.** See [INSTALL.md](INSTALL.md#2-windows-warnings-what-is-normal).

## Restore everything by hand

1. Uninstall FS25 RealFFB from Windows Settings. `game.xml` and HidHide go back to their previous state.
2. If needed:
   - the original `game.xml` is in `%APPDATA%\FS25 RealFFB\backup\game.xml.*.bak`;
   - the original HidHide setup is in `backup\hidhide-original-*.txt`. Each line is a `HidHideCLI.exe` option.
