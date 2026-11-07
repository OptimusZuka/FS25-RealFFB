# Installation guide

## 1. Download

Download the installer **only** from the official Releases page:
`https://github.com/OptimusZuka/FS25-RealFFB/releases` → `FS25_RealFFB_Setup_x.y.z.exe`

## Verify the file

Every release publishes `SHA256SUMS.txt`. If the value computed on your PC matches, the file is exactly the one GitHub Actions built from this repository's source code.

1. Open your Downloads folder, right-click an empty area → **Open in Terminal** (or PowerShell).
2. Run:
   ```powershell
   Get-FileHash .\FS25_RealFFB_Setup_0.6.0.exe -Algorithm SHA256
   ```
3. Compare the `Hash` with the line in `SHA256SUMS.txt`. Upper or lower case does not matter.
   - **Same** → safe to run.
   - **Different** → do not run it. Delete the file and download it again from the official page.

Without PowerShell, run `certutil -hashfile FS25_RealFFB_Setup_0.6.0.exe SHA256` in Command Prompt instead.

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
| 3. Wheel | lists the supported wheels that are connected | no |
| 4. HidHide | if missing or outdated, downloads the official installer from `github.com/nefarius/HidHide`, checks Nefarius' digital signature and the SHA-256 published by GitHub, and runs it unmodified (UAC) | installs the driver |
| 5. Devices | shows exactly what will change in HidHide and applies it only when you click **Apply** | allows RealFFB.exe, hides only the chosen wheel, adjusts `game.xml` (with a backup) |
| 6. RealFFB | copies the app, the helper and the mod | files |
| 7. Final check | checks files, mod, game.xml, HidHide and the wheel | no |

- **If a restart is needed** after HidHide, the installer tells you, and step 5 opens by itself after the restart.
- **If the wheel is not connected**, you can still finish. Later, plug it in and use **Start → FS25 RealFFB - Configure devices**.

## 4. First use

1. Open **FS25 RealFFB** (desktop shortcut). The app starts FS25 through Steam.
2. In the game, under *Mods*, enable **Real FFB Telemetry** for your savegame.
3. In the app, check steering and pedals (use the *Detect* buttons) and tune the force.
4. Logitech: turn off the centering spring in G HUB.

## Updating

When a new version is out, a yellow button appears in the app. You can also download the new installer and run it over the old one. Your settings (`%APPDATA%\FS25 RealFFB`) and the HidHide setup are kept.

## Uninstalling

Go to **Settings → Apps → FS25 RealFFB → Uninstall**.
- `game.xml` and HidHide are restored to how they were.
- HidHide itself stays installed because other programs may use it. Remove it separately if you want.
- At the start, the uninstaller asks whether to keep your settings.

---
© 2026 OptimusZuka. All rights reserved. See [LICENSE](../LICENSE).
