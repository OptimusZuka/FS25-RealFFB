# Code signing: status and plan

## Current status

**The installer and executables of FS25 RealFFB are not signed**, and they are never presented as signed. Authenticity is verified with the SHA-256 checksums published in every release ([INSTALL.md](INSTALL.md#verify-the-file)). The source is open and every release is built by GitHub Actions, so anyone can review the process.

## What each Windows protection does with an unsigned file

| Protection | Effect | Fixable for free? |
|---|---|---|
| SmartScreen | warning with "Run anyway"; fades as the file gains reputation | only a recognised signature removes it from day one |
| Smart App Control | **blocks** it, no override | only with a signature from a trusted CA (or Microsoft cloud reputation) |
| Windows Defender | scans it like any other file; false positives can be submitted to Microsoft | yes: `https://www.microsoft.com/wdsi/filesubmission` |
| UAC | only appears for HidHide, which **is signed** by Nefarius | — |

Measures already in place to reduce false positives:
- no PowerShell scripts in the installer;
- no obfuscated code and no executable packers;
- installation without administrator rights;
- HidHide is downloaded from its official source and run unmodified.

For PCs with Smart App Control on, each release also ships `FS25_RealFFB_BuildOnPC_x.y.z.zip`: `install.bat` compiles the app on the user's own PC with the C# compiler built into Windows.

## Signing options

### SignPath Foundation (not available with the current license)
[SignPath Foundation](https://signpath.org) signs open source projects for free, but it requires an OSI-approved open source license. Since FS25 RealFFB is now **all rights reserved** (see [LICENSE](../LICENSE)), it no longer qualifies. It would qualify again only if the project went back to an open source license.

### Microsoft Store (free registration for individual developers)
Publishing an MSIX package through the Microsoft Store has Microsoft sign it, so Smart App Control accepts it. Registration is free for individual developers. Each version goes through Microsoft's review. RealFFB needs the HidHide driver, which the Store cannot install, so the package would have to download HidHide separately; whether the review accepts that is not known yet.

### Paid certificates
OV/EV code signing certificates and Azure Trusted Signing work regardless of the license, but they are not free.

---
© 2026 OptimusZuka. All rights reserved. See [LICENSE](../LICENSE).
