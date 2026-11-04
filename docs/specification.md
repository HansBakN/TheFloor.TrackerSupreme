# TheFloor.TrackerSupreme — Full Autonomous Implementation Specification

You are an experienced, autonomous senior full-stack software engineer responsible for implementing a complete, polished, maintainable web application named **TheFloor.TrackerSupreme**.

The application is a locally hosted quiz game inspired by the head-to-head duel mechanic of the television show *The Floor*.

The primary experience is two contestants answering questions against independent countdown clocks. The application must provide a dedicated host interface, an audience display, and a comprehensive system for creating, organizing, sharing, importing, and exporting custom quiz content.

The application will primarily be used at private events, including parties, birthdays, and gatherings with friends.

**Your objective is to deliver the complete working application, not merely scaffolding, an implementation plan, or a prototype.**

You are responsible for the full engineering lifecycle: architecture, implementation, testing, documentation, GitHub Issues, commits, pull requests, integration, and final verification.

Work autonomously wherever possible. The user will review the finished product but is not expected to participate in routine development decisions.

---

# 1. Existing Repository and Environment

A previous Codex session has already prepared the development environment and created the GitHub repository.

**Repository:** `TheFloor.TrackerSupreme`

The repository is public and should already contain the initial project documentation, development environment verification results, and GitHub workflow configuration.

## Initial actions

Before implementing anything:

1. Locate and open the existing local repository.
2. Verify that the Git remote points to `TheFloor.TrackerSupreme`.
3. Identify the repository owner from the Git remote or authenticated GitHub CLI.
4. Read the existing `README.md`.
5. Read `docs/environment.md`.
6. Inspect existing repository branches, GitHub Issues, GitHub Actions workflows, and repository settings.
7. Verify GitHub authentication and permissions.
8. Verify that the required development tools are available.
9. Identify any outstanding blockers from the setup session.
10. Review the repository's existing conventions and documentation.

Reuse the environment and GitHub configuration prepared during the previous session.

**Do not create a new repository, overwrite existing repository history, or repeat completed setup work unnecessarily.**

The previous setup session may have used React for frontend smoke tests. That does not change the requirements: **the actual application must use Vue 3, not React.**

Install or update missing development dependencies automatically where permitted.

Request user intervention only when authentication, macOS permissions, or account authorization makes it unavoidable.

Do not request general Computer Use access to the macOS desktop.

Use the built-in ChatGPT browser where available and Playwright for automated browser testing.

---

# 2. Technology Stack

Use the following technologies.

| Layer | Technology |
|---|---|
| Backend | ASP.NET Core / .NET 10 |
| Backend language | C# |
| Frontend | Vue 3 |
| Frontend language | TypeScript |
| Vue component pattern | Composition API with `script setup` |
| Build tooling | Vite |
| Routing | Vue Router |
| Shared frontend state | Pinia where appropriate |
| Styling | Tailwind CSS |
| Real-time communication | ASP.NET Core SignalR |
| Application state database | SQLite |
| ORM | Entity Framework Core |
| Category and set definitions | YAML |
| Media storage | Local filesystem |
| Backend testing | xUnit |
| Frontend testing | Vitest + Vue Test Utils |
| Browser testing | Playwright |
| Hosting | Locally hosted ASP.NET Core application |

Use stable, compatible versions of libraries.

Use strict TypeScript configuration and strongly typed API contracts.

Prefer Vue Single-File Components, reusable composables, and clearly separated services.

Avoid unnecessary frontend or backend abstractions.

## Offline operation

The finished application must:

- Run entirely offline after installation.
- Require no external APIs or cloud services.
- Store all user content locally.
- Serve all frontend assets locally.
- Support locally stored images and audio.
- Work on Windows, macOS, and Linux.

Development may require downloading packages and dependencies.

The production ASP.NET Core application must serve the compiled Vue frontend, so the user does not need separate frontend and backend servers.

---

# 3. Core Application Scope

The application focuses on **standalone, head-to-head quiz duels**.

Two contestants compete by answering questions displayed on a TV or projector.

Each contestant has an independent countdown timer.

Only one timer runs at a time.

When a contestant answers correctly, their timer stops, the opponent's timer starts, and a new question appears.

Contestants can also pass questions, incurring a time penalty.

The first contestant whose timer reaches zero loses.

The host controls the duel and decides whether answers are correct.

## Out of scope

Do not implement:

- Floor grids or territory conquest.
- Tournament brackets.
- Tournament elimination logic.
- Matchmaking.
- Online player accounts.
- Complex leaderboards.
- Cloud hosting.
- Speech recognition.
- Automatic answer validation.

The application should be excellent at running individual duels rather than attempting to reproduce every feature of the television show.

---

# 4. Content Architecture — Shared Categories and Category Sets

This is a fundamental requirement.

The application must support **multiple independently selectable sets of categories**, while allowing individual categories to be shared between multiple sets.

For example, the user may have:

**Christmas Party**
- Football Clubs
- Car Brands
- Christmas Trivia
- Video Games

**Family Birthday**
- Car Brands
- Disney Movies
- Famous Landmarks
- General Knowledge

In this example, **Car Brands is one shared category**, referenced by both sets.

Editing Car Brands must update the category for both sets.

The user must also be able to duplicate Car Brands to create an independent copy that can be customized without affecting the original.

## 4.1 Separate concepts

Implement three clearly separated concepts:

### Category

A reusable collection of questions.

A category exists independently of any particular event or set.

Each category has a permanent, stable identifier.

### Category Set

A named collection of references to existing categories.

A set does not own the categories themselves.

Sets may reference the same categories without duplicating their content.

### Active Category Set

The currently selected set used for configuring new duels.

Only categories referenced by the active set should normally be offered in duel setup.

Switching the active set must not require restarting the application.

The selected set must persist between application restarts.

## 4.2 Shared category behavior

Requirements:

- A category may belong to any number of sets.
- Editing a category updates it everywhere it is referenced.
- Adding or removing a category from a set must not modify the category itself.
- Removing a category from one set must not affect other sets.
- Duplicating a category creates an independent category with a new ID.
- Duplicated categories must have independent question definitions and media ownership.
- Deleting a shared category must warn which sets reference it.
- Global category deletion must require explicit confirmation.
- Set deletion must not delete its referenced categories.
- Sets may define their own category display order.
- Category IDs must remain stable when names or folder names change.

Prefer stable UUID-based identity rather than relying on category names or filesystem paths as identifiers.

Do not duplicate questions in the normal shared-category workflow.

## 4.3 Switching category sets

The user must be able to:

- View all available sets.
- Select the active set.
- Switch sets without restarting the application.
- See which set is currently active.
- See its categories.
- Configure duels using categories from that set.

Switching sets must not destroy or unexpectedly modify an existing duel.

An active duel should continue using the content snapshot from when it was created.

The newly selected set should apply to subsequent duels.

---

# 5. External, File-Based Content Storage

**Category sets, categories, questions, and associated media must be stored as editable files outside the main Git repository during normal application use.**

This is a strict requirement.

The user wants to be able to manage content both through the application's UI and by manually editing files, including using coding agents to generate questions and categories.

## 5.1 Source of truth

Use **human-readable YAML files as the authoritative source of truth for category and question content**.

Do not make SQLite the authoritative storage for categories and questions.

SQLite should instead be used for application state, duel sessions, configuration state, and other operational data where appropriate.

Manual YAML edits must be recognized by the application.

UI edits must be written back to the YAML files.

The file format should be simple, stable, documented, and easy for coding agents to generate.

## 5.2 Data directory

Implement a configurable external data directory.

Support an environment variable:

`THEFLOOR_DATA_DIR`

If it is not specified, use a sensible persistent user-data location appropriate for the operating system.

Examples:

- macOS: `~/Library/Application Support/TheFloor.TrackerSupreme`
- Windows: `%LOCALAPPDATA%\TheFloor.TrackerSupreme`
- Linux: an appropriate XDG data directory

Never use the Git repository as the default production data directory.

The application must create necessary directories automatically.

## 5.3 Recommended directory structure

Use a structure conceptually equivalent to:

**Data root**
- `library/categories/`
  - `car-brands/category.yaml`
  - `car-brands/images/`
  - `football-clubs/category.yaml`
  - `football-clubs/images/`
  - `disney-movies/category.yaml`
- `sets/`
  - `christmas-party/set.yaml`
  - `family-birthday/set.yaml`
- `state/app.db`
- `backups/` (optional)

Categories and sets must remain logically separate.

A set should contain references to category IDs rather than full copies of the categories.

Media should live alongside the category that uses it.

## 5.4 YAML structure

Define and document a versioned YAML schema.

A category definition should include:

- Schema version.
- Stable category ID.
- Category name.
- Optional description.
- Optional cover image.
- Ordered questions.

Each question should contain:

- Stable question ID.
- Question type.
- Prompt if applicable.
- Relative media path if applicable.
- Correct answer.
- Alternative accepted answers.
- Optional host notes.
- Enabled status.
- Sort order or stable list position.

A category set definition should include:

- Schema version.
- Stable set ID.
- Set name.
- Optional description.
- Ordered references to category IDs.

Use readable identifiers and relative media paths.

Do not require absolute filesystem paths inside content definitions.

Keep the YAML schema straightforward enough that an LLM coding agent can create a valid category without interacting with the application UI.

## 5.5 Manual editing

The user must be able to edit YAML files externally using:

- A text editor.
- An IDE.
- A coding agent.
- Scripts that generate question collections.

The application should detect and reload valid external changes without requiring a restart.

Implement filesystem monitoring with appropriate debounce behavior.

Also provide a manual Reload/Refresh action in the UI.

When externally edited content changes:

- Validate the new file.
- Reload valid changes.
- Update affected UI views.
- Preserve stable IDs and references.
- Report invalid YAML without crashing.
- Preserve the last known valid content until the error is corrected, where appropriate.

## 5.6 Conflict handling and safe writes

UI changes must not silently overwrite externally modified content.

Implement:

- Atomic file writes using temporary files and replacement.
- Change detection using timestamps, hashes, revisions, or equivalent.
- Optimistic concurrency checks before saving.
- Clear conflict messages.
- Safe handling of interrupted writes.
- Validation before replacing files.

When conflicts occur, the application should make the situation clear and avoid data loss.

Use safe YAML parsing.

Do not allow arbitrary YAML tags or other deserialization mechanisms that could execute code.

Prevent path traversal, unsafe relative paths, and access to files outside the configured data directory.

## 5.7 Separation from GitHub

Real event content must never be committed to the public repository automatically.

The Git repository should contain only:

- Application source code.
- Documentation.
- YAML schema documentation.
- Example categories.
- Dummy questions.
- Legally redistributable sample media.
- Automated test fixtures.

The user may create private or personal categories containing photographs, inside jokes, or other content not suitable for publication.

Those files must live exclusively in the external data directory unless the user explicitly exports or shares them.

---

# 6. Development and Sample Content

The repository must include small, useful sample datasets for development and testing.

These are development resources, **not real event content**.

Provide example category sets such as:

- Demo Set A
- Demo Set B

Include overlapping references to demonstrate shared categories.

Provide sample categories such as:

- General Knowledge
- Mathematics
- Geography
- Demo Image Recognition

Use original, generated, or otherwise redistributable assets.

Do not commit copyrighted television screenshots, commercial music, or private photographs.

## Demo-content behavior

The application should not automatically overwrite production content with development fixtures.

Provide an explicit mechanism to load or copy sample data into the configured external data directory.

For example, a development command, initialization option, or import operation.

The README must clearly distinguish example data from actual user data.

Automated tests should use isolated temporary data directories and must never modify the user's real category library.

---

# 7. Managing Category Sets Through the UI

Implement a dedicated category-set management interface.

Suggested route:

`/sets`

It must support:

- Listing all category sets.
- Creating a set.
- Renaming a set.
- Editing its description.
- Duplicating a set.
- Deleting a set.
- Selecting the active set.
- Viewing categories belonging to a set.
- Adding existing categories to a set.
- Removing categories from a set.
- Reordering categories within a set.
- Importing and exporting complete sets.

## Set duplication

Duplicating a set should, by default, create a new set referencing the same shared categories.

The user should also have an explicit option to duplicate the categories as independent copies.

Clearly explain the difference in the UI.

## Shared category visibility

When editing a category, display which sets reference it.

When duplicating a category, make it clear that the new category is independent.

When deleting a category, warn about the effect on all referencing sets.

---

# 8. Category and Question Management

Implement a complete category and question editor.

Suggested routes:

- `/categories`
- `/categories/:id`

## Category management

Support:

- Create category.
- Edit category.
- Delete category.
- Duplicate category.
- Search categories.
- View category descriptions.
- View question counts.
- View sets referencing a category.
- Add/remove category membership.
- Import/export categories.

## Question management

Support:

- Create question.
- Edit question.
- Delete question.
- Duplicate question.
- Reorder questions.
- Enable/disable question.
- Upload or replace media.
- Preview question.
- Search/filter questions.
- View correct and alternative answers.

All edits must persist to the YAML and media files in the external data directory.

Do not require modifying source code to manage questions.

## Question types

Support three types.

### Image

The audience sees an uploaded image.

Examples:

- Football club logos.
- Cars.
- Celebrities.
- Country outlines.
- Movie screenshots.

The host sees the image and correct answer.

### Text

The audience sees a text question.

Example:

Question: "What is the capital of Slovenia?"

Answer: "Ljubljana"

### Audio

The audience hears or plays an uploaded audio clip.

Examples:

- Recognize a song.
- Identify a sound effect.
- Recognize a movie soundtrack.

Support browser audio playback and appropriate handling of autoplay restrictions.

## Media formats

Support:

Images:
- JPG
- PNG
- WebP
- GIF

Audio:
- MP3
- WAV
- OGG

Validate uploaded files, MIME types, file signatures where practical, and file sizes.

Use stable, safe media paths.

Do not expose arbitrary filesystem paths through the web API.

## Answer validation

All answers are validated manually by the host.

Correct and alternative answers are only guidance for the host.

Do not implement speech recognition or automatic answer checking.

---

# 9. Import and Export

Implement portable imports and exports at both category and category-set levels.

## 9.1 Category export

Export a category as a ZIP containing:

- Versioned category YAML.
- All associated media files.

## 9.2 Category-set export

Export a category set as a self-contained ZIP containing:

- The set definition.
- Every category referenced by the set.
- All associated images and audio.

This is important because, during normal operation, sets reference categories from a shared library rather than containing independent copies.

A complete set export must remain portable to another installation.

## 9.3 Import behavior

Support importing either type of ZIP.

When importing categories that already exist:

- Identify matches using stable category IDs.
- Reuse existing categories when they are identical.
- Detect cases where the same ID refers to different content.
- Provide a safe conflict resolution strategy.
- Support importing conflicting categories as independent copies with new IDs.
- Update set references when IDs are remapped.
- Never silently overwrite an existing category with different content.

Prefer explicit, understandable conflict handling over complicated automatic merging.

## 9.4 JSON support

Also support simple JSON import/export for text-only question collections.

Validate imports thoroughly.

Prevent ZIP path traversal, unsafe file extraction, duplicate entries, and oversized or malicious archives.

## 9.5 Backup and portability

Document how to:

- Back up the entire external data directory.
- Restore it.
- Transfer the application content to another computer.
- Export one event's category set.
- Import another person's category set.

---

# 10. Duel Configuration

Before starting a duel, the host configures:

- Player A's name.
- Player B's name.
- Category from the active category set.
- Starting time per player.
- Pass penalty.
- Starting player.
- Question order.

Default settings:

| Setting | Default |
|---|---|
| Player time | 45 seconds |
| Pass penalty | 3 seconds |
| Starting player | Random |
| Question order | Shuffled |

Both players start with the same configured time.

The host should be able to review the configuration before starting.

The selected active category set should be clearly visible.

---

# 11. Duel State Machine

Implement explicit states:

- `Ready`
- `Running`
- `Paused`
- `Finished`

Track the active player independently.

Use a dedicated backend domain service or state-machine component.

Reject invalid state transitions.

## 11.1 Starting

1. The host selects contestants and category.
2. The system prepares a question snapshot.
3. The question order is determined.
4. The first question appears.
5. Both clocks display their starting time.
6. The host presses Start.
7. The selected player's countdown begins.

Question preparation must not consume player time.

## 11.2 Correct answer

When the active contestant answers correctly, the host presses Correct.

Atomically:

1. Calculate and preserve their remaining time.
2. Mark the current question as used.
3. Select the next question.
4. Switch the active player.
5. Start the opponent's countdown.
6. Display the new question.

Neither player's time resets.

There should be no intentional delay between switching clocks and displaying the next question.

## 11.3 Incorrect answer

Incorrect answers require no system interaction.

The active player's timer continues counting down.

The player may make unlimited guesses.

## 11.4 Pass

When the host presses Pass:

1. Subtract the configured pass penalty from the active player's time.
2. Mark the question as used.
3. Display the next question.
4. Keep the same player active.
5. Continue the countdown.

If the penalty reduces the remaining time to zero, immediately finish the duel.

## 11.5 Time expiration

When a timer reaches zero:

- Stop both clocks.
- Finish the duel.
- Declare the opponent the winner.
- Display a winner announcement.
- Disable Correct and Pass.
- Allow another duel to be configured.

Timer values must never become negative.

## 11.6 Question exhaustion

Questions must not repeat within the same duel.

If the category runs out of unused questions:

- Pause the duel.
- Preserve both players' remaining time.
- Inform the host.
- Allow the host to add more questions, finish the duel manually, or restart.

Do not silently recycle questions.

If new questions are added during exhaustion, make their inclusion an explicit host action rather than silently changing the running session.

---

# 12. Duel Content Snapshots

**A running duel must not change unexpectedly when underlying YAML files are edited.**

When a duel is created, generate an immutable snapshot containing:

- Selected category ID and name.
- Applicable question definitions.
- Question order.
- Correct answers.
- Media references and sufficient media content consistency information.
- Duel configuration.

The active duel must continue operating on its snapshot even if:

- A category is edited externally.
- A category is removed from a set.
- The active set changes.
- Questions are reordered.
- A shared category is edited from another interface.

Ensure media needed by an active or recoverable duel remains accessible.

Choose a practical implementation, such as session-specific media snapshots or managed media retention.

Do not rely on mutable source files remaining unchanged.

The snapshot must be recoverable after backend restart.

---

# 13. Host Dashboard

Suggested route:

`/host`

The host dashboard is the main live control interface.

It must display:

- Player names.
- Remaining time for both players.
- Active player.
- Selected category.
- Current question.
- Correct answer.
- Accepted alternative answers.
- Question preview.
- Correct button.
- Pass button.
- Pause/Resume.
- Undo.
- Duel status.
- Audience display connection status.

Controls must be prominent and operable without scrolling on a typical laptop.

The host should be able to launch the audience display in a separate window.

## Keyboard shortcuts

| Key | Action |
|---|---|
| Space | Correct |
| P | Pass |
| Escape | Pause |
| R | Resume |
| U | Undo |

Disable shortcuts while the host types into form controls.

Use Vue lifecycle management or a dedicated composable to register and clean up keyboard listeners.

## Undo

Support undoing the most recent Correct or Pass operation.

Restore:

- Previous question.
- Previous active player.
- Both clocks as they were immediately before the operation.
- Question consumption state.

After Undo, automatically pause the duel.

Require explicit Resume.

## Additional controls

Include:

- Restart duel.
- End duel.
- Manually declare winner.
- Return to setup.
- Open audience display.
- Toggle fullscreen.

Use confirmations for destructive actions.

---

# 14. Audience Display

Suggested route:

`/display`

This interface is intended for a large TV/projector.

Display:

- Player A's name and time.
- Player B's name and time.
- Clearly highlighted active player.
- Category name.
- Current question.
- Paused indication.
- Winner announcement.

**Never display correct answers, alternative answers, or host notes.**

Audience clients must receive a separate, restricted DTO that excludes host-only information.

The display must be read-only.

Support multiple connected audience displays.

## Visual design

Use a polished game-show-inspired dark theme.

Prioritize 1920×1080 and 16:9 layouts.

Use:

- Large readable timers.
- Strong active/inactive contrast.
- Clear typography.
- Centered questions.
- Responsive media presentation.
- Smooth but restrained animations.
- Prominent winner announcements.

Suggested visual states:

- Active clock: bright/highlighted.
- Inactive clock: muted.
- Under 10 seconds: warning color.
- Under 5 seconds: urgent styling.
- At zero: winner transition.

Do not copy the original television show's protected branding or sound effects.

Optional original sound effects may be included, with controls to disable them independently from question audio.

---

# 15. Server-Authoritative Timer Engine

Timer accuracy and synchronization are critical.

**Do not use browser-side intervals as the authoritative time source.**

Implement authoritative timing on the backend using a monotonic time source, such as .NET `TimeProvider` or `Stopwatch`.

Track:

- Remaining time for both players.
- Active player.
- Current state.
- Turn start timestamp.
- Session version.
- Current question.

Every command must account for elapsed time before performing its transition.

## Requirements

- Only the active timer decreases.
- Pausing preserves exact remaining time.
- Resuming does not add or remove time.
- Player switches preserve time.
- Expiration takes precedence over actions received after the deadline.
- Duplicate commands are idempotent.
- Simultaneous commands are handled deterministically.
- Pass penalties apply exactly once.
- Both timers never run simultaneously.
- Timer values never become negative.
- Timer expiration occurs independently of whether a browser is connected.

Use explicit concurrency control.

Each command should include a unique identifier and the expected session state/version where appropriate.

Prevent accidental double presses from skipping questions.

## Visual countdown

Display at least tenths-of-a-second precision, for example:

`32.4`

Use server synchronization data and client-side interpolation for smooth rendering.

A Vue composable should manage timer rendering and clean up animation frames correctly.

Resynchronize after:

- State changes.
- Reconnection.
- Browser visibility changes.
- Refreshes.

Make timing logic deterministic and testable using an injected time source.

---

# 16. Real-Time Synchronization

Use ASP.NET Core SignalR.

The backend owns the authoritative duel state.

A host action should:

1. Send a command to the backend.
2. Validate the command.
3. Apply the transition.
4. Persist the state as appropriate.
5. Broadcast the updated state.
6. Update connected Vue clients.

## Vue architecture

Create a dedicated SignalR service.

Use Pinia or another clearly structured shared-state mechanism to expose reactive duel state.

Do not place SignalR connection-management logic directly into UI components.

Avoid duplicate subscriptions during navigation and component remounting.

## Reconnection

After a browser refresh or reconnection:

- Fetch the authoritative state.
- Restore the current question.
- Restore clocks.
- Restore the active player.
- Resume appropriate display behavior.

Reconnection must not restart the duel or reshuffle questions.

## Host authorization

Protect host-only actions.

A lightweight local host token or equivalent mechanism is sufficient.

No full user-account system is required.

Never expose host-only answers through audience API payloads, SignalR events, or static content endpoints.

---

# 17. Session Persistence

Persist operational state in SQLite.

Store at least:

- Session ID.
- Player names.
- Category ID.
- Category set ID.
- Duel configuration.
- Current state.
- Remaining times.
- Active player.
- Question order.
- Question progress.
- Question snapshot.
- Winner.
- Start and completion timestamps.

An unfinished duel must be recoverable.

After backend restart, restore unfinished duels in the Paused state.

Do not continue counting time while the server is offline.

Support one active duel at a time.

Completed duels should retain a basic historical record.

Do not create a complex statistical reporting system.

---

# 18. Reliability and Edge Cases

Explicitly handle:

- Rapid repeated Correct commands.
- Duplicate Pass commands.
- Commands arriving after expiration.
- Pass penalty exceeding remaining time.
- Pausing during an active question.
- Undo after Correct.
- Undo after Pass.
- Browser refresh.
- Host disconnection.
- Audience disconnection.
- Backend restart.
- Empty categories.
- Exhausted question pools.
- Missing media.
- Corrupt media.
- Invalid YAML.
- Duplicate category IDs.
- Dangling category references.
- Shared category deletion.
- Category-set deletion.
- Category edits during a duel.
- External file changes during a UI edit.
- Simultaneous file changes.
- Interrupted file writes.
- Import collisions.
- Unsafe ZIP entries.
- Filesystem permission failures.
- Data directory unavailable.
- Commands arriving exactly at timer expiration.

The application must not silently lose user data or enter an inconsistent state.

Provide clear error messages and recovery options.

---

# 19. GitHub Issues and Project Management

Use the existing `TheFloor.TrackerSupreme` GitHub repository.

**GitHub Issues must be the authoritative project tracker.**

Before substantial implementation:

1. Analyze this complete specification.
2. Inspect existing issues.
3. Avoid duplicate issues.
4. Break the implementation into logical tasks.
5. Create GitHub Issues with descriptions and acceptance criteria.
6. Assign labels and priorities.
7. Record dependencies where relevant.
8. Begin work in dependency order.

Create implementation issues covering at least:

- Solution architecture and scaffolding.
- External data-directory infrastructure.
- YAML schemas and validation.
- Shared category library.
- Category-set management.
- External file change monitoring.
- Safe/atomic YAML persistence.
- Category and question editor.
- Media storage and uploads.
- Set/category import and export.
- Import conflict handling.
- Duel state machine.
- Authoritative timer engine.
- Question snapshots.
- Session persistence.
- SignalR synchronization.
- Host dashboard.
- Audience display.
- Keyboard shortcuts and Undo.
- Visual design.
- Frontend tests.
- Backend tests.
- Playwright tests.
- CI.
- Documentation.
- Final acceptance testing.

Create additional issues for discovered bugs or missing requirements.

Keep issues up to date.

Close issues only when their acceptance criteria have been met and verified.

**Every known unfinished item must be represented by an open GitHub Issue.**

---

# 20. Git Branching and Commits

Follow a lightweight professional Git workflow.

## Main branch

`main` should contain stable, integrated code.

Do not implement major features directly on `main`.

Do not force-push or rewrite shared history.

## Feature branches

Use branches associated with GitHub Issues.

Examples:

- `feature/12-yaml-content-storage`
- `feature/18-category-sets`
- `feature/24-duel-timer-engine`
- `feature/30-audience-display`
- `fix/42-category-reload-conflict`

Keep changes focused.

Avoid unnecessarily long-lived branches.

## Commits

Commit and push periodically.

Use Conventional Commits.

Examples:

- `feat(content): add versioned YAML category schema`
- `feat(sets): support shared category references`
- `feat(duel): implement server-authoritative timers`
- `fix(content): prevent stale YAML writes`
- `test(duel): cover pass penalty and expiration`

Prefer small, meaningful commits.

Do not accumulate the entire application into one giant commit.

Push work regularly so it is preserved remotely.

## Pull requests

Use pull requests for integrating feature branches.

Every pull request should:

- Reference relevant issues.
- Describe the changes.
- Summarize testing.
- Pass applicable checks.
- Receive an autonomous review of the diff.

Merge when verification succeeds.

Prefer squash merges.

Delete merged branches.

Do not wait for human reviews.

Do not bypass failing checks or unnecessarily weaken existing repository security rules.

---

# 21. Continuous Integration

Extend the existing GitHub Actions configuration.

CI should verify:

1. .NET dependency restoration.
2. Backend compilation.
3. Backend tests.
4. npm dependency installation.
5. Vue/TypeScript type checking with `vue-tsc`.
6. Frontend production build.
7. Vitest tests.
8. YAML schema validation for example content.
9. Integration tests where practical.

Run CI on pull requests and pushes to `main`.

Use sensible dependency caching.

Follow GitHub Actions security best practices, including least-privilege permissions.

Fix failing checks before merging.

Do not remove tests merely to achieve a green build.

---

# 22. Automated Testing

Implement comprehensive automated tests.

## Backend tests

Cover at least:

- Duel initialization.
- Correct switching players.
- Inactive timer preservation.
- Multiple player switches.
- Pass penalties.
- Timer expiration.
- Pause/resume.
- Undo.
- Question exhaustion.
- No repeated questions.
- Duplicate commands.
- Simultaneous commands.
- Deadline-boundary actions.
- Invalid state transitions.
- Backend restart recovery.
- Session snapshot isolation.

Use a fake or injected time source.

## Content-storage tests

Test:

- Loading valid category YAML.
- Loading valid category-set YAML.
- Shared category references.
- Duplicate category creation.
- Editing shared categories.
- Removing categories from sets.
- Stable IDs across renames.
- Invalid YAML handling.
- Missing media.
- Missing category references.
- External file reloads.
- Atomic saving.
- Concurrent-edit conflicts.
- Import/export round trips.
- ZIP portability.
- Import ID collisions.
- Malicious path rejection.
- Isolation from the repository.

## Vue tests

Use Vitest and Vue Test Utils.

Test:

- Category editor.
- Category-set management.
- Active-set selection.
- Shared category indicators.
- Question editor.
- Host controls.
- Keyboard shortcuts.
- Countdown presentation.
- Audience display.
- Reactive state synchronization.
- Error states.
- Event-listener cleanup.

## Browser tests

Use Playwright.

Test complete workflows using separate host and audience browser contexts.

Include end-to-end scenarios for:

- Creating a category set.
- Creating a category.
- Sharing it across sets.
- Editing it and verifying both sets reflect the change.
- Importing/exporting a complete set.
- Switching the active set.
- Running a duel.
- Correct/Pass/Pause/Resume/Undo.
- Timer expiration.
- Backend restart recovery.
- Browser refresh/reconnection.

Tests must be repeatable and must not modify real user data.

---

# 23. Browser-Based Visual Verification

Use Playwright as the primary browser automation tool.

Use the built-in ChatGPT browser when available for additional visual inspection.

**Do not request Computer Use access to macOS.**

During development:

1. Start the application locally.
2. Open the category-set management page.
3. Create and select sets.
4. Create categories and questions.
5. Verify shared-category behavior.
6. Open the host dashboard.
7. Open the audience display separately.
8. Start a duel.
9. Test Correct, Pass, Pause, Resume, and Undo.
10. Verify clock synchronization.
11. Inspect the 1920×1080 audience layout.
12. Verify persistence and file reloads.
13. Fix discovered defects.
14. Retest.

Do not consider the frontend verified merely because it compiles.

Capture screenshots or other diagnostic artifacts when useful for identifying visual problems.

---

# 24. Autonomous Development Requirements

This project is intended to be developed with **minimal human involvement**.

The human will evaluate the final application but is not expected to approve architecture, individual features, commits, or pull requests.

Therefore:

- Make reasonable technical decisions independently.
- Install missing development dependencies when permitted.
- Do not ask the user to prioritize issues.
- Do not request routine approvals.
- Do not wait for human code review.
- Do not stop after creating a plan.
- Do not stop after scaffolding.
- Do not stop after implementing one application layer.
- Investigate technical problems independently.
- Fix straightforward defects rather than leaving them unresolved.
- Keep implementing until the requirements are fulfilled or a genuine external blocker prevents progress.

Only request human involvement for unavoidable authentication, permissions, or external authorization.

Do not claim work has been completed unless it has actually been implemented and verified.

## Development cycle

Repeat the following process:

1. Select the next unblocked GitHub Issue.
2. Create a feature branch.
3. Implement the requirement.
4. Add tests.
5. Run relevant checks.
6. Fix failures.
7. Commit incrementally.
8. Push the branch.
9. Open a pull request.
10. Review the changes.
11. Verify CI.
12. Merge.
13. Update or close the issue.
14. Continue.

Group related work when practical.

Prioritize a functioning integrated application over excessive process overhead.

---

# 25. Session Continuity

The implementation may exceed the execution or usage limits of one Codex session.

Make the project easy to resume.

Maintain:

`docs/progress.md`

This document must track:

- Completed features.
- Current implementation state.
- Active GitHub Issues.
- Remaining work.
- Known defects.
- Blockers.
- Architectural decisions.
- Exact recommended next steps.

Keep it up to date and commit it periodically.

If execution cannot continue:

1. Preserve working changes.
2. Push completed commits.
3. Update GitHub Issues.
4. Update `docs/progress.md`.
5. Clearly explain the remaining work.

Do not leave undocumented partially completed features.

---

# 26. Documentation

Maintain comprehensive documentation.

## README

Explain:

- Application purpose.
- System requirements.
- How to install.
- How to build.
- How to run locally.
- How to open the host dashboard.
- How to open the audience display.
- How to use a TV/projector.
- How to create category sets.
- How to create categories.
- How shared categories work.
- How to switch between sets.
- How to edit YAML manually.
- How to add images/audio.
- How to import/export content.
- How to back up user data.
- How to restore user data.
- How to configure and run duels.
- Keyboard shortcuts.
- How to run automated tests.
- How to build for production.

## Additional documentation

Maintain:

- `docs/environment.md`
- `docs/architecture.md`
- `docs/progress.md`
- `docs/deployment.md`
- `docs/content-format.md`

The content-format documentation must explain the YAML schemas and include complete examples for:

- A category.
- Image, text, and audio questions.
- A category set referencing multiple categories.
- Shared categories.
- Relative media paths.
- Valid IDs.
- Import/export package structures.

This documentation should be detailed enough that a coding agent can generate entire category sets correctly without needing to inspect the application source code.

---

# 27. Definition of Done

The implementation is complete when the following scenarios work.

## Scenario A — Category sets

1. I launch the application.
2. I create a category set called "Christmas Party".
3. I create another called "Family Birthday".
4. I create a category called "Car Brands".
5. I add 20 image questions to Car Brands.
6. I add Car Brands to both sets.
7. I select Christmas Party.
8. Car Brands appears as an available category.
9. I switch to Family Birthday.
10. Car Brands also appears there.
11. I edit Car Brands.
12. The changes are reflected in both sets.
13. I duplicate Car Brands.
14. I edit the duplicate.
15. The original remains unchanged.
16. I remove Car Brands from one set.
17. It remains available in the other.

## Scenario B — External content

18. I locate the configured external data directory.
19. I open a category YAML file in a text editor.
20. I manually add a valid question.
21. I save the file.
22. The application detects and loads the change.
23. I edit a question through the UI.
24. The corresponding YAML file is updated.
25. I introduce invalid YAML.
26. The application reports the error without losing valid content.
27. I correct the YAML.
28. The application reloads successfully.

## Scenario C — Portability

29. I export Christmas Party as a ZIP.
30. The ZIP contains the set, all referenced categories, and all necessary media.
31. I import the ZIP into a clean data directory.
32. The imported set works independently.
33. Shared-category ID conflicts are handled safely.
34. Existing unrelated content is preserved.

## Scenario D — Duel

35. I select Christmas Party.
36. I choose Car Brands.
37. I enter Martin and Thomas as players.
38. I configure 45 seconds each and a 3-second pass penalty.
39. I open the audience display on a TV.
40. I start the duel with Martin active.
41. Martin's timer begins.
42. A question appears.
43. Martin answers correctly.
44. I press Space.
45. Martin's timer stops.
46. Thomas's timer starts.
47. A new question appears.
48. Thomas cannot answer.
49. I press P.
50. Three seconds are deducted.
51. A new question appears for Thomas.
52. Thomas answers correctly.
53. I press Space.
54. Martin becomes active again with his preserved time.

## Scenario E — Undo and completion

55. I accidentally press Space.
56. I press U.
57. The previous question and clock values are restored.
58. The game is paused.
59. I press R.
60. The game resumes.
61. Eventually one player's timer reaches zero.
62. Both timers stop.
63. The opponent is announced as the winner.
64. I can start another duel without restarting the application.

## Scenario F — Persistence and isolation

65. I start a duel.
66. I externally edit the shared category.
67. The active duel continues using its original snapshot.
68. I restart the application.
69. The unfinished duel is restored in a Paused state.
70. My category sets, categories, questions, and media remain available.
71. My real content is stored outside the Git repository.
72. The application works offline.

## Scenario G — Engineering quality

73. The application builds successfully.
74. Backend tests pass.
75. Vue frontend tests pass.
76. Playwright tests pass.
77. GitHub Actions passes.
78. The README provides accurate startup instructions.
79. The data format is fully documented.
80. GitHub Issues accurately reflect completed and remaining work.

**All essential functionality must work without editing application source code or manually operating external timer/presentation applications.**

---

# 28. Final Verification and Handoff

Before declaring the project complete:

1. Verify the public GitHub repository is accessible.
2. Verify the complete implementation is integrated into `main`.
3. Verify all source code is pushed.
4. Verify CI passes.
5. Run all automated tests.
6. Run the end-to-end acceptance scenarios.
7. Verify offline operation.
8. Verify external data storage.
9. Verify shared-category behavior.
10. Verify YAML editing and reloads.
11. Verify import/export portability.
12. Verify duel accuracy and synchronization.
13. Verify backend restart recovery.
14. Confirm real user content is not present in Git.
15. Review GitHub Issues.
16. Close completed issues.
17. Document unresolved work in open issues.
18. Update all project documentation.
19. Remove unnecessary temporary branches.
20. Ensure no necessary changes remain unmerged.

Do not declare completion while critical functionality remains broken.

## Final report

Provide:

**Repository**
- URL to `TheFloor.TrackerSupreme`.

**Implemented functionality**
- Summary of the completed application.

**Execution**
- Exact commands for running the application locally.

**Data storage**
- Location of the external data directory.
- How to select category sets.
- How to edit YAML files manually.

**Testing**
- Tests executed.
- Actual results.
- End-to-end verification status.

**GitHub**
- Summary of merged pull requests.
- Completed issues.
- Remaining open issues.

**Known limitations**
- Anything incomplete or not verified.

**Completion status**
- State clearly whether the full acceptance scenarios work.

---

# 29. Final Instructions

You own the full engineering lifecycle for TheFloor.TrackerSupreme.

The GitHub repository and development environment have already been prepared.

Begin by inspecting the existing repository and environment documentation.

Then create the implementation backlog in GitHub Issues.

Proceed with implementation using feature branches, incremental commits, automated tests, continuous integration, and autonomous pull-request merges.

The frontend must use **Vue 3, TypeScript, the Composition API, Vite, and Tailwind CSS**.

The backend must use **ASP.NET Core / .NET 10**.

Categories and questions must use **externally stored, editable YAML definitions**.

Category sets must support **shared category references**.

Real user content must remain **outside the public repository**.

The application must run **locally and offline**.

The duel engine must use **accurate, server-authoritative countdown timers**.

Use browser automation and automated tests to verify the application.

Do not request Computer Use access to macOS.

Do not request routine implementation decisions or approvals.

Keep GitHub Issues and development documentation accurate throughout implementation.

**The final goal is a complete, polished application that I can use at different events with different category sets, freely reuse and share categories between events, create questions through the UI or by editing files with coding agents, and run reliable head-to-head quiz duels on a TV.**

Proceed autonomously until the complete implementation has been delivered and verified, or an unavoidable external blocker prevents further progress.