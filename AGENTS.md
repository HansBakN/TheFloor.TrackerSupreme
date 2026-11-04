# Project instructions

[docs/specification.md](docs/specification.md) is the authoritative requirements document for TheFloor.TrackerSupreme. Read it in full before beginning implementation and consult the relevant sections and acceptance scenarios throughout design, coding, testing, and review. Preserve the original specification and record any user-approved changes explicitly.

Use GitHub Issues as the project tracker. Link work to issue numbers and specification sections, use focused feature branches and Conventional Commits, and integrate through reviewed pull requests after checks pass. Keep [docs/progress.md](docs/progress.md) current so a new session can resume the next unblocked issue.

Use isolated temporary data directories for testing. Keep real event data, generated databases, credentials, and local configuration out of Git. Use Playwright for browser verification.

Keep `TreatWarningsAsErrors` enabled for every C# project through the root `Directory.Build.props`.
