# TheFloor.TrackerSupreme

TheFloor.TrackerSupreme is a locally hosted quiz-duel application for private events. It includes a host dashboard, a TV audience display, and a shared library of editable quiz categories.

Development uses the .NET 10 SDK and Node.js 24. See the [development environment](docs/environment.md), [project specification](docs/specification.md), and [implementation progress](docs/progress.md).

## Build and run

```sh
dotnet publish src/TheFloor.Server -c Release -o artifacts/app
cd artifacts/app
dotnet TheFloor.Server.dll --urls http://localhost:5080
```

Open [the local application](http://localhost:5080). The published application serves its compiled frontend from the same ASP.NET Core process. See the [deployment guide](docs/deployment.md) for offline installation and using a second display device.

## Prepare and run a duel

Open **Category sets** and create a set. In **Categories**, create a category, add questions, and select the sets that should share it. Edit a shared category once to update every set that references it. Choose **Make active** on the set you want to use.

Open **Host**, enter the contestants, choose a category, and prepare the duel. Open **Audience display** in a separate window and move it to the TV or projector. Start the duel when the contestants are ready. Only the host can preview the prepared question; the audience sees it when the clock starts. Pass hides the question for the configured penalty duration while the active clock keeps running. Use **End duel and return to setup** to abandon an unfinished duel without declaring a winner; confirm the action to return both screens to setup/waiting.

| Key | Action |
| --- | --- |
| Space | Correct |
| P | Pass |
| Esc | Pause |
| R | Resume |
| U | Undo the last Correct or Pass and pause |

Manage text, image, and audio questions from the category editor. The [content format guide](docs/content-format.md) explains the external data directory and manual YAML editing.

Use **Export set ZIP** to share an entire event, or **Export category ZIP** for a single category and its media. Import a package from the matching editor. The [content format guide](docs/content-format.md#backup-and-transfer) explains full backups and restores.

To clear the entire category library, open **Categories → Reset all categories** and confirm the listed categories and affected sets. This removes all categories, questions and media from the library and empties the sets. Sets, the active-set selection and existing duel snapshots are retained. Removed folders and original affected set definitions are backed up under `backups/category-reset-…` in your data directory; the success message shows the backup location. See the [restore instructions](docs/content-format.md#category-reset-backups).

## Rehearse and pair displays

Open **Rehearsal** from the host dashboard to check the active set and preview a question on a separate device. The rehearsal link has its own volume and fullscreen controls. End the rehearsal to clear that preview.

Under **Pair displays and choose audio**, copy the display link or scan the QR code. For another device, use the server’s LAN address as described in the [deployment guide](docs/deployment.md#start-the-server). Match the display code at the top of the audience screen to the **Audio output display** list. Automatic mode plays audio on one connected display. If a specifically selected display disconnects, choose another display or return to automatic mode.

Open **Duel presets** in setup to save or apply clock, pass penalty, starting-player, and question-order settings. Presets are stored in the host browser.

## Optional demo content

From `artifacts/app`, run `dotnet TheFloor.Server.dll --load-demo` with the server stopped. This loads the bundled examples into the configured external data directory and exits. Start the app and select **Demo Set A** or **Demo Set B** under **Category sets**. The demo sets share General Knowledge and Demo Image Recognition. The loader keeps existing content and reports UUID conflicts.

The [examples](examples/demo) contain original dummy questions and color swatches for development. Real event content belongs in the external data directory.

## Development

Run each process in its own terminal from the repository root.

```sh
dotnet run --project src/TheFloor.Server -- --urls http://localhost:5080
```

```sh
npm --prefix src/web ci
npm --prefix src/web run dev
```

Use the Vite URL displayed in the second terminal. Run `dotnet test`, `npm --prefix src/web run typecheck`, and `npm --prefix src/web test` for local checks. After publishing, run `npx playwright install chromium` and `npm run test:e2e` from `src/web`.

## Run a Mini Floor tournament

Open **Mini Floor** (`/tournaments`). Choose the number of participants, board columns and rows, then enter distinct names and clock/pass settings. The default is six players on a 3×2 board; setup supports 2–256 players and 1–64 columns/rows, with at least one cell per player. The host is separate. Your active set needs **at least `3 × players + 1` distinct playable categories** (19 for six, 22 for seven), each with an enabled question and valid media. Setup shows the required capacity and a board preview.

Creation freezes category content/media and randomizes starting cells and category selection order. Every player receives one starting cell. Any spare cell next to owned territory goes randomly to one of its distinct neighboring owners; newly assigned cells help fill further gaps until all cells have owners. These assignments are saved and survive refresh/restart. The floor wraps on both axes: opposite edges connect, diagonals do not. Opponents are derived from all currently owned cells, so extra territory can change the initial number of opponents. Click a highlighted floor cell or eligible opponent button to challenge.

Each matchup uses the existing duel controls and audience display. The defender’s category is first, with the defender answering first. The challenger’s category is second, with the challenger first. At 1–1, the defender bans one of five categories, the challenger bans two, and the defender chooses from the remaining two; the challenger starts that deciding duel. The server records results automatically, eliminates the loser and transfers all their territories.

After each nonfinal victory, select a fresh category, then **Continue challenging** or **Return to floor**. Return randomly chooses another active player. After `players − 1` matchups, the champion owns the entire board. Standings and matchup history remain available for review. To run a standalone duel afterward, open **Host → Return to setup**; the completed tournament history is retained. Categories are never replayed within a tournament; unchosen and banned categories remain available. A new tournament can reuse the library.

Return to **Mini Floor** to resume an interrupted tournament. Refreshing preserves draws, assignments, scores, bans and decisions. A server restart restores an unfinished duel paused, with no offline time charged. Tournament state and frozen content live under the configured external data directory (`state/app.db` and `state/snapshots/`); back up that directory with the server stopped.

Under **Tournament controls and recovery**, **Undo latest duel result** restores the state before that result and rolls back all later choices, including territory transfers, eliminations, replacement picks and an unrecorded downstream duel. It prepares the corrected duel again with the same category, questions and starting player and fresh clocks. Replay it or use **Duel options → Declare … winner** to record the correction. Undo, restart and deletion require confirmation. Pause a live tournament duel before navigating away; standalone duel setup is available again after the tournament completes or is deleted.

Open **Audience display** (`/display`) before creation. It now follows the whole tournament: the floor, current player, legal opponents, category offers, bans/pick, matchup results, replacement picks, continue/return and champion/standings. It switches to the existing question-and-clock view during duels and back to tournament flow afterward. Audience screens are read-only and receive no host answers, alternatives, notes or question decks. Presentation survives reconnect/restart. To present a resumed or completed tournament explicitly, use **Tournament controls and recovery → Show tournament on audience display**. Starting a standalone duel restores the standalone display.

The [follow-up requirements](docs/mini-floor-extensions.md) and [approved amendments](docs/requirements-amendments.md) define configurable boards and audience flow. The full [Mini Floor specification](docs/mini-floor-specification.md) and [verification checklist](docs/mini-floor-checklist.md) describe the rules and acceptance evidence.
