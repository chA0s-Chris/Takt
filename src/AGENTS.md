# AGENTS.md for Production Code

## Architecture Rules

- `Takt.Core` must not reference Avalonia or any other UI framework. Domain logic, storage, tracking, Jira sync, and credential handling live there. `Takt.App` contains only Avalonia views, view models, and DI wiring.
- Access persisted data only through the repository interfaces in `Takt.Core` (`ITimeEntryRepository`, `ITemplateRepository`, ...). Do not use `LiteDatabase` or other LiteDB types outside the `Storage` folder — the storage engine must remain swappable.
- All persisted timestamps are UTC `DateTime` values (`DateTimeKind.Utc`). Convert to local time only at the ViewModel/UI edge, never inside `Takt.Core`.
- At most one `TimeEntry` may be open (`EndedAt == null`) at any time. The open entry *is* the running timer. All tracking state changes go through `TrackingService`; UI code never mutates entries to start or stop tracking.
- Jira Cloud is a sync target, never the source of truth. The local database is authoritative. An entry's `SyncState` changes only after a confirmed Jira API response.
- The Jira API token goes through `ICredentialStore` only. Never write secrets to LiteDB, configuration files, or logs.

## Build & Distribution Rules

- The application ships as a single self-contained executable per RID (win-x64, linux-x64).
- Never enable `PublishTrimmed` or NativeAOT: LiteDB's `BsonMapper` relies on reflection and breaks silently under trimming.
- `Takt.Core` must remain pure managed code. Platform-specific behavior (credential stores, data paths) is isolated behind interfaces in `Takt.Core`, with the implementation chosen at startup.
