# Architecture

The ASP.NET Core process owns application data and serves the production Vue SPA. Vite proxies HTTP and WebSocket traffic to the same local backend during development. Frontend assets ship in the publish directory and load from the local process.

The content layer will read versioned YAML from the external data root. The runtime will persist operational data to SQLite, including immutable question and media snapshots for duels. Host and audience projections will be separate API contracts, and the server will own elapsed-time calculations and transitions.
