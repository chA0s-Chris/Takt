// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.App.Tests.ViewModels;

using FluentAssertions;
using NUnit.Framework;
using Takt.App.Tests.TestSupport;
using Takt.App.ViewModels;
using Takt.Core.Domain;
using Takt.Core.Storage;
using Takt.Core.Tests.TestSupport;
using Takt.Core.Tracking;

/// <summary>
/// Sociable unit tests: the overview runs against the real tracking service and LiteDB
/// repositories on a temporary database; the clock is fixed to a Friday in UTC so the
/// local-time rendering is deterministic.
/// </summary>
[TestFixture]
public class OverviewViewModelTests
{
    private static readonly DateTime BaseTime = new(2026, 8, 21, 9, 0, 0, DateTimeKind.Utc);

    private StubJiraClient _jiraClient;
    private TempDatabase _tempDatabase;
    private LiteDbTimeEntryRepository _timeEntries;
    private TestTimeProvider _timeProvider;
    private TrackingService _trackingService;
    private OverviewViewModel _viewModel;

    [SetUp]
    public void SetUp()
    {
        _tempDatabase = new();
        _timeEntries = new(_tempDatabase.Database);
        _timeProvider = new()
        {
            UtcNow = BaseTime
        };
        _trackingService = new(_timeEntries, _timeProvider);
        _jiraClient = new();
        _viewModel = new(_timeEntries, _trackingService, _jiraClient, _timeProvider);
    }

    [TearDown]
    public void TearDown() => _tempDatabase.Dispose();

    [Test]
    public void Refresh_ShowsTodaysEntriesWithTheirTotal()
    {
        Insert("Daily & sprint planning", BaseTime.AddHours(-0.5), BaseTime.AddHours(0.25), "TEAM-1201");
        Insert("Investigate gateway timeouts", BaseTime, BaseTime.AddHours(2));
        Insert("Yesterday's work", BaseTime.AddDays(-1), BaseTime.AddDays(-1).AddHours(1));

        _viewModel.Refresh();

        _viewModel.Days.Should().HaveCount(1);
        _viewModel.Days[0].Entries.Select(row => row.TaskName)
                  .Should().Equal("Daily & sprint planning", "Investigate gateway timeouts");
        _viewModel.TotalText.Should().Be("2 h 45 m");
        _viewModel.IsEmpty.Should().BeFalse();
    }

    [Test]
    public void Refresh_RendersTheRowsInLocalTime()
    {
        Insert("Daily & sprint planning", BaseTime, BaseTime.AddMinutes(45), "TEAM-1201");

        _viewModel.Refresh();

        var row = _viewModel.Days[0].Entries.Single();
        row.TimeRangeText.Should().Be("09:00 – 09:45");
        row.DurationText.Should().Be("45 m");
        row.IssueKey.Should().Be("TEAM-1201");
        row.HasIssueKey.Should().BeTrue();
        row.StatusText.Should().Be("Local");
    }

    [Test]
    public void Refresh_MarksModifiedEntries()
    {
        Insert("Meetings (Q3)", BaseTime, BaseTime.AddHours(1), "TEAM-1234", SyncState.LocallyModified);

        _viewModel.Refresh();

        var row = _viewModel.Days[0].Entries.Single();
        row.IsModified.Should().BeTrue();
        row.StatusText.Should().Be("Modified");
    }

    [Test]
    public void Tick_CountsTheRunningEntryUp()
    {
        _trackingService.Start("Implement entry editor", "TEAM-1210");
        _timeProvider.UtcNow = BaseTime.AddMinutes(5);

        _viewModel.Tick();

        var row = _viewModel.Days[0].Entries.Single();
        row.IsRunning.Should().BeTrue();
        row.StatusText.Should().Be("Recording");
        row.TimeRangeText.Should().Be("09:00 – now");
        row.DurationText.Should().Be("00:05:00");
        _viewModel.TotalText.Should().Be("5 m");
        _viewModel.RunningStatusText.Should().Be("Timer running since 09:00");
    }

    [Test]
    public void Refresh_ReactsToTrackingChanges()
    {
        _trackingService.Start("Started elsewhere");

        _viewModel.Days.SelectMany(day => day.Entries).Should().ContainSingle();
    }

    [Test]
    public void PreviousPeriod_MovesToTheDayBefore()
    {
        Insert("Yesterday's work", BaseTime.AddDays(-1), BaseTime.AddDays(-1).AddHours(1));

        _viewModel.PreviousPeriodCommand.Execute(null);

        _viewModel.Days[0].Entries.Should().ContainSingle();
        _viewModel.RangeTitle.Should().Be("Thursday, Aug 20");
    }

    [Test]
    public void GoToToday_ReturnsFromAnotherDay()
    {
        _viewModel.NextPeriodCommand.Execute(null);

        _viewModel.GoToTodayCommand.Execute(null);

        _viewModel.RangeTitle.Should().Be("Today, Aug 21");
    }

    [Test]
    public void WeekView_ShowsSevenDaysWithHeaders()
    {
        Insert("Monday work", BaseTime.AddDays(-4), BaseTime.AddDays(-4).AddHours(1));
        Insert("Friday work", BaseTime, BaseTime.AddHours(1));

        _viewModel.ShowWeekCommand.Execute(null);

        _viewModel.IsWeekView.Should().BeTrue();
        _viewModel.Days.Should().HaveCount(7);
        _viewModel.Days.Should().OnlyContain(day => day.IsHeaderVisible);
        _viewModel.Days[0].HeaderText.Should().Be("Monday, Aug 17");
        _viewModel.Days[0].Entries.Should().ContainSingle();
        _viewModel.Days[4].Entries.Should().ContainSingle();
        _viewModel.TotalText.Should().Be("2 h 00 m");
    }

    [Test]
    public void NewEntry_RequestsAnEditorForAnUnsavedEntry()
    {
        EntryEditorViewModel? requested = null;
        _viewModel.EditRequested += (_, editor) => requested = editor;

        _viewModel.NewEntryCommand.Execute(null);

        requested.Should().NotBeNull();
        requested.Title.Should().Be("New entry");
        _timeEntries.GetMostRecent(1).Should().BeEmpty();
    }

    [Test]
    public void Edit_RequestsAnEditorForTheSelectedRow()
    {
        Insert("Code review", BaseTime, BaseTime.AddMinutes(30));
        _viewModel.Refresh();
        EntryEditorViewModel? requested = null;
        _viewModel.EditRequested += (_, editor) => requested = editor;

        _viewModel.Edit(_viewModel.Days[0].Entries.Single());

        requested.Should().NotBeNull();
        requested.Title.Should().Be("Edit entry");
        requested.TaskName.Should().Be("Code review");
    }

    [Test]
    public void Refresh_ReportsAnEmptyDay()
    {
        _viewModel.Refresh();

        _viewModel.IsEmpty.Should().BeTrue();
        _viewModel.TotalText.Should().Be("0 m");
        _viewModel.RunningStatusText.Should().BeNull();
    }

    private void Insert(
        String taskName,
        DateTime startedAt,
        DateTime endedAt,
        String? issueKey = null,
        SyncState syncState = SyncState.Local) =>
        _timeEntries.Insert(new()
        {
            TaskName = taskName,
            StartedAt = startedAt,
            EndedAt = endedAt,
            JiraIssueKey = issueKey,
            SyncState = syncState
        });
}
