# FS25 RealFFB – Compatibility report format (version 1)

The **Hardware → Export compatibility report** button creates `%APPDATA%\FS25 RealFFB\compatibility-report.txt`. It is plain text, one `key: value` per line, and contains **no user name, PC name or file paths**.

| Key | Content |
|---|---|
| `app_version` | FS25 RealFFB version |
| `date` | UTC date |
| `windows` | Windows version and architecture |
| `product`, `vid_pid` | Product name and USB vendor:product ID reported by Windows |
| `instance_hash` | First 8 hex characters of SHA-256 of the DirectInput instance GUID. It only tells two reports of the same wheel apart; the GUID itself is not included. |
| `catalog_model`, `catalog_validation`, `catalog_source` | What the device catalog says and where it was loaded from |
| `quirks` | Device quirks applied |
| `interface`, `adapter`, `output_mode_setting` | How the forces were sent |
| `effects` | Status of each effect: NotDeclared / Declared / Created / Playing / Failed, plus FF sample period and properties |
| `declared_effects` | Effect names declared by the driver |
| `peak_nm` | Peak torque used, and where it came from (catalog / user / assumed) |
| `vendor_ff_percent`, `max_force_nm`, `drive_profile` | Calibration values |
| `health` | Commands accepted and rejected by the driver, recoveries |
| `torque_loop_hz`, `stale_stops` | Computed-torque mode only |
| `guided_test` | Answers from "Test my wheel" (left, right, spring, damper, friction, vibration) |
| `works_in_FS25`, `notes` | Filled in by the user before sending |

"Playing" means Windows reports the effect as playing. Only the user's answers in the guided test, and their driving in FS25, show whether the force actually reached the wheel.
