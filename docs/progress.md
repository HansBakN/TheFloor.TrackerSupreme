# Implementation progress

Development resumed from the duel backend checkpoint on November 4, 2026. [GitHub Issues](https://github.com/HansBakN/TheFloor.TrackerSupreme/issues) track acceptance criteria, specification references, and dependencies.

## Integrated on main

- [PR #12](https://github.com/HansBakN/TheFloor.TrackerSupreme/pull/12) preserves the full attachment and establishes [docs/specification.md](specification.md) as authoritative in [AGENTS.md](../AGENTS.md). The source and checked-in file match byte for byte (1,751 newline characters, 46,879 bytes, SHA-256 `93554afdf1a8e318d2d4016fadf0076a06e2eeb12b7e08d7d30e23a1e12e1c4b`).
- [PR #33](https://github.com/HansBakN/TheFloor.TrackerSupreme/pull/33) adds the .NET 10 and Vue 3 workspace, production publishing, GitHub build and test jobs, and repository-wide `TreatWarningsAsErrors` in `Directory.Build.props`.
- [PR #34](https://github.com/HansBakN/TheFloor.TrackerSupreme/pull/34) adds the external data root, shared YAML categories and sets, revision-checked atomic writes, file monitoring, media validation, and SQLite active-set persistence. The host library API uses a local access cookie.

## Duel backend ([PR #35](https://github.com/HansBakN/TheFloor.TrackerSupreme/pull/35))

- The server prepares a duel from the active set, supports Ready, Running, Paused, and Finished, and serializes versioned commands. It accounts for monotonic elapsed time before a transition and expires timers independently of browser connections. Correct, Pass, Undo, exhaustion, explicit append, restart, and manual finish are implemented on the backend.
- SQLite stores one current duel plus historical records. Unfinished duels restore paused, using the last persisted clock checkpoint after an abrupt shutdown. Session-specific media copies survive edits to the category library.
- The SignalR hub broadcasts separate host and audience projections. Host commands and content require host access, and the audience projection contains only the public question and duel state.
- Local verification passed locked restore, warning-free Release compilation, 24 xUnit tests, strict Vue type checking, 2 Vitest tests, production publish, 3 Playwright tests, and the Git whitespace check. Playwright covered the production shell, external YAML reload and stale writes, and restricted audience HTTP state through expiry. No local test failures remain at the checkpoint.

SharpLens MCP was verified against both solution projects. See [development environment](environment.md) for local configuration.

## Live Vue interfaces

The host and display routes use separate role-specific SignalR subscriptions and Pinia state. They resync on reconnect, refresh, and visibility changes. Only the active clock interpolates between server samples. The host can review a prepared duel, run the game with buttons or shortcuts, and launch a separate audience display.

Verified with 24 xUnit tests, five Vitest tests, type checking, a production publish, and four Playwright tests. The browser flow captured role-specific hub messages in separate contexts, interrupted and restored the audience connection, refreshed the host, and verified Correct, Pass, Undo, Pause, Resume, manual finish, and return to setup. The paused display was visually checked at 1920×1080.

## Content editors

The /sets and /categories pages provide shared membership, ordering, active-set selection, independent duplication, question editing, and media previews. The editors refresh from the watched YAML library and keep a dirty draft visible when the underlying revision changes. Conflicts require loading the latest version explicitly.

Local checks passed 32 xUnit tests, five Vitest tests, strict type checking, production publish, and five Playwright tests. The editor browser workflow verified shared edits, external revision conflicts, active selection, PNG display, and WAV playback in an independent audience context.

## Portable content

Category ZIP, self-contained set ZIP, and text-only JSON import and export are available in both editors. Imports validate archive sizes, paths, definitions, and media before installing. Identical categories are reused. Explicit independent import remaps conflicting UUIDs and keeps existing files.

Verified 41 xUnit tests, five Vitest tests, strict type checking, production publish, and six Playwright tests. The browser test imports a downloaded ZIP into a second server with a clean data directory, then tests the independent-copy option on a collision.

## Demo content

The published app includes opt-in original sample content. Run `dotnet TheFloor.Server.dll --load-demo` before launching the server. Both demo sets share categories, and repeated loading reuses identical definitions. The xUnit suite validates the example YAML, relationships, and image media during CI.

## Release acceptance

The published app was verified on macOS using 46 xUnit cases, 10 Vitest cases, and eight Chromium Playwright workflows. Strict Vue type checking, locked .NET restore, warning-free Release compilation, production publish, and the Git whitespace check passed. CI runs the application checks on Ubuntu and Windows.

| Specification scenario | Verification |
| --- | --- |
| A · Category sets | The browser editor flow creates and shares a category across sets, edits the shared definition, duplicates it independently, and removes one reference. A separate browser test saves and reloads 20 image questions. |
| B · External content | Browser and xUnit tests cover manual YAML changes, UI writes, stale draft protection, invalid YAML diagnostics, and successful reload after correction. |
| C · Portability | The browser exports and imports into a clean second server, then resolves a collision by explicit independent copy. xUnit verifies media round trips, identical reuse, remapping, and archive rejection. |
| D · Duel | Separate host and audience browser contexts exercise setup, Start, Correct and Pass while monitoring both clocks and the role-specific SignalR messages. |
| E · Undo and completion | Host Undo, Pause, Resume, winner declaration, and return to setup pass in Chromium. The server expiry test verifies both clocks stop with the opponent declared winner. |
| F · Persistence and isolation | An actual server restart restores a paused duel with the same question despite a library edit. The browser workflow blocks outbound requests and completes with only local traffic. Isolated test roots and the Git checkout guard are verified. |
| G · Engineering quality | The production build, automated suites, example validation, and published run commands are covered by the release checks. |

Visual inspection covered the paused 1920×1080 display, the 1280×720 host controls, and the category-set editor. A graceful shutdown saves the latest clock state. A forced stop recovers the last durable periodic checkpoint.

The full startup and offline deployment instructions are in the [README](../README.md) and [deployment guide](deployment.md). See the [content format guide](content-format.md) for data locations, YAML and package examples, and backup and restore.
