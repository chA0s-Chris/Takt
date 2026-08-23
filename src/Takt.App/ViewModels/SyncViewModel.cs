// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.App.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using Takt.App.Services;
using Takt.Core.Jira;

/// <summary>
/// The sync page: everything that has been tracked but is not in Jira yet, grouped by
/// day. Nothing leaves the machine on its own — a push happens because the user asked
/// for one entry, one day, or everything.
/// </summary>
public sealed partial class SyncViewModel : ObservableObject
{
    private readonly JiraIssueCache _issues;
    private readonly IJiraClient _jira;
    private readonly SyncService _sync;
    private readonly TimeProvider _timeProvider;

    [ObservableProperty]
    private Boolean _isBusy;

    [ObservableProperty]
    private Boolean _isEmpty;

    [ObservableProperty]
    private Boolean _isNotConfigured;

    [ObservableProperty]
    private String? _localOnlyText;

    [ObservableProperty]
    private String _statusText = String.Empty;

    /// <summary>Creates the view model.</summary>
    /// <param name="sync">The service performing the pushes.</param>
    /// <param name="issues">The cache used to show issue summaries.</param>
    /// <param name="jira">The Jira client, asked whether it is configured at all.</param>
    /// <param name="timeProvider">Supplies the local time zone.</param>
    /// <param name="notifier">Announces changed settings.</param>
    public SyncViewModel(
        SyncService sync,
        JiraIssueCache issues,
        IJiraClient jira,
        TimeProvider timeProvider,
        SettingsNotifier notifier)
    {
        ArgumentNullException.ThrowIfNull(sync);
        ArgumentNullException.ThrowIfNull(issues);
        ArgumentNullException.ThrowIfNull(jira);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(notifier);
        _sync = sync;
        _issues = issues;
        _jira = jira;
        _timeProvider = timeProvider;

        // Another base URL or account means the looked-up summaries — and the keys that
        // came back unknown — say nothing about the instance now configured.
        notifier.Changed += (_, _) => _issues.Clear();
    }

    /// <summary>The days holding the entries to push, oldest first.</summary>
    public ObservableCollection<SyncDayGroupViewModel> Days { get; } = new();

    /// <summary>
    /// Fills in the issue summaries, which turns a mistyped key into a visible warning
    /// before anything is pushed. Failures are silent: the summaries are a convenience,
    /// and the push itself reports what Jira says.
    /// </summary>
    /// <param name="cancellationToken">Cancels the lookups.</param>
    /// <returns>A task that completes once every key has been looked up.</returns>
    public async Task LoadIssueSummariesAsync(CancellationToken cancellationToken = default)
    {
        if (!_jira.IsConfigured)
        {
            return;
        }

        var byKey = Days.SelectMany(day => day.Rows)
                        .Where(row => !String.IsNullOrWhiteSpace(row.IssueKey))
                        .GroupBy(row => row.IssueKey!, StringComparer.OrdinalIgnoreCase);
        foreach (var group in byKey)
        {
            JiraIssueSummary? issue;
            try
            {
                issue = await _issues.GetAsync(group.Key, cancellationToken).ConfigureAwait(true);
            }
            catch (JiraException)
            {
                return;
            }

            foreach (var row in group)
            {
                row.ApplyIssue(issue);
            }
        }
    }

    /// <summary>Reloads the pending entries; called whenever the page is shown.</summary>
    public void Refresh()
    {
        var timeZone = _timeProvider.LocalTimeZone;
        Days.Clear();
        var days = _sync.GetPending()
                        .Select(entry => new SyncRowViewModel(entry, timeZone))
                        .GroupBy(row => row.LocalDate)
                        .OrderBy(group => group.Key);
        foreach (var group in days)
        {
            var day = new SyncDayGroupViewModel(group.Key);
            foreach (var row in group)
            {
                day.Rows.Add(row);
            }

            day.UpdateTotal();
            Days.Add(day);
        }

        IsEmpty = Days.Count == 0;
        IsNotConfigured = !_jira.IsConfigured;

        var localOnly = _sync.GetLocalOnly().Count;
        LocalOnlyText = localOnly switch
        {
            0 => null,
            1 => "1 entry has no issue key and stays local.",
            _ => $"{localOnly} entries have no issue key and stay local."
        };

        UpdateStatus();
        _ = LoadIssueSummariesAsync();
    }

    [RelayCommand]
    private Task PushAll() => PushRowsAsync(Days.SelectMany(day => day.Rows).ToList());

    [RelayCommand]
    private Task PushDay(SyncDayGroupViewModel? day) =>
        day is null ? Task.CompletedTask : PushRowsAsync(day.Rows.ToList());

    [RelayCommand]
    private Task PushEntry(SyncRowViewModel? row) => row is null ? Task.CompletedTask : PushRowsAsync([row]);

    private async Task PushRowsAsync(IEnumerable<SyncRowViewModel> rows)
    {
        if (IsBusy)
        {
            return;
        }

        var pushable = rows.Where(row => row.IsPushable).ToList();
        if (pushable.Count == 0)
        {
            return;
        }

        IsBusy = true;
        try
        {
            foreach (var row in pushable)
            {
                row.BeginPush();
                var result = await _sync.PushAsync(row.Entry).ConfigureAwait(true);
                row.Apply(result);
                UpdateStatus();
            }
        }
        finally
        {
            IsBusy = false;
            UpdateStatus();
        }
    }

    private void UpdateStatus()
    {
        var pending = Days.SelectMany(day => day.Rows).Where(row => row.IsPushable).ToList();
        var failed = Days.SelectMany(day => day.Rows).Count(row => row.HasFailed);
        if (pending.Count == 0)
        {
            StatusText = Days.Count == 0
                ? "Nothing to push — every closed entry is in Jira."
                : "All pushed.";
            return;
        }

        var total = TimeFormat.FormatDuration(pending.Aggregate(TimeSpan.Zero, (sum, row) => sum + row.Duration));
        var entries = pending.Count == 1 ? "1 entry" : $"{pending.Count} entries";
        StatusText = failed == 0
            ? $"{entries} ready to push · {total}"
            : $"{entries} left · {failed} failed";
    }
}
