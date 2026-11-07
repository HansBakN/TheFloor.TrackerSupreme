# Local deployment

## Build on the development machine

Install the .NET 10 SDK and Node.js 24. From the repository root, publish the server and bundled Vue application together.

```sh
dotnet publish src/TheFloor.Server -c Release -o artifacts/app
```

Copy the entire `artifacts/app` directory to the machine running the event. A framework-dependent build requires the ASP.NET Core 10 runtime and a current browser on that machine. The build downloads dependencies. The published application serves its assets locally and runs offline.

To publish with the runtime included, select the runtime identifier for the destination. This example targets Apple Silicon. Use `win-x64` or `linux-x64` for the corresponding 64-bit Windows or Linux target.

```sh
dotnet publish src/TheFloor.Server -c Release -r osx-arm64 --self-contained true -o artifacts/app
```

Launch the published `TheFloor.Server` executable, or `TheFloor.Server.exe` on Windows, for a self-contained build.

## Start the server

From the published directory:

```sh
dotnet TheFloor.Server.dll --urls http://localhost:5080
```

Open `http://localhost:5080/host` in a browser on the server computer. Open `http://localhost:5080/display` on a second browser window and move it to the connected TV or projector. Use the browser's fullscreen control for the audience window.

For a second device on the same private network, bind the server to its LAN interface with `--urls http://0.0.0.0:5080`. Keep the host dashboard on `localhost`. Open `http://<server-ip>:5080/display` on the audience device. The display can connect over the local network while the internet is disconnected.

## Data and recovery

Set `THEFLOOR_DATA_DIR` before starting the server to choose a persistent directory outside the checkout. The platform defaults and folder structure are in the [content format guide](content-format.md). Stop the server before copying the data directory for backup or transfer.

The server restores an unfinished duel in the Paused state. Confirm the question and clocks on the dashboard, then choose **Resume**. If the process was killed abruptly, the clocks restore from the last saved checkpoint. The audience display reconnects to the same server address.

Use the same server process for all views. Start a separate instance with a different external data directory and port when testing or rehearsing.

## Tournament recovery and backup

Mini Floor uses the same external data root as standalone duels. Include `state/app.db` and the complete `state/snapshots/` directory when backing up or moving tournaments; stop the server before copying. Category YAML alone does not contain tournament progress. The application upgrades existing SQLite installations in place by adding tournament tables.

Open **Mini Floor** to find unfinished tournaments and completed history. An interrupted live duel restores paused with its saved question order, media, clocks and explicit starting player. Resume it through Mini Floor. Category offers, bans and challenger decisions do not reroll on restart. A frozen tournament pool remains usable even if its source category set later changes.

The minimum pool is `3 × players + 1` distinct eligible categories: 19 for six players or 22 for seven. The bundled small demo sets are intentionally insufficient: add or import event categories before starting Mini Floor. No online service or separate participant device is required.


The existing `/display` URL shows the whole tournament between duels. No new audience account/device setup is needed. Display selection persists in SQLite alongside tournament state and is restored after a server restart; the live duel restores paused as before. Use the tournament's **Show tournament on audience display** action when presenting completed history. New standalone duels automatically restore standalone display behavior. The public projection contains no host answers or question decks. Board dimensions and spare-cell allocations are included in the same backup/recovery process as tournament progress.
