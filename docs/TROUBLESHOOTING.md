# Troubleshooting

First step, always: **Start → FS25 RealFFB - Diagnostic report**, or the button in the app. The report contains no user name, PC name or serial numbers. Attach it to an [issue](https://github.com/OptimusZuka/fs25-realffb/issues).

Logs are in `%APPDATA%\FS25 RealFFB\` (`RealFFB_log.txt`, `setup_log.txt`). Backups are in the `backup\` subfolder.

| Symptom / diagnostic line | Likely cause | What to do |
|---|---|---|
| `HidHide installed: ERROR` | HidHide installation cancelled or failed | run the RealFFB installer again |
| `HidHide driver: ERROR ... not responding` | the driver has not loaded yet, or the *HidHide Configuration Client* is open | restart Windows; close the Configuration Client |
| `Device hiding: DISABLED` | normal while FS25 RealFFB is closed | nothing: the app enables it when it opens |
| `FS25 RealFFB whitelist: ERROR` | configuration problem: RealFFB.exe not allowed (e.g. moved to another folder) | Start → *Configure devices* |
| `Steering wheel detected: ERROR ... hardware/driver problem` | Windows sees no force feedback wheel: off, unplugged, compatibility mode or missing driver | switch the base on, check USB and power. Fanatec: PC mode (red LED) + Fanatec driver. Logitech: G HUB. MOZA: Pit House. Thrustmaster: its driver package |
| `Steering wheel selected for hiding: ERROR` | configuration problem: the wheel is not in the HidHide list | Start → *Configure devices*, then unplug and replug the wheel |
| `FS25 RealFFB device access: WARN` | the app does not see force feedback on the wheel | close the manufacturer's software if it holds the wheel; replug it |
| `HidHide configuration: INCOMPLETE` | one of the lines above failed; the line also shows the last setup result | follow that line |
| `Other HidHide entries` | entries set by you or another program | nothing: FS25 RealFFB never changes them |
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
- **Force feedback works for a moment and then stops / fights with the game.** FS25 still sees the wheel. Use *Configure devices*, unplug and replug the wheel, then restart the game.
- **The wheel does not steer in the game.** Either the command bridge is off or FS25 still sees the wheel. Use *Configure devices* and restart the game.
- **The wheel does not work in other games.** Close RealFFB; hiding turns off when the app closes. If the app crashed, open and close it again.
- **Logitech wheel pulls to centre on its own.** Turn off the centering spring in Logitech G HUB.
- **Wrong force strength on a different wheel.** Set *Wheel torque (peak)* to the manufacturer's value, or use **Hardware → Calibrate force**.
- **The steering wheel does not return to centre on a tractor.** With *Realistic centering* on, this is intended: hydrostatic steering does not self-centre in real life. Switch it per vehicle (Auto · On · Off) or turn the option off.
- **The installer stopped half-way** (power cut, PC turned off). Run it again. Each step checks the current state first: it does not reinstall HidHide or duplicate entries.
- **"Windows protected your PC" / Smart App Control.** See [INSTALL.md](INSTALL.md#2-windows-warnings-what-is-normal).

## Restore everything by hand

1. Uninstall FS25 RealFFB from Windows Settings. `game.xml` and HidHide go back to their previous state.
2. If needed:
   - the original `game.xml` is in `%APPDATA%\FS25 RealFFB\backup\game.xml.*.bak`;
   - the original HidHide setup is in `backup\hidhide-original-*.txt`. Each line is a `HidHideCLI.exe` option.
