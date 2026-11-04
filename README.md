# TheFloor.TrackerSupreme

TheFloor.TrackerSupreme is a locally hosted quiz-duel application for private events. The specification describes a host dashboard, a TV audience display, and a shared library of editable quiz categories.

Development uses the .NET 10 SDK and Node.js 24. See the [development environment](docs/environment.md), [project specification](docs/specification.md), and [implementation progress](docs/progress.md).

## Build and run the current development version

```sh
dotnet publish src/TheFloor.Server -c Release -o artifacts/app
cd artifacts/app
dotnet TheFloor.Server.dll --urls http://localhost:5080
```

Open [the local application](http://localhost:5080). The published application serves its compiled frontend from the same ASP.NET Core process.

## Prepare and run a duel

Open **Category sets** and create a set. In **Categories**, create a category, add questions, and select the sets that should share it. Edit a shared category once to update every set that references it. Choose **Make active** on the set you want to use.

Open **Host**, enter the contestants, choose a category, and prepare the duel. Open **Audience display** in a separate window and move it to the TV or projector. Start the duel when the contestants are ready.

| Key | Action |
| --- | --- |
| Space | Correct |
| P | Pass |
| Esc | Pause |
| R | Resume |
| U | Undo the last Correct or Pass and pause |

Manage text, image, and audio questions from the category editor. The [content format guide](docs/content-format.md) explains the external data directory and manual YAML editing.

Use **Export set ZIP** to share an entire event, or **Export category ZIP** for a single category and its media. Import a package from the matching editor. The [content format guide](docs/content-format.md#backup-and-transfer) explains full backups and restores.

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
