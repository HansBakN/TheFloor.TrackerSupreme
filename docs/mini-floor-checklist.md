# Mini Floor implementation and verification gate

Authoritative source: [complete user specification](mini-floor-specification.md), preserved verbatim. Tracker: [#54](https://github.com/HansBakN/TheFloor.TrackerSupreme/issues/54). This extension supersedes only the original specification §3 exclusion of floor/tournament features. No other tournament format is authorized.

Every box stays open until implementation and verification evidence exist. Test names and implementation references will be added during the final audit. Reread the complete specification and this checklist after resuming or compaction.

## Setup, pool and identity (§§1–2,18)
- [ ] Exactly six valid, distinct participant names/identities; host is separate; stable player/tournament IDs.
- [ ] Host can review eligible active-set categories and duel settings; require at least 19 distinct playable categories accounting for filtering/media; reject invalid setup.
- [ ] Random one-player-per-cell 3-column × 2-row assignments and independently randomized initial pick order; both durable and stable on refresh/restart.
- [ ] Create, start, resume, review completed tournaments; restart/delete with confirmation. Category lifecycle is scoped to each tournament.

## Floor (§§3,6,9,13,18–19)
- [ ] Central dimension-independent modulo adjacency, both axes wrapped, orthogonal only, distinct neighbors, no self.
- [ ] 3×2: exact neighbor sets at (0,0)/(2,1), three distinct neighbors at every cell. 4×4: exact sets at (0,0)/(3,3)/(1,1), four at every cell; symmetry, no diagonal/self/duplicates, small-board handling and invalid coordinates.
- [ ] Derive player opponents from all currently owned cells; deduplicate; exclude self/eliminated; recalculate after every transfer; reject invalid/nonadjacent challenges.
- [ ] Prominent floor with consistent player distinctions/owner per cell, all valid targets selectable/highlighted including opposite edges, invalid targets distinct, explicit wrap legend; UI list matches domain.
- [ ] Transfer ALL loser cells without moving positions; six cells exactly one owner; newly captured cells create new adjacency; prevent repeated transfers.

## Categories and initial picks (§§4–5,10,18–19)
- [ ] Central Available/Selected/Played lifecycle and ownership, using existing IDs; never offer selected/played categories or replay played ones.
- [ ] Persist one random five-distinct-available draw per step, never reroll on render/refresh/restart; validate before each draw; useful error with no relaxed draw rule.
- [ ] Rejected/offered/banned categories stay available and can reappear later; only chosen categories reserved; only played categories consumed.
- [ ] Six sequential initial picks in durable order; exactly one active category each; no progression before all six; reject changing confirmed selections without correction.
- [ ] Every nonfinal matchup winner must pick a fresh category from five before any next matchup; neither player retains or inherits a played category; no shared selected category.

## Matchups and decider (§§6–8,13,16,18–19)
- [ ] Persist uniform random initial challenger after six picks; choose freely among eligible opponents; fixed challenger/defender identities throughout matchup.
- [ ] Existing duel engine handles questions, clocks, controls, winner and paused recovery; tournament chooses players/category/explicit starter and automatically consumes authoritative results; preserve standalone behavior.
- [ ] Duel 1 defender category/defender first; duel 2 challenger category/challenger first; always play both; first to two; 2–0 stops, 1–1 enters decider.
- [ ] Decider: five available, defender bans one, challenger bans two distinct, defender picks from two, challenger starts; enforce count/order/no duplicate/banned picks/no skipped phases/no third after 2–0.
- [ ] Persist original decider draw and bans/pick; visually mark banned/remaining/chosen, acting player/role and clear Start action; four unused remain available.
- [ ] Matchup intro shows both categories/order; live context shows roles, score, duel number/category/starter and previous results alongside existing live controls.

## Results, progression and champion (§§9–14,18–19)
- [ ] Automatic final score/winner/loser/completion, eliminate one, transfer all cells, update statistics and recalculate adjacency; results show gained cells and updated floor.
- [ ] After replacement pick offer Continue or Return; continue keeps winner; return uniformly chooses another active player excluding returner; persist decision/challenger/source, no reroll.
- [ ] Automatic final champion at one survivor, all six cells, no replacement pick or continue choice; exactly five matchups; clear champion and final floor/history/statistics.
- [ ] Standings: stable identity/name, active/eliminated, category, floor-derived territory count, matchups played/won/lost, duels won/lost, elimination order. Do not conflate duel/matchup wins.
- [ ] Matchup history: ID/roles/categories/starters/duel winners/final score/winner/loser/transferred cells/status; tournament ID/dimensions/phase/current challenger/current matchup/pool/history/champion.
- [ ] Coherent host workflow for all ten §13 functions, showing previous event, next action/actor, progress, eliminated players and active categories.

## Persistence, correction and defensive validation (§§15,17–19)
- [ ] Persist all players/floor/order/offers/selections/category states/challenger/source/matchup/results/bans/history/eliminations/decisions/status; atomic important transitions, no partial commits.
- [ ] Versioned commands with IDs protect duplicates/concurrent/stale submissions; no double result/transfer/elimination/draw/category assignment; refresh and navigation do not mutate state.
- [ ] Restore every major phase, including initial offer, between duels, every ban stage, after victory, replacement offer, winner decision, completed tournament; stable randomness.
- [ ] In-progress duel restores same session/questions/first player/clocks paused; unfinished/abandoned duel never recorded as won; all valid states have recovery/next action.
- [ ] Undo latest recorded duel result with confirmation; restore prior score, completion, territories, elimination, categories, pending selections and all invalidated downstream actions; safely retire invalidated duel. History remains reviewable.
- [ ] Reject malformed state/board/no owner/duplicate cells/invalid active categories/nonadjacent/self/eliminated challenges/bad bans/third after sweep/matchup before fresh pick/after completion. Clear host-facing errors.

## Tests, delivery and audit (§§19–20)
- [ ] Automated domain tests cover every category, matchup, ban, floor, ownership and progression requirement listed above, including reuse of rejected offers and banned categories.
- [ ] Atomic/idempotent/concurrent/recovery/undo tests, with restoration across every major phase.
- [ ] Complete six-player integration: all 2–0 and all 2–1 tournaments, five matchups/five eliminations/one champion/six champion cells/no replay/legal challenges, including wrapped edge.
- [ ] Vue tests for host phases, valid target list, bans/picks, standings/history, errors and destructive confirmation.
- [ ] Playwright full tournament via UI and existing duel engine, separate audience, refresh/restart, correction, champion; visual review at laptop/1080p; isolated test directories.
- [ ] Existing backend/frontend/browser suites, strict typecheck, warning-free build, production publish, whitespace check and CI pass.
- [ ] Documentation explains start/run/resume/correction/19-category pool/rules/storage/architecture; progress and GitHub accurate; autonomous diff review, PR and main integration.
- [ ] Revisit every original section and final acceptance bullet, attach evidence to all boxes, report exact results and remaining limitations without claiming unverified completion.

## Implementation plan and integration points

1. Add serializable tournament state, reusable floor adjacency and pure transition rules. Inject randomness for deterministic tests. Derive standings from floor/history.
2. Add SQLite tournament/checkpoint tables and one shared duel execution gate. Prepare tournament duels and save tournament state in the same transaction; reconcile finished duel results durably. Preserve immutable category/media pool for tournament recovery independent of later content edits.
3. Add safe correction snapshots before recorded results. Restoring the latest snapshot removes all dependent choices and retires an unrecorded downstream duel atomically. Expose host-only versioned actions.
4. Build a /tournaments host workflow and persistent floor/context around the existing host component. Prevent independent actions from corrupting an attached tournament duel.
5. Add domain/service tests, Vue rendering/interaction tests and isolated end-to-end workflows. Audit every checkbox against source and passing tests, then review and integrate through PR #54's linked delivery.
