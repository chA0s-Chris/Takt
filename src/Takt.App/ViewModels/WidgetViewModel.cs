// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.App.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using Takt.Core.Domain;
using Takt.Core.Storage;
using Takt.Core.Tracking;

/// <summary>
/// Drives the floating widget: the current task with its live elapsed time, and the
/// quick-switch list built from templates and recent tasks.
/// </summary>
public sealed partial class WidgetViewModel : ObservableObject
{
    private const Int32 MaxQuickSwitchItems = 8;
    private const String NotTrackingText = "Not tracking";
    private const String ZeroElapsedText = "00:00:00";

    private readonly ITemplateRepository _templates;
    private readonly ITimeEntryRepository _timeEntries;
    private readonly TimeProvider _timeProvider;
    private readonly TrackingService _trackingService;

    private TimeEntry? _currentEntry;

    [ObservableProperty]
    private String? _currentIssueKey;

    [ObservableProperty]
    private String _currentTaskName = NotTrackingText;

    [ObservableProperty]
    private String _elapsedText = ZeroElapsedText;

    [ObservableProperty]
    private Boolean _isTracking;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartNewTaskCommand))]
    private String? _newTaskName;

    /// <summary>Creates the view model and loads the current tracking state.</summary>
    /// <param name="trackingService">The tracking engine.</param>
    /// <param name="templates">The template repository feeding the quick-switch list.</param>
    /// <param name="timeEntries">The entry repository feeding the recent tasks.</param>
    /// <param name="timeProvider">The clock used to render the elapsed time.</param>
    public WidgetViewModel(
        TrackingService trackingService,
        ITemplateRepository templates,
        ITimeEntryRepository timeEntries,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(trackingService);
        ArgumentNullException.ThrowIfNull(templates);
        ArgumentNullException.ThrowIfNull(timeEntries);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _trackingService = trackingService;
        _templates = templates;
        _timeEntries = timeEntries;
        _timeProvider = timeProvider;

        _trackingService.TrackingChanged += (_, _) => Refresh();
        Refresh();
    }

    /// <summary>Raised after a quick-switch or new-task start, so the view can close the flyout.</summary>
    public event EventHandler? SwitchCompleted;

    /// <summary>The quick-switch entries: active templates first, then distinct recent tasks.</summary>
    public ObservableCollection<QuickSwitchItem> QuickSwitchItems { get; } = new();

    /// <summary>
    /// Re-reads the tracking state and rebuilds the quick-switch list. Called on every
    /// tracking change; safe to call at any time.
    /// </summary>
    public void Refresh()
    {
        _currentEntry = _trackingService.CurrentEntry;
        IsTracking = _currentEntry is not null;
        CurrentTaskName = _currentEntry?.TaskName ?? NotTrackingText;
        CurrentIssueKey = _currentEntry?.JiraIssueKey;
        Tick();
        LoadQuickSwitchItems();
    }

    /// <summary>Updates the elapsed time display. Called once per second by the view.</summary>
    public void Tick()
    {
        var entry = _currentEntry;
        ElapsedText = entry is null
            ? ZeroElapsedText
            : FormatElapsed(entry.GetDuration(_timeProvider.GetUtcNow().UtcDateTime));
    }

    private static String FormatElapsed(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        return $"{(Int32)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}";
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

    [RelayCommand]
    private void Stop() => _trackingService.Stop();
}
