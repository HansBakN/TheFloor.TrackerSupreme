
Implement a complete "Mini Floor" tournament mode in the existing application.

CONTEXT

The application currently supports running individual category-based duels but does not support tournament management.

Your task is to extend the application so that I, as the host, can run an entire Mini Floor tournament from start to finish, using the existing duel functionality.

This is a significant new feature. Inspect the existing codebase thoroughly before implementing it. Follow its architecture, conventions, design system, state management, and persistence patterns.

Reuse the existing duel engine rather than reimplementing duel mechanics.

Implement ONLY Mini Floor. Do not implement Three Lives, Champions League, or other tournament formats yet.

However, keep the architecture modular enough that other tournament formats can be introduced later without rewriting the duel engine or tournament infrastructure.


==================================================
1. TOURNAMENT OVERVIEW
==================================================

Mini Floor is a territory-based elimination tournament inspired by the television show "The Floor".

The tournament has:
- Exactly 6 participants.
- One host who operates the application but does not participate.
- A 2x3 floor containing 6 territories.
- One starting territory per participant.
- One active category per participant.
- Best-of-three matchups between neighboring players.
- Elimination of the matchup loser.
- Transfer of all the loser's territories to the winner.
- New category selection for the winner.
- A choice for the winner to continue challenging or return to the floor.
- Wraparound adjacency, meaning opposite edges of the board are connected.

The last remaining participant wins the tournament.

Terminology:
- "Duel" = One category-based game handled by the existing duel engine.
- "Matchup" = A best-of-three series of duels between two players.
- "Territory" = One cell on the floor.
- "Challenger" = The player initiating a matchup.
- "Defender" = The player being challenged.
- "Active category" = The category currently owned by a player, which has not yet been played.
- "Wraparound adjacency" = Territories on opposite edges of the board are considered neighbors.

A completed six-player tournament will have exactly 5 matchups, since every matchup eliminates one participant.

Each matchup contains 2 or 3 duels.


==================================================
2. TOURNAMENT CREATION
==================================================

Provide a tournament setup interface.

The host must be able to:
- Create a new Mini Floor tournament.
- Enter the names of 6 participants.
- Review the available category pool.
- Start the tournament.
- Resume an existing unfinished tournament.

On tournament creation:

1. Create a 2x3 floor.
2. Randomly assign each of the 6 participants to one territory.
3. Randomize the order in which players select their initial categories.
4. Begin initial category selection.

Persist the generated territory assignments and category selection order.

They must not change after refreshing or restarting the application.

Display the floor prominently throughout the tournament, clearly identifying the owner of each territory.

Use consistent player colors or other visual distinctions.

BOARD DIMENSIONS:

For the initial implementation, use:
- Width: 3 columns.
- Height: 2 rows.
- Coordinates: x = 0..2, y = 0..1.

However, implement adjacency calculations in a dimension-independent way so that larger boards, such as 4x4, could be supported later.

The tournament UI only needs to support the six-player 2x3 configuration for now.


==================================================
3. FLOOR ADJACENCY AND EDGE WRAPPING
==================================================

IMPORTANT: This section defines a fundamental game rule.

The board must use WRAPAROUND ADJACENCY.

This means the board behaves topologically like a torus:
- The left edge connects to the right edge.
- The right edge connects to the left edge.
- The top edge connects to the bottom edge.
- The bottom edge connects to the top edge.

Players must be able to challenge opponents across these boundaries.

The goal is to avoid giving players on edges or corners fewer available opponents.

In the initial stage, every player must have the same number of distinct neighboring opponents.

ADJACENCY RULES:

Two territories are adjacent if they are:
- Directly above/below one another.
- Directly left/right of one another.
- Connected through the horizontal wraparound.
- Connected through the vertical wraparound.

Diagonal adjacency is never allowed.

EXAMPLE: 4x4 BOARD

Suppose a player owns territory [0,0], the bottom-left corner.

That territory must be adjacent to:
- [1,0] — Right.
- [0,1] — Above.
- [3,0] — Wrapped across the left/right boundary.
- [0,3] — Wrapped across the top/bottom boundary.

Therefore, the player has four neighboring territories.

The same applies to every position on a 4x4 board.

EXAMPLE: ACTUAL 3x2 BOARD

The actual tournament board has 3 columns and 2 rows.

For territory [0,0], the neighboring territories are:
- [1,0] — Right.
- [2,0] — Wrapped horizontally to the opposite edge.
- [0,1] — Vertical neighbor.

IMPORTANT SMALL-BOARD DETAIL:

Because this board has only 2 rows, moving upward and downward from a territory may identify the same neighboring territory.

That territory must only be counted once.

Therefore, on the initial 3x2 board, each player has exactly 3 DISTINCT neighboring players.

All six players must have exactly 3 eligible opponents initially.

Do not assume that every cell always has four distinct neighbors.

ADJACENCY IMPLEMENTATION:

For a board with width W and height H, the four candidate neighbors of coordinate (x,y) are:

Right:
((x + 1) % W, y)

Left:
((x - 1 + W) % W, y)

Down:
(x, (y + 1) % H)

Up:
(x, (y - 1 + H) % H)

Remove duplicates.

Never include the territory itself as a neighbor.

Implement adjacency in a central reusable function or service.

Do not implement separate hardcoded corner, edge, and middle-cell logic.

PLAYER-LEVEL ADJACENCY:

Players may eventually own multiple territories.

A player can challenge another player if ANY territory owned by the challenger is adjacent to ANY territory owned by the defender, including through wraparound boundaries.

Example:

Player A owns [0,0] and [1,0].

Player B owns [2,0].

Player A and Player B are adjacent because:
- [1,0] neighbors [2,0] normally.
- [0,0] neighbors [2,0] through horizontal wrapping.

The opponent should only appear ONCE as an eligible target.

Never duplicate opponents because multiple territories share a boundary.

After each territory transfer, recalculate the eligible neighboring opponents based on the updated floor.

Do not cache initial player adjacency as a permanent relationship.

FLOOR UI:

The UI must clearly communicate wraparound adjacency.

When a challenger is selected:
- Highlight every eligible neighboring opponent.
- Allow clicking/selecting those opponents.
- Include opponents connected across opposite edges.
- Do not require territories to visually touch on the screen.
- Visually distinguish valid targets from invalid targets.

Provide a small explanation or visual indicator showing that opposite board edges are connected.

A player should never be unable to challenge an opponent merely because the opponent is displayed on the opposite edge.

Do not add diagonal adjacency.

TESTING REQUIREMENTS:

Verify that:
- Every player has 3 distinct neighboring opponents initially on a 3x2 board.
- Every position has 4 distinct neighboring territories on a 4x4 board.
- [0,0] and [3,0] are adjacent on a 4x4 board.
- [0,0] and [0,3] are adjacent on a 4x4 board.
- [0,0] and [3,3] are NOT adjacent on a 4x4 board.
- A 2-row board does not produce duplicate vertical neighbors.
- Player-level adjacency works when players own multiple territories.
- Eligible opponents are deduplicated.
- Adjacency is recalculated after territory transfers.


==================================================
4. CATEGORY MANAGEMENT
==================================================

Categories may be played only ONCE across the entire tournament.

A category can be in one of three logical states:

AVAILABLE:
- Not currently owned by any player.
- Has never been played.
- Can be offered during category selection.

SELECTED:
- Currently owned by a player.
- Cannot be offered to any other player.
- Cannot be drawn for a deciding duel.

PLAYED:
- Has been used in a duel.
- Permanently unavailable for the remainder of the tournament.

IMPORTANT:

Being offered a category does NOT reserve it permanently.

If a player is offered five categories and chooses one, the other four remain available.

Therefore, a category may appear in multiple players' offers at different times, as long as nobody has selected or played it.

All five categories within a single offer must be distinct.

A selected category must never be offered to another player.

A played category must never be offered or played again.

Use the existing category identifiers and category data.

Keep category availability scoped to the current tournament. Starting another tournament should allow the original category collection to be reused.

Ensure category state transitions are consistent and persisted.

A category that has been offered and then rejected is still available.

A category that was banned during third-duel selection is also still available.

Category availability must be managed centrally rather than independently in different UI components.


==================================================
5. INITIAL CATEGORY SELECTION
==================================================

Before the first matchup, every player must select an initial category.

Players select one at a time, using the randomized selection order established during tournament creation.

For each player:

1. Randomly draw 5 distinct AVAILABLE categories.
2. Display all five categories.
3. Let the player select exactly one.
4. Assign the selected category to that player.
5. Mark it as SELECTED.
6. Keep the remaining four categories AVAILABLE.
7. Proceed to the next player.

Continue until all six players own exactly one category.

Do not permanently reserve all 30 offered categories.

Only the six categories actually selected become unavailable.

The draw must happen once per selection step.

Rerendering or refreshing the application must not generate a new set of categories.

Do not allow the tournament to proceed until all six players have selected their categories.

The host operates the interface, so no separate player accounts or devices are required.


==================================================
6. INITIAL CHALLENGER AND OPPONENT SELECTION
==================================================

Once initial category selection is complete:

1. Randomly select one of the six active players as the first challenger.
2. Display the challenger prominently.
3. Highlight all neighboring opponents on the floor.
4. Let the challenger select whom to challenge.
5. Start a best-of-three matchup.

A valid opponent must:
- Still be active in the tournament.
- Be a different player from the challenger.
- Own at least one territory adjacent to one of the challenger's territories.

Adjacency MUST use the wraparound rules from section 3.

On the initial 3x2 floor, every player must have exactly 3 possible opponents.

The challenger freely chooses among these opponents.

Once an opponent is chosen:
- Record the challenger.
- Record the defender.
- Start a new best-of-three matchup.

Challenger and defender roles remain fixed for the entire matchup.

Do not randomly reverse the roles during the matchup.


==================================================
7. BEST-OF-THREE MATCHUP RULES
==================================================

Each matchup consists of at least two and at most three duels.

The first player to win two duels wins the matchup.

The category order is FIXED.

DUEL 1:
- Use the DEFENDER'S active category.
- The DEFENDER must answer the first question.

DUEL 2:
- Use the CHALLENGER'S active category.
- The CHALLENGER must answer the first question.

These two duels must always be played.

After two duels:
- If one player won both, the matchup ends 2-0.
- If the score is 1-1, proceed to a third, deciding duel.

DUEL 3:
- Use the special five-category banning and selection process described in section 8.
- The CHALLENGER must answer the first question.

There must be no randomization of:
- Which player's category is played first.
- Who answers the first question in duel 1.
- Who answers the first question in duel 2.
- Who answers the first question in duel 3.
- Which player bans first in the deciding category selection.

These rules are deliberate and must be followed exactly.

Integrate with the existing duel engine to enforce the starting player for each duel.

The tournament system must track individual duel results and automatically determine the matchup winner.

The host should see the current matchup score throughout the matchup, for example:

Anders 1 - 1 Brian

The UI must clearly indicate:
- Challenger.
- Defender.
- Current matchup score.
- Which duel is being played.
- Current category.
- Who answers first.
- Previous duel results.

The first two categories are marked PLAYED when their respective duels are played.

The third category is marked PLAYED only if a deciding duel is actually played.


==================================================
8. THIRD-DUEL CATEGORY BANNING
==================================================

If the first two duels result in a 1-1 score, the players select the deciding category through a ban/pick process.

STEP 1:
Randomly draw 5 distinct AVAILABLE categories.

Show all 5 categories to both players.

STEP 2:
The DEFENDER bans 1 category.

There are now 4 remaining categories.

STEP 3:
The CHALLENGER bans 2 categories.

There are now 2 remaining categories.

STEP 4:
The DEFENDER chooses which of the remaining 2 categories will be played.

STEP 5:
Play the deciding duel using that category.

The CHALLENGER answers the first question.

IMPORTANT:
- The roles are never randomized.
- Defender always bans first.
- Challenger always bans two.
- Defender always makes the final selection.
- Challenger always answers first in the deciding duel.

The UI should make the ban/pick sequence obvious.

For example:
- Show all five initially.
- Visually mark banned categories.
- Indicate whose turn it is.
- Show the remaining categories.
- Highlight the selected category.
- Provide a clear action to start the deciding duel.

Only the category actually played in the deciding duel becomes PLAYED.

The four unused categories must remain AVAILABLE.

Persist the five originally drawn categories and all ban/pick decisions so that refreshing does not reset the process.

Validate the number and order of selections.

The host must not be able to skip required decisions or select an invalid category.


==================================================
9. COMPLETING A MATCHUP
==================================================

Once a player has won two duels:

1. Mark the matchup as completed.
2. Record the final score (2-0 or 2-1).
3. Record the winner and loser.
4. Mark the loser as eliminated.
5. Transfer ALL territories belonging to the loser to the winner.
6. Update tournament standings and statistics.
7. Ensure all categories used in the matchup are marked PLAYED.
8. Recalculate floor adjacency and eligible opponents.
9. Proceed to the winner's category selection, unless this was the final matchup.

The winner must NEVER inherit the loser's category.

Both players' active categories were played in the first two duels, so neither can be reused.

The winner must obtain a fresh category before another matchup.

The loser no longer owns an active category.

There must never be two active players with the same selected category.

TERRITORY TRANSFER:

The winner receives ALL territories owned by the loser.

Example:

Before:
- Anders owns 3 territories.
- Brian owns 2 territories.
- Christian owns 1 territory.

Anders defeats Brian.

After:
- Anders owns 5 territories.
- Brian is eliminated and owns 0 territories.
- Christian owns 1 territory.

All five territories belonging to Anders must be treated as a single combined ownership region when calculating whom Anders can challenge.

This includes wraparound adjacency.

The original positions of the territories do not change.

Only their ownership changes.


==================================================
10. NEW CATEGORY AFTER A VICTORY
==================================================

After winning a matchup, the winner must select a new category.

This happens before they can participate in another matchup.

The process is:

1. Draw 5 distinct AVAILABLE categories.
2. Display all five.
3. Allow the winner to select one.
4. Mark the chosen category as SELECTED and assign it to the winner.
5. Keep the remaining four AVAILABLE.

The winner now has exactly one active category.

This must happen after EVERY victory, except when the winner has won the entire tournament.

Do not allow another matchup to start until the winner has selected a new category.

Persist the draw and chosen category.

The player's previous category is not retained, because it has already been played.


==================================================
11. CONTINUE CHALLENGING OR RETURN TO FLOOR
==================================================

After choosing a new category, the matchup winner receives two options:

OPTION A: CONTINUE CHALLENGING

- The winner becomes the challenger for the next matchup.
- The winner selects another neighboring active player.
- Start a new matchup using the normal rules.

Adjacency must be calculated using the winner's CURRENT collection of territories, including wraparound edges.

OPTION B: RETURN TO FLOOR

- The winner relinquishes the challenger role.
- Randomly select a NEW challenger from the other remaining active players.
- The new challenger selects an eligible neighboring opponent.
- Continue the tournament.

When selecting a new random challenger after returning to the floor, exclude the player who just returned, provided another active player exists.

All active players other than the returning player should have equal probability of being selected.

Persist the new challenger selection.

There must be no rerolling simply by refreshing the application.

If the winner is the only remaining player, skip this choice and finish the tournament.

The application should indicate whether the current challenger was randomly selected or chose to continue after a victory.


==================================================
12. TOURNAMENT COMPLETION
==================================================

The tournament ends automatically when only one active player remains.

At this point:
- The remaining player owns all six territories.
- Declare that player the tournament champion.
- Mark the tournament as completed.
- Display a clear winner screen.
- Display the final floor.
- Display relevant statistics and matchup results.

The champion must not be asked to choose another category.

Provide a way to review the tournament history after completion.


==================================================
13. TOURNAMENT UI
==================================================

Implement the necessary screens or views to run the complete tournament.

The exact UI architecture should follow the existing application.

Required functionality:

A. TOURNAMENT SETUP
- Enter six players.
- Review tournament settings.
- Start tournament.

B. INITIAL CATEGORY SELECTION
- Display current player.
- Show five drawn categories.
- Select one.
- Advance to the next player.

C. MAIN TOURNAMENT OVERVIEW
- Show the 3x2 floor.
- Show territory ownership.
- Indicate eliminated players.
- Show the current challenger.
- Highlight eligible opponents.
- Include opponents reachable through wraparound adjacency.
- Show active categories.
- Show tournament progress.
- Provide access to standings and matchup history.

D. MATCHUP INTRODUCTION
- Display challenger and defender.
- Show each player's category.
- Explain the duel order.
- Start the first duel.

E. MATCHUP PROGRESS
- Display current score.
- Display completed duel results.
- Indicate current duel.
- Display the category.
- Indicate the player who must answer first.
- Launch the existing duel engine.

F. DECIDING CATEGORY SELECTION
- Display five categories.
- Handle defender's first ban.
- Handle challenger's two bans.
- Handle defender's final choice.
- Start the deciding duel.

G. MATCHUP RESULTS
- Display the final score.
- Display the winner.
- Display territories gained.
- Show the updated floor.

H. WINNER'S NEW CATEGORY SELECTION
- Offer five available categories.
- Require one selection.

I. CHALLENGER DECISION
- Continue challenging.
- Return to floor and select a new random challenger.

J. TOURNAMENT WINNER
- Present the champion.
- Display final results and statistics.

These do not necessarily need to be ten separate pages.

Prefer a coherent host-driven workflow that transitions between phases without excessive navigation.

The host should always know:
- What just happened.
- What happens next.
- Which player must act.
- Which action is currently required.

Keep the floor and tournament context easily accessible.

WRAPAROUND UI REQUIREMENTS:

The floor must not misleadingly imply that players can only challenge territories physically touching on screen.

When selecting an opponent:
- Highlight ALL valid opponents.
- Include wraparound neighbors.
- Allow selecting opponents on opposite board edges.
- Deduplicate opponent selection.
- Provide a clear explanation that the board wraps at its boundaries.

It is acceptable for the floor to remain a normal rectangular grid visually.

There is no need to render an actual torus.

However, the interaction must correctly implement toroidal adjacency.

Consider using a subtle visual indicator or a legend explaining that opposite edges connect.


==================================================
14. SCOREKEEPING AND STATISTICS
==================================================

Track at least:

PER PLAYER:
- Player ID and name.
- Whether the player is active or eliminated.
- Current active category, if any.
- Current territories owned.
- Number of matchups played.
- Matchups won.
- Matchups lost.
- Individual duels won.
- Individual duels lost.
- Elimination order, if eliminated.

PER MATCHUP:
- Unique matchup ID.
- Challenger.
- Defender.
- Duel categories.
- Starting player for each duel.
- Winner of each duel.
- Final matchup score.
- Matchup winner and loser.
- Territories transferred.
- Completion status.

PER TOURNAMENT:
- Tournament ID.
- Participants.
- Board dimensions.
- Floor ownership.
- Current phase.
- Current challenger.
- Current matchup, if any.
- Category availability and ownership.
- Matchup history.
- Tournament champion, if completed.

Display an appropriate standings/statistics view.

Do not confuse individual duel wins with matchup wins.

Territories owned should always be derived from or consistent with the current floor state.

Where useful, display:
- Total territories controlled.
- Matchup win rate.
- Duel win rate.
- Most consecutive matchup wins.
- Total territories captured.

Avoid unnecessary statistics that complicate implementation without adding value.


==================================================
15. PERSISTENCE AND RESUMING
==================================================

This is essential.

The tournament must survive application restarts, page refreshes, or accidental navigation.

Persist all tournament state, including:
- Players.
- Floor.
- Initial category selection order.
- Category draws and selections.
- Category lifecycle states.
- Current challenger.
- Current matchup.
- Completed duel results.
- Current ban/pick stage.
- Completed matchups.
- Eliminations.
- Winner decisions.
- Tournament status.

The host must be able to resume a tournament from the correct stage.

For example:
- If the application closes during initial category selection, resume at the correct player with the same offered categories.
- If it closes between duel 1 and duel 2, resume with the correct score and next duel.
- If it closes during the third-duel banning process, retain all completed bans.
- If it closes after a victory but before the winner selects a new category, resume at category selection.
- If it closes before the winner chooses to continue or return, resume at that decision.

Use the existing application's persistence approach if suitable.

If there is no suitable persistence solution, introduce one consistent with the architecture.

Avoid partially committed tournament state.

Important state transitions should be atomic or otherwise protected against inconsistent updates.

Reopening a screen or clicking an action twice must not:
- Record a duel result twice.
- Transfer territories twice.
- Eliminate multiple players accidentally.
- Draw new categories unexpectedly.
- Award the same category to multiple players.

Random draws should be generated once and persisted.


==================================================
16. INTEGRATION WITH THE EXISTING DUEL ENGINE
==================================================

The application already contains working logic for individual duels.

Inspect it carefully and reuse as much as possible.

The tournament system should be responsible for:
- Deciding which two players participate.
- Choosing the category.
- Setting the starting player.
- Launching the duel.
- Receiving the result.
- Recording that result.
- Progressing the matchup.
- Progressing the tournament.

The existing duel engine should remain responsible for:
- Running the actual duel.
- Question/game presentation.
- Timing and interaction mechanics.
- Determining the individual duel winner.

If the duel engine does not currently support explicitly specifying the starting player, extend its interface to support this.

Prefer a clean integration boundary with inputs such as:
- Player identifiers.
- Category identifier.
- Starting player identifier.
- Tournament/matchup context, where relevant.

And a result containing:
- Duel winner.
- Relevant duel metadata.

Adapt these conceptual inputs/outputs to the existing architecture rather than forcing a particular interface design.

Do not duplicate existing duel logic.

Preserve the existing standalone duel functionality.

A duel should work as before when launched outside a tournament.

If the existing duel engine can be interrupted and resumed, integrate with that capability.

Otherwise, ensure the tournament can safely recover without incorrectly recording an unfinished duel as completed.


==================================================
17. CORRECTIONS AND HOST CONTROLS
==================================================

The host must have a safe way to correct mistakes.

At minimum, support:
- Undoing or correcting the latest recorded duel result.
- Reviewing completed matchup results.
- Resuming an unfinished tournament.
- Restarting or deleting a tournament with confirmation.

Undo/correction must correctly restore dependent state.

For example:
- The matchup score must be recalculated.
- A wrongly completed matchup must no longer count as completed.
- Territory ownership and elimination status must be restored if necessary.
- Category states and pending selections must remain consistent.
- Downstream actions invalidated by a correction must be rolled back safely.

Do not allow arbitrary edits that silently corrupt tournament history.

Choose an appropriate strategy, such as reversible state transitions, snapshots, or event-based history, according to the existing architecture.

Confirm destructive actions.


==================================================
18. VALIDATION AND EDGE CASES
==================================================

Handle at least the following:

TOURNAMENT SETUP:
- Fewer than six valid participant names.
- Duplicate participant identities.
- Not enough available categories for a five-category draw.
- Invalid tournament state.

CATEGORY HANDLING:
- A category selected by another active player.
- A category that has already been played.
- Attempting to play a category twice.
- Insufficient available categories for a draw.
- Attempting to change an already confirmed category selection without an appropriate correction operation.

FLOOR:
- Invalid territory coordinates.
- A territory with no owner.
- Duplicate territory ownership.
- An invalid or non-adjacent challenge.
- An eliminated player being selected.
- Duplicate opponents caused by multiple adjacent territories.
- Duplicate neighbors caused by wrapping on small boards.
- Incorrect edge or corner adjacency.
- Failure to recalculate adjacency after capturing territories.
- Attempting to challenge yourself.

MATCHUPS:
- Attempting a duel without a valid active category.
- Incorrect number of bans.
- Banning the same category more than once.
- Selecting a banned category.
- Trying to start duel 3 when one player already won 2-0.
- Attempting another matchup before the winner has selected a new category.
- Attempting to transfer territories multiple times.
- Starting another matchup after the tournament has finished.

PERSISTENCE:
- Application refresh during any tournament phase.
- Duplicate action submissions.
- Partially completed state transitions.
- Restoring a tournament with an in-progress duel.

Validate category availability during tournament setup and before each draw.

Do not silently relax the five-category rule if insufficient categories are available.

Instead, prevent invalid progression and show a useful host-facing explanation.

Make sure every valid tournament state has a defined next action.

CATEGORY POOL CAPACITY:

A complete six-player tournament can require:
- Six initial selected categories.
- Four replacement categories for winners.
- Up to five additional categories for deciding duels.

Because every category offer requires five AVAILABLE categories, the pool needs sufficient additional capacity beyond just the number of categories eventually played.

For this exact six-player format, require at least 19 distinct eligible categories at tournament setup to guarantee that all five-category draws remain possible, even if every matchup reaches a third duel.

If the application has category filtering or other constraints, account for those when validating the available pool.

Do not allow a tournament to start if it cannot be completed under the defined category rules.


==================================================
19. TESTING
==================================================

Add automated tests following the project's existing testing conventions.

Test at least:

CATEGORY SELECTION:
- Five distinct categories are offered.
- Selected categories become unavailable.
- Unselected categories remain available.
- Previously offered but unselected categories can be offered again.
- Played categories cannot be reused.

MATCHUP RULES:
- Defender's category is first.
- Defender answers first in duel 1.
- Challenger's category is second.
- Challenger answers first in duel 2.
- A 2-0 result ends the matchup.
- A 1-1 result triggers deciding category selection.

DECIDING DUEL:
- Defender bans one.
- Challenger bans two.
- Defender selects from the remaining two.
- Challenger answers first.
- Only the chosen deciding category is consumed.

FLOOR ADJACENCY:

Implement comprehensive unit tests for wraparound adjacency.

For a 4x4 board:

Neighbors of [0,0] must be exactly:
- [1,0]
- [3,0]
- [0,1]
- [0,3]

Neighbors of [3,3] must be exactly:
- [2,3]
- [0,3]
- [3,2]
- [3,0]

Neighbors of [1,1] must be exactly:
- [0,1]
- [2,1]
- [1,0]
- [1,2]

Each position must have 4 distinct neighbors.

For the actual 3x2 board:

Neighbors of [0,0] must be exactly:
- [1,0]
- [2,0]
- [0,1]

Neighbors of [2,1] must be exactly:
- [1,1]
- [0,1]
- [2,0]

Every position must have exactly 3 distinct neighbors.

Also test:
- No diagonal adjacency.
- No self-adjacency.
- No duplicate neighbors.
- All adjacency relationships are symmetric.
- All six initial players have equal numbers of eligible opponents.
- Multi-territory players can challenge through any owned territory.
- Opponents are not duplicated if several territories are adjacent.
- Adjacency remains correct after mergers and eliminations.
- The UI's eligible opponent list matches domain-level adjacency calculations.

TERRITORY OWNERSHIP:
- Winner receives all loser's territories.
- Loser is eliminated.
- Territory ownership remains valid.
- All six territories always have exactly one owner.
- The winner can use newly acquired territories to access new opponents.
- Wraparound adjacency still works after territory transfers.

PROGRESSION:
- Winner selects a fresh category.
- Winner may continue challenging.
- Winner may return to floor.
- New challenger selection excludes the returning player.
- The tournament ends when one player remains.

PERSISTENCE:
- State can be restored during every major phase.
- Random category offers remain stable after restoration.
- Random challenger selections remain stable after restoration.
- No action can accidentally be committed twice.
- Undo/correction restores consistent tournament state.

Add an integration test or equivalent validation that runs a complete six-player tournament from initial setup to champion declaration.

A completed tournament should:
- Have exactly five matchups.
- Eliminate exactly five players.
- Have exactly one champion.
- Leave all six territories owned by the champion.
- Never reuse a played category.
- Respect wraparound adjacency in every challenge.

Include at least one full tournament integration scenario where a player challenges an opponent through a wrapped board edge.

Also test both:
- A tournament where matchups end 2-0.
- A tournament where every matchup reaches a third duel.


==================================================
20. IMPLEMENTATION EXPECTATIONS
==================================================

Before implementing:

1. Inspect the existing project structure.
2. Understand the duel engine's inputs, outputs, and lifecycle.
3. Identify existing category handling and persistence.
4. Identify relevant UI and state management patterns.
5. Design the tournament domain and state transitions.
6. Identify how starting players can be passed to the duel engine.
7. Design the wraparound adjacency logic as a reusable, testable domain function.

Then implement the complete feature.

ARCHITECTURAL EXPECTATIONS:

- Keep tournament orchestration separate from duel execution.
- Centralize tournament rules and state transitions.
- Do not scatter rule logic across UI components.
- Implement board adjacency independently of UI layout.
- Use modular adjacency logic based on board dimensions and territory ownership.
- Make the tournament state serializable and recoverable.
- Use stable identifiers.
- Keep the domain model extensible for future tournament formats.
- Avoid unnecessary abstractions and overengineering.
- Follow the existing application's patterns.
- Make the UI suitable for a single host operating the game in front of six participants.
- Preserve backwards compatibility with standalone duels.

The tournament should be completely runnable through the UI.

Do not stop after adding domain models or backend services.

Implement the full user-facing workflow and integrate it with the existing duel engine.

Where implementation details are not specified, use the existing codebase's conventions and make reasonable decisions.

Do not change the explicitly defined game rules.

IMPORTANT GAME RULES THAT MUST NOT BE CHANGED:

1. Six players on a 3x2 floor.
2. Orthogonal adjacency with wrapping on both axes.
3. Every player initially has 3 distinct neighboring opponents.
4. Diagonal adjacency is not permitted.
5. Players choose their initial category from five random available categories.
6. Categories may only be played once.
7. Matchups are best-of-three.
8. Defender's category is played first, with defender answering first.
9. Challenger's category is played second, with challenger answering first.
10. At 1-1, five available categories are offered for the deciding duel.
11. Defender bans one category.
12. Challenger bans two categories.
13. Defender selects one of the two remaining categories.
14. Challenger answers first in the deciding duel.
15. Only played categories are consumed permanently.
16. The matchup winner receives all territories owned by the loser.
17. The loser is eliminated.
18. The winner selects a fresh category from five random available categories.
19. The winner can continue challenging or return to the floor.
20. Returning to the floor triggers random selection of another active player as challenger.
21. The last remaining player wins the tournament.

When finished, provide:
1. A summary of the implementation.
2. Major architectural decisions.
3. Files/components added or modified.
4. Tests added and their results.
5. Any limitations or remaining issues.

FINAL ACCEPTANCE CRITERIA:

The completed application must allow me to:

- Launch the application.
- Create a six-player Mini Floor tournament.
- Randomly assign starting territories.
- Let each player select an initial category.
- Select the initial challenger.
- Highlight all eligible neighboring opponents, including across opposite edges.
- Choose a valid opponent.
- Run the complete best-of-three matchup using the existing duel engine.
- Enforce the correct starting player in every duel.
- Handle the deciding category ban/pick process.
- Automatically record matchup results.
- Transfer all losing territories.
- Update and display the floor.
- Let the winner choose a fresh category.
- Let the winner continue or return to the floor.
- Continue until only one player remains.
- Display the final champion and tournament statistics.
- Resume an interrupted tournament without losing progress.
- Correct an accidentally recorded duel result without corrupting tournament state.

The entire tournament must be playable without manually managing scores, territories, categories, or tournament progression outside the application.
