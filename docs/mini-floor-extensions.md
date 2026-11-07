# Mini Floor follow-up completion gate

Authority: the November 7 user follow-ups and [approved amendment](requirements-amendments.md#configurable-mini-floor-and-tournament-audience-flow-november-7-follow-up). Preserve and consult the original [Mini Floor specification](mini-floor-specification.md) and [baseline checklist](mini-floor-checklist.md); their unchanged gameplay rules still apply. Track #58 (board) and #59 (audience), branch `feature/58-configurable-tournament-display`. Reread these documents after resuming or compaction.

## Configurable board (#58)
- [ ] Host chooses participant count, rows and columns; names/settings persist; invalid dimensions/capacity/duplicate names rejected; defaults remain six players/3×2.
- [ ] Assign one random distinct starting cell per player, then randomly fill empty cells from distinct wrapped orthogonal adjacent owners; newly assigned cells count; fill all cells with no new player or diagonal assignment.
- [ ] Persist final starting floor and extra-cell assignment provenance; no rerolls on refresh/restart; explain assignment rule in setup and show board capacity/extra cells.
- [ ] Generalize initial pick order/selection/challenger, N−1 matchups/eliminations, champion ownership, display text and colors. Generalize strict pool capacity to 3N+1; do not reduce offers.
- [ ] Restart retains participant count/board/settings; recover existing six-player saves and undo checkpoints without migration/data loss; standalone unchanged.
- [ ] Backend tests for nonrectangular counts, iterative growth, wrapped eligibility, deduplicated random owners, no gaps/duplicates, invalid setups, complete tournaments with deciders/sweeps, restart/undo and legacy recovery.
- [ ] Host Vue/browser workflow verifies configuration, dynamic counts, extra cells, recovery, champion and original six-player regressions.

## Audience flow (#59)
- [ ] Explicit public tournament DTO/API with only safe names, categories, floor, options and scores; no answers/notes/alternatives/decks/commands/internal checkpoints; anonymous read, no anonymous mutation.
- [ ] Persist presentation selection. Auto-present creation/actions; explicit host present action for resumed/completed tournaments. Starting standalone clears tournament presentation; deletion clears only the affected presentation.
- [ ] Deliver SignalR updates and authoritative HTTP refresh/reconnect state; avoid duplicate subscriptions/listeners and stale transitions. Restore presentation after actual backend restart.
- [ ] Audience floor and wrapped eligible opponent highlights, player category/ownership and distinct colors; read-only with clear current actor and instructions.
- [ ] Initial/fresh five-category offers, all ban stages with banned/remaining/chosen states, matchup intro with fixed starters/categories and score/results, continue/return options, champion and standings.
- [ ] Switch seamlessly into the existing live duel view and back to flow after results; keep tournament score context during duel. Handle Ready/Paused/recovery, expiry, correction, restart, deletion and standalone.
- [ ] Backend privacy/projection/presentation tests, Vue phase/read-only/sync/cleanup tests, separate-context Playwright for every phase, configuration, actual restart and outbound-free operation.
- [ ] Inspect screenshots at 1920×1080, including options/opponents, bans, result, champion and live duel; fix clipping/readability issues.

## Plan / integration

1. Generalize `TournamentRules` and add a board allocation domain helper with persisted provenance. Extend setup with width/height defaults; derive all counts and pool requirements. Preserve existing payload defaults.
2. Add a lightweight SQLite presentation pointer, explicit safe tournament DTO/projection, endpoint and versioned host present action. Broadcast via the existing SignalR service after creation/actions and engine completion; resync via existing connection lifecycle.
3. Update host setup/board/colors/counts; add audience tournament view around the existing duel display. Keep server legality authoritative.
4. Add meaningful domain/service, Vue and full browser tests; run all regressions/build/publish/typecheck; audit every box with evidence; review PR(s), require green CI, merge and verify final main.

## Delivery
- [ ] Update README/architecture/deployment and progress with exact verified state and limitations.
- [ ] All local checks and Linux/Windows CI pass; reviewed PRs merged; pushed main clean; issues accurate/closed only after acceptance verified.
- [ ] Every box linked to implementation/test evidence, with final completeness report and no undisclosed omissions.
