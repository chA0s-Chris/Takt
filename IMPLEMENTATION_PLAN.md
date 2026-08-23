# Takt — Implementation Plan

A per-user desktop time tracker with a floating widget, local-first storage, and
manual push of worklogs to Jira Cloud.

## Locked design decisions

| Area          | Decision                                                                 |
|---------------|--------------------------------------------------------------------------|
| Runtime       | .NET 10 (LTS)                                                            |
| UI            | Avalonia 12.x, MVVM via CommunityToolkit.Mvvm                            |
| Storage       | LiteDB (single file, pure managed) behind thin repository interfaces     |
| Distribution  | Single self-contained executable per RID (win-x64, linux-x64); no trimming, no AOT |
| Jira          | Jira Cloud REST v3, email + API token, manual push-with-review           |
| Credentials   | `ICredentialStore`: Windows Credential Manager (P/Invoke) on Windows, AES-encrypted file (per-user key, `0600`) on Linux |
| Scope         | Purely per-user. Deferred: idle detection, worklog rounding, shared templates, reporting |
| Interaction   | Widget-first: tracking, pause/resume, switching, and Jira issue assignment (text search over keys and titles) happen in the widget; the main window is for review, editing, templates, and pushing worklogs |

## Solution layout

```
Takt.slnx
Directory.Build.props            # net10.0, nullable enable, implicit usings, LangVersion
src/
  Takt.Core/                     # no UI dependencies
    Domain/                      #   TimeEntry, Template, AppSettings, SyncState
    Storage/                     #   ITimeEntryRepository, ITemplateRepository, LiteDB impls, backups
    Tracking/                    #   TrackingService (start/stop/switch, crash recovery)
    Jira/                        #   IJiraClient, JiraCloudClient, SyncService, issue cache
    Security/                    #   ICredentialStore + Windows/Linux implementations
  Takt.App/                      # Avalonia: widget window, main window, tray, DI wiring
tests/
  Takt.Core.Tests/               # NUnit + FluentAssertions; LiteDB against temp files, Jira client against stubbed HttpMessageHandler
```

Two runtime projects only — the app is small; more granularity would be ceremony.

## Data model

```csharp
class TimeEntry {
    Guid Id;                      // Guid, not ObjectId: LiteDB types stay inside Storage/
    string TaskName;
    string? JiraIssueKey;         // optional by design
    string? Note;
    DateTime StartedAt;           // always UTC
    DateTime? EndedAt;            // null == running timer (exactly one may be open)
    SyncState SyncState;          // Local | Synced | LocallyModified
    string? JiraWorklogId;        // set after first successful push
}

class Template {
    Guid Id;
    string Name;                  // e.g. "Meetings (Q2)"
    string? DefaultJiraIssueKey;  // e.g. TEAM-1234
    string? DefaultNote;
    int SortOrder;
    bool Archived;
}
```

Indexes: `TimeEntry.StartedAt`, `TimeEntry.SyncState`.
DB location: `%LOCALAPPDATA%\Takt` / `$XDG_DATA_HOME/takt` (fallback `~/.local/share/takt`).
On startup: rotate a copy of `takt.db` (keep last 5) before opening.

---

## Milestone 1 — Scaffold + domain & storage core

- Add the projects to the existing scaffold (`Takt.slnx`, `Directory.Build.props`,
  `Directory.Packages.props` with central package management, `.editorconfig`).
- Domain types above; `BsonMapper` configuration (UTC `DateTime`, enum as string).
- LiteDB repositories + indexes; backup rotation; platform data-path resolver.
- Release publish profile: `SelfContained`, `PublishSingleFile`,
  `IncludeNativeLibrariesForSelfExtract`, `EnableCompressionInSingleFile`.

**Done when:** `dotnet test` green (repo round-trips incl. UTC integrity, open-entry
query, backup rotation); `dotnet publish -r win-x64` and `-r linux-x64` each yield
one runnable binary.

## Milestone 2 — Tracking engine + floating widget

- `TrackingService`: `Start(taskName, issueKey?)`, `Stop()`, `SwitchTo(...)`
  (atomic stop+start); enforces a single open entry; raises change events.
- Crash/reboot recovery: on startup, an open entry triggers a prompt —
  *continue tracking* or *set an end time*.
- Avalonia shell: DI (`Microsoft.Extensions.DependencyInjection`), single-instance
  guard (named mutex + pipe to activate the running instance).
- Widget window: borderless, always-on-top, draggable, position persisted;
  task name + live elapsed time (1 s tick); start/stop; quick-switch popup fed by
  templates and recent tasks.
- Tray icon: show/hide widget, open main window (placeholder), exit.
  Closing windows never stops tracking; only explicit exit does.

**Done when:** a full day can be tracked end-to-end from the widget alone, entries
land correctly in LiteDB, and killing/restarting the app recovers the open entry.

## Milestone 3 — Main window: overview, editing, templates, settings ✅

- Entry overview with day/week navigation; per-day totals; the running entry counts up.
- Create/edit/delete entries: times (local-time UI, UTC storage), task name, note,
  issue key (with the same Jira text search as the widget). Editing a `Synced` entry
  flips it to `LocallyModified`. Overlap detection warns but does not block.
- Template CRUD + *duplicate* (the quarterly cycle: duplicate "Meetings (Q2)",
  rename, change issue key), archive/restore, and ordering.
- Settings: Jira base URL + email (LiteDB), API token via `ICredentialStore`,
  connection test against `/rest/api/3/myself`. Widget preferences (always on top,
  show issue key, reset position) apply live through a `SettingsNotifier`.

Deviations from the design canvas, deliberate:

- The navigation rail carries Overview / Templates / Settings; *Sync* arrives with
  Milestone 4 rather than as a dead entry.
- Icons are text glyphs instead of the drawn SVG set, and template ordering uses
  ↑/↓ buttons instead of drag handles.
- The application pins the light theme; the widget stays hand-coloured dark.

## Milestone 4 — Jira sync

- `JiraCloudClient` (REST v3, Basic auth email:token; issue-picker text search and
  the connection test already shipped):
  - `GET /rest/api/3/issue/{key}?fields=summary` — validate keys, cache summaries locally;
  - `POST /rest/api/3/issue/{key}/worklog` — push (`started`, `timeSpentSeconds`, comment from note);
  - `PUT .../worklog/{id}` — re-push `LocallyModified` entries.
- `SyncService`: push one entry / one day / all pending; per-entry results;
  state transitions only on confirmed success; friendly errors for 401/403/404
  (bad token, no permission, wrong issue key). Entries without an issue key are
  never pushed and shown as "local only".
- Sync view in the main window: pending + modified entries, push actions,
  per-entry status/error column.
- Client tests against a stubbed `HttpMessageHandler` (no live Jira in tests).

**Done when:** a reviewed day can be pushed to real Jira Cloud issues; worklogs
appear with correct start time and duration; a post-push edit re-syncs via PUT;
failures surface per entry without corrupting local state.

## Milestone 5 — Packaging & polish

- Verify single-file publish on both RIDs; smoke test on a Linux desktop
  (X11 expected fine; on Wayland accept compositor-managed placement).
- Version stamping, app icon, README (install = copy the binary; where data lives;
  how to create a Jira API token).
- Optional: GitHub Actions workflow producing both binaries per tag.

**Done when:** a colleague can download one file, run it, configure Jira, and track.

---

## Risks

- **Wayland widget placement** — compositors may ignore self-positioning and
  always-on-top. Accepted for the secondary platform; X11 and Windows unaffected.
- **LiteDB maintenance pace** — mitigated by the repository interfaces and the
  triviality of the schema; storage is swappable without touching UI or sync.
- **Reflection vs. future trimming** — do not enable `PublishTrimmed`/AOT without
  revisiting LiteDB mapping; noted in the csproj as a comment.

## Suggested first session

Milestone 1 in full, plus the M2 `TrackingService` with tests — everything
headless, fast to verify, and the foundation the UI work sits on.
