// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.Core.Domain;

/// <summary>
/// A recorded period of work on one task. The entry with <see cref="EndedAt"/> set to
/// <c>null</c> is the currently running timer; at most one such entry may exist.
/// All timestamps are UTC.
/// </summary>
public sealed class TimeEntry
{
    /// <summary>The UTC instant at which tracking ended, or <c>null</c> while the timer is running.</summary>
    public DateTime? EndedAt { get; set; }

    /// <summary>The unique identifier of the entry.</summary>
    public Guid Id { get; set; }

    /// <summary>Indicates whether this entry is the running timer.</summary>
    public Boolean IsRunning => EndedAt is null;

    /// <summary>The associated Jira issue key, for example <c>TEAM-1234</c>. Optional.</summary>
    public String? JiraIssueKey { get; set; }

    /// <summary>The identifier of the Jira worklog created for this entry, once pushed.</summary>
    public String? JiraWorklogId { get; set; }

    /// <summary>
    /// The issue the worklog was created on. Kept separately from <see cref="JiraIssueKey"/>
    /// because an edit may move the entry to another issue, and the old worklog has to be
    /// removed from the issue it actually lives on.
    /// </summary>
    public String? JiraWorklogIssueKey { get; set; }

    /// <summary>An optional free-text note; used as the worklog comment when pushed to Jira.</summary>
    public String? Note { get; set; }

    /// <summary>The UTC instant at which tracking started.</summary>
    public DateTime StartedAt { get; set; }

    /// <summary>The synchronization state relative to Jira.</summary>
    public SyncState SyncState { get; set; }

    /// <summary>The display name of the tracked task.</summary>
    public String TaskName { get; set; } = String.Empty;

    /// <summary>
    /// Returns the tracked duration. For a running entry, the duration is calculated
    /// against <paramref name="utcNow"/>.
    /// </summary>
    /// <param name="utcNow">The current UTC instant used for running entries.</param>
    /// <returns>The elapsed time between start and end (or now).</returns>
    public TimeSpan GetDuration(DateTime utcNow) => (EndedAt ?? utcNow) - StartedAt;
}
