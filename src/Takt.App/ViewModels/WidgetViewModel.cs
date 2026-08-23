// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.App.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using Takt.Core.Domain;
using Takt.Core.Jira;
using Takt.Core.Storage;
using Takt.Core.Tracking;

/// <summary>
/// Drives the floating widget: the running or paused task with its elapsed time, and
/// the quick-switch list built from templates and recent tasks. Pausing ends the
/// current entry; resuming starts a new entry on the same task, so Jira later receives
/// one worklog per work stint. The time readout shows the displayed task's accumulated
/// time for today, so pausing freezes it and resuming continues from where it stood.
/// </summary>
public sealed partial class WidgetViewModel : ObservableObject
{
    private const Int32 MaxQuickSwitchItems = 8;
    private const String NotTrackingText = "Not tracking";
    private const String SetIssueText = "+ issue";
    private const String SetNoteText = "+ note";
    private const String ZeroElapsedText = "00:00:00";

    private readonly ISettingsRepository _settings;
    private readonly ITemplateRepository _templates;
    private readonly ITimeEntryRepository _timeEntries;
    private readonly TimeProvider _timeProvider;
    private readonly TrackingService _trackingService;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ResumeCommand))]
    private Boolean _canResume;

    private TimeSpan _completedToday;

    private DateOnly _completedTodayDate;

    private TimeEntry? _currentEntry;

    [ObservableProperty]
    private String? _currentIssueKey;

    [ObservableProperty]
    private String _currentTaskName = NotTrackingText;

    [ObservableProperty]
    private String _elapsedText = ZeroElapsedText;

    [ObservableProperty]
    private Boolean _isIssueButtonVisible;

    [ObservableProperty]
    private Boolean _isNoteButtonVisible;

    [ObservableProperty]
    private Boolean _isTracking;

    [ObservableProperty]
    private String _issueButtonText = SetIssueText;

    private TimeEntry? _lastEntry;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartNewTaskCommand))]
    private String? _newTaskName;

    [ObservableProperty]
    private String _noteButtonText = SetNoteText;

    [ObservableProperty]
    private String? _noteDraft;

    /// <summary>Creates the view model and loads the current tracking state.</summary>
    /// <param name="trackingService">The tracking engine.</param>
    /// <param name="templates">The template repository feeding the quick-switch list.</param>
    /// <param name="timeEntries">The entry repository feeding the recent tasks.</param>
    /// <param name="settings">The settings repository holding the widget preferences.</param>
    /// <param name="timeProvider">The clock used to render the elapsed time.</param>
    /// <param name="jiraClient">The Jira client used for the issue search.</param>
    public WidgetViewModel(
        TrackingService trackingService,
        ITemplateRepository templates,
        ITimeEntryRepository timeEntries,
        ISettingsRepository settings,
        TimeProvider timeProvider,
        IJiraClient jiraClient)
    {
        ArgumentNullException.ThrowIfNull(trackingService);
        ArgumentNullException.ThrowIfNull(templates);
        ArgumentNullException.ThrowIfNull(timeEntries);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(jiraClient);
        _trackingService = trackingService;
        _templates = templates;
        _timeEntries = timeEntries;
        _settings = settings;
        _timeProvider = timeProvider;
        IssueSearch = new(jiraClient);

        _trackingService.TrackingChanged += (_, _) => Refresh();
        Refresh();
    }

    /// <summary>Raised after a Jira issue was assigned, so the view can close the search flyout.</summary>
    public event EventHandler? IssueAssigned;

    /// <summary>Raised after the note was saved, so the view can close the note flyout.</summary>
    public event EventHandler? NoteSaved;

    /// <summary>Raised after a quick-switch or new-task start, so the view can close the flyout.</summary>
    public event EventHandler? SwitchCompleted;

    /// <summary>The Jira issue search behind the widget's issue button.</summary>
    public JiraIssueSearchViewModel IssueSearch { get; }

    /// <summary>The quick-switch entries: active templates first, then distinct recent tasks.</summary>
    public ObservableCollection<QuickSwitchItem> QuickSwitchItems { get; } = new();

    /// <summary>
    /// Re-reads the tracking state and rebuilds the quick-switch list. Called on every
    /// tracking change and whenever the settings change; safe to call at any time.
    /// Without a running timer, the most recent entry is shown as the paused,
    /// resumable task.
    /// </summary>
    public void Refresh()
    {
        _currentEntry = _trackingService.CurrentEntry;
        _lastEntry = _currentEntry is null ? _timeEntries.GetMostRecent(1).FirstOrDefault() : null;
        IsTracking = _currentEntry is not null;
        var displayEntry = _currentEntry ?? _lastEntry;
        CurrentTaskName = displayEntry?.TaskName ?? NotTrackingText;
        CurrentIssueKey = displayEntry?.JiraIssueKey;
        CanResume = _lastEntry is not null;
        IsIssueButtonVisible = displayEntry is not null && _settings.Get().WidgetShowIssueKey;
        IssueButtonText = displayEntry?.JiraIssueKey ?? SetIssueText;
        IsNoteButtonVisible = displayEntry is not null;
        var note = displayEntry?.Note;
        NoteDraft = note;
        NoteButtonText = String.IsNullOrWhiteSpace(note) ? SetNoteText : note;
        RecomputeCompletedToday(displayEntry);
        Tick();
        LoadQuickSwitchItems();
    }

    /// <summary>
    /// Updates the elapsed time display: the displayed task's completed stints of
    /// today plus the running stint. Called once per second by the view. For a paused
    /// task the value stays frozen; resuming continues from where it stood.
    /// </summary>
    public void Tick()
    {
        var displayEntry = _currentEntry ?? _lastEntry;
        if (displayEntry is null)
        {
            ElapsedText = ZeroElapsedText;
            return;
        }

        var now = _timeProvider.GetUtcNow();
        if (DateOnly.FromDateTime(now.ToLocalTime().DateTime) != _completedTodayDate)
        {
            RecomputeCompletedToday(displayEntry);
        }

        var runningStint = _currentEntry?.GetDuration(now.UtcDateTime) ?? TimeSpan.Zero;
        ElapsedText = TimeFormat.FormatClock(_completedToday + runningStint);
    }

    [RelayCommand]
    private void AssignIssue(JiraIssueSummary? issue)
    {
        if (issue is null)
        {
            return;
        }

        var entry = _currentEntry ?? _lastEntry;
        if (entry is null)
        {
            return;
        }

        entry.JiraIssueKey = issue.Key;
        if (entry.SyncState == SyncState.Synced)
        {
            entry.SyncState = SyncState.LocallyModified;
        }

        _timeEntries.Update(entry);
        IssueSearch.Clear();
        Refresh();
        IssueAssigned?.Invoke(this, EventArgs.Empty);
    }

    private Boolean CanStartNewTask() => !String.IsNullOrWhiteSpace(NewTaskName);

    private void LoadQuickSwitchItems()
    {
        var templateItems = _templates.GetActive()
                                      .Select(t => new QuickSwitchItem(t.Name, t.DefaultJiraIssueKey, t.DefaultNote, true));
        var recentItems = _timeEntries.GetMostRecent(20)
                                      .Select(e => new QuickSwitchItem(e.TaskName, e.JiraIssueKey, null, false));

        var items = templateItems
                    .Concat(recentItems)
                    .DistinctBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
                    .Where(i => !String.Equals(i.Name, _currentEntry?.TaskName, StringComparison.OrdinalIgnoreCase))
                    .Take(MaxQuickSwitchItems);

        QuickSwitchItems.Clear();
        foreach (var item in items)
        {
            QuickSwitchItems.Add(item);
        }
    }

    private void OnSwitchCompleted() => SwitchCompleted?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void Pause() => _trackingService.Stop();

    private void RecomputeCompletedToday(TimeEntry? displayEntry)
    {
        var localNow = _timeProvider.GetUtcNow().ToLocalTime();
        _completedTodayDate = DateOnly.FromDateTime(localNow.DateTime);
        if (displayEntry is null)
        {
            _completedToday = TimeSpan.Zero;
            return;
        }

        var startOfDay = new DateTimeOffset(localNow.Date, localNow.Offset);
        var nowUtc = localNow.UtcDateTime;
        _completedToday = _timeEntries
                          .GetBetween(startOfDay.UtcDateTime, startOfDay.AddDays(1).UtcDateTime)
                          .Where(e => !e.IsRunning
                                      && String.Equals(e.TaskName, displayEntry.TaskName, StringComparison.OrdinalIgnoreCase))
                          .Aggregate(TimeSpan.Zero, (total, entry) => total + entry.GetDuration(nowUtc));
    }

    [RelayCommand(CanExecute = nameof(CanResume))]
    private void Resume()
    {
        var lastEntry = _lastEntry;
        if (lastEntry is null)
        {
            return;
        }

        _trackingService.SwitchTo(lastEntry.TaskName, lastEntry.JiraIssueKey, lastEntry.Note);
    }

    /// <summary>
    /// Writes the edited note to the displayed entry. The note becomes the worklog
    /// comment, so it is worth capturing while the work is still fresh rather than
    /// afterwards in the main window.
    /// </summary>
    [RelayCommand]
    private void SaveNote()
    {
        var entry = _currentEntry ?? _lastEntry;
        if (entry is null)
        {
            return;
        }

        var note = NoteDraft?.Trim();
        entry.Note = String.IsNullOrEmpty(note) ? null : note;
        if (entry.SyncState == SyncState.Synced)
        {
            entry.SyncState = SyncState.LocallyModified;
        }

        _timeEntries.Update(entry);
        Refresh();
        NoteSaved?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void StartItem(QuickSwitchItem? item)
    {
        if (item is null)
        {
            return;
        }

        _trackingService.SwitchTo(item.Name, item.JiraIssueKey, item.Note);
        OnSwitchCompleted();
    }

    [RelayCommand(CanExecute = nameof(CanStartNewTask))]
    private void StartNewTask()
    {
        var taskName = NewTaskName;
        if (String.IsNullOrWhiteSpace(taskName))
        {
            return;
        }

        _trackingService.SwitchTo(taskName);
        NewTaskName = null;
        OnSwitchCompleted();
    }
}
