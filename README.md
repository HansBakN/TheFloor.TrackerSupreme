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
