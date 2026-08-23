// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.App.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using Takt.Core.Domain;
using Takt.Core.Jira;

/// <summary>
/// One entry waiting to be pushed to Jira. The row keeps the outcome of the last push
/// visible instead of vanishing from the list, so it stays clear what happened.
/// </summary>
public sealed partial class SyncRowViewModel : ObservableObject
{
    private readonly TimeZoneInfo _timeZone;

    [ObservableProperty]
    private Boolean _hasFailed;

    [ObservableProperty]
    private Boolean _hasUnknownIssue;

    [ObservableProperty]
    private Boolean _isPushed;

    [ObservableProperty]
    private Boolean _isPushing;

    [ObservableProperty]
    private String? _issueSummary;

    [ObservableProperty]
    private String? _resultText;

    /// <summary>Creates a row for the given entry.</summary>
    /// <param name="entry">The entry to push.</param>
    /// <param name="timeZone">The time zone the times are rendered in.</param>
    public SyncRowViewModel(TimeEntry entry, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(timeZone);
        Entry = entry;
        _timeZone = timeZone;
    }

    /// <summary>The tracked duration. Only closed entries reach the sync list.</summary>
    public TimeSpan Duration => Entry.EndedAt is { } endedAt ? endedAt - Entry.StartedAt : TimeSpan.Zero;

    /// <summary>The tracked duration, for example <c>1 h 30 m</c>.</summary>
    public String DurationText => TimeFormat.FormatDuration(Duration);

    /// <summary>The underlying entry.</summary>
    public TimeEntry Entry { get; }

    /// <summary>Indicates whether the entry has not been pushed yet.</summary>
    public Boolean IsPushable => !IsPushed;

    /// <summary>The Jira issue key.</summary>
    public String? IssueKey => Entry.JiraIssueKey;

    /// <summary>The local date the entry started on; the day it is grouped under.</summary>
    public DateOnly LocalDate => DateOnly.FromDateTime(ToLocal(Entry.StartedAt));

    /// <summary>The note, which becomes the worklog comment.</summary>
    public String? Note => Entry.Note;

    /// <summary>Whether this is a first push or a re-push of an edited entry.</summary>
    public String StatusText => Entry.SyncState == SyncState.LocallyModified ? "Edited after push" : "Not pushed yet";

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

    /// <summary>Records the outcome of a push.</summary>
    /// <param name="result">The result reported by the sync service.</param>
    public void Apply(SyncResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        IsPushing = false;
        IsPushed = result.Success;
        HasFailed = !result.Success;
        ResultText = result.Message;
        OnPropertyChanged(nameof(IsPushable));
        OnPropertyChanged(nameof(StatusText));
    }

    /// <summary>Shows the issue summary next to the key, or marks the key as unknown.</summary>
    /// <param name="issue">The issue Jira returned, or <c>null</c> when it does not know the key.</param>
    public void ApplyIssue(JiraIssueSummary? issue)
    {
        HasUnknownIssue = issue is null;
        IssueSummary = issue?.Summary;
    }

    /// <summary>Marks the row as being pushed right now.</summary>
    public void BeginPush()
    {
        IsPushing = true;
        HasFailed = false;
        ResultText = null;
    }

    private DateTime ToLocal(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _timeZone);
}
