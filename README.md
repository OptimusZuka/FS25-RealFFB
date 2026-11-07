<p align="center">
  <img src="docs/banner.png" alt="FS25 RealFFB - real force feedback for Farming Simulator 25">
</p>

<p align="center">
  <a href="../../releases"><img src="https://img.shields.io/github/v/release/OptimusZuka/FS25-RealFFB?include_prereleases&label=version&color=FFCC00&labelColor=111111" alt="version"></a>
  <a href="../../releases"><img src="https://img.shields.io/github/downloads/OptimusZuka/FS25-RealFFB/total?label=downloads&color=FFCC00&labelColor=111111" alt="downloads"></a>
  <img src="https://img.shields.io/badge/Windows-10%20%7C%2011-FFCC00?labelColor=111111" alt="Windows 10 and 11">
  <img src="https://img.shields.io/badge/languages-EN%20%7C%20PT%20%7C%20ES%20%7C%20FR-FFCC00?labelColor=111111" alt="languages">
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-FFCC00?labelColor=111111" alt="MIT license"></a>
</p>

<p align="center">
  <a href="../../releases"><b>⬇ Download the installer</b></a> ·
  <a href="docs/INSTALL.md">Installation guide</a> ·
  <a href="docs/TROUBLESHOOTING.md">Troubleshooting</a>
</p>

---

## The problem

Farming Simulator 25 supports steering wheels, but its force feedback is almost non-existent. On a direct drive base like the Fanatec CSL DD the wheel feels loose. It has no weight, does not return to centre and gives no feel of the ground. Changing the game settings does not help, because the game simply does not generate those forces.

## The solution

**FS25 RealFFB** creates the force feedback outside the game. A small mod reads what the tractor is doing every frame. A Windows app turns that into real forces at your wheel, the way racing sims and Euro Truck Simulator do it.

<p align="center">
  <img src="docs/screenshot-app.png" alt="FS25 RealFFB running: live commands and forces" width="900">
  <br><sub>The app running: settings on the left, live commands and forces on the right.</sub>
</p>

## What you feel

| | |
|---|---|
| 🛞 **Realistic centering** | The wheel returns to centre only while moving, harder the faster and heavier you are. At a standstill it stays where you leave it, like a real tractor. |
| 🌾 **Terrain** | Tarmac, dirt, grass and fields each feel different. On the road you feel the hum of the agricultural tyres. |
| 🕳️ **Potholes and impacts** | Suspension bumps, potholes and collisions reach the wheel with the right strength. |
| 🚜 **Engine** | A vibration that follows engine RPM and load. |
| 🎚️ **Per-vehicle profiles** | Every tractor or machine keeps its own tuning, switched automatically when you change vehicle. |
| 🎮 **Other controllers untouched** | Button boxes, throttle quadrants and shifters keep working directly in the game. |
| 📌 **Pin on top** | A compact window that always stays visible, to watch commands and forces while you play. |
| 🌍 **4 languages** | English, Portuguese (Brazil), Spanish and French, in the app and in the installer. |
| 🔄 **Updates** | The app tells you when a new version is out and installs it only with your permission, after checking the file. |

## Supported wheels

| Brand | Models | Status |
|---|---|---|
| **Fanatec** (PC mode) | CSL DD, GT DD Pro, ClubSport DD, Podium DD1/DD2, ClubSport V2/V2.5, CSL Elite | ✅ tested on the **CSL DD 8 Nm** |
| **Logitech** | G29, G920, G923, G PRO, G27, G25 | 🧪 supported, not yet tested on real hardware |

For Logitech wheels, install **Logitech G HUB** and turn off the wheel's **centering spring** in it.

## Install in 4 steps

1. Open **[Releases](../../releases)** and download `FS25_RealFFB_Setup_x.y.z.exe`.
2. Run it and follow the 7 steps. The installer finds the game, the wheel and HidHide by itself.
3. Open the **FS25 RealFFB** shortcut. It starts the app and the game.
4. In the game, enable the **Real FFB Telemetry** mod in your savegame.

There is nothing to compile, and you do not need Python or Visual Studio. The installer:
- does not need administrator rights, except for HidHide, which is a driver;
- backs up everything it changes;
- leaves your PC as it was when you uninstall.

> **Windows warning:** the installer is not digitally signed yet.
> - **SmartScreen** may show a warning. Click "More info" → "Run anyway".
> - If **Smart App Control** is on, Windows blocks the installer. In that case download the `FS25_RealFFB_BuildOnPC_x.y.z.zip` from the same page and double-click `install.bat`.
>
> Every release publishes SHA-256 checksums so you can confirm the file is the original. Details: [docs/INSTALL.md](docs/INSTALL.md) and [docs/SIGNING.md](docs/SIGNING.md).

## How it works

<p align="center">
  <img src="docs/architecture.svg" alt="Architecture: mod in FS25, RealFFB.exe and the wheel" width="900">
</p>

1. **The mod** (`FS25_RealFFBTelemetry`) reads speed, mass, surface, each wheel's suspension, the engine and the steering angle. It sends them to the app through a local Windows pipe.
2. **RealFFB.exe** calculates the forces and sends them to the wheel through DirectInput in exclusive mode:
   - a centering spring that depends on speed;
   - damping and friction;
   - bumps and surface texture;
   - engine vibration.
3. **HidHide** ([Nefarius](https://github.com/nefarius/HidHide), free and open source) hides the wheel from the game.
   - Without it, FS25 grabs the base and blocks force feedback from any other program.
   - Only RealFFB.exe is allowed to see the wheel.
   - When the app closes, the wheel works normally in other games again.
4. **The command bridge** passes the steering and pedals read by RealFFB back to the game, through a small file the mod reads every frame.

The force never goes above the limit you set in "Maximum force".

## How it was made

FS25 RealFFB was born from frustration: an 8 Nm CSL DD that felt like a toy in Farming Simulator 25. It was built by **OptimusZuka** with the help of **Claude** (Anthropic), step by step, and tested lap after lap on the real wheel.

- **C# / .NET Framework 4.x**, compiled with the compiler that ships with Windows, so the app has no dependencies to install.
- **DirectInput 8** for the force feedback: spring, damper, friction, constant force and two sine waves (texture and engine).
- **Lua** for the FS25 mod.
- **HidHide** to isolate the wheel from the game.
- **Inno Setup** and **GitHub Actions** to build, test and publish every release automatically.

Along the way several approaches failed: the game's own FFB, Windows.Gaming.Input, and a DLL injected into the game, which Smart App Control blocked. The current design is the one that works without turning off any Windows protection.

## Project layout

```
src/RealFFB/         app: FFB engine, command bridge, profiles, updates, languages
src/RealFFB.Setup/   installer helper: detection, HidHide, game.xml, diagnostics
mod/                 FS25 Lua mod
installer/           Inno Setup script
tests/               automated tests (no hardware needed)
docs/                guides and images
```

To build, run `build.bat` (everything goes to `dist\`). To install from source, run `install.bat`.

## Help and feedback

- **Something not working?** In the app, click **"Create diagnostic report"** and attach the file to an [issue](../../issues). The report does not include your user name or PC name.
- **Tried a wheel that is not on the list?** Tell us how it went in an issue.

## License

[MIT](LICENSE). HidHide is a separate project by Nefarius Software Solutions with its own license. It is downloaded from its official repository and not redistributed here.

Farming Simulator is a trademark of GIANTS Software, Fanatec of Endor AG, and Logitech of Logitech International. This project is not affiliated with any of them.
