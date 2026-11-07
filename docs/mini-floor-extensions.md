# Mini Floor follow-up completion gate

Authority: the November 7 user follow-ups and [approved amendment](requirements-amendments.md#configurable-mini-floor-and-tournament-audience-flow-november-7-follow-up). Preserve and consult the original [Mini Floor specification](mini-floor-specification.md) and [baseline checklist](mini-floor-checklist.md); their unchanged gameplay rules still apply. Track #58 (board) and #59 (audience), branch `feature/58-configurable-tournament-display`. Reread these documents after resuming or compaction.

## Configurable board (#58)
- [x] Host chooses participant count, rows and columns; names/settings persist; invalid dimensions/capacity/duplicate names rejected; defaults remain six players/3×2.
- [x] Assign one random distinct starting cell per player, then randomly fill empty cells from distinct wrapped orthogonal adjacent owners; newly assigned cells count; fill all cells with no new player or diagonal assignment.
- [x] Persist final starting floor and extra-cell assignment provenance; no rerolls on refresh/restart; explain assignment rule in setup and show board capacity/extra cells.
- [x] Generalize initial pick order/selection/challenger, N−1 matchups/eliminations, champion ownership, display text and colors. Generalize strict pool capacity to 3N+1; do not reduce offers.
- [x] Restart retains participant count/board/settings; recover existing six-player saves and undo checkpoints without migration/data loss; standalone unchanged.
- [x] Backend tests for nonrectangular counts, iterative growth, wrapped eligibility, deduplicated random owners, no gaps/duplicates, invalid setups, complete tournaments with deciders/sweeps, restart/undo and legacy recovery.
- [x] Host Vue/browser workflow verifies configuration, dynamic counts, extra cells, recovery, champion and original six-player regressions.

## Audience flow (#59)
- [x] Explicit public tournament DTO/API with only safe names, categories, floor, options and scores; no answers/notes/alternatives/decks/commands/internal checkpoints; anonymous read, no anonymous mutation.
- [x] Persist presentation selection. Auto-present creation/actions; explicit host present action for resumed/completed tournaments. Starting standalone clears tournament presentation; deletion clears only the affected presentation.
- [x] Deliver SignalR updates and authoritative HTTP refresh/reconnect state; avoid duplicate subscriptions/listeners and stale transitions. Restore presentation after actual backend restart.
- [x] Audience floor and wrapped eligible opponent highlights, player category/ownership and distinct colors; read-only with clear current actor and instructions.
- [x] Initial/fresh five-category offers, all ban stages with banned/remaining/chosen states, matchup intro with fixed starters/categories and score/results, continue/return options, champion and standings.
- [x] Switch seamlessly into the existing live duel view and back to flow after results; keep tournament score context during duel. Handle Ready/Paused/recovery, expiry, correction, restart, deletion and standalone.
- [x] Backend privacy/projection/presentation tests, Vue phase/read-only/sync/cleanup tests, separate-context Playwright for every phase, configuration, actual restart and outbound-free operation.
- [x] Inspect screenshots at 1920×1080, including options/opponents, bans, result, champion and live duel; fix clipping/readability issues.

## Plan / integration

1. Generalize `TournamentRules` and add a board allocation domain helper with persisted provenance. Extend setup with width/height defaults; derive all counts and pool requirements. Preserve existing payload defaults.
2. Add a lightweight SQLite presentation pointer, explicit safe tournament DTO/projection, endpoint and versioned host present action. Broadcast via the existing SignalR service after creation/actions and engine completion; resync via existing connection lifecycle.
3. Update host setup/board/colors/counts; add audience tournament view around the existing duel display. Keep server legality authoritative.
4. Add meaningful domain/service, Vue and full browser tests; run all regressions/build/publish/typecheck; audit every box with evidence; review PR(s), require green CI, merge and verify final main.

## Delivery
- [x] Update README/architecture/deployment and progress with exact verified state and limitations.
- [ ] All local checks and Linux/Windows CI pass; reviewed PRs merged; pushed main clean; issues accurate/closed only after acceptance verified.
- [ ] Every box linked to implementation/test evidence, with final completeness report and no undisclosed omissions.

## Implementation and verification evidence

| Requirement | Owning implementation | Verification |
| --- | --- | --- |
| Configurable participants, dimensions and capacity | `TournamentSetup.Width/Height`, `StartingFloor.ValidateDimensions`, `TournamentRules.Create/Validate`, `Tournaments.vue` | `StartingFloorTests.InvalidDimensionsAndInsufficientCapacityAreRejected`; host Vue configuration test; browser seven-player 4×3 setup with 22 categories and five spare cells. Defaults remain six/3×2. Operational bounds are 2–256 players, 1–64 rows/columns. |
| Iterative random neighboring allocation | `StartingFloor.Allocate` samples one cell per player, then a frontier cell and a distinct adjacent owner using central wrapped adjacency | `EveryExtraCellIsAssignedToADistinctCurrentlyAdjacentOwner` replays every assignment across 30 seeds and six layouts (including 1D, 3×3/7 and 4×4/7). `GrowthCanReachCellsNotAdjacentToAnyOriginalStartingTerritory` verifies propagation. No gaps, duplicate coordinates, diagonal assignment or duplicate-owner weighting. |
| Durable assignments and scalable progression | `StartingTerritories`, `ExtraAssignments`, `TournamentRules.Apply/RecordResult`, restart construction, dynamic labels and color helper | `ConfiguredTournamentFillsFloorRecoversAndCompletes` runs 2-, 3- and 5-player tournaments with recovery, restart, correction, sweeps/deciders and full conquest. Full browser path runs seven players through six deciding matchups, then correction, standalone return, presentation, restart and deletion. Initial browser refresh compares floor/order/offer; allocation provenance is persisted. |
| Capacity guarantees and unchanged game rules | `MinimumCategories(N) = 3N+1`; existing five-category/category lifecycle/best-of-three/starter/ban rules retained | Capacity tests for 2/3/6/7/16 players. All original rule tests and both six-player full browser paths remain. New configurations require 7/10/19/22/49 eligible categories respectively; no reduced draws. |
| Legacy state and standalone compatibility | Added JSON arrays have empty defaults; SQLite presentation table initialized in place; original dimensions retained; new standalone session clears presentation | `LegacyTournamentWithoutAllocationOrPresentationMetadataStillRecovers`; existing recovery tests; every full browser tournament returns to standalone, prepares a duel, retains champion/history and can explicitly present history again. |
| Safe public state and persisted selection | `TournamentAudience.Project`, `TournamentPresentations`, versioned `present` action and `/api/display/tournament` | `PublicTournamentProjectionIsSafePersistentAndFollowsPresentationSelection` checks safe fields, restoration, restart and deletion. Browser anonymous host requests are rejected; public HTTP and actual audience SignalR frames are checked for answers/alternatives/notes/decks/commands/internal snapshot fields, and host-only events are excluded. |
| Live synchronization and expiry | `LiveDuel.Broadcast` projects both views under the duel gate from one settled session; connection refresh/reconnect fetches public state and cleans up `tournament` subscriptions | Live-store test rejects stale tournament versions without replacing clock samples. `AudienceReadSettlesNaturalExpirationAndImmediatelyShowsTheNextTournamentPhase` verifies result/next-phase advancement. Actual server restart retains public state and restores paused question/clocks. Existing standalone SignalR reconnect/cleanup tests remain. |
| Every audience phase, read-only | `TournamentAudience.vue`, audience mode of `TournamentFloor`, existing `Display.vue` question/scoreboard view with matchup strip | Vue tests category picks/counts, legal opponents, ordered bans/remaining/chosen, starters, results, continue/return and champion; no buttons/forms on flow screens. Full browser scenarios assert all phases and current actors/options, enter live duels, correct results, present history, restart and delete. |
| Offline and visual behavior | Existing local production bundle, relative APIs and SignalR; responsive floor/options/player strip | Seven-player browser scenario blocks outbound HTTP requests and completes the entire flow. Inspected 1920×1080 screenshots: initial offers, wrapped opponents, bans, result, champion and live duel; host control visibility remains checked at 1280×720. |

Local verification at the audit checkpoint: 126 xUnit cases (23 additions), 28 Vitest cases (6 additions), strict TypeScript checking, locked restore, warning-free production publish, whitespace validation and 15 Playwright workflows (including the new full seven-player workflow). The final full browser rerun also passed all 15 workflows with the reviewed single-settle broadcast change. SharpLens reports no errors/warnings. The original specification hashes remain unchanged.

Physical TV/projector hardware and sudden power-loss behavior are outside this test setup. Unfinished duel recovery continues to use its durable clock checkpoint. Very large configured boards/participant lists may require scrolling; no alternate tournament rules or formats were added.
