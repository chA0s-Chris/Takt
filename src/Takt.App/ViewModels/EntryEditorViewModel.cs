// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.App.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Globalization;
using Takt.Core.Domain;
using Takt.Core.Jira;
using Takt.Core.Storage;

/// <summary>
/// Creates or edits one time entry. Dates and times are edited in local time and
/// stored as UTC. Editing an entry that was already pushed to Jira flips it back to
/// <see cref="SyncState.LocallyModified"/>. Overlapping entries are reported but never
/// block saving; the running entry keeps its open end.
/// </summary>
public sealed partial class EntryEditorViewModel : ObservableObject
{
    private const String TimeFormatPattern = "HH:mm";

    private readonly Boolean _isNew;
    private readonly ITimeEntryRepository _timeEntries;
    private readonly TimeProvider _timeProvider;

    [ObservableProperty]
    private String _durationText = String.Empty;

    [ObservableProperty]
    private DateTime? _endDate;

    [ObservableProperty]
    private String _endTimeText = String.Empty;

    [ObservableProperty]
    private Boolean _isDeleteConfirmationVisible;

    [ObservableProperty]
    private String? _issueKey;

    [ObservableProperty]
    private String? _note;

    [ObservableProperty]
    private String? _overlapWarning;

    [ObservableProperty]
    private DateTime? _startDate;

    [ObservableProperty]
    private String _startTimeText = String.Empty;

    [ObservableProperty]
    private String _taskName = String.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private String? _validationError;

    /// <summary>Creates the editor for a new or an existing entry.</summary>
    /// <param name="entry">The entry to edit, or <c>null</c> to create one.</param>
    /// <param name="timeEntries">The entry repository.</param>
    /// <param name="jiraClient">The Jira client backing the issue search.</param>
    /// <param name="timeProvider">The clock and time zone used for the conversions.</param>
    /// <param name="defaultDate">The local date a new entry starts on.</param>
    public EntryEditorViewModel(
        TimeEntry? entry,
        ITimeEntryRepository timeEntries,
        IJiraClient jiraClient,
        TimeProvider timeProvider,
        DateOnly defaultDate)
    {
        ArgumentNullException.ThrowIfNull(timeEntries);
        ArgumentNullException.ThrowIfNull(jiraClient);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeEntries = timeEntries;
        _timeProvider = timeProvider;
        _isNew = entry is null;
        Entry = entry ?? CreateEntry(timeProvider, defaultDate);
        IssueSearch = new(jiraClient);

        TaskName = Entry.TaskName;
        IssueKey = Entry.JiraIssueKey;
        Note = Entry.Note;
        var start = ToLocal(Entry.StartedAt);
        StartDate = start.Date;
        StartTimeText = start.ToString(TimeFormatPattern, CultureInfo.CurrentCulture);
        if (Entry.EndedAt is { } endedAt)
        {
            var end = ToLocal(endedAt);
            EndDate = end.Date;
            EndTimeText = end.ToString(TimeFormatPattern, CultureInfo.CurrentCulture);
        }

        Validate();
    }

    /// <summary>Raised when the dialog should close; <c>true</c> when the entry was saved or deleted.</summary>
    public event EventHandler<Boolean>? CloseRequested;

    /// <summary>Raised after an issue was picked, so the view can close the search flyout.</summary>
    public event EventHandler? IssueAssigned;

    /// <summary>The edited entry. Only <see cref="Save"/> writes it to the database.</summary>
    public TimeEntry Entry { get; }

    /// <summary>Indicates whether the entry can be deleted (existing entries only).</summary>
    public Boolean IsDeletable => !_isNew;

    /// <summary>Indicates whether the end of the entry can be edited; the running timer has none.</summary>
    public Boolean IsEndEditable => !Entry.IsRunning;

    /// <summary>Indicates whether the edited entry is the running timer.</summary>
    public Boolean IsRunning => Entry.IsRunning;

    /// <summary>The Jira issue search behind the issue field.</summary>
    public JiraIssueSearchViewModel IssueSearch { get; }

    /// <summary>The dialog title.</summary>
    public String Title => _isNew ? "New entry" : "Edit entry";

    private static DateTime ConvertToUtc(DateTime local, TimeZoneInfo zone)
    {
        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(local))
        {
            // A local time skipped by a daylight-saving change; move it past the gap.
            local = local.AddHours(1);
        }

        return TimeZoneInfo.ConvertTimeToUtc(local, zone);
    }

    private static TimeEntry CreateEntry(TimeProvider timeProvider, DateOnly defaultDate)
    {
        var zone = timeProvider.LocalTimeZone;
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(timeProvider.GetUtcNow().UtcDateTime, zone);
        var localEnd = DateOnly.FromDateTime(localNow) == defaultDate
            ? localNow.AddTicks(-(localNow.Ticks % TimeSpan.TicksPerMinute))
            : defaultDate.ToDateTime(new(17, 0));
        return new()
        {
            StartedAt = ConvertToUtc(localEnd.AddMinutes(-30), zone),
            EndedAt = ConvertToUtc(localEnd, zone),
            SyncState = SyncState.Local
        };
    }

    private static String? Normalize(String? value) => String.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Boolean TryParseTime(String text, out TimeOnly time) =>
        TimeOnly.TryParse(text, CultureInfo.CurrentCulture, out time)
        || TimeOnly.TryParseExact(text, TimeFormatPattern, CultureInfo.InvariantCulture, DateTimeStyles.None, out time);

    [RelayCommand]
    private void AssignIssue(JiraIssueSummary? issue)
    {
        if (issue is null)
        {
            return;
        }

        IssueKey = issue.Key;
        IssueSearch.Clear();
        IssueAssigned?.Invoke(this, EventArgs.Empty);
    }

    private Boolean CanSave() => ValidationError is null;

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(this, false);

    [RelayCommand]
    private void ClearIssue() => IssueKey = null;

    [RelayCommand]
    private void ConfirmDelete()
    {
        if (_isNew)
        {
            return;
        }

        _timeEntries.Delete(Entry.Id);
        CloseRequested?.Invoke(this, true);
    }

    [RelayCommand]
    private void Delete() => IsDeleteConfirmationVisible = true;

    private String? DescribeOverlap(DateTime startUtc, DateTime endUtc)
    {
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var neighbours = _timeEntries.GetBetween(startUtc.AddDays(-1), endUtc.AddDays(1));
        foreach (var other in neighbours)
        {
            if (other.Id == Entry.Id)
            {
                continue;
            }

            var otherEnd = other.EndedAt ?? nowUtc;
            if (other.StartedAt < endUtc && otherEnd > startUtc)
            {
                var range = $"{TimeFormat.FormatTimeOfDay(ToLocal(other.StartedAt))} – "
                            + $"{TimeFormat.FormatTimeOfDay(ToLocal(otherEnd))}";
                return $"Overlaps with \"{other.TaskName}\" ({range}). Saving is still allowed.";
            }
        }

        return null;
    }

    partial void OnEndDateChanged(DateTime? value) => Validate();

    partial void OnEndTimeTextChanged(String value) => Validate();

    partial void OnStartDateChanged(DateTime? value) => Validate();

    partial void OnStartTimeTextChanged(String value) => Validate();

    partial void OnTaskNameChanged(String value) => Validate();

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        if (!TryBuildRange(out var startUtc, out var endUtc))
        {
            return;
        }

        Entry.TaskName = TaskName.Trim();
        Entry.JiraIssueKey = Normalize(IssueKey);
        Entry.Note = Normalize(Note);
        Entry.StartedAt = startUtc;
        Entry.EndedAt = Entry.IsRunning ? null : endUtc;
        if (Entry.SyncState == SyncState.Synced)
        {
            Entry.SyncState = SyncState.LocallyModified;
        }

        if (_isNew)
        {
            _timeEntries.Insert(Entry);
        }
        else
        {
            _timeEntries.Update(Entry);
        }

        CloseRequested?.Invoke(this, true);
    }

    private DateTime ToLocal(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _timeProvider.LocalTimeZone);

    private DateTime ToUtc(DateTime local) => ConvertToUtc(local, _timeProvider.LocalTimeZone);

    private Boolean TryBuildRange(out DateTime startUtc, out DateTime endUtc)
    {
        startUtc = default;
        endUtc = default;
        if (StartDate is not { } startDate || !TryParseTime(StartTimeText, out var startTime))
        {
            return false;
        }

        startUtc = ToUtc(startDate.Date.Add(startTime.ToTimeSpan()));
        if (Entry.IsRunning)
        {
            endUtc = _timeProvider.GetUtcNow().UtcDateTime;
            return true;
        }

        if (EndDate is not { } endDate || !TryParseTime(EndTimeText, out var endTime))
        {
            return false;
        }

        endUtc = ToUtc(endDate.Date.Add(endTime.ToTimeSpan()));
        return true;
    }

    private void Validate()
    {
        var hasRange = TryBuildRange(out var startUtc, out var endUtc);
        if (hasRange && endUtc >= startUtc)
        {
            DurationText = TimeFormat.FormatDuration(endUtc - startUtc);
            OverlapWarning = DescribeOverlap(startUtc, endUtc);
        }
        else
        {
            DurationText = String.Empty;
            OverlapWarning = null;
        }

        if (String.IsNullOrWhiteSpace(TaskName))
        {
            ValidationError = "A task name is required.";
        }
        else if (StartDate is null || !TryParseTime(StartTimeText, out _))
        {
            ValidationError = "Enter a start date and a start time (HH:mm).";
        }
        else if (!Entry.IsRunning && (EndDate is null || !TryParseTime(EndTimeText, out _)))
        {
            ValidationError = "Enter an end date and an end time (HH:mm).";
        }
        else
        {
            ValidationError = hasRange && endUtc < startUtc ? "The end must not be before the start." : null;
        }
    }
}
