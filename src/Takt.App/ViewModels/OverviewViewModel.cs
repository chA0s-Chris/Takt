// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.App.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Globalization;
using Takt.App.Services;
using Takt.Core.Jira;
using Takt.Core.Storage;
using Takt.Core.Tracking;

/// <summary>
/// The entry overview: one local day or one week at a time, with per-day totals and
/// the running entry counting up. Rows open the entry editor; the editor itself is
/// created here and handed to the view, which owns the dialog.
/// </summary>
public sealed partial class OverviewViewModel : ObservableObject
{
    private readonly IJiraClient _jiraClient;
    private readonly ITimeEntryRepository _timeEntries;
    private readonly TimeProvider _timeProvider;
    private readonly TrackingService _trackingService;

    [ObservableProperty]
    private Boolean _isWeekView;

    [ObservableProperty]
    private String _rangeTitle = String.Empty;

    [ObservableProperty]
    private String? _runningStatusText;

    private DateOnly _selectedDate;

    [ObservableProperty]
    private String _totalText = String.Empty;

    /// <summary>Creates the overview and loads the current day.</summary>
    /// <param name="timeEntries">The entry repository.</param>
    /// <param name="trackingService">The tracking engine, asked for the running entry.</param>
    /// <param name="jiraClient">The Jira client handed to the entry editor.</param>
    /// <param name="timeProvider">The clock and time zone used for the conversions.</param>
    /// <param name="dataChanges">Announces entries written anywhere, the widget included.</param>
    public OverviewViewModel(
        ITimeEntryRepository timeEntries,
        TrackingService trackingService,
        IJiraClient jiraClient,
        TimeProvider timeProvider,
        DataChangeNotifier dataChanges)
    {
        ArgumentNullException.ThrowIfNull(timeEntries);
        ArgumentNullException.ThrowIfNull(trackingService);
        ArgumentNullException.ThrowIfNull(jiraClient);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(dataChanges);
        _timeEntries = timeEntries;
        _trackingService = trackingService;
        _jiraClient = jiraClient;
        _timeProvider = timeProvider;

        _selectedDate = Today;
        dataChanges.TimeEntriesChanged += (_, _) => Refresh();
        Refresh();
    }

    /// <summary>Raised when an entry should be edited; the view shows the dialog.</summary>
    public event EventHandler<EntryEditorViewModel>? EditRequested;

    /// <summary>The visible days, each with its rows and total.</summary>
    public ObservableCollection<DayGroupViewModel> Days { get; } = new();

    /// <summary>Indicates whether the visible range contains no entries at all.</summary>
    public Boolean IsEmpty => Days.All(day => day.Entries.Count == 0);

    private DateOnly RangeStart => IsWeekView ? StartOfWeek(_selectedDate) : _selectedDate;

    private DateOnly Today =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(UtcNow, _timeProvider.LocalTimeZone));

    private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

    /// <summary>Reloads the visible range from the database.</summary>
    public void Refresh()
    {
        var rangeStart = RangeStart;
        var dayCount = IsWeekView ? 7 : 1;
        var fromUtc = ToUtc(rangeStart);
        var toUtc = ToUtc(rangeStart.AddDays(dayCount));
        var utcNow = UtcNow;

        var rows = _timeEntries.GetBetween(fromUtc, toUtc)
                               .Select(entry => new TimeEntryRowViewModel(entry, _timeProvider.LocalTimeZone, utcNow))
                               .ToList();

        Days.Clear();
        for (var offset = 0; offset < dayCount; offset++)
        {
            var date = rangeStart.AddDays(offset);
            var group = new DayGroupViewModel(date, IsWeekView);
            foreach (var row in rows.Where(r => r.LocalDate == date))
            {
                group.Entries.Add(row);
            }

            if (IsWeekView || group.Entries.Count > 0 || date == _selectedDate)
            {
                Days.Add(group);
            }
        }

        RangeTitle = BuildRangeTitle(rangeStart, dayCount);
        UpdateTotals();
        OnPropertyChanged(nameof(IsEmpty));
    }

    /// <summary>Refreshes the running entry's duration and the totals; called once per second.</summary>
    public void Tick()
    {
        var utcNow = UtcNow;
        var hasRunningRow = false;
        foreach (var row in Days.SelectMany(day => day.Entries).Where(row => row.IsRunning))
        {
            row.Update(utcNow);
            hasRunningRow = true;
        }

        if (hasRunningRow)
        {
            UpdateTotals();
        }
    }

    private static DateOnly StartOfWeek(DateOnly date) => date.AddDays(-((Int32)date.DayOfWeek + 6) % 7);

    private String BuildRangeTitle(DateOnly rangeStart, Int32 dayCount)
    {
        var culture = CultureInfo.CurrentCulture;
        if (dayCount == 1)
        {
            return rangeStart == Today
                ? $"Today, {rangeStart.ToDateTime(TimeOnly.MinValue).ToString("MMM d", culture)}"
                : rangeStart.ToDateTime(TimeOnly.MinValue).ToString("dddd, MMM d", culture);
        }

        var last = rangeStart.AddDays(dayCount - 1);
        return $"{rangeStart.ToDateTime(TimeOnly.MinValue).ToString("MMM d", culture)} – "
               + $"{last.ToDateTime(TimeOnly.MinValue).ToString("MMM d, yyyy", culture)}";
    }

    private EntryEditorViewModel CreateEditor(TimeEntryRowViewModel? row) =>
        new(row?.Entry, _timeEntries, _jiraClient, _timeProvider, _selectedDate);

    /// <summary>
    /// Opens the editor for the row with its delete confirmation already showing.
    /// Deleting an entry cannot be undone, so it stays behind the same question the
    /// editor asks instead of becoming one stray click in a dense list.
    /// </summary>
    [RelayCommand]
    private void Delete(TimeEntryRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        var editor = CreateEditor(row);
        editor.DeleteCommand.Execute(null);
        EditRequested?.Invoke(this, editor);
    }

    /// <summary>Opens the editor for the given row.</summary>
    [RelayCommand]
    private void Edit(TimeEntryRowViewModel? row)
    {
        if (row is not null)
        {
            EditRequested?.Invoke(this, CreateEditor(row));
        }
    }

    [RelayCommand]
    private void GoToToday()
    {
        _selectedDate = Today;
        Refresh();
    }

    [RelayCommand]
    private void NewEntry() => EditRequested?.Invoke(this, CreateEditor(null));

    [RelayCommand]
    private void NextPeriod()
    {
        _selectedDate = _selectedDate.AddDays(IsWeekView ? 7 : 1);
        Refresh();
    }

    partial void OnIsWeekViewChanged(Boolean value) => Refresh();

    [RelayCommand]
    private void PreviousPeriod()
    {
        _selectedDate = _selectedDate.AddDays(IsWeekView ? -7 : -1);
        Refresh();
    }

    [RelayCommand]
    private void ShowDay() => IsWeekView = false;

    [RelayCommand]
    private void ShowWeek() => IsWeekView = true;

    private DateTime ToUtc(DateOnly date)
    {
        var local = date.ToDateTime(TimeOnly.MinValue);
        var zone = _timeProvider.LocalTimeZone;
        return TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(zone.IsInvalidTime(local) ? local.AddHours(1) : local, DateTimeKind.Unspecified),
            zone);
    }

    private void UpdateTotals()
    {
        foreach (var day in Days)
        {
            day.UpdateTotal();
        }

        var total = Days.SelectMany(day => day.Entries).Aggregate(TimeSpan.Zero, (sum, row) => sum + row.Duration);
        TotalText = TimeFormat.FormatDuration(total);

        var runningEntry = _trackingService.CurrentEntry;
        RunningStatusText = runningEntry is null
            ? null
            : "Timer running since "
              + TimeFormat.FormatTimeOfDay(
                  TimeZoneInfo.ConvertTimeFromUtc(
                      DateTime.SpecifyKind(runningEntry.StartedAt, DateTimeKind.Utc),
                      _timeProvider.LocalTimeZone));
    }
}
