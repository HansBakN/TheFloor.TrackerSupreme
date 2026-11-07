# Architecture

The ASP.NET Core process owns application data and serves the compiled Vue SPA. In development, Vite proxies HTTP and WebSocket traffic to the backend. Production ships the frontend, fonts supplied by the local system, and optional example files with the server.

## Content and operational state

The external data root contains independent category directories and ordered set definitions. Category and question UUIDs are stable across renames. The UI edits typed definitions, the content service validates them and verifies the current revision, and a same-directory temporary file replaces a saved definition atomically. A filesystem watcher debounces external edits. The library retains its last valid in-memory definition when a file becomes invalid and returns diagnostics with the library view.

SQLite through EF Core stores active-set selection, the current duel, and completed duel records. It does not store editable library content. A duel copies ordered enabled questions and their media into a session snapshot. An archive import validates its definitions and media before staging files for installation. Existing IDs are reused for identical content or remapped by explicit request.

Set import and shared category deletion stage changes under `state` and persist a recovery plan before publishing any step. A scan replays pending steps under the library lock. Each step recognizes an already applied result or verifies the expected revision before changing a file. A conflicting external edit pauses recovery and surfaces the affected path.

## Duel timing and synchronization

The domain rules implement Ready, Running, Paused, and Finished. A per-process lock serializes transitions and the timer service calculates elapsed time using `TimeProvider`. The server checks the expected version and command UUID before applying a transition. Expiration takes precedence over late commands. The background ticker writes periodic checkpoints and persists the final clock sample on graceful shutdown. The question deck is stored separately and only rewritten when the deck changes. Startup reads legacy inline decks and upgrades the current duel without resetting its clocks. Unfinished duels restore paused.

The HTTP command endpoint writes committed state before broadcasting via SignalR. The hub uses separate host and audience groups. Host-only HTTP routes and hub connections require a local host cookie. The audience receives the public question, player names, clocks, status, and winner. Its media route serves only the current visible question's snapshot asset. Prepared questions stay host-only until Start, including after recovery. Pass stores a remaining lockout duration alongside the active clock; both consume the same elapsed interval. The next question and answer controls unlock only after the server completes the interval. A version change immediately persists and broadcasts that transition. Pause and recovery freeze both durations. A host-only media route supports prepared previews. Returning an unfinished duel to setup archives it without a winner and clears the current session in one transaction. See the [approved requirement amendments](requirements-amendments.md).

Pinia holds the live projection. Vue renders tenths from the latest server sample plus elapsed browser animation time on the active clock. Clients fetch authoritative state after reconnect or visibility changes, and clean up subscriptions and animation frames when leaving the duel views.


## Event preparation

The host can preflight the active set and publish a selected question to a separate rehearsal URL. Its in-memory preview holds only the public prompt and a copy of any media. The preview changes independently of the live duel and is cleared when the host ends rehearsal or the server restarts.

The audience registry selects one audio output from connected displays. The host can override that selection. SignalR sends the selected display ID with the public projection, and other audience tabs omit audio controls. Pairing QR codes are encoded in the browser using locally bundled assets. Duel presets store four setup fields in the host browser.

## Mini Floor tournaments

`Tournaments/` separates orchestration from `Duels/`. `TournamentRules` owns the serializable phase machine, five-category offers and lifecycle, fixed matchup order/starters, ban sequence, elimination and progression. `FloorAdjacency` uses dimension-independent modulo neighbors and derives distinct eligible players from current floor ownership. The public host view supplies eligible IDs; Vue uses them directly for both floor highlighting and the opponent list. Standings derive from current floor ownership and recorded matchup/duel history.

`TournamentService` serializes mutations through the duel service's reentrant execution gate, so tournament commands and timer completion cannot race. Commands carry a unique ID and expected tournament version. SQLite `Tournaments` stores the operational payload and latest result checkpoint; `TournamentDecks` stores immutable category definitions independently of checkpoints. Initialization adds both tables to existing installations without recreating their database. The active set supplies a pool of at least 19 eligible categories. Creation copies enabled questions and media once using the duel snapshot mechanism; later library edits, active-set switches or resets do not alter the tournament pool.

A prepared tournament duel uses the existing `DuelSession`, engine, snapshots, host controls, SignalR projections and audience display. Challenger is engine player A and defender is engine player B; the starter is explicitly B for duel 1 and A for duels 2/3. Tournament/matchup IDs persist on the duel. Preparation and its tournament transition share one EF `SaveChanges` transaction. A category becomes Played when Start/Resume first runs the duel or the host declares its winner, rather than merely preparing it. `TournamentPersistence` runs inside duel persistence: natural expiry and manual completion update the score, phase, elimination and territory transfer in the same transaction as the finished duel. Failed commits change neither cached duel state nor durable tournament state.

Before each recorded result, the same transaction saves a checkpoint of tournament state and the completed duel snapshot. Undo restores it, retires an invalidated downstream duel and clears all later choices atomically. The corrected duel receives a new ID and fresh clocks while retaining category/questions/fixed starter. A later correction cannot replay stale HTTP commands. Tournament restart uses the original frozen pool and settings with new assignments, identities and pick order; deletion removes its tournament record/deck and retires its attached duel. Snapshot media is retained because archived duel records can still reference it.

The `/tournaments/:id?` host view uses a dedicated typed composable with versioned requests and lifecycle-cleaned polling. No category/adjacency/matchup rules are independently reimplemented in components. `TournamentFloor` renders authoritative target IDs. During Playing the existing `Host` is embedded. Recorded tournament duels reject independent return/restart commands; changing a recorded result requires tournament correction. Host-only endpoints under `/api/host/tournaments` inherit existing local-host access controls and are never exposed through audience payloads.
