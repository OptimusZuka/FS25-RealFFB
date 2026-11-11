# FS25 RealFFB – Compatibility matrix

FS25 RealFFB is created by **OptimusZuka**. This page lists what is known about each wheel family. It never claims more than has been proven.

## Levels

| Level | Meaning |
|---|---|
| **C – Confirmed** | Tested on real hardware with FS25 RealFFB. |
| **D – Documented** | The device's force feedback goes through a documented interface (DirectInput / USB HID PID) that the app implements, **but nobody has tested it with FS25 RealFFB yet**. |
| **T – Theoretical** | Should work in principle; no documentation was checked and it was never tested. |
| **E – Experimental** | Works in simulation or partially. Use with care and report results. |
| **N – Not supported** | Known not to work, or outside the project's scope. |
| **U – Unknown** | Not in the catalog. The app still tries it through its detected capabilities, with conservative forces. |

Rules:
- A device moves to **C** only after a real report (guided test + "works in FS25") from someone who owns it.
- Automated tests with simulated devices (`build\FfbTests.exe`) **never** count as a hardware test.
- Detecting a device is not the same as supporting its force feedback.

## Matrix (version 0.13.0)

| Device | Level | Interface | Notes |
|---|---|---|---|
| Fanatec CSL DD, 8 Nm Boost Kit (0EB7:0020) | **C** | DirectInput, condition effects | Author's hardware. |
| Fanatec Gran Turismo DD Pro (same ID 0EB7:0020) | D | DirectInput | Same USB ID as the CSL DD, never tested. |
| Fanatec Podium DD1 / DD2, ClubSport V2/V2.5, CSL Elite, CSR Elite | D | DirectInput | Fanatec documents FFB as standard DirectInput. |
| Fanatec ClubSport DD / DD+ | D / U | DirectInput | No USB ID in the catalog yet: works through capabilities, peak torque must be set. |
| Logitech G29 | D | DirectInput (Logitech driver) | A user report exists in the project history but is not confirmed. |
| Logitech G920, G923, G27, G25, Driving Force GT, PRO Racing Wheel | D | DirectInput (Logitech driver) | G HUB centering spring must be off. |
| Logitech TRUEFORCE | **N** | Proprietary SDK (partners only) | Standard FFB still works. |
| Thrustmaster T128, T150, TMX, T248, T300 RS, TX, T500 RS, T-GT, TS-XW, TS-PC, T818 | D | DirectInput (Thrustmaster driver) | Peak torque per model not in the catalog: calibrate. T150: PS3 switch position on PC. |
| MOZA R3, R5, R9, R12, R16, R21 | D | DirectInput | Recognised by product name; set the peak torque. |
| MOZA via MOZA Racing SDK | T | MOZA SDK | The SDK has FFB functions; licence and benefit not evaluated. Not implemented. |
| Simucube 2 Sport / Pro / Ultimate | D | DirectInput | True Drive "DirectInput effect fine tuning" sliders can mute spring/damper: keep them up or use computed torque. |
| Simagic Alpha / Alpha Mini / Alpha Ultimate (0483:0522), EVO / Sport / Pro (3670:0500-0502) | D | DirectInput (HID PID) | Set the peak torque. |
| Asetek, Cammus, VRS and other HID PID direct-drive bases | D / U | DirectInput (HID PID) | Recognised by name where possible. |
| PXN | E | DirectInput | Supported effects vary per model. |
| AccuForce | T | DirectInput | Discontinued, never tested. |
| **Computed-torque output mode** (any wheel) | **E** | DirectInput constant force | Validated only in simulation. Needs a real test on the CSL DD. |

## How to help

1. Open the app → **Hardware** → **Test my wheel** (6 short steps, low forces).
2. Drive in FS25 for a few minutes.
3. **Export compatibility report**. The report is anonymous. Fill in "works_in_FS25" and attach it to a GitHub issue.

The data behind this table is `src/RealFFB/Devices/devices.ini`.
