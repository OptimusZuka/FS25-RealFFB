# Installation guide

## 1. Download

Download the installer **only** from the official Releases page:
`https://github.com/OptimusZuka/fs25-realffb/releases` → `FS25_RealFFB_Setup_x.y.z.exe`

## Verify the file

Every release publishes `SHA256SUMS.txt`. If the value computed on your PC matches, the file is exactly the one GitHub Actions built from this repository's source code.

1. Open your Downloads folder, right-click an empty area → **Open in Terminal** (or PowerShell).
2. Run:
   ```powershell
   Get-FileHash .\FS25_RealFFB_Setup_x.y.z.exe -Algorithm SHA256
   ```
3. Compare the `Hash` with the line in `SHA256SUMS.txt`. Upper or lower case does not matter.
   - **Same** → safe to run.
   - **Different** → do not run it. Delete the file and download it again from the official page.

Without PowerShell, run `certutil -hashfile FS25_RealFFB_Setup_x.y.z.exe SHA256` in Command Prompt instead.

## 2. Windows warnings (what is normal)

The installer is **not digitally signed** yet (see [SIGNING.md](SIGNING.md)).

- **SmartScreen** may say "Windows protected your PC". After checking the SHA-256, click **More info → Run anyway**.
- **Smart App Control** (Windows 11), when **on**, blocks unsigned apps with no override. **Do not turn it off for RealFFB.** Use the `FS25_RealFFB_BuildOnPC_x.y.z.zip` package instead:
  1. Extract it.
  2. Double-click `install.bat`. It compiles the app on your PC with the compiler built into Windows and opens the same installer.
- **UAC** only appears to install **HidHide** (a driver), showing "Nefarius Software Solutions" as the verified publisher. The rest installs in your user account.
- **Antivirus:** if it flags the file, check the SHA-256 and open an issue with the detection name.

## 3. Installer steps

The installer starts in English. You can choose Português, Español or Français in the first window.

| Step | What it does | Changes anything? |
|---|---|---|
| 1. System | checks for 64-bit Windows 10/11 | no |
| 2. FS25 | searches every Steam library (all drives) and the usual folders of other stores; asks for the folder if not found | no |
| 3. Wheel | lists the connected wheel bases: every device that Windows reports with force feedback, any brand | no |
| 4. HidHide | if missing or outdated, downloads the official installer from `github.com/nefarius/HidHide`, checks Nefarius' digital signature and the SHA-256 published by GitHub, and runs it unmodified (UAC) | installs the driver |
| 5. Devices | allows RealFFB.exe in HidHide (always), finds the wheel base, shows exactly what will be hidden, applies it when you click **Apply**, then reads HidHide again to check | allows RealFFB.exe, hides only the wheel base, adjusts `game.xml` (with a backup) |
| 6. RealFFB | copies the app, the helper and the mod | files |
| 7. Final check | checks files, mod, game.xml, HidHide and the wheel | no |

- **If a restart is needed** after HidHide, the installer tells you, and step 5 opens by itself after the restart.
- **If the wheel is not connected**, you can still finish. Later, plug it in and use **Start → FS25 RealFFB - Configure devices**.

## How the wheel is set up in HidHide

[HidHide](https://github.com/nefarius/HidHide) hides the wheel from FS25 so that only FS25 RealFFB controls its force feedback. Setup configures it automatically, with the official `HidHideCLI` (no administrator rights needed for this part):

1. **RealFFB.exe is allowed** to see hidden devices, using the real install folder. This is done every time, even with no wheel connected.
2. **The wheel base is identified**: the device that Windows (DirectInput) reports **with force feedback**, whatever the brand (Fanatec, Logitech, Thrustmaster, MOZA, Simucube, Simagic, ...). Pedals, shifters, handbrakes, stalks, button boxes and anything without force feedback are never chosen. With several wheels you choose one; if none can be identified safely, you can pick it from the list (with a warning) or cancel, and nothing is hidden.
3. **Only that wheel is hidden**: its HID entries, plus its USB entry only when that USB device contains nothing else.
4. **Setup checks** that HidHide really kept the settings and tells you if it did not.

Entries of other programs are never removed, and running setup again does not create duplicates. Hiding itself is switched on by the app while it is open and off when it closes, so the wheel keeps working in other games.

**After the setup, unplug the wheel's USB cable and plug it back in (or restart Windows)**: Windows applies HidHide changes when the device reconnects.

If something goes wrong, run **Start → FS25 RealFFB - Configure devices** (it repairs the setup) and, if needed, **Start → FS25 RealFFB - Diagnostic report**.

## 4. First use

1. Open **FS25 RealFFB** (desktop shortcut). The app starts FS25 through Steam.
2. In the game, under *Mods*, enable **Real FFB Telemetry** for your savegame.
3. In the app, check steering and pedals (use the *Detect* buttons) and tune the force. Optional: turn on **Realistic centering** on the first page.
4. Logitech: turn off the centering spring in G HUB.

## Updating

When a new version is out, a yellow button appears in the app. You can also download the new installer and run it over the old one. Your settings (`%APPDATA%\FS25 RealFFB`) and the HidHide setup are kept.

## Uninstalling

Go to **Settings → Apps → FS25 RealFFB → Uninstall**.
- At the start, the uninstaller asks whether to keep your settings.
- Then it asks **"Would you also like to uninstall HidHide?"**:
  - **No, keep HidHide installed** (the default): only the FS25 RealFFB entries are removed from HidHide, so your wheel is no longer hidden. Other programs' entries are not touched.
  - **Yes, uninstall HidHide**: after a second confirmation (other programs such as DS4Windows or UCR may use HidHide), device hiding is switched off and HidHide is removed with its **official uninstaller** (UAC; Windows may ask for a restart). If HidHide was already installed before FS25 RealFFB, the uninstaller says so and recommends keeping it.
- `game.xml` is restored to how it was.
