# Takt

A small desktop time tracker for people who log their work in Jira by hand.

Takt lives in a floating widget: start a task, switch to another, pause when you step
away. Everything is stored locally. When a day is done you review it in the main
window and push the worklogs to Jira — deliberately, never automatically.

- **Widget-first.** Tracking, switching, pausing, assigning a Jira issue and writing
  the note all happen in the widget. The main window is for reviewing, correcting and
  pushing.
- **Local-first.** One LiteDB file per user. Nothing is sent anywhere until you press
  a push button.
- **One file to install.** A self-contained executable, no runtime to install, no
  installer, no background service.

## Install

1. Download the binary for your platform from the
   [latest release](https://github.com/chA0s-Chris/Takt/releases/latest):
   `Takt-<version>-win-x64.exe` or `Takt-<version>-linux-x64`.
2. Copy it wherever you keep your tools.
3. Run it.

On Linux, make it executable first: `chmod +x Takt-<version>-linux-x64`.

The binaries are not code-signed. Windows SmartScreen will therefore warn on first
launch — *More info* → *Run anyway*.

Takt starts in the tray. The tray icon opens the widget on click; its menu has
*Show widget*, *Open Takt…*, *Jira settings…* and *Exit*. Closing a window never stops
tracking — only *Exit* does.

## Connecting Jira

Takt talks to Jira Cloud with your e-mail address and a personal API token.

1. Create a token at
   [id.atlassian.com/manage-profile/security/api-tokens](https://id.atlassian.com/manage-profile/security/api-tokens)
   → *Create API token*. Copy it; Atlassian shows it only once.
2. In Takt, open *Settings* (or *Jira settings…* in the tray menu) and fill in:
   - **Base URL** — `https://your-company.atlassian.net`
   - **E-mail** — the address of your Atlassian account
   - **API token** — the token from step 1
3. Press *Test connection*. On success Takt reports the account it reached.

The token is stored in the Windows Credential Manager, or in an AES-encrypted file
with owner-only permissions on Linux. It never goes into the database, the settings or
a log file.

## Tracking

- **Start or switch**: the ▾ button on the widget lists your templates and recent
  tasks, and takes a new task name.
- **Pause / resume**: pausing ends the current stint; resuming starts a new one on the
  same task. The widget shows the task's total for the day, so it keeps counting where
  it left off. Each stint becomes its own Jira worklog.
- **Issue**: the `+ issue` button searches Jira by key and summary text and assigns the
  issue to the running task.
- **Note**: the `+ note` button writes the note that becomes the Jira worklog comment.
  Enter saves, Shift+Enter adds a line.

**Templates** are for work that repeats — "Meetings (Q3)" with its issue key and note.
At the quarter rollover, duplicate the template, rename it and change the issue key.

## Pushing to Jira

The *Sync* page lists everything tracked but not yet in Jira, grouped by day. Push one
entry, a whole day, or everything. What to expect:

- Each entry becomes one worklog: start time, duration, and the note as the comment.
- Editing an entry that was already pushed marks it *modified*; pushing it again
  deletes the old worklog and creates a new one, which also handles an entry you moved
  to a different issue.
- Entries **without an issue key are never pushed** — they stay local and are counted
  in one line on the sync page.
- Entries shorter than a minute are refused: Jira does not accept them.
- Deleting an entry in Takt leaves its Jira worklog alone. The push is one-way on
  purpose.

## Where your data lives

| | |
|---|---|
| Windows | `%LOCALAPPDATA%\Takt` |
| Linux | `$XDG_DATA_HOME/takt`, or `~/.local/share/takt` |

The directory holds `takt.db` (all entries, templates and settings), up to five rotated
backups written on startup, and — on Linux — the encrypted credential file. Moving to
another machine is a matter of copying `takt.db`; the Jira token is not in it and has
to be entered again.

## Building from source

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download) (the exact version is
pinned in `global.json`).

```bash
dotnet test Takt.slnx
dotnet publish src/Takt.App/Takt.App.csproj -c Release -r linux-x64   # or win-x64
```

The publish output is the single executable, roughly 48 MB. Do not add `PublishTrimmed`
or AOT: LiteDB maps documents by reflection and breaks silently under trimming.

## Status and scope

Takt is deliberately small and personal. Out of scope for now: idle detection, worklog
rounding, shared templates and reporting. macOS is not built yet — the code is
platform-neutral, but it has never been run there.

See [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md) for the milestones and the
decisions behind them.

## License

MIT — see [LICENSE](LICENSE).
