# Keeping proof of authorship

This is not legal advice. Copyright normally exists automatically from creation, with no registration needed (Berne Convention). Good evidence makes it much easier to prove who created what, and when.

## What already exists

- **Git history**: every commit since 2026-11-07 is authored by `OptimusZuka`, with timestamps. The history was never rewritten.
  - The first commit (`3008a93`) contains FS25 Real FFB v0.17.
  - The following commits show the installer, interface, profiles, Logitech support and translations.
- **Releases** on GitHub (from v0.5.0 onwards), with dates, files and SHA-256 checksums.
- **Development before Git**: the original working folder on the author's PC and the development conversations.

The commits carry a `Co-Authored-By: Claude` line because the development was AI-assisted. In some jurisdictions only human creative contribution is protected by copyright. The author's design decisions, direction, testing, tuning, visual identity and selection should therefore be documented, as these notes and the history already do.

## Recommendations

1. **Never rewrite or delete the Git history.** Do not squash, force-push over `main`, or delete releases.
2. **Sign commits and tags.** GitHub supports free SSH or GPG signing, and signed commits show as "Verified".
3. **Archive the repository in Software Heritage** (free, https://archive.softwareheritage.org/save/). It keeps a permanent, independently dated copy.
4. **Timestamp important files.** Keep the SHA-256 checksums of each release, and optionally timestamp them for free with OpenTimestamps (https://opentimestamps.org).
5. **Keep the originals**: logo source files, working folders, early versions, screenshots and YouTube videos showing the project, all with their dates.
6. **Optional registration**: some countries offer voluntary registration of software or works, for example INPI Brasil (computer programs) or IGAC in Portugal. It is not required, but it adds dated evidence. Check fees and requirements on the official sites.
7. **Keep notices in place**: `LICENSE`, `NOTICE.md` and the copyright line at the top of the source files.

---
© 2026 OptimusZuka. All rights reserved.
