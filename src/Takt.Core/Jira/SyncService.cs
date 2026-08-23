// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.Core.Jira;

using Takt.Core.Domain;
using Takt.Core.Storage;

/// <summary>
/// Pushes time entries to Jira worklogs. A push is deliberate and never automatic: the
/// caller decides which entries go, and the local state only moves after Jira confirmed
/// the change. Re-pushing an edited entry deletes the previous worklog and creates a new
/// one, which also covers an entry that was moved to another issue.
/// </summary>
public sealed class SyncService
{
    /// <summary>Jira rejects worklogs shorter than this.</summary>
    public static readonly TimeSpan MinimumDuration = TimeSpan.FromMinutes(1);

    private readonly ITimeEntryRepository _entries;
    private readonly IJiraClient _jira;

    /// <summary>Creates the service.</summary>
    /// <param name="entries">The repository holding the time entries.</param>
    /// <param name="jira">The Jira client used for the worklog calls.</param>
    public SyncService(ITimeEntryRepository entries, IJiraClient jira)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(jira);
        _entries = entries;
        _jira = jira;
    }

    /// <summary>
    /// Returns the completed entries that have no issue key and therefore never reach
    /// Jira, in chronological order.
    /// </summary>
    /// <returns>The entries that stay local.</returns>
    public IReadOnlyList<TimeEntry> GetLocalOnly() =>
        _entries.GetUnsynced()
                .Where(entry => String.IsNullOrWhiteSpace(entry.JiraIssueKey))
                .ToList();

    /// <summary>Returns the completed entries that carry an issue key and are not in sync.</summary>
    /// <returns>The entries awaiting a push, in chronological order.</returns>
    public IReadOnlyList<TimeEntry> GetPending() =>
        _entries.GetUnsynced()
                .Where(entry => !String.IsNullOrWhiteSpace(entry.JiraIssueKey))
                .ToList();

    /// <summary>
    /// Pushes one entry. Entries that cannot be pushed — running, without an issue key,
    /// or too short — are reported as failures rather than thrown, so a bulk push keeps
    /// going and the reason lands next to the entry.
    /// </summary>
    /// <param name="entry">The entry to push.</param>
    /// <param name="cancellationToken">Cancels the Jira calls.</param>
    /// <returns>The outcome of the push.</returns>
    public async Task<SyncResult> PushAsync(TimeEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (!_jira.IsConfigured)
        {
            return Failure(entry, "Jira is not configured yet — add the base URL, e-mail, and API token first.");
        }

        if (entry.EndedAt is not { } endedAt)
        {
            return Failure(entry, "Still running — stop the timer before pushing.");
        }

        if (String.IsNullOrWhiteSpace(entry.JiraIssueKey))
        {
            return Failure(entry, "No issue key — this entry stays local.");
        }

        var duration = endedAt - entry.StartedAt;
        if (duration < MinimumDuration)
        {
            return Failure(entry, "Shorter than a minute — Jira does not accept such worklogs.");
        }

        try
        {
            await RemovePreviousWorklogAsync(entry, cancellationToken).ConfigureAwait(false);

            var worklogId = await _jira
                                  .CreateWorklogAsync(
                                      new(entry.JiraIssueKey, entry.StartedAt, duration, entry.Note),
                                      cancellationToken)
                                  .ConfigureAwait(false);

            entry.JiraWorklogId = worklogId;
            entry.JiraWorklogIssueKey = entry.JiraIssueKey;
            entry.SyncState = SyncState.Synced;
            _entries.Update(entry);

            return Success(entry, $"Pushed to {entry.JiraIssueKey}.");
        }
        catch (JiraException exception)
        {
            return Failure(entry, exception.Message);
        }
    }

    private static SyncResult Failure(TimeEntry entry, String message) =>
        new(entry.Id, entry.TaskName, false, message);

    private static SyncResult Success(TimeEntry entry, String message) =>
        new(entry.Id, entry.TaskName, true, message);

    /// <summary>
    /// Deletes the worklog of an already pushed entry, if there is one. The entry forgets
    /// the worklog right away: once Jira no longer has it, a failed retry must not try to
    /// delete it a second time.
    /// </summary>
    private async Task RemovePreviousWorklogAsync(TimeEntry entry, CancellationToken cancellationToken)
    {
        if (entry.JiraWorklogId is not { Length: > 0 } worklogId)
        {
            return;
        }

        var issueKey = entry.JiraWorklogIssueKey is { Length: > 0 } previousKey ? previousKey : entry.JiraIssueKey!;
        await _jira.DeleteWorklogAsync(issueKey, worklogId, cancellationToken).ConfigureAwait(false);

        entry.JiraWorklogId = null;
        entry.JiraWorklogIssueKey = null;
        entry.SyncState = SyncState.Local;
        _entries.Update(entry);
    }
}
