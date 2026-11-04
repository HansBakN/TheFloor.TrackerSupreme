# Architecture

The ASP.NET Core process owns application data and serves the compiled Vue SPA. In development, Vite proxies HTTP and WebSocket traffic to the backend. Production ships the frontend, fonts supplied by the local system, and optional example files with the server.

## Content and operational state

The external data root contains independent category directories and ordered set definitions. Category and question UUIDs are stable across renames. The UI edits typed definitions, the content service validates them and verifies the current revision, and a same-directory temporary file replaces a saved definition atomically. A filesystem watcher debounces external edits. The library retains its last valid in-memory definition when a file becomes invalid and returns diagnostics with the library view.

SQLite through EF Core stores active-set selection, the current duel, and completed duel records. It does not store editable library content. A duel copies ordered enabled questions and their media into a session snapshot. An archive import validates its definitions and media before staging files for installation. Existing IDs are reused for identical content or remapped by explicit request.

## Duel timing and synchronization

The domain rules implement Ready, Running, Paused, and Finished. A per-process lock serializes transitions and the timer service calculates elapsed time using `TimeProvider`. The server checks the expected version and command UUID before applying a transition. Expiration takes precedence over late commands. The background ticker writes periodic checkpoints and persists the final clock sample on graceful shutdown. Unfinished duels restore paused.

The HTTP command endpoint writes committed state before broadcasting via SignalR. The hub uses separate host and audience groups. Host-only HTTP routes and hub connections require a local host cookie. The audience receives the public question, player names, clocks, status, and winner. Its media route serves only the current question's snapshot asset.

Pinia holds the live projection. Vue renders tenths from the latest server sample plus elapsed browser animation time on the active clock. Clients fetch authoritative state after reconnect or visibility changes, and clean up subscriptions and animation frames when leaving the duel views.
