// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.App.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using Takt.Core.Domain;

/// <summary>
/// One row of the entry overview. Wraps a <see cref="TimeEntry"/> and renders its
/// times in the local time zone; the stored timestamps stay UTC.
/// </summary>
public sealed partial class TimeEntryRowViewModel : ObservableObject
{
    private readonly TimeZoneInfo _timeZone;

    [ObservableProperty]
    private String _durationText = String.Empty;

    /// <summary>Creates a row for the given entry.</summary>
    /// <param name="entry">The entry to render.</param>
    /// <param name="timeZone">The time zone the times are rendered in.</param>
    /// <param name="utcNow">The current UTC instant, used for the running entry.</param>
    public TimeEntryRowViewModel(TimeEntry entry, TimeZoneInfo timeZone, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(timeZone);
        Entry = entry;
        _timeZone = timeZone;
        Update(utcNow);
    }

    /// <summary>The tracked duration as of the last <see cref="Update"/>.</summary>
    public TimeSpan Duration { get; private set; }

    /// <summary>The underlying entry.</summary>
    public TimeEntry Entry { get; }

    /// <summary>Indicates whether the entry carries a Jira issue key.</summary>
    public Boolean HasIssueKey => !String.IsNullOrEmpty(Entry.JiraIssueKey);

    /// <summary>Indicates whether the entry was edited after having been pushed to Jira.</summary>
    public Boolean IsModified => !Entry.IsRunning && Entry.SyncState == SyncState.LocallyModified;

    /// <summary>Indicates whether this row is the running timer.</summary>
    public Boolean IsRunning => Entry.IsRunning;

    /// <summary>Indicates whether the entry is in sync with Jira.</summary>
    public Boolean IsSynced => !Entry.IsRunning && Entry.SyncState == SyncState.Synced;

    /// <summary>The Jira issue key, or <c>null</c>.</summary>
    public String? IssueKey => Entry.JiraIssueKey;

    /// <summary>The local date the entry started on; the day it is grouped under.</summary>
    public DateOnly LocalDate => DateOnly.FromDateTime(ToLocal(Entry.StartedAt));

    /// <summary>The sync state rendered as text.</summary>
    public String StatusText => Entry.IsRunning
        ? "Recording"
        : Entry.SyncState switch
        {
            SyncState.Synced => "Synced",
            SyncState.LocallyModified => "Modified",
            _ => "Local"
        };

    /// <summary>The display name of the tracked task.</summary>
    public String TaskName => Entry.TaskName;

    /// <summary>The local start and end times, for example <c>08:30 – 09:15</c>.</summary>
    public String TimeRangeText
    {
        get
        {
            var start = TimeFormat.FormatTimeOfDay(ToLocal(Entry.StartedAt));
            var end = Entry.EndedAt is { } endedAt ? TimeFormat.FormatTimeOfDay(ToLocal(endedAt)) : "now";
            return $"{start} – {end}";
        }
    }

    /// <summary>Recomputes the duration; only a running entry changes over time.</summary>
    /// <param name="utcNow">The current UTC instant.</param>
    public void Update(DateTime utcNow)
    {
        Duration = Entry.GetDuration(utcNow);
        DurationText = Entry.IsRunning ? TimeFormat.FormatClock(Duration) : TimeFormat.FormatDuration(Duration);
    }

    private DateTime ToLocal(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _timeZone);
}
