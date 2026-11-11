# Code signing: status and plan

## Current status

**The installer and executables of FS25 RealFFB are not signed**, and they are never presented as signed. Authenticity is verified with the SHA-256 checksums published in every release ([INSTALL.md](INSTALL.md#verify-the-file)). The source code is public and every release is built by GitHub Actions, so anyone can review the process.

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

## Options for the future

- **SignPath Foundation** signs open source projects for free, but it requires an OSI-approved licence. FS25 RealFFB is now "all rights reserved" (see `LICENSE`), so it does not qualify as things stand.
- **Microsoft Store**: free developer registration for individuals; Microsoft signs the package.
- **A commercial code signing certificate** (OV/EV) from a trusted CA: paid, issued to the author.

No signing is in place yet, and no release is ever described as signed until it really is.
