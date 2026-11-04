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

## Remaining work and resume order

1. Continue #21 with a Vue SignalR client and Pinia state. Bootstrap the host cookie, subscribe to the role-specific group, fetch authoritative state after refresh, reconnect and visibility changes, and interpolate tenths locally. Add a browser test that inspects real host and audience SignalR messages in separate browser contexts. The existing Playwright test verifies the HTTP projection.
2. Build /sets and /categories under #13 and #14. Wire server reload diagnostics and conflict handling into the editors for #9. Verify all supported media formats and authorization in #11.
3. Add the host dashboard, keyboard controls, and audience view under #22–#24. Backend Undo is available. Test keys, audio activation, countdown sync, disconnection, and the projected layout.
4. Complete portable interchange and collisions (#15–#16), demo data (#26), visual polish (#25), full coverage and CI (#27–#30), documentation (#31), and the complete acceptance scenarios (#32).

The visible Vue application currently serves the health-check shell. The full browser workflows, offline acceptance, and 1920×1080 visual verification are still open.
