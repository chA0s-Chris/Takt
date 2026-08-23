// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.Core.Tracking;

using Takt.Core.Domain;
using Takt.Core.Storage;

/// <summary>
/// The single entry point for starting, stopping, and switching the running timer.
/// Enforces that at most one <see cref="TimeEntry"/> is open at any time. The open
/// entry itself is the persisted tracking state, so a crash loses nothing.
/// </summary>
public sealed class TrackingService
{
    private readonly ITimeEntryRepository _timeEntries;
    private readonly TimeProvider _timeProvider;

    /// <summary>Creates the service.</summary>
    /// <param name="timeEntries">The repository the entries are persisted in.</param>
    /// <param name="timeProvider">The clock used for start and end timestamps.</param>
    public TrackingService(ITimeEntryRepository timeEntries, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeEntries);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeEntries = timeEntries;
        _timeProvider = timeProvider;
    }

    /// <summary>Raised after tracking started, stopped, or switched.</summary>
    public event EventHandler? TrackingChanged;

    /// <summary>The currently running entry, or <c>null</c> when no timer is running.</summary>
    public TimeEntry? CurrentEntry => _timeEntries.GetOpenEntry();

    /// <summary>Indicates whether a timer is currently running.</summary>
    public Boolean IsTracking => CurrentEntry is not null;

    /// <summary>
    /// Returns the elapsed time of the running timer, or <see cref="TimeSpan.Zero"/>
    /// when no timer is running.
    /// </summary>
    /// <returns>The elapsed time.</returns>
    public TimeSpan GetElapsed()
    {
        var entry = CurrentEntry;
        return entry?.GetDuration(UtcNow()) ?? TimeSpan.Zero;
    }

    /// <summary>
    /// Starts tracking a new task.
    /// </summary>
    /// <param name="taskName">The name of the task.</param>
    /// <param name="jiraIssueKey">The optional Jira issue key.</param>
    /// <param name="note">The optional note.</param>
    /// <returns>The newly created open entry.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="taskName"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a timer is already running.</exception>
    public TimeEntry Start(String taskName, String? jiraIssueKey = null, String? note = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskName);
        if (IsTracking)
        {
            throw new InvalidOperationException(
                "A timer is already running. Stop it first or use SwitchTo to change tasks.");
        }

        var entry = new TimeEntry
        {
            TaskName = taskName.Trim(),
            JiraIssueKey = NormalizeOptional(jiraIssueKey),
            Note = NormalizeOptional(note),
            StartedAt = UtcNow(),
            SyncState = SyncState.Local
        };
        _timeEntries.Insert(entry);
        OnTrackingChanged();
        return entry;
    }

    /// <summary>
    /// Stops the running timer, if any.
    /// </summary>
    /// <returns>The completed entry, or <c>null</c> when no timer was running.</returns>
    public TimeEntry? Stop()
    {
        var entry = _timeEntries.GetOpenEntry();
        if (entry is null)
        {
            return null;
        }

        CloseEntry(entry);
        OnTrackingChanged();
        return entry;
    }

    /// <summary>
    /// Stops the running timer, if any, and starts tracking a new task.
    /// </summary>
    /// <param name="taskName">The name of the new task.</param>
    /// <param name="jiraIssueKey">The optional Jira issue key.</param>
    /// <param name="note">The optional note.</param>
    /// <returns>The newly created open entry.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="taskName"/> is empty.</exception>
    public TimeEntry SwitchTo(String taskName, String? jiraIssueKey = null, String? note = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskName);

        var openEntry = _timeEntries.GetOpenEntry();
        if (openEntry is not null)
        {
            CloseEntry(openEntry);
        }

        var entry = new TimeEntry
        {
            TaskName = taskName.Trim(),
            JiraIssueKey = NormalizeOptional(jiraIssueKey),
            Note = NormalizeOptional(note),
            StartedAt = UtcNow(),
            SyncState = SyncState.Local
        };
        _timeEntries.Insert(entry);
        OnTrackingChanged();
        return entry;
    }

    private static String? NormalizeOptional(String? value) =>
        String.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void CloseEntry(TimeEntry entry)
    {
        entry.EndedAt = UtcNow();
        if (entry.SyncState == SyncState.Synced)
        {
            entry.SyncState = SyncState.LocallyModified;
        }

        _timeEntries.Update(entry);
    }

    private void OnTrackingChanged() => TrackingChanged?.Invoke(this, EventArgs.Empty);

    private DateTime UtcNow() => _timeProvider.GetUtcNow().UtcDateTime;
}
