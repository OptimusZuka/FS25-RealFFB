# Changelog

## [Unreleased] - 2026-11-13
- **Docs:** Logitech G29, Logitech G27 and MOZA R5 are now listed as **confirmed** (tested on real hardware) in the README and the compatibility matrix. Thank you to TheNobunaga for tirelessly testing the mod and for all the feedback.

## [0.15.0] - 2026-11-13 (beta)
Torque calibration safety, MOZA models, force feedback that no longer stays off unnoticed, dedicated servers, and a more realistic engine start. Found through a tester's MOZA R5 reports.

### What was wrong
- **MOZA bases were not identified by model.** The catalogue only knew the brand, so a MOZA R5 got the "unknown base" value (20 Nm) and ended at 21 Nm. The app then sent about a quarter of the intended force, and the guided test did not move the wheel.
- **Force feedback stayed OFF after the wheel test or the calibration.** Both pause force feedback and asked the user to switch it on again. A tester drove with FFB off (gain 0 %) without knowing why.
- **On a dedicated server the game reports every wheel "in the air".** Wheel physics runs on the server, so the client never gets ground contact. The app took that as airborne and nearly removed the centering. Network speed steps were also read as impacts several times per second.
- **Engine start: the same strong sequence for every vehicle.** Starter vibration, a strong jolt when the engine catches, then idle vibration were all intentional (0.12), but at tractor strength in trucks and pickups too, and the "catch" was a strong buzz at idle frequency.
- "Maximum force" capped each effect, but the engine vibration was not part of the total, so the sum could exceed it.

### Torque calibration and hardware limit
- MOZA models in the catalogue with the manufacturer's torque (mozaracing.com): R3 3.9, R5 5.5, R9 9, R12 12, R16 16, R21 21 Nm (R21 Ultra: sustained rating). The R5 is identified by its USB ID (from a tester's report); the others by the exact model token in the device name. The brand alone never identifies a model.
- **Hard ceiling:** for an identified model the configured peak torque can never be higher than the manufacturer's peak. Enforced in the slider (maximum), the calibration dialog (maximum and validation), configuration import and migration, and in the force calculation itself (the effective peak never exceeds the hardware value).
- A value above the hardware is corrected with an explanation (log and message), never silently. A lower value is accepted (limited bases, e.g. CSL DD without Boost Kit) with a warning.
- Unknown models: still assume 20 Nm, so forces start low, and the calibration dialog lets you choose your model from the catalogue. The chosen model then becomes that wheel's ceiling.
- Hardware page shows manufacturer, model and how it was identified, the manufacturer's peak, the configured peak and the maximum allowed.
- "Maximum force" is now a true ceiling for the sum of centering, friction, bumps, texture and engine. Vibrations keep at least 25 % of it.

### Force feedback state
- After the wheel test or the calibration, FFB returns to the state it had before, but only if the wheel is still open.
- Why FFB is off is always shown in the live panel and logged ("FFB off - wheel test finished...", "turned off with the FFB button", "STOP button"...).
- The log records telemetry lost and restored, entering and leaving vehicles, session type and every engine state change, with the engine force at that moment. Stats lines include the FFB state and reason.

### Dedicated servers (multiplayer client)
- The mod reports the session type (single player, host, client).
- On a client, missing wheel contact is treated as **unknown**, not "in the air": centering works with neutral grip. No ground contact or surface is invented, so terrain texture and speed-based impact detection are off there (documented limitation). Single player is unchanged.

### Engine start and engine feel
- New **Engine start** slider (starter motor and catch), separate from **Engine vibration**. Each can be set to 0 without affecting the rest of the force feedback.
- Engine feedback depends on the vehicle type (an approximation, because the game does not expose engine mounting): full in tractors (engine bolted to the chassis), less in large tractors, harvesters and loaders, about half in trucks (suspended cab), little in cars and pickups.
- The catch is now a short low-frequency rock of the engine on its mounts (about 10 Hz) before it settles into idle. Before, it was a strong buzz at idle frequency.
- The starter vibration stops after 6 s even if the game stays in "starting". Switching vehicles resets the engine state, so there is no false "catch" from the previous vehicle.

### Tests
- 67 new automated checks (simulated, no hardware): MOZA catalogue and identification, ceiling rejection, bypass attempts through configuration files and the force multiplier, final output magnitude, persistence of manual calibration, sum ceiling, engine start sequence for tractor and truck, repeated starts, vehicle switch, engine sliders, FFB activation/telemetry loss/vehicle exit transitions, dedicated-server parsing, centering and impacts.
- **Not tested on physical hardware:** MOZA bases (the R5 data comes from a tester's diagnostic report), the new engine start feel, dedicated-server play.

## [0.14.0] - 2026-11-11 (beta)
Automatic HidHide setup for every force feedback wheel, and a choice to remove HidHide when uninstalling.

**What was wrong:** the installer only recognised Fanatec and Logitech wheels (by USB vendor ID). With any other wheel, for example the MOZA R5 of a tester, it stopped with "no supported wheel" and changed nothing: RealFFB.exe was not allowed in HidHide and the wheel was not hidden, so FS25 and the app fought over the wheel and force feedback stopped after a moment. `game.xml` was not adjusted either.

### Installer
- RealFFB.exe is allowed in HidHide **always**, using the real install folder (custom folders too), even with no wheel connected. An old entry of our own from a previous folder is replaced.
- The wheel base is identified for **any brand**: the device that Windows (DirectInput) reports with force feedback, plus the known Fanatec/Logitech IDs. Pedals, shifters, handbrakes, stalks, button boxes, keyboards, mice and devices without force feedback are never selected. MOZA bases with a stalk or Farming Control System attached: only the base is hidden.
- Only the wheel's HID entries are hidden; its USB entry only when that USB device contains nothing else, and its XInput entry when there is one (official HidHide guidance for composite devices).
- Several wheels: you choose. None identified safely: you can pick from the list (with a warning) or cancel, and nothing is hidden.
- After applying, setup reads HidHide again and reports anything that was not kept.
- Idempotent: running setup again never duplicates entries, and other programs' entries are never removed.
- `game.xml` is adjusted even when no wheel is connected.
- The official HidHide installer gets a time limit (5 minutes silent): if it hangs, setup reports it instead of waiting forever.
- Reminder to unplug and replug the wheel after setup (Windows applies HidHide changes when the device reconnects).
- Silent installs (`/VERYSILENT`) never stop on a dialog.

### Uninstaller
- New question: **"Would you also like to uninstall HidHide?"** with **"Yes, uninstall HidHide"** / **"No, keep HidHide installed"** (default: keep). Yes asks for a second confirmation, explaining that other programs may depend on HidHide; it then switches device hiding off and runs HidHide's **official uninstaller**, checks the result and reports a restart if one is needed. If HidHide was already installed before FS25 RealFFB, the uninstaller says so.
- Keeping HidHide: only FS25 RealFFB's entries are removed; the wheel is no longer hidden; other programs' entries stay.
- The Windows start-up entry that un-hides the wheel is removed with the app.
- Fixed: the original `game.xml` values could be lost from the install state, so uninstalling did not restore them.

### Diagnostics
- New HidHide section: installed / driver / device hiding / FS25 RealFFB whitelist / steering wheel detected / selected for hiding / device access / configuration COMPLETE or INCOMPLETE, plus the result of the last setup.
- Separates **configuration problems** (use *Configure devices*) from **hardware or driver problems** (no force feedback wheel seen by Windows).
- Lists the controllers as seen by HidHide (marked when hidden) and by RealFFB.exe (marked when they have force feedback).

### Tests
- 59 automated installer tests with simulated devices (Fanatec, Logitech G920 with XInput, MOZA R5 with stalk and Farming Control System, Thrustmaster composite device, active pedals, Razer keypad).
- New automated install test on a clean Windows machine (GitHub Actions) with the real official HidHide: fresh install, install with HidHide already present and configured, repeated setup, other programs' entries preserved, diagnostics, uninstall keeping HidHide, official HidHide removal. No physical wheel is attached there.
- **Not tested on physical hardware yet:** the new detection with a real MOZA, Thrustmaster or other non-Fanatec wheel, and the uninstaller's interactive dialogs.

## [0.13.1] - 2026-11-11 (beta)
Small fixes after the 0.13.0 release. Forces unchanged.
- "Realistic centering" card: the explanation was cut off after two lines. The card is taller now.
- Live panel: "FORCE AT THE WHEEL (gain %)" was cut off in narrower windows. The title now shrinks to fit.
- Mod description corrected: it is not read-only (it passes the wheel and pedals to the vehicle you drive).
- Docs: the installer sets up HidHide automatically only for Fanatec and Logitech wheels; [INSTALL.md](docs/INSTALL.md#other-wheel-brands) explains the one-time manual setup for other brands. New screenshots of the 0.13 interface.

The highlights since v0.6.0 are in the 0.13.0 entry below.

## [0.13.0] - 2026-11-11 (beta)
### Highlights since v0.6.0
First public release since v0.6.0. It includes everything from 0.7 to 0.13:
- **Realistic centering (optional):** mechanical steering returns to centre; hydrostatic, articulated, tracked and lever-steered machines do not, just like the real ones. Per-vehicle Auto / On / Off.
- **Engine start feel:** the wheel reacts as soon as you turn the key, with starter-motor pulses and a jolt when the engine catches.
- **Wheel follows AI workers and GPS steering,** and turning the wheel against it hands the vehicle back to you.
- **Live vehicle view** with one colour per force source, impact flashes and a compact "Pin vehicle" window.
- **New "Hardware" page:** detected devices, guided wheel test, force calibration and an anonymous compatibility report.
- **Universal wheel architecture:** a data-driven device catalog with honest validation levels, and an experimental computed-torque mode for wheels whose spring/damper effects are missing.
- **Safety:** instant STOP, a watchdog that cuts every force if the app freezes, an oscillation guard, and a hard cap on the maximum force.
- Many force-feedback refinements measured on a Fanatec CSL DD 8 Nm (see the entries below).

Tested by the author on a **Fanatec CSL DD 8 Nm** with the default output mode. The computed-torque mode and every other wheel are untested on real hardware: see [the compatibility matrix](docs/dev/COMPATIBILITY-MATRIX.md).

### Universal wheel architecture
- **The physics no longer knows the device.** `FfbEngine` now returns a `ForceRequest` in Nm and 0..1 coefficients. The final mix (multiplier, gain, priority limit, cap, slew) moved to `Ffb/FfbMixer.cs`. The translation to the wheel moved to adapters. An automated test enforces this.
- **Adapters:**
  - `DiConditionAdapter`: the existing mode, migrated 1:1. Same maths, same values on the CSL DD.
  - `DiTorqueAdapter` (**experimental**): the app computes centering, damping and friction itself and sends only constant force at ~500 Hz on its own thread. It is for wheels whose spring/damper effects are missing or muted by the manufacturer's software.
- **Capabilities are read, not assumed:** EnumEffects, FF sample period, gain and autocenter properties, and each effect's real status (Declared / Created / Playing / Failed). In "Automatic" output mode, a wheel without working spring/damper uses computed torque.
- **Device catalog as data:** `Devices/devices.ini` holds model, peak torque, drive type, validation level, quirks, notes and source. It is embedded in the exe and can be extended with `%APPDATA%\FS25 RealFFB\devices.ini`. Logitech behaviour is now named quirks instead of `if (vid == Logitech)`. Product-name hints cover MOZA, Simucube, Cammus, Asetek, PXN and AccuForce.
- **Two identical bases:** the chosen wheel is remembered by its instance, not only by its USB ID.
- **New "Hardware" page:**
  - detected devices (wheel base / pedals / shifter / handbrake / gamepad, with or without FFB);
  - wheel in use, adapter, effect status, peak torque and where it comes from, validation level, catalog notes;
  - output mode: Automatic / Condition effects / Computed torque;
  - **Test my wheel:** 6 guided steps with low forces;
  - **Calibrate force:** peak torque and the manufacturer's FF %;
  - **Export compatibility report:** anonymous.
- **Manufacturer FF % calibration:** if the base's own FF is at 50 %, the app scales so that 1 Nm is still 1 Nm.

### Safety
- **Watchdog:** if the FFB loop (window thread) stops for more than 150 ms with FFB on, a separate thread stops every force.
- **Oscillation guard:** fast oscillation around the centre halves centering and raises damping for 2 s.
- Computed torque:
  - zero torque if the request is older than 150 ms or the wheel position is not read for 50 ms;
  - power limit: it does not push a wheel that is already spinning fast.
- **The centering "return assist" can no longer exceed the force cap.** Before, it could reach 1.8x the saturation.
- The wheel opens with constant force only. Spring/damper are no longer mandatory; the computed-torque mode takes over when they are missing.

### Tests and docs
- `build\FfbTests.exe` (built and run by `build.bat`): architecture, mixer limits, parity with 0.12, adapter contract against 5 simulated device profiles, watchdog, oscillation guard, computed-torque stability on a virtual wheel, catalog rules (only the author's CSL DD may be "Confirmed"), device classification, scaling. **Simulated, not hardware tests.**
- `docs/dev/COMPATIBILITY-MATRIX.md`, `ADDING-AN-ADAPTER.md`, `DEVICE-REPORT-FORMAT.md`.

## [0.12.0] - 2026-11-11 (beta, not compiled here)
### Realistic centering (new)
- New option at the top of the first page: "Realistic centering for vehicles" (off by default).
- In real life only vehicles with mechanical steering (cars, pickups, trucks) bring the wheel back to the center. Hydrostatic steering (orbitrol: practically every modern tractor, combines, harvesters, self-propelled sprayers and mowers, telehandlers, front loaders, forklifts), articulated steering (wheel loaders, articulated tractors, forwarders), tracks and levers do not. With the option on, those vehicles:
  - get no centering spring: let go and the wheel stays where it is;
  - keep the viscous weight of the orbitrol at any speed (the wheel is never loose);
  - pass almost no steering kickback from bumps (only strong impacts that shake the whole cab).
- Engine off (option on): no steering pump. Hydrostatic steering becomes ~3x heavier (the orbitrol works as a hand pump), mechanical steering ~2x (no power assist).
- A card shows, for the vehicle you are in: whether it centers in real life (green tick / yellow cross), why, in one simple sentence, and what your wheel is doing now. The live panel says "OFF: no centering" instead of showing an empty bar.
- Per vehicle: "Centering for this vehicle" = Automatic (by vehicle type), On (always) or Off (never). Saved in %APPDATA%\FS25 RealFFB\centragem.ini, independent of the vehicle profiles. Works for mod vehicles.
- Changes are smooth (~0.5 s) when you switch vehicle or option.
- Mod 0.12.0.0: also sends the store category, articulated steering and tracks (game log: `[RealFFB] direcao: ...`). With the old mod the app uses the vehicle type only.

### Engine start
- **The wheel reacts the moment you turn the key.** FS25 only reports the engine as running, with RPM, at the end of its start sequence (~2-3 s, the starter sound; the dashboard rev counter waits too). Until now the wheel stayed silent all that time. The mod now sends the motor state (off / ignition / starting / running) and the app plays the starter motor straight away: irregular shakes at ~9-12 Hz, pulsing with each compression, then a short jolt when the engine catches, settling into idle. Uses the "Engine vibration" slider.
- Right after the engine catches, if the game has not sent RPM yet, idle is assumed (no gap).
- Game log shows `[RealFFB] motor: ...` once per vehicle (what the game exposes), useful if a vehicle does not crank.

### Fixes from the code audit
- **STOP / Esc is now instant.** Before, after STOP the loop restarted the effects straight away and the centering spring could only drop ~2.4 Nm/s, so it took up to ~1 s to let go. Now every force goes to zero at once and nothing is sent while FFB is off.
- Installing an update: forces are stopped first (the download freezes the window for a few seconds and the last force stayed on the wheel).
- Diagnostic report: runs in the background (the app used to wait up to 30 s with the force stuck).
- Turning "Manage HidHide automatically" on/off: runs in the background (up to 15 s frozen before).
- Update check finishing after the window closed could show an error window. Fixed.
- The log box and RealFFB_log.txt no longer grow forever: the box keeps the last ~60,000 characters; the file rolls over to RealFFB_log.old.txt at 5 MB.

## [0.11.2] - 2026-11-09 (beta, not compiled here)
- **The app no longer hands the tractor back by itself in sharp AI turns.** The 2026-11-09 trace showed the wheel trailing the AI's steering by up to about 0.5 of the axis in headland turns (the wheel moved at about 0.45 of the axis per second, the AI's steering at up to 1.0), and 0.11.1 read that lag as the driver holding the wheel. Now a takeover only counts when the wheel is **not moving towards** the target: held still, or turned the other way. Replayed on the trace: the false takeover is gone and the two real ones (wheel turned against the AI) are still detected.
- The wheel keeps up better: the app's own damping while following is lower (it was holding the wheel back) and the follow force goes up to 2.5 Nm (never above "Maximum force").
- Mod unchanged (0.11.1.0).

## [0.11.1] - 2026-11-09 (beta, not compiled here)
- **Taking control now gives you the tractor back.** In 0.11.0, holding or turning the wheel against an AI worker only stopped the wheel from following for 3 s, and the worker kept driving (log of 2026-11-09: 19 takeovers in 3 minutes, the AI never stopped). Now the app asks the mod to hand the vehicle over: the AI worker is dismissed (same as the dismiss key; Courseplay too, AutoDrive is stopped) and the GPS steering is switched off. The wheel stops following at once and steering and pedals are yours again. The game's log records `[RealFFB] condutor assumiu: ...`.
- Mod 0.11.1.0 reads `o=` (takeover) from the command file.

## [0.11.0] - 2026-11-09 (beta, not compiled here)
**The wheel follows AI workers and the GPS steering.** No change to the forces while you drive yourself.
- When an AI worker (also Courseplay/AutoDrive) or the game's GPS steering assist drives, the wheel turns by itself to the game's steering angle: a spring centred on the target, at most 2 Nm (and never above "Maximum force"), no friction. Bumps, surface and engine stay on. The target moves at most 1.5 of the axis per second, and the force fades in with the usual 2.4 Nm/s limit, so there is no jolt when it starts.
- **Take control:** hold or turn the wheel firmly away from where it is going (about 80 degrees for half a second). With the GPS, your steering goes back to the game and the game switches the GPS off by itself. With an AI worker, the wheel stops following for 3 s.
- While the wheel follows, the mod does not send the wheel's steering to the game (it would only be a copy, and large steering changes switch the GPS off). With an AI worker it does not send the pedals either; with the GPS the pedals stay with you.
- **Option:** "Wheel follows AI workers and GPS steering" on the "Steering and pedals" page (on by default; `FollowAuto` in RealFFB.ini). Off = exactly the 0.10.0 behaviour.
- Mod 0.11.0.0: sends `auto=` (0 driver, 1 AI worker, 2 GPS) and reads `f=` from the command file. The app log records each change ("Auto steering: ... | wheel follows: ...").
- Steering sign taken from the 2026-11-09 trace: the game's steering is always minus the steering sent (36,915 samples, none with the same sign).

## [0.10.0] - 2026-11-09 (beta, not compiled here)
Fixes from the 2026-11-09 audit. **No change to the steering forces** of the Fanatec CSL DD or the Logitech G29.
- **The wheel no longer stays hidden after the app closes.** The 2026-11-08 log showed "show the wheel again" failing twice when Windows was shutting down (error 0xC0000142), so the wheel stayed hidden from every other program. Now: the app retries up to 3 times; it also releases the wheel when Windows starts shutting down; and, with automatic HidHide management on, a small `RealFFB.exe --unhide` runs at Windows start-up and shows the wheel again if the app is not open (covers forced closes and power cuts). Registered per user in `HKCU\...\Run`, no administrator.
- **Unknown wheels start with low forces.** An unknown wheel was treated as 3 Nm, so on a 15-25 Nm base the app could ask for 100% of the motor. It is now treated as a 20 Nm base (forces start low) and the log asks you to set the real peak torque.
- **Wheel catalogue** (`WheelCatalog.cs`): each known wheel has its drive type. Belt-driven wheels (Fanatec ClubSport V2/V2.5, CSL Elite, CSR Elite, Thrustmaster) use the profile with internal friction instead of the CSL DD model; the Logitech G PRO (direct drive) no longer gets the gear profile and its 0.4 Nm spring boost. CSL DD and G29 map exactly as before. The new profiles are not tested.
- **The wheel can no longer die silently.** If a recovery failed (e.g. another program holding the wheel), the effects were gone and nothing tried again, while the header still said "Wheel connected". The app now retries every 2 s and the header shows "Wheel not responding".
- **HidHide calls can no longer freeze the app:** the 5-second limit now really works (the CLI output is read asynchronously).
- **Diagnostic trace** stops by itself after 30 minutes or 50 MB, and its time column now has millisecond resolution.
- **G29 user report: "Open FS25" button hidden** in a narrow window. The status chips sat on top of the buttons; they now shrink or hide, and the buttons always stay on top and visible.
- **G29 user report: ~3 s before the wheel starts vibrating after entering a vehicle.** The app faded the forces in over 1 s; now 0.3 s (the centering spring keeps its own 2.4 Nm/s limit, so there is no jolt). The rest of the wait is most likely FS25 starting the engine: the engine vibration only starts when the game reports the engine running. The diagnostic trace now records `active;engineOn;rpm;gain;engineMag` so the next report can be measured.
- **Vehicle view brighter:** body, glass, tyres, glows and ground lines are lighter and easier to see.
- Code: `MainForm.cs` split into `MainForm.Pin.cs` (pin modes, help) and `MainForm.Trace.cs` (trace), same code; unused path-calibration code removed from the force engine (it never changed the forces); outdated comments fixed; shutdown runs once; failed wheel opens release the device.

## [0.9.10] - 2026-11-08 (beta, not compiled here)
- **"Pin vehicle":** new button next to "Pin on top". Small window (380 x 330, resizable) always on top of the game with only the vehicle drawing and the force bars (steering, suspension, surface, engine) and impacts. An x in the drawing's corner goes back to the normal window. "Pin on top" (compact mode with the live panel) is unchanged.
- **Help "?" is easy to close:** it now opens a small panel with an x; clicking the "?" again, pressing Esc or clicking anywhere else also closes it.

## [0.9.9] - 2026-11-08 (beta)
- **Braking no longer counts as an impact (drawing and wheel).** 0.9.8 still looked at a single telemetry sample, so the moment the game snaps the speed to zero at the end of a stop, or noise in the speed, fired the impact. An impact now needs the vehicle to lose more than 7 km/h within about 0.1 s (more than 9 km/h while braking); even a full-brake stop stays far below that. The history restarts on vehicle change. Every impact is written to the log with the speed lost and the brake position, so false positives can be checked.
- **"?" button on the vehicle view:** hover or click to see what each colour, the bars beside the wheels and the bottom legend mean.
- **Nicer drawing:** background gradient, ground lines that move with your speed, blue glow on steered wheels, legend on its own strip with thicker bars.

## [0.9.8] - 2026-11-08 (beta, not compiled here)
- **Vehicle view, one colour per force source,** with a legend and live level bars at the bottom: steering/centering = blue (outline of the steered wheels), suspension/bumps = orange (a bar beside each wheel shows how hard that wheel is hit), surface texture = green (glow under wheels touching the ground), engine = red (engine area). Impacts = red-white flash with an "IMPACT" label at the hit point.
- **Hard braking no longer shows as an impact:** a collision now needs a sudden jump in deceleration (more than 6 m/s2 between frames) above 9 m/s2, or above 16 m/s2 while the brake pedal is pressed. Steering, damping, terrain and engine forces unchanged.
- **Smaller window without losing information:** click the active section in the left menu to fold the settings column away (click any section to bring it back); the right column (vehicle view, readouts, log) scrolls instead of being cut off.

## [0.9.7] - 2026-11-08 (beta, not yet tested on Logitech)
- Logitech: half the dry friction while driving (the wheel stuck less and small forces get through). Everything else of 0.9.x that is not specific to direct drive already applies to Logitech wheels: new layout, live vehicle view with per-wheel glow and impact flash, collision location, faster telemetry bridge, no torque jumps on vehicle change, diagnostic trace. The direct-drive steering model (no moving friction, reliance on the base's Natural Damper, shaped centering, return assist, arrival damping) stays direct-drive only: it depends on effects a G29 does not have.

## [0.9.6] - 2026-11-08 (beta, test build)
- **Collisions:** hitting a tree, a wall or another vehicle (a sudden deceleration above about 7 m/s2, already detected before) now shows an impact flash on the drawing at the point of the vehicle that was hit (front, rear, left or right side). On direct drive the impact kick is 50% stronger (Logitech unchanged). Steering, damping, terrain and engine forces are not changed.
- **More detailed drawings:** shading, ground shadow, tyre tread; tractor with front weights, bonnet grille, exhaust, rear fenders, glazed cab and hitch; combine with cutter bar and auger, glazed cab, grain tank and folded unloading pipe; truck with mirrors and a ribbed body; car with windows and roof.

## [0.9.5] - 2026-11-08 (beta, test build)
- **Live vehicle view, seen from above,** with a drawing per category: tractor, combine/harvester, truck, car/pickup, other. Each wheel is drawn at its real position on the vehicle (sent by the mod), so you can see which wheel the force comes from: front or rear, left or right. Each wheel lights up with its own suspension bumps; wheels touching the ground with the surface texture; steered wheels with the centering force (and they turn with the steering); the engine area with the engine vibration.
- Mod: sends each wheel's position on the vehicle and the vehicle type. The app still works with the previous mod (wheels then drawn in list order).

## [0.9.4] - 2026-11-08 (beta, test build)
- Direct drive: **brake on arrival at the centre.** The 0.9.3 trace showed the wheel passing the centre by about 57 degrees (median) before stopping. Extra damping now acts only in the last ~25 degrees and only when the wheel arrives fast (over ~80 degrees/s), so turning and holding a curve are not heavier.
- **New layout "Cockpit":** side menu (Force feedback, Steering and pedals, Vehicle profile, Buttons, System), one page per section, Save and Restore always visible at the bottom, compact header with wheel/telemetry/bridge status, new neutral graphite palette with yellow only as accent, monospaced numbers.
- **Live tractor:** a drawing in the live panel lights up where the forces are: steering wheel and front wheel (centering), chassis and axles (bumps), tyres at the ground (texture), bonnet (engine).

## [0.9.3] - 2026-11-08 (beta, test build)
- Direct drive: the return assist now fades out near the centre (none below about 11 degrees, full above about 65 degrees on a 1080-degree wheel). Measured in the 0.9.2 trace: returns from about 100 degrees took 0.4-0.9 s, but the wheel reached its top speed (300-600 degrees/s) 3-36 degrees from the centre and overshot by 20-80 degrees: the extra pull felt at the end of the return.

## [0.9.2] - 2026-11-08 (beta, test build)
- Direct drive: **return assist.** In the 0.9.0 test the wheel returned too slowly: the base's factory Natural Damper (NDP 50%) brakes the wheel in proportion to its speed. When the wheel is already moving towards the centre, the centering force now rises up to 1.8x, also in proportion to that speed, cancelling the NDP brake only on the return. Holding a curve or turning away: normal force, so turning does not get heavier. Past the centre the assist switches off and the spring and NDP brake the wheel.

## [0.9.1] - 2026-11-08 (beta, test build)
- Fixed: the diagnostic trace never started (integer overflow in the 2-second check). No change to the forces.

## [0.9.0] - 2026-11-08 (beta, test build)
Direct-drive profile built from the 2026-11-08 audit and tests on a Fanatec CSL DD 8 Nm (0.8.x travel-based return dropped: too slow at speed). Logitech unchanged.
- **Works with the base's factory settings.** Tests showed the weight and the slow return came from resistance stacked twice: the base's Natural Damper (NDP 50% from factory) plus the app's damping and friction. While driving the app now adds only 30% of its damping and no dry friction (friction also made the wheel stick and release). Standstill weight unchanged.
- **Stronger, more progressive centering:** maximum about 1.0 Nm at 15+ km/h (0.7.6: 0.9), enough to overcome the factory NDP; full force at about 43 degrees instead of 22, so the return slows down gradually near the centre instead of feeling like a rubber band.
- No more artificial difference between turning and returning (0.7.4-0.7.6 used 85% when turning).
- **Safety:** the centering force changes by at most about 2.4 Nm/s, so a vehicle change, telemetry loss or start-up never produces a torque jump.
- **Diagnostic trace:** create an empty file `trace.on` in the app's data folder and the app records every cycle (about 60 per second) to `RealFFB_trace.csv`: speed, wheel position, steering sent and applied by the game, and every effect. Delete the file to stop. Off by default, no cost.
- Settings reset once to the recommended values (the model changed). Vehicle profiles are kept; use "Restore default settings" to reset them too.
- New docs/FANATEC.md: required and recommended base settings.

## [0.7.6] - 2026-11-08 (beta)
Direct drive, based on the 0.7.5 test log (most driving at 4-15 km/h, wheel often at full rotation):
- **Standstill weight now fades by about 4 km/h** (was about 11 km/h). At 4-7 km/h damping + friction were 0.33-0.51 Nm, as much as or more than the centering force: the wheel was heavy to turn, stopped halfway when returning, and the friction swallowed the ground texture. Now about 0.16 Nm there. Still the same weight at 0-1 km/h.
- **Centering arrives earlier:** full at about 12 km/h (was about 18). Net return force (centering minus resistance) at 4 km/h: 0.26 Nm (was negative: the wheel stalled); at 7 km/h: 0.46 (was 0.20).
- **Ground texture 30% stronger,** and no longer absorbed by friction at low speed.
- Less base friction while moving (0.15 instead of 0.25 of the slider).
- Logitech unchanged.

## [0.7.5] - 2026-11-08 (beta)
Based on the app log of the 0.7.3/0.7.4 test drives:
- **Removed the stiffness-proportional damping added in 0.7.2-0.7.4.** The log showed the damper at 21-42% while driving (normal: 3-6%). A viscous damper that strong slows every wheel movement: it muffled the ground texture, added weight when turning and slowed the return. Damping is again only the "Damping" slider.
- **Turning away from centre at 85%** (0.7.4: 65%, which took too much feel away). Maximum centering about 0.88 Nm at 20 km/h (0.7.3: 1.24, 0.7.4: 0.91). Shaped curve kept: strong near the centre.
- Log finding: the wheel is used up to 90-94% of its axis (about 400-500 degrees each way on a 900-1080 degree wheel), because Steering sensitivity is 100%. Returning from there takes time with any spring; a higher Steering sensitivity shortens the travel.

## [0.7.4] - 2026-11-08 (beta)
- **Light to turn, firm to return (direct drive).** 0.7.3 returned well but felt heavy when steering. The centering force now depends on the direction the wheel moves: turning away from the centre it is at 65%, returning (hands off or straightening) at 100%, with a smooth 80 ms transition. At 20 km/h and 45 degrees, turning now needs about 0.48 Nm (0.7.3: 0.91).
- The force near the centre stays about the same as 0.7.3 (20 km/h, 10 degrees: 0.37 Nm returning) because the shaped curve is steeper; the maximum force is lower (0.74 Nm at 20 km/h, was 1.0).
- Proportional damping lowered a little (it also added weight when turning).

## [0.7.3] - 2026-11-08 (beta)
- **Centering (direct drive), the real cause.** The DirectInput spring has a maximum stiffness of one force unit per axis unit. On a 900-1080 degree wheel that gives only about 0.2 Nm at 20 degrees from centre, whatever maximum force is configured, which is why 0.7.2 changed nothing. The app now moves the spring's centre every cycle so that, at the current wheel position, the spring produces the wanted force (full force at about 25-35 degrees from centre). The moved centre always stays on the real centre's side, so the force always points to the centre. Measured: about 2x more force near the centre (10 degrees at 20 km/h: 0.39 Nm, was 0.14).
- Maximum centering force raised (about 1.0 Nm at 20 km/h, was 0.6), with damping proportional to the stiffness so the wheel stops at the centre without oscillating.
- App log: [stats] now shows spring stiffness, damping, spring centre and wheel position.
- Measured in the app log: telemetry now at 45-61 Hz (the 0.7.2 bridge fix).

## [0.7.2] - 2026-11-08 (beta)
- **Centering (direct drive) fixed.** The spring stiffness only reached full force with about 80 degrees of wheel off centre (at 10 km/h), so over the last 30-40 degrees there was almost no force and the wheel stalled halfway: the return was slow and the tractor's path unpredictable. Full force now arrives at about 35-45 degrees and the force near the centre is about 2.5x stronger. The maximum weight in long turns is unchanged. A small damping proportional to the stiffness stops the wheel at the centre without overshooting. Logitech unchanged.
- **Command bridge without sharing violations.** The game log showed about 25% of the game's reads of the steering file failing ("Sharing violation", about 10 errors per second written to log.txt), because the file was being replaced while the game opened it. The file is now rewritten in place with a fixed size, and a value is retried if a write fails.
- Measured in the game log: the mod sends 41-60 times per second, the same as the game's frame rate. Telemetry was not limited at the mod.

## [0.7.1] - 2026-11-08 (beta)
- **Command bridge off the force feedback loop.** The steering/pedal file for the game was rewritten up to 120 times per second inside the FFB loop; each rewrite costs several ms, more with the antivirus scanning every new file, and the game read the file while it was being replaced. It is now written on its own thread, only when the values change (at most about 60 times per second, the game's frame rate) plus a heartbeat every 200 ms. Likely cause of the ~20 Hz telemetry; the mod's rate log in log.txt confirms it or not.
- **Standstill weight back up a little:** direct drive about 0.42 Nm (0.7.0: 0.37), Logitech about 0.41 Nm. Mostly viscous (damping), so the engine vibration at a standstill stays clear.

## [0.7.0] - 2026-11-08 (beta)
Force feedback review aimed at a natural, Euro Truck Simulator-like feel: light steering, a clean return to centre, and effects that do not pile up.
- **Less dry friction.** Friction was what made the steering sticky and heavy, and it swallowed small forces (the last part of the return to centre, the texture). It is now small and disappears at cruising speed. Direct drive at 10 km/h: 0.05 Nm (0.6.5: about 0.12).
- **Lighter standstill.** Direct drive about 0.37 Nm in total (0.6.5: about 0.5); Logitech about 0.37 Nm (0.6.5: about 0.48). Still the same at 0 and 1 km/h.
- **Effects are mixed, not stacked.** During a bump the texture and the engine drop to half, so the bump reads clean. On a vibrating surface the engine steps back (about 40% on rough ground). Impacts oscillate less, so they overlap less with the texture.
- **Priority limiter.** Steering (centering and friction) comes first and is never reduced by a bump; bumps and texture use the remaining headroom. Before, everything was scaled down together, so a bump in a turn also took away centering.
- **Settings reset to the recommended values** once, because the model changed (force multiplier back to 100%). Vehicle profiles are kept; use "Restore default settings" to reset them too.
- **Mod:** logs game frames per second and sends per second in the game's log.txt, to find the cause of the ~20 Hz telemetry rate.

## [0.6.6] - 2026-11-08 (beta)
- **New: Set as default.** The current settings become the default, used by every vehicle without its own profile.
- **New: Save for all vehicles.** The current settings apply to every vehicle; existing vehicle profiles are moved to a backup folder, not deleted.
- Fixed: with per-vehicle profiles on and no vehicle in the game, changes to the default were not saved (an old copy was written instead).

## [0.6.5] - 2026-11-08 (beta)
Review of the whole force model, so the effects work together instead of piling up:
- **Steering weight lighter (both wheels).** A real tractor's hydraulic steering is light and viscous, with no centering at a standstill. Direct drive: about 0.25 Nm friction + 0.25 Nm damping when stopped (0.6.4 had about 0.4 + 0.4). Logitech: about 0.14 + 0.34 Nm. The weight is the same at 0 and 1 km/h and fades by about 11 km/h.
- **Centering (direct drive):** keeps the stiffer, quicker return of 0.6.4 but with a lower maximum force, so it returns fast without feeling heavy in long turns.
- **Engine:** stronger at a standstill (1.3x) and softer while driving (down to 0.4x from about 14 km/h), on both wheels.
- **Force multiplier scales everything again**, engine included: it is the single control to make all effects lighter or stronger in proportion.
- **New: Restore default settings.** Puts every force slider and the multiplier back to the recommended values for the detected wheel. Vehicle profiles are moved to a backup folder, not deleted.
- **Tooltips:** hovering over a setting explains what it does (EN, PT, ES, FR).
- **Pin on top:** the window re-asserts its on-top position every half second without taking focus from the game. FS25 must run in borderless or windowed mode; in exclusive fullscreen Windows always draws the game above every window.
- Removed the "Use the wheel's 2nd interface" option: it was not used by the app.

## [0.6.4] - 2026-11-08 (beta)
- **Standstill weight (all wheels):** new hydraulic-steering model. Stopped or crawling, the wheel does not centre but has viscous weight (damper) plus tyre scrub (friction). It is the same at 0 and 1 km/h and fades smoothly by about 11 km/h, so stopping no longer feels like the tractor was switched off. Controlled by the "Standstill weight" slider.
  - Direct drive: about 0.4 Nm friction + 0.4 Nm damping at a standstill (was about 0.12 + 0.07 Nm).
  - Logitech: the weight comes mostly from damping (friction lowered), because strong friction absorbed the engine vibration in the gears.
- **Centering in motion (direct drive):** stiffer and reaches full strength earlier (about 60% of the reference speed), so the wheel returns in about a second. Logitech keeps the 0.6.2 curve.
- **Logitech terrain:** bumps and texture at 40% of the previous strength (they reached almost the G29's maximum torque).
- **Logitech engine:** higher base (about 0.45 Nm at idle with the slider at 50%), because smaller forces are lost in the gears.
- The force multiplier no longer scales the engine vibration; it has its own slider.

## [0.6.3] - 2026-11-08 (beta)
All wheels (Fanatec direct drive included):
- **Engine:** the vibration is now felt at a standstill on a direct drive base (about 0.15 Nm at idle with the slider at 50%, previously about 0.05 Nm). It follows the RPM continuously (6-cylinder firing frequency, about 42 Hz at idle up to 110 Hz at 2200 rpm), grows with RPM and load, and gives a short shake when you rev the engine. RPM is smoothed because telemetry arrives at about 20 Hz.
- **Bumps:** smoothed on direct drive too (12 ms rise, 30 ms fall), so impacts no longer arrive as digital steps. Strength unchanged.
- **Terrain texture:** amplitude and frequency vary randomly like a real surface (dirt and fields very irregular, tarmac almost constant), and the ground is felt from walking pace instead of only from about 15 km/h.
- Logitech: engine amplitude recalibrated to the new model (similar to 0.6.2).

## [0.6.2] - 2026-11-08 (beta)
See the Logitech gear-driven profile below (tested on a G29 by a user).

## [0.6.1] - 2026-11-08 (beta)
- **HidHide installation fixed:** the installer could not find the HidHide installer in GitHub's answer. GitHub's release data has nested objects that the old reader could not handle, so it always reported "not found" and HidHide was not installed. Without HidHide, FS25 takes the wheel and only its own centering is felt; this is what happened on a Logitech G29. The answer is now read with a real JSON parser.
- HidHide download:
  - a connection or GitHub API failure is reported as such (network, API error, rate limit), with a Retry option;
  - the file size and the SHA-256 published by GitHub are checked before the signature check;
  - MSI installers are supported, should HidHide switch to them.
- Force feedback diagnostics in the log:
  - wheel capabilities, and whether each effect was created and is playing;
  - effects the driver rejects, and send success/failure counts in every `[stats]` line;
  - a clear warning when HidHide is missing, also shown in the app's "Wheel" section.
- Logitech: force feedback is cleared with STOPALL instead of RESET (RESET turns the driver's centering spring back on), and the driver's own centering spring is turned off again after the force feedback reset, while RealFFB holds the wheel (Logitech only; Fanatec unchanged).
- Recovery after another program took the wheel now releases and re-acquires it properly. Before, `CONFLICT` could repeat forever, even after closing FS25, and the force never came back until RealFFB was restarted.
- **Weaker wheels feel right now:** the centering spring stiffness was relative to each wheel's maximum torque, so on a 2.2 Nm G29 it was much softer than intended. It is now scaled by the square root of the torque ratio, which gives 1.9× on a G29. Damping is unchanged, and on the CSL DD (8 Nm) nothing changes.
- New "Force multiplier" slider (25–400 %, starts at 100 %): scales all forces at once.
- License changed from MIT to **all rights reserved** (see `LICENSE` and `NOTICE.md`). Versions up to v0.6.0 remain under the MIT License for the copies already published.
- Copyright notices added to the source files, mod, documentation and diagram. No functional changes.

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

---
© 2026 OptimusZuka. All rights reserved. See [LICENSE](LICENSE).
