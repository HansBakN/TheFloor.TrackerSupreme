# Project instructions

[docs/specification.md](docs/specification.md) is the authoritative requirements document for TheFloor.TrackerSupreme. Read it in full before beginning implementation and consult the relevant sections and acceptance scenarios throughout design, coding, testing, and review. Preserve the original specification and record any user-approved changes explicitly.

Use GitHub Issues as the project tracker. Link work to issue numbers and specification sections, use focused feature branches and Conventional Commits, and integrate through reviewed pull requests after checks pass. Keep [docs/progress.md](docs/progress.md) current so a new session can resume the next unblocked issue.

Use isolated temporary data directories for testing. Keep real event data, generated databases, credentials, and local configuration out of Git. Use Playwright for browser verification.

Keep `TreatWarningsAsErrors` enabled for every C# project through the root `Directory.Build.props`.

## C# Semantic Analysis — SharpLens MCP

- Prefer SharpLens for semantic navigation, symbol discovery, references, method inspection, type relationships, dependencies, diagnostics, impact analysis, and refactoring. Use semantic tools alongside text search when investigating C# behavior or dependencies.
- Choose the appropriate operations, including `search_symbols`, `find_references`, and `get_method_source`. Inspect affected symbols and references before substantial C# refactoring.
- Use `sync_documents` after external C# edits when the workspace needs synchronization. Reload the solution after solution or project structure changes when necessary.
- Use `dotnet build`, `dotnet test`, and the appropriate development tools for compilation and testing. Semantic analysis complements these checks.
- For trivial changes or unrelated tasks, use the simplest tool. Fall back to standard tools if SharpLens is unavailable.
