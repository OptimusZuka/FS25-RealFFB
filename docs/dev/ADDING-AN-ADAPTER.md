# FS25 RealFFB – Architecture and how to add a device or an adapter

FS25 RealFFB is created by **OptimusZuka**.

## Layers

```
FS25 mod (Lua telemetry)
   │  named pipe
TelemetryServer ─► FfbEngine (physics)            src/RealFFB/FfbEngine.cs
                     │  ForceRequest (Nm, 0..1 coefficients – no device units)
                   FfbMixer (universal mix)        src/RealFFB/Ffb/FfbMixer.cs
                     │  multiplier, gain, priority limit, cap, slew
                   OscillationGuard + Watchdog     src/RealFFB/Ffb/Safety.cs
                     │
                   IFfbAdapter                     src/RealFFB/Devices/WheelContracts.cs
                     ├─ DiConditionAdapter         src/RealFFB/Devices/DirectInput/DiConditionAdapter.cs
                     └─ DiTorqueAdapter (exp.)     src/RealFFB/Devices/DirectInput/DiTorqueAdapter.cs
                     │  effect commands
                   IEffectSink                     FfbDevice (real DirectInput) / SimulatedEffectDevice (tests)
                     │
                   Wheel base
```

- The physics never sees DirectInput types or vendor names. The automated test `PhysicsIsDeviceIndependent` enforces it.
- Device-specific behaviour has three homes:
  1. **Data** – `src/RealFFB/Devices/devices.ini`: model, peak torque, drive type, validation level, quirks, notes, source.
  2. **Quirks** – small, named behaviours applied by `FfbDevice` (`StopAllOnOpen`, `AutocenterOffEachAcquire`, `SpringFloor1`, `SpringBoost=n`).
  3. **Adapters** – only when a device needs a different way of producing force.

## Add a device (most cases: no code)

1. Find the USB vendor and product ID: Hardware page, or the app log line `[ffb] device VVVV:PPPP`.
2. Add a section to `devices.ini`:
   ```ini
   [VVVV:PPPP]
   Model=Brand Model
   PeakNm=10
   Drive=DirectDrive
   Validation=Documented
   Source=where the information comes from
   ```
   Testers can put the same text in `%APPDATA%\FS25 RealFFB\devices.ini` without rebuilding.
3. Use `Validation=Confirmed` **only** with a real hardware report. A test fails if anything other than the author's CSL DD is marked Confirmed. Update the test when a new device is genuinely confirmed.
4. Run `build.bat` (the tests run automatically).

## Add a quirk

1. Add the flag to `DeviceQuirks` (`DeviceCatalog.cs`), with a comment explaining which device needs it and why.
2. Apply it in `FfbDevice` where the behaviour happens. Never use `if (vid == ...)`.
3. Add a test with `SimulatedEffectDevice`.

## Add an adapter (a different interface, e.g. a vendor SDK)

1. Create `src/RealFFB/Devices/<Vendor>/` with:
   - a class implementing `IFfbAdapter` (`Name`, `Version`, `Level`, `Apply(ForceRequest, wheelPos, dt, Settings)`, `Stop`, `Dispose`);
   - if the vendor talks to the device itself, an `IEffectSink`-like device class, or have the adapter own its device;
   - a `README.md` with the exact API used, its version, links to the vendor's documentation and its licence. **Do not invent APIs.** Check the redistribution licence before shipping any vendor DLL.
2. Contract requirements, checked by `tests/Ffb/FfbTests.cs`:
   - `Apply` never throws and never sends a magnitude above `Settings.ToMag(Settings.ForceCapNm)`;
   - `Stop` stops every force at once and no command is sent after it;
   - a disconnected device receives nothing;
   - a device without force feedback is never opened.
3. Register it in `MainForm.ChooseAdapter`, behind an explicit user choice, until it is validated.
4. Add it to `COMPATIBILITY-MATRIX.md` at level E or D, never C.

## Simulated devices

`src/RealFFB/Devices/Virtual/SimulatedDevices.cs`:
- `SimulatedEffectDevice` has the profiles Standard, NoConditions, AcceptsNotPlays, DropsAfterN and Failing.
- `VirtualWheel` models inertia, damping and friction, for stability tests of the computed-torque mode.

They test the app's logic only. They are **not** hardware tests.

## Safety rules (do not weaken)

- Forces are capped at min(Maximum force, peak torque) in the mixer, and again in every adapter.
- Centering may change by at most 0.3 × peak per second.
- Watchdog: no FFB cycle for 150 ms with FFB on → `EmergencyStop` from the watchdog thread.
- Computed torque: a request older than 150 ms, or no position reading for 50 ms → zero torque.
- Computed torque also has a power limit: it does not push a wheel that is already spinning fast.
- Oscillation guard: fast oscillation around the centre → centering halved and damping raised for 2 s.
- COM calls from threads other than the window use raw vtable pointers under the device lock. The DirectInput objects belong to the window's STA thread.
