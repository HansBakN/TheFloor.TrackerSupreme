# Approved requirement amendments

The original [specification](specification.md) is preserved unchanged. These amendments record the user's November 7, 2026 bug-fix request, tracked in [issue #50](https://github.com/HansBakN/TheFloor.TrackerSupreme/issues/50).

## Question visibility before Start (§11.1)

Preparing a duel permits the host to preview the first question. Audience clients receive no question or media until the host starts the clock. A prepared duel recovered in Paused state remains hidden until Resume starts the clock. The public media endpoint enforces the same restriction.

## Pass lockout (§11.4 and scenario D, steps 49–51)

Pass consumes the current question and starts a lockout lasting the configured pass seconds. The active player's clock continues to run during the lockout; there is no additional immediate subtraction. Audience question content and the next host preview remain hidden until the lockout ends. Correct and Pass are rejected during the lockout.

Pause freezes the clock and lockout together. Resume continues their remaining durations. Undo restores the previous question and clocks, clears the lockout and pauses. Recovery preserves the remaining lockout in Paused state and charges no offline time. Zero pass seconds advances immediately. Timer expiry takes precedence; passing the last question pauses on exhaustion after the lockout.

## Ending an unfinished duel (§13 additional controls)

A prominent **End duel and return to setup** control is available while Ready, Running or Paused, including during a lockout. It requires confirmation, stops the duel, archives a completed record without a winner and clears the current session atomically. The audience returns to its waiting screen. Manual winner declaration and return after normal completion remain available.

## Resetting all categories (§§4.2, 5.6, 8, 12 and 18)

The user's November 7, 2026 feature request, tracked in [issue #52](https://github.com/HansBakN/TheFloor.TrackerSupreme/issues/52), adds **Reset all categories** to the category editor. Explicit confirmation covers the whole library, including categories hidden by search, their questions and media, and references in every affected set. Sets, the active-set selection and existing duel snapshots remain available.

Reset retains category folders and the original affected set definitions in an external local backup. Confirmation is checked against all category and set revisions. Invalid or ambiguous content must be corrected before resetting. The content journal resumes interrupted resets and preserves conflicting external edits.

## Mini Floor tournament extension (original §3 scope)

The user's November 7, 2026 Mini Floor request authorizes the floor, matchmaking and elimination features specifically defined by the [Mini Floor specification](mini-floor-specification.md). That complete specification is authoritative for this mode. The original specification is preserved. Standalone duels and the amendments above remain applicable. Other tournament formats remain outside scope. See [issue #54](https://github.com/HansBakN/TheFloor.TrackerSupreme/issues/54) and the [completion checklist](mini-floor-checklist.md).

## Configurable Mini Floor and tournament audience flow (November 7 follow-up)

The user's follow-up supersedes the fixed six-player/3×2 restrictions in Mini Floor §§1–3, 5–6, 12–14 and 18–20. Hosts choose participant count and rectangular board dimensions independently; the participant count need not fill a rectangle. Each player receives one random starting cell. Remaining cells are assigned iteratively: randomly choose an empty cell adjacent to owned territory, then choose uniformly among distinct adjacent owners, using the existing orthogonal wrapped adjacency. Newly assigned cells count when filling subsequent gaps. The user explicitly confirmed this rule. All cells must have an owner before category selection; the generated floor persists unchanged on refresh/recovery.

For N participants, there are N initial picks, N−1 matchups/eliminations and one champion owning all board cells. Five-category offers remain mandatory. The guaranteed pool is `3N+1` distinct eligible categories (still 19 for six): immediately before the final deciding draw, up to `3N−4` categories have been played and five must remain available. Existing duel order/starters, ban rules, category lifecycle, territory transfers, recovery, correction and standalone behavior remain unchanged. The original six-player documents remain preserved as the baseline; their equal-three-neighbor promise applies only to its full 3×2 starting board, not arbitrary layouts after random extra-cell assignments.

The user also requests that `/display` follow the full tournament between duels: floor/owners, acting player, eligible opponents, category offers, ordered bans/pick, matchup intro/score/results, replacement selection, continue/return decision and champion/standings. Reuse the existing duel display while a duel is active. Audience clients remain read-only and receive an explicit public projection containing no answers, alternatives, host notes, question decks or host commands. Creation/actions/results/correction/restart/deletion, refresh/reconnection and backend restart must update the audience correctly. Presentation selection persists; starting a standalone duel restores standalone display behavior.

Track [#58](https://github.com/HansBakN/TheFloor.TrackerSupreme/issues/58) and [#59](https://github.com/HansBakN/TheFloor.TrackerSupreme/issues/59) using the [extension checklist](mini-floor-extensions.md). No other tournament formats are authorized.
