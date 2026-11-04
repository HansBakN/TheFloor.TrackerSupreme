# Development environment

Verified on November 4, 2026, on macOS 15.6.1 (24G90), Apple Silicon (arm64). This repository contains setup files. Application implementation is reserved for a separate session.

## Tools and smoke tests

| Tool | Version and verification |
| --- | --- |
| Git | Apple Git 2.39.5 (Apple Git-154). Initialized the checkout, committed the setup files, and pushed over HTTPS. |
| GitHub CLI | 2.65.0. Authenticated as `HansBakN` through the macOS keyring. The checkout uses the CLI as its HTTPS credential helper. |
| Homebrew | 7.0.9. Installed `node@24` alongside the pre-existing Node 23 installation. |
| Node.js and npm | Node.js 24.21.0 LTS and npm 11.19.0. Installed dependencies from npm and built a temporary frontend. |
| .NET and ASP.NET Core | .NET SDK 10.0.401 and runtime 10.0.12. Built and ran a temporary `net10.0` ASP.NET Core server on loopback. |
| SignalR | ASP.NET Core 10.0.12. A hub negotiation returned a connection ID and advertised WebSockets. |
| EF Core and SQLite | NuGet restored `Microsoft.EntityFrameworkCore.Sqlite` 10.0.12. The server created a SQLite database, saved a row, and returned it through HTTP. The macOS SQLite CLI (3.43.2) read the same row. |
| Frontend | Vite 8.3.5, React 19.3.1, TypeScript 7.0.2, Tailwind CSS and its Vite plugin 4.3.4. Type checking and the production build passed, and the compiled CSS contained the Tailwind utility used in the test. |
| Playwright | `@playwright/test` 1.64.1 with Playwright-managed Chromium 156.0.8078.4. The interactive test below passed. |

Use the Homebrew LTS runtime in each development shell:

```sh
export PATH="/opt/homebrew/opt/node@24/bin:$PATH"
node --version
npm --version
dotnet --version
```

Frontend packages belong in the future frontend project's local dependencies. The smoke-test package versions above describe the tested environment.

## GitHub workflow

- [HansBakN/TheFloor.TrackerSupreme](https://github.com/HansBakN/TheFloor.TrackerSupreme) is public. An anonymous API request confirmed visibility and the default branch `main`.
- The authenticated account has repository admin access. Public repository creation, HTTPS push to `main`, workflow-file publication, issue creation and labeling, label creation and editing, and a repository settings update were exercised.
- Issues, squash merging, and automatic deletion of merged branches are enabled. At setup, `main` had no active ruleset rules or classic branch protection.
- Labels include `feature`, `bug`, `enhancement`, `testing`, `documentation`, `infrastructure`, `priority:high`, and `blocked`.
- The CLI pushed a feature branch with Conventional Commits and opened [PR #2](https://github.com/HansBakN/TheFloor.TrackerSupreme/pull/2), which was accessible through the unauthenticated GitHub API. After its check passed, the CLI squash-merged it as `4b7d121` without a review or admin override. GitHub automatically closed [issue #1](https://github.com/HansBakN/TheFloor.TrackerSupreme/issues/1). The CLI deleted the remote verification branch, verified the merged files on `main`, and removed the local verification branch.

For subsequent work, start from current `main`, create a feature branch, and use Conventional Commit messages. Push the branch and open a PR targeting `main`. Use `Closes #N` in the PR description when it resolves an issue. Inspect the diff and require a successful validation check before squashing the PR. Delete the feature branch and fast-forward local `main`.

## GitHub Actions

[Repository validation](https://github.com/HansBakN/TheFloor.TrackerSupreme/actions/workflows/repository-validation.yml) runs on pushes and pull requests targeting `main`. It checks that the README, MIT license, and environment guide are present and nonempty, and that the README links to the guide. The workflow uses a pinned checkout action and a read-only repository token. The repository default token is read-only, and workflows cannot approve pull requests.

GitHub CLI confirmed that the Required documentation job succeeded on the [initial push to main](https://github.com/HansBakN/TheFloor.TrackerSupreme/actions/runs/41881025193) and the [final revision of PR #2](https://github.com/HansBakN/TheFloor.TrackerSupreme/actions/runs/41881600388).

## Browser testing

The desktop app's [browser documentation](https://learn.chatgpt.com/docs/browser) describes automated interaction with the built-in browser as Computer Use. This session used the Playwright fallback under the Computer Use restriction.

Playwright opened a form on a temporary Node HTTP server bound to `127.0.0.1`, confirmed its initial state, filled the text input, clicked Submit, and asserted the updated status text in Chromium. The browser ran headless with Chromium's sandbox enabled. The temporary backend, frontend, database, and test files were removed. The Chromium download remains in the standard Playwright browser cache.

## Instructions for the next Codex session

1. Select Node 24 using the command above and verify the .NET 10 SDK.
2. Run `gh auth status --hostname github.com` and `gh repo view` with GitHub network access before relying on remote operations.
3. Use the feature-branch workflow and keep credentials, local settings, databases, and generated files out of Git.
4. Install Playwright in the frontend project, run `npx playwright install chromium` for the selected Playwright version, and exercise user flows against a loopback server.
5. Extend the documentation check with .NET and frontend build and test jobs once implementation begins.

The managed shell blocked external DNS and opening a loopback listener under its default sandbox. Narrow command-level approval was used for network calls, Git metadata writes, package and browser-cache installation, and loopback smoke tests. In a future session these operations depend on that session's tool policy and the continued validity of the GitHub keyring login.
