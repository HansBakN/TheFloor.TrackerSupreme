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

## Full specification audit

The follow-up audit reread all 29 sections and all 80 acceptance steps. [Issue #42](https://github.com/HansBakN/TheFloor.TrackerSupreme/issues/42) tracks the gaps found and resolved. The specification is preserved byte for byte.

| Specification sections | Implementation and evidence |
| --- | --- |
| 1–3 · Environment, stack, offline and scope | The locked .NET 10 and Vue 3 build produces one offline server. The production and recovery workflows check local assets and the standalone duel scope. |
| 4–8 · Content model, storage, samples and editors | Content and media tests verify shared UUIDs, duplication, safe paths, format validation and example YAML. Browser tests exercise question editing, a 20-image category, sharing, set activation, invalid YAML recovery and persistence. The audit added public-web-root isolation and tests for temporary library unavailability. |
| 9 · Portability | Package tests cover category and set ZIPs, text JSON, media, archive limits and remapped collisions. The browser imports a complete set into a second installation. The audit checks ambiguous IDs, stale cached sets and the size of the resulting YAML before installation. |
| 10–13 · Setup, rules, snapshots and host | Fake-time tests exercise the state machine, penalties, undo, exhaustion, duplicate commands and deadlines. Chromium verifies the host controls at laptop size and the full Correct/Pass/Correct sequence. The audit adds a guard for repeat answer input after a fast response. |
| 14–18 · Display, synchronization, persistence and reliability | Separate browser contexts inspect actual role-specific hub frames, reconnect the display, switch sets while a duel is running, and verify natural expiry and return to setup. The restart workflow and fake-time tests verify paused recovery and preservation of the question and media. |
| 19–26 · Workflow, CI, tests and documentation | Issues and reviewed PRs track changes through Linux and Windows CI. The README, environment, architecture, progress, deployment and format guides document the integrated app. |
| 27–29 · Acceptance and handoff | The release acceptance table above maps A–G to the test suites. The audit adds coverage for switching the active set during a duel and completing a naturally expired duel entirely through the browser. |

The tests use isolated storage and Chromium. The audience layout was inspected at 1920×1080. Physical TV and projector hardware and sudden power loss are outside the verification setup. A forced process stop restores the last durable timer checkpoint. Definition writes are atomic per file. Category deletion removes set references before deleting the category definition. Set imports install categories before publishing the set definition.


## Event tools and recovery

[Issues #44–#48](https://github.com/HansBakN/TheFloor.TrackerSupreme/issues?q=is%3Aissue+%2344+OR+%2345+OR+%2346+OR+%2347+OR+%2348) track rehearsal and active-set checks, pairing and a single audio output, browser-local setup presets, content recovery, and checkpoint optimization.

The checkpoint profile serialized synthetic decks with a 250-character prompt, 80-character answer, two alternatives, and 150-character notes per question. On the development Mac, 20, 200, and 1,000 questions produced 14,595, 135,015, and 670,215 bytes per old checkpoint. The 1,000-question sample took about 1.22 ms per serialization across 200 iterations. The same session without its question deck was 551 bytes and about 0.002 ms per serialization. These measurements cover serialization and payload size. They do not measure SQLite disk throughput.

SQLite now stores the deck separately and upgrades an existing inline deck when it loads the current duel. The content journal resumes interrupted set imports and shared category deletions, and stops on a conflicting external edit so the edit can be preserved.

Local verification passed 56 xUnit tests, 11 Vitest tests, and nine Playwright workflows. The new browser workflow verifies preset persistence and application, a locally rendered QR code, one designated audio display across separate contexts, missing-media diagnostics, and fullscreen and volume controls on the separate rehearsal view. It verifies that the live duel version and question remain unchanged by rehearsal. Recovery tests replay partially applied operations and preserve conflicting external edits. The SQLite upgrade test starts with an inline deck and verifies that later checkpoints leave the stored deck intact.

## Duel flow corrections — issue #50

The November 7, 2026 user request supersedes the original prepared-question and immediate-pass behaviour. [Approved requirement amendments](requirements-amendments.md) record the changes to §§11.1 and 11.4 and clarify §13 without modifying the original specification.

- Prepared questions remain available to the host, while audience HTTP/SignalR projections and public snapshot media stay hidden until Start. Recovery of a prepared session keeps the audience hidden until Resume.
- Pass starts a server-authoritative lockout for the configured duration. The active clock runs through that interval without an extra immediate deduction. Question content and answer controls unlock only after the interval. Pause, undo, exhaustion, zero penalty, expiry and restart recovery preserve consistent clock and lockout state.
- **End duel and return to setup** is available in the main control row. Confirmation atomically archives the unfinished duel without a winner and clears the current session; retries cannot clear a subsequent duel.

Local verification: 64 xUnit tests, 14 Vitest tests, 10 Playwright workflows, strict TypeScript checks, locked restore, warning-free Release build and production publish passed. Playwright uses isolated data and separate host/audience contexts, checks public payloads and media authorization, measures the full three-second lockout, and exercises confirmation dismissal and direct return to setup. Visual inspection covered the 1920×1080 lockout screen; the existing laptop control visibility check remains in the browser suite.

[PR #51](https://github.com/HansBakN/TheFloor.TrackerSupreme/pull/51) contains the delivery and tracks CI results for issue #50. All three requested corrections are implemented and locally verified; no additional implementation work remains for this issue.

## Library-wide category reset — issue #52

The November 7, 2026 request adds **Categories → Reset all categories**. [Issue #52](https://github.com/HansBakN/TheFloor.TrackerSupreme/issues/52) and the [approved requirement amendment](requirements-amendments.md#resetting-all-categories-42-56-8-12-and-18) track this extension to specification §§4.2, 5.6, 8, 12 and 18; the original specification is unchanged.

Confirmation lists every category and affected set, including categories hidden by search. Reset moves complete category folders into `backups/category-reset-…`, saves the original affected set definitions, and empties references while retaining sets and the active selection. Existing duel snapshots and their media remain playable and recoverable. Stale confirmations and invalid or ambiguous libraries are rejected. Journaled archive operations resume interrupted resets without overwriting conflicting edits. Restore instructions are in the [content format guide](content-format.md#category-reset-backups).

Verification passed: locked .NET restore, warning-free Release compilation, 73 xUnit cases, 14 Vitest cases, strict Vue type checking, production publish, all 11 Playwright workflows and `git diff --check`. The new isolated browser workflow verifies search-independent scope, cancelled confirmation with unsaved edits, host authorization, stale confirmation rejection, backups, retained sets/active selection, a playable duel and recovery after an actual server restart. Visual inspection covered the reset result at 1280×720. Review, integration and CI are tracked in issue #52 and its linked pull request; no local implementation blockers remain.

Initial Linux CI passed the reset workflow but exposed an existing pass-lockout browser assertion that assumed persistence and broadcasting took under 100 ms. The assertion now accounts for measured request duration while still checking the configured three-second interval and the remaining lockout through question reveal. Deterministic backend lockout checks remain unchanged.

## Mini Floor — issue #54 (delivered)

The complete [authoritative Mini Floor specification](mini-floor-specification.md) is preserved verbatim and supersedes the original §3 scope only for this format. The [completion checklist and section-by-section audit](mini-floor-checklist.md) map every rule, workflow, persistence edge and final acceptance item to implementation and verification. Reread both on resume.

Integrated into `main` through [PR #55](https://github.com/HansBakN/TheFloor.TrackerSupreme/pull/55) as `3eb63f7`: six-player setup with a 19-category capacity gate; frozen category/media pool; persisted random floor/order/offers; dimension-independent wrapped adjacency; central category lifecycle; fixed best-of-three categories/starters; ordered decider bans; automatic result recording, territory transfer and elimination; fresh winner picks; continue/return; champion; standings/history; versioned actions; atomic SQLite transitions; paused duel recovery; confirmed latest-result rollback, restart and deletion. The existing duel engine and standalone behavior are retained.

Verification: locked restore, warning-free Release build, 103 xUnit cases (30 new Mini Floor cases), 22 Vitest cases (8 new), strict Vue type checking, production publish and all 14 Playwright workflows (3 new) passed. Full service and browser tournaments cover both all-2–0 and all-decider outcomes. Fake-time completion, wrapped challenges, stable restoration at every major phase, concurrent submissions, injected failed SQLite commits, actual backend restart and final-result correction pass. Host controls were checked at 1280×720; separate audience at 1920×1080; live host/audience, decider and champion/history screenshots were inspected. Both specification hashes match their original sources.

Architecture: `Tournaments/` owns the rules, adjacency, serializable state, persistence adapter and host API. Preparation and completion update the existing duel plus tournament in one transaction. Correction rolls back the latest result and all dependent choices, then prepares that duel with its original snapshot/starter and fresh clocks. Category picks and challenger decisions retain their histories. Data remains external; tests use temporary roots.

The autonomous diff review is recorded on PR #55. Documentation, Ubuntu and Windows CI all passed in [the PR validation run](https://github.com/HansBakN/TheFloor.TrackerSupreme/actions/runs/42371777723); the PR was squash-merged and its feature branch deleted. All checklist requirements have verified evidence. The final cosmetic follow-up was republished, all three Mini Floor browser workflows passed again and its final host screenshot was inspected. No known missing Mini Floor functionality or next unblocked implementation issue remains. Physical TV/projector hardware and sudden power-loss recovery remain unverified; interrupted clocks use the existing durable checkpoint. Future work should begin from current `main` and use new user-approved requirements; no other tournament formats are authorized.

Final handoff review caught and fixed the standalone return after a champion: **Host → Return to setup** now retires the already recorded duel while retaining tournament history. Both full tournament integration/browser scenarios then prepare a standalone duel, and integration also creates another tournament with the reusable original pool. The unfinished-tournament guard remains enforced. The fix is tracked on #54 with its focused follow-up PR; the final report includes its merged/check status.

## Configurable boards and tournament audience — #58 / #59 (delivered)

The user authorizes configurable participant count/rows/columns, iterative random assignment of spare cells to adjacent owners (explicitly confirmed), and full audience flow between duels. The [approved amendment](requirements-amendments.md) supersedes fixed-size restrictions. The [follow-up checklist](mini-floor-extensions.md) contains verified evidence for every requirement; preserve and consult the original spec/checklist for unchanged gameplay rules.

Integrated into `main` as `942c666` through [PR #60](https://github.com/HansBakN/TheFloor.TrackerSupreme/pull/60): configurable setup/preview (2–256 players, 1–64 rows/columns, sufficient cells), wrapped iterative allocation with durable provenance, N-dependent progression and 3N+1 category capacity, backward-compatible recovery/restart/correction, safe public tournament projection, persistent presentation and SignalR synchronization, and read-only audience screens for all phases around the existing duel view. Completed history can be explicitly presented; new standalone duels restore standalone display behavior. No other formats or duel/ban rules were added.

Verification passed: 126 xUnit cases (23 additions), 28 Vitest cases (6 additions), strict Vue typing, locked restore, warning-free production publish, whitespace checks and all 15 Playwright workflows. Coverage includes allocation replay across seeds/layouts, 1D/larger boards, complete 2-/3-/5-/6-/7-player tournaments, capacity, legacy payloads, natural expiry/public advancement, actual restart, correction/restart/deletion/presentation, standalone compatibility, public HTTP/SignalR privacy and outbound-free operation. Inspected 1080p offers/opponents/bans/results/champion/live-duel screenshots and laptop host controls. Original specification hashes are unchanged.

Autonomous diff review is recorded on PR #60; [documentation/Linux/Windows CI](https://github.com/HansBakN/TheFloor.TrackerSupreme/actions/runs/42389910907) passed before squash merge. The feature branch was deleted and local main synchronized. No known missing functionality or next unblocked implementation issue remains for either follow-up. Physical TV/projector and sudden power loss are unverified; very large boards/lists may require scrolling. Future work starts from current main with new user-approved requirements.

## Video questions (delivered)

The user requested video categories alongside image and audio ones; `audio` questions already existed. A new `video` question type accepts MP4 or WebM clips of at most 5 seconds and 8 MiB. The server checks the file signature and reads the duration from the MP4 `mvhd` or WebM `Info` header (clips with an unreadable duration are rejected) on upload, ZIP import, duel snapshot and rehearsal. Clips play muted and looping, with no controls, on the display, host, rehearsal and editor preview; audience media stays hidden until Start like images. Sound belongs in `audio` questions.

Sample data: Demo Video Clips (three generated 2 s WebM clips, in Demo Set B). Verification: 130 xUnit cases (real encoded MP4/WebM durations, demo validation, ZIP round trip, overlong rejection), 29 Vitest cases, strict type checking and all 17 Playwright workflows, including two new ones: editor upload/rejection/preview and a duel that checks privacy before Start, range responses, actual playback and loop wrap-around on the 1920×1080 display (screenshot inspected). Chromium cannot play H.264 MP4, so browser tests use WebM; MP4 is covered by parser tests against an ffmpeg-encoded fixture.
