# Approved requirement amendments

The original [specification](specification.md) is preserved unchanged. These amendments record the user's November 7, 2026 bug-fix request, tracked in [issue #50](https://github.com/HansBakN/TheFloor.TrackerSupreme/issues/50).

## Question visibility before Start (§11.1)

Preparing a duel permits the host to preview the first question. Audience clients receive no question or media until the host starts the clock. A prepared duel recovered in Paused state remains hidden until Resume starts the clock. The public media endpoint enforces the same restriction.

## Pass lockout (§11.4 and scenario D, steps 49–51)

Pass consumes the current question and starts a lockout lasting the configured pass seconds. The active player's clock continues to run during the lockout; there is no additional immediate subtraction. Audience question content and the next host preview remain hidden until the lockout ends. Correct and Pass are rejected during the lockout.

Pause freezes the clock and lockout together. Resume continues their remaining durations. Undo restores the previous question and clocks, clears the lockout and pauses. Recovery preserves the remaining lockout in Paused state and charges no offline time. Zero pass seconds advances immediately. Timer expiry takes precedence; passing the last question pauses on exhaustion after the lockout.

## Ending an unfinished duel (§13 additional controls)

A prominent **End duel and return to setup** control is available while Ready, Running or Paused, including during a lockout. It requires confirmation, stops the duel, archives a completed record without a winner and clears the current session atomically. The audience returns to its waiting screen. Manual winner declaration and return after normal completion remain available.
