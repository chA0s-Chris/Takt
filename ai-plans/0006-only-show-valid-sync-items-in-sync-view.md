# Only show valid sync items in sync view

> Issue: [#6](https://github.com/chA0s-Chris/Takt/issues/6)

## Rationale

The sync view lists every closed, unsynced entry that has a Jira issue key, including entries shorter than one minute. Jira rejects worklogs below one minute, so pushing such a row always fails. The view offers an action that can't succeed.

The sync view should only offer entries Jira will accept. Short entries should stay visible as a count, the same way entries without an issue key are already reported.

## Acceptance Criteria

- [x] Closed, unsynced entries with an issue key and a duration under one minute no longer appear as rows in the sync view. This applies to entries that were never pushed and to entries edited after a push.
- [x] Entries of exactly one minute or longer still appear and can be pushed.
- [x] When short entries exist, the sync view shows a separate muted hint with their count (for example "1 entry is shorter than a minute and stays local." / "2 entries are shorter than a minute and stay local."). No hint is shown when there are none.
- [x] The existing "no issue key" hint is unchanged, and entries without an issue key are not counted in the short-entry hint.
- [x] When the sync view has no rows but short entries or entries without an issue key remain, neither the empty-state text nor the status text claims that every closed entry is in Jira.
- [x] Automated tests cover the pending and short-entry filtering in `SyncService`, including the one-minute boundary and an edited-after-push entry, and the hint text and the empty-list status text in `SyncViewModel`.

## Technical Details

- `SyncService.MinimumDuration` stays the single source of the one-minute rule. `GetPending()` excludes entries whose duration is below it. A new `SyncService` query (e.g. `GetTooShort()`) returns the short entries that have an issue key. The minimum-duration guard in `PushAsync` stays as a safety net.
- `SyncViewModel.Refresh()` fills a new nullable text property for the short-entry hint, following the `LocalOnlyText` pattern. `SyncView.axaml` shows it as another muted `TextBlock` in the status row.
- An edited-after-push entry that is now under a minute keeps its previous worklog in Jira. It's hidden and counted like any other short entry, and it shows up for a re-push again once it's edited back to one minute or longer. This is accepted behavior, not something to fix here.
- Two texts claim everything is in Jira when the list is empty: the `EmptyText` in `SyncView.axaml` ("…every closed entry with an issue key is in Jira.") and the empty-list branch of `SyncViewModel.UpdateStatus` ("Nothing to push — every closed entry is in Jira."). The first becomes inaccurate once short entries are hidden; the second is already inaccurate when entries without an issue key exist. Reword both so they don't claim entries that stay local are in Jira.
