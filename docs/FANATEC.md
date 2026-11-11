# Fanatec direct drive (CSL DD 8 Nm) with FS25 RealFFB

FS25 RealFFB is tuned for Fanatec direct-drive bases **with the factory settings of the Tuning menu**. You do not need to change anything in the Fanatec software.

## Required

| Setting | Value | Why |
|---|---|---|
| FF (force feedback) | 100% | The app sets the force itself; a lower value makes everything weaker |
| SPR (spring effect) | not OFF (100% = factory) | Centering uses the spring effect |
| DPR (damper effect) | not OFF (100% = factory) | Standstill weight uses the damper effect |
| FOR (force effect) | not OFF (100% = factory) | Bumps use the constant-force effect |

## Recommended (factory values)

| Setting | Value | Note |
|---|---|---|
| NDP (natural damper) | 50% | The app counts on it: it adds only 30% of its own damping while driving. Lower NDP = faster, lighter return; if the wheel then oscillates at the centre, raise "Damping" in the app |
| NFR (natural friction) | OFF | Friction makes the return stick |
| NIN (natural inertia) | OFF | |
| INT (interpolation) | 6 | Higher values soften bumps and texture |
| FEI (force effect intensity) | 100 | Lower values soften all effects |
| SEN (rotation) | AUTO | Steering sensitivity is set in the app |

The app cannot read or change these values: they live in the wheel base. If you changed them for another game, the feel changes; use "Damping" and "Centering" in the app to compensate.
