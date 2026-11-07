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

## Free option: SignPath Foundation

[SignPath Foundation](https://signpath.org) signs open source projects for free. The certificate is issued to "SignPath Foundation", and each signature is tied to a build made from this public repository.

Main requirements (always check SignPath's page, they may change):

1. public repository with an OSI license (MIT ✔);
2. built from source on a CI service (GitHub Actions ✔);
3. MFA enabled on the GitHub and SignPath accounts;
4. manual approval of each release;
5. a project page with the code signing policy.

Steps for the maintainer:

1. Apply at https://signpath.org/apply.
2. Once approved, add the [official action](https://github.com/SignPath/github-action-submit-signing-request) to `release.yml`:
   - sign `RealFFB.exe` and `RealFFB.Setup.exe` before ISCC runs;
   - sign the installer after ISCC;
   - store the token in the `SIGNPATH_API_TOKEN` secret.
3. Remove the "not signed" notice from the release notes.

Another free route is publishing through the Microsoft Store (free developer registration for individuals), where Microsoft signs the package.
