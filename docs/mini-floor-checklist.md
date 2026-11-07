# Mini Floor implementation and verification gate

Authoritative source: [complete user specification](mini-floor-specification.md), preserved verbatim. Tracker: [#54](https://github.com/HansBakN/TheFloor.TrackerSupreme/issues/54). This extension supersedes only the original specification §3 exclusion of floor/tournament features. No other tournament format is authorized.

Every box stays open until implementation and verification evidence exist. Test names and implementation references will be added during the final audit. Reread the complete specification and this checklist after resuming or compaction.

## Setup, pool and identity (§§1–2,18)
- [x] Exactly six valid, distinct participant names/identities; host is separate; stable player/tournament IDs.
- [x] Host can review eligible active-set categories and duel settings; require at least 19 distinct playable categories accounting for filtering/media; reject invalid setup.
- [x] Random one-player-per-cell 3-column × 2-row assignments and independently randomized initial pick order; both durable and stable on refresh/restart.
- [x] Create, start, resume, review completed tournaments; restart/delete with confirmation. Category lifecycle is scoped to each tournament.

## Floor (§§3,6,9,13,18–19)
- [x] Central dimension-independent modulo adjacency, both axes wrapped, orthogonal only, distinct neighbors, no self.
- [x] 3×2: exact neighbor sets at (0,0)/(2,1), three distinct neighbors at every cell. 4×4: exact sets at (0,0)/(3,3)/(1,1), four at every cell; symmetry, no diagonal/self/duplicates, small-board handling and invalid coordinates.
- [x] Derive player opponents from all currently owned cells; deduplicate; exclude self/eliminated; recalculate after every transfer; reject invalid/nonadjacent challenges.
- [x] Prominent floor with consistent player distinctions/owner per cell, all valid targets selectable/highlighted including opposite edges, invalid targets distinct, explicit wrap legend; UI list matches domain.
- [x] Transfer ALL loser cells without moving positions; six cells exactly one owner; newly captured cells create new adjacency; prevent repeated transfers.

## Categories and initial picks (§§4–5,10,18–19)
- [x] Central Available/Selected/Played lifecycle and ownership, using existing IDs; never offer selected/played categories or replay played ones.
- [x] Persist one random five-distinct-available draw per step, never reroll on render/refresh/restart; validate before each draw; useful error with no relaxed draw rule.
- [x] Rejected/offered/banned categories stay available and can reappear later; only chosen categories reserved; only played categories consumed.
- [x] Six sequential initial picks in durable order; exactly one active category each; no progression before all six; reject changing confirmed selections without correction.
- [x] Every nonfinal matchup winner must pick a fresh category from five before any next matchup; neither player retains or inherits a played category; no shared selected category.

## Matchups and decider (§§6–8,13,16,18–19)
- [x] Persist uniform random initial challenger after six picks; choose freely among eligible opponents; fixed challenger/defender identities throughout matchup.
- [x] Existing duel engine handles questions, clocks, controls, winner and paused recovery; tournament chooses players/category/explicit starter and automatically consumes authoritative results; preserve standalone behavior.
- [x] Duel 1 defender category/defender first; duel 2 challenger category/challenger first; always play both; first to two; 2–0 stops, 1–1 enters decider.
- [x] Decider: five available, defender bans one, challenger bans two distinct, defender picks from two, challenger starts; enforce count/order/no duplicate/banned picks/no skipped phases/no third after 2–0.
- [x] Persist original decider draw and bans/pick; visually mark banned/remaining/chosen, acting player/role and clear Start action; four unused remain available.
- [x] Matchup intro shows both categories/order; live context shows roles, score, duel number/category/starter and previous results alongside existing live controls.

## Results, progression and champion (§§9–14,18–19)
- [x] Automatic final score/winner/loser/completion, eliminate one, transfer all cells, update statistics and recalculate adjacency; results show gained cells and updated floor.
- [x] After replacement pick offer Continue or Return; continue keeps winner; return uniformly chooses another active player excluding returner; persist decision/challenger/source, no reroll.
- [x] Automatic final champion at one survivor, all six cells, no replacement pick or continue choice; exactly five matchups; clear champion and final floor/history/statistics.
- [x] Standings: stable identity/name, active/eliminated, category, floor-derived territory count, matchups played/won/lost, duels won/lost, elimination order. Do not conflate duel/matchup wins.
- [x] Matchup history: ID/roles/categories/starters/duel winners/final score/winner/loser/transferred cells/status; tournament ID/dimensions/phase/current challenger/current matchup/pool/history/champion.
- [x] Coherent host workflow for all ten §13 functions, showing previous event, next action/actor, progress, eliminated players and active categories.

## Persistence, correction and defensive validation (§§15,17–19)
- [x] Persist all players/floor/order/offers/selections/category states/challenger/source/matchup/results/bans/history/eliminations/decisions/status; atomic important transitions, no partial commits.
- [x] Versioned commands with IDs protect duplicates/concurrent/stale submissions; no double result/transfer/elimination/draw/category assignment; refresh and navigation do not mutate state.
- [x] Restore every major phase, including initial offer, between duels, every ban stage, after victory, replacement offer, winner decision, completed tournament; stable randomness.
- [x] In-progress duel restores same session/questions/first player/clocks paused; unfinished/abandoned duel never recorded as won; all valid states have recovery/next action.
- [x] Undo latest recorded duel result with confirmation; restore prior score, completion, territories, elimination, categories, pending selections and all invalidated downstream actions; safely retire invalidated duel. History remains reviewable.
- [x] Reject malformed state/board/no owner/duplicate cells/invalid active categories/nonadjacent/self/eliminated challenges/bad bans/third after sweep/matchup before fresh pick/after completion. Clear host-facing errors.

## Tests, delivery and audit (§§19–20)
- [x] Automated domain tests cover every category, matchup, ban, floor, ownership and progression requirement listed above, including reuse of rejected offers and banned categories.
- [x] Atomic/idempotent/concurrent/recovery/undo tests, with restoration across every major phase.
- [x] Complete six-player integration: all 2–0 and all 2–1 tournaments, five matchups/five eliminations/one champion/six champion cells/no replay/legal challenges, including wrapped edge.
- [x] Vue tests for host phases, valid target list, bans/picks, standings/history, errors and destructive confirmation.
- [x] Playwright full tournament via UI and existing duel engine, separate audience, refresh/restart, correction, champion; visual review at laptop/1080p; isolated test directories.
- [x] Existing backend/frontend/browser suites, strict typecheck, warning-free build, production publish, whitespace check and CI pass.
- [x] Documentation explains start/run/resume/correction/19-category pool/rules/storage/architecture; progress and GitHub accurate; autonomous diff review, PR and main integration.
- [x] Revisit every original section and final acceptance bullet, attach evidence to all boxes, report exact results and remaining limitations without claiming unverified completion.

## Implementation plan and integration points

1. Add serializable tournament state, reusable floor adjacency and pure transition rules. Inject randomness for deterministic tests. Derive standings from floor/history.
2. Add SQLite tournament/checkpoint tables and one shared duel execution gate. Prepare tournament duels and save tournament state in the same transaction; reconcile finished duel results durably. Preserve immutable category/media pool for tournament recovery independent of later content edits.
3. Add safe correction snapshots before recorded results. Restoring the latest snapshot removes all dependent choices and retires an unrecorded downstream duel atomically. Expose host-only versioned actions.
4. Build a /tournaments host workflow and persistent floor/context around the existing host component. Prevent independent actions from corrupting an attached tournament duel.
5. Add domain/service tests, Vue rendering/interaction tests and isolated end-to-end workflows. Audit every checkbox against source and passing tests, then review and integrate through PR #54's linked delivery.

## Section-by-section evidence

The original specification was reread against the implementation and tests. `docs/specification.md` is byte-for-byte unchanged (SHA-256 `93554afdf1a8e318d2d4016fadf0076a06e2eeb12b7e08d7d30e23a1e12e1c4b`). The copied Mini Floor specification matches the attachment (SHA-256 `2fea33fe1cba2527b57263af8b8385518c990315dbb150470911f86c69fc846c`).

All backend evidence below is in [TournamentTests.cs](../tests/TheFloor.Tests/TournamentTests.cs), including `FloorAdjacencyTests`. UI evidence is in [Tournaments.test.ts](../src/web/src/Tournaments.test.ts) and [mini-floor.spec.ts](../src/web/e2e/mini-floor.spec.ts). The existing duel, content, Vue and browser regression tests remain in place.

| Specification | Implementation and verified evidence |
| --- | --- |
| §1: exactly six participants, elimination, five best-of-three matchups | `TournamentRules.Create`, `RecordResult`; `CompleteSixPlayerTournamentWithSweepsOrEveryMatchupDecided` runs both all-2–0 and all-2–1 tournaments, checking five eliminations, one champion, six owned cells and 10/15 unique categories. Both paths also run entirely through the browser UI. |
| §2: creation, random floor/order, stable IDs, resume, colors | `Create` separately shuffles owners/order and stores both. Setup tests reject invalid names/settings/capacity. `SetupAndEveryPickHaveStableDrawsAndOnlySelectedCategoriesAreReserved` reopens the service at each pick; Playwright refreshes initial picks and compares floor/order/offer. `TournamentFloor` uses persistent player colors and shows every owner. |
| §3: dimension-independent wrapped adjacency and UI targets | `FloorAdjacency.Neighbors` implements modulo candidates, deduplication and no self. Tests assert every specified 3×2/4×4 neighbor set, symmetry, cardinality and invalid coordinates, including 1×1/1×2. `Opponents` derives targets from current ownership. The merged-ownership test checks newly accessible opponents, eliminated/self exclusion and deduplication. Vue checks opposite-edge selection and diagonal exclusion; Playwright compares highlighted cell owners against the server's target list. |
| §4: Available/Selected/Played | Central `TournamentCategory` lifecycle in `Draw`, `Apply`, `ConsumeCategory` and `NextDuel`. Selected IDs never enter draws; rejected and banned IDs remain available; replay is rejected. Selection history stores every original offer and chosen ID. `RejectedOffersCanBeDrawnAgainAndInsufficientDrawsAreNeverReduced` checks offer reuse and strict draw size; bans tests verify all four unused deciding categories remain available. |
| §5: six ordered initial picks, no rerolls or permanent reservations | `InitialSelection`, durable `SelectionOrder`, `SelectionIndex`, `Offer`, `Selections`. Setup/pick tests check current actor, five distinct offers, all six selected categories, rejected availability and serialized restoration at every pick; invalid challenges before completion fail. Browser fills names and makes all six picks. |
| §6: initial uniform random challenger, fixed roles, legal choice | `Apply` uses `Random.Next` over all six after picks, then authoritative `Opponents`. Challenge tests reject self/nonneighbors and confirm fixed roles, with a wrapped challenge. Playwright freely selects from the eligible list. Challenger/source persist and are displayed. |
| §7: mandatory first two duels, explicit categories/starters and score | `NextDuel`, `PrepareTournament`, `RecordResult`; challenger maps to engine A, defender to B. Tests inspect actual engine configuration/first player for every duel, enforce 2–0/1–1 progression and reject further launches after completion. Vue/browser show roles, score, category order, first player and prior results. |
| §8: fixed defender-one/challenger-two/defender-pick bans | Explicit `DefenderBan`, `ChallengerBans`, `DefenderPick`, `DuelReady` phases, original offer/bans/choice persisted. `BansEnforceFixedRolesCountsOrderAndUnchosenCategoriesStayAvailable` tests counts, repeats, banned choices, skipped phases, restore after every decision and challenger-first. Vue tests two-selection gating and banned styling; all-decider Playwright refreshes mid-bans and completes all five decisions. Chosen category is highlighted with a separate prepare/start action. |
| §9: completion, score, elimination, all-cell transfer, no inheritance | `RecordResult` completes at two wins, transfers every loser cell without moving coordinates and derives standings/targets from current floor. Full integrations verify ownership after each matchup and final complete conquest. Winner/loser categories are consumed on their own duels; replacement is required. Browser history shows the final score, winner and number of captured territories. |
| §10: winner replacement category | The result transaction persists a new five-category offer; `next` opens `WinnerCategory`, `pick` assigns one and enters `WinnerDecision`. Every nonfinal round in both integrations/browser tournaments takes this route, with restoration at result/offer/decision. Challenges are rejected before the pick. Final completion skips the draw. |
| §11: continue or return | `Apply` persists `ChallengerDecision`, new challenger and source. Continue retains winner; return chooses uniformly using `Random.Next` over active players excluding winner. Both complete integration/browser paths alternate these choices and assert exclusion or retention. Restoration compares the full serialized state. |
| §12: automatic champion, no final pick, review | Fifth elimination automatically sets champion/completion. Both full integration and browser paths assert one survivor, all cells, exactly five matchups and no further progression. Vue checks champion/standings/history with no continue/pick prompt. Browser reloads the completed tournament and corrects/re-records its final result. |
| §13: all ten host functions | `/tournaments/:id?`, `Tournaments.vue`, `TournamentFloor.vue`, typed `useTournament`, embedded existing `Host.vue`. Setup, sequential offers, floor/targets, intro, live scores/controls, bans, result, replacement, decision, champion and review all run through Playwright. Player colors, wrap legend, explicit phase/actor and action labels reviewed. Live controls are checked in a 1280×720 viewport; separate audience uses 1920×1080. |
| §14: player/matchup/tournament statistics | `TournamentState`, `Matchup`, `TournamentDuel`, `PlayerStanding`, `Standings`. IDs, roles, categories, starters, individual winners, transferred cells and completion persist; final scores derive from duel results. Standings derive territory counts from floor and keep duel W–L separate from matchup W–L. Integration verifies aggregate totals/elimination order; Vue/browser verify displayed standings/history. |
| §15: recovery, atomicity, duplicates | `Tournaments` + `TournamentDecks` in existing SQLite; all mutations share the duel gate, expected versions and command IDs. Launch and authoritative completion share EF transactions. `SQLiteFailureCannotPartiallyCommitDuelLaunchOrResult` injects a failed SQLite write at each boundary and verifies neither side partially commits. Repeated/concurrent tests prove one pick/result; restart/delete retries are safe. Serialization checks cover every major phase and all stable random choices. |
| §16: existing engine, standalone compatibility | Existing `DuelService`, `DuelRules`, clocks, snapshots, controls, media authorization and audience SignalR projections are reused. No duel mechanics are copied. Explicit context/category/starter guards reject mismatched integration. Natural timer expiry and manual winners both drive tournament results. Actual server-restart browser test preserves session/question/starter and restores paused. Both full service/browser tournament paths also return to standalone setup and prepare an independent duel without deleting tournament history; a new tournament can reuse the complete pool. All pre-existing suites pass. |
| §17: latest-result correction, review, restart/delete | Result transaction saves `ResultCheckpoint`. Confirmed undo restores it and retires any unrecorded downstream duel, then prepares the original duel with a new ID and fresh clocks. The rollback test undoes completion after replacement/return/challenge/next-duel launch and compares floor, players, score, categories and decisions. Browser cancels correction, accepts it, reverses final elimination/champion and re-records. Restart/delete tests enforce confirmation, reset lifecycle, idempotency and standalone return. |
| §18: validation and capacity | `Create`, `Draw`, `Validate`, `NextDuel`, `Apply` and host-only versioned endpoints enforce names/identity, 19 eligible active-set categories, strict five-offer size, lifecycle, floor/ownership, phase/active categories, bans, roles and challenge/progression constraints. Setup filters disabled/empty/missing-media categories and rejects library errors. Invalid-state tests cover duplicate/missing cells, absent owners and malformed categories. Host sees explanatory API errors. Anonymous audience requests cannot read or mutate tournament endpoints. |
| §19: required automated coverage | 30 new backend cases plus 8 Vue cases and 3 isolated Playwright workflows. Full service tournaments cover all 2–0 and all 2–1, including forced x=0→x=2 challenge, no repeats, correct starters, five matchups/eliminations and one champion. Browser tests use real engine controls with separate host/audience, refresh, actual restart and correction. All earlier regression workflows are retained. |
| §20 and final acceptance | Domain orchestration stays in `Tournaments/`; dimension-independent adjacency is UI-independent; state/content/media recover through existing storage. README, architecture and deployment explain the full host flow, minimum pool, offline data and correction. The final acceptance bullet list is covered by the full browser workflows and tests above. Reviewed PR and CI evidence are recorded below when available. |

## Verification results

- Backend: 103 passing xUnit cases, including 30 new Mini Floor cases. Injected clocks exercise timer completion; injected SQLite failures exercise atomic boundaries.
- Frontend: 22 passing Vitest cases, including 8 Mini Floor cases; strict `vue-tsc` passes.
- Production build: locked restore and publish pass; C# warnings remain errors. SharpLens reports no errors or warnings.
- Browser regression: 14 passing Chromium workflows, including 3 Mini Floor workflows. The final full regression run also passed all 14 workflows, including 1280×720 control visibility. Screenshots of live host/audience, deciding picks and champion/history were visually inspected.
- Both original specification files have their expected hashes. Real event data, databases, screenshots and test artifacts are excluded from Git. All tournament fixtures use isolated temporary roots.
- Verification boundary: physical TV/projector hardware and sudden power loss are not tested. Abrupt restart uses the existing durable duel clock checkpoint; no offline time is charged. No Mini Floor requirement is intentionally deferred.

## Delivery verification

The complete implementation is integrated into `main` as `3eb63f7` through [reviewed PR #55](https://github.com/HansBakN/TheFloor.TrackerSupreme/pull/55), resolving [issue #54](https://github.com/HansBakN/TheFloor.TrackerSupreme/issues/54). [PR validation](https://github.com/HansBakN/TheFloor.TrackerSupreme/actions/runs/42371777723) passed documentation, Ubuntu and Windows builds/tests, including all browser workflows. The autonomous diff review is recorded on the PR. The feature branch was deleted after squash merge; the local checkout was fast-forwarded to the merged source with no source changes remaining.

All 40 checklist items above are verified by the section-by-section evidence and delivery checks. Both six-player tournament paths run from UI setup through champion declaration, with no external scorekeeping. There are no known unimplemented Mini Floor requirements. The physical hardware/power-loss verification boundary above remains explicit. The final cosmetic follow-up was republished and the three Mini Floor browser workflows passed again; the final 1280×720 screenshot was inspected after the label correction.

Final handoff review added regression coverage for returning from the last completed tournament duel to standalone setup. Retiring that already recorded duel preserves its tournament history/checkpoint; unfinished tournament duels still cannot be abandoned through standalone return. Backend, Vue and full browser paths verify the transition. This follow-up raises the Vue total to 22 cases (8 Mini Floor cases); backend/browser case totals remain 103/14.
