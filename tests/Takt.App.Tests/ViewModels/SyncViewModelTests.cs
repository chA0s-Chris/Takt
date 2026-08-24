// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.App.Tests.ViewModels;

using FluentAssertions;
using NUnit.Framework;
using Takt.App.ViewModels;
using Takt.Core.Domain;
using Takt.Core.Storage;
using Takt.Core.Tests.TestSupport;

[TestFixture]
public class SyncViewModelTests
{
    private static readonly DateTime BaseTime = new(2026, 8, 21, 9, 0, 0, DateTimeKind.Utc);

    private LiteDbTimeEntryRepository _entries;
    private StubJiraClient _jira;
    private TempDatabase _tempDatabase;
    private TestTimeProvider _timeProvider;
    private SyncViewModel _viewModel;

    [SetUp]
    public void SetUp()
    {
        _tempDatabase = new();
        _entries = new(_tempDatabase.Database);
        _jira = new()
        {
            IsConfigured = true
        };
        _timeProvider = new()
        {
            UtcNow = BaseTime
        };
        _viewModel = new(new(_entries, _jira), new(_jira), _jira, _timeProvider, new(),
                         new(_entries, new LiteDbTemplateRepository(_tempDatabase.Database)));
    }

    [TearDown]
    public void TearDown() => _tempDatabase.Dispose();

    [Test]
    public void Refresh_GroupsThePendingEntriesByDay()
    {
        Insert("Investigate gateway timeouts", "TEAM-1187", BaseTime);
        Insert("Code review", "TEAM-2", BaseTime.AddHours(3));
        Insert("Standup", "TEAM-3", BaseTime.AddDays(1));

        _viewModel.Refresh();

        _viewModel.Days.Should().HaveCount(2);
        _viewModel.Days[0].Rows.Select(row => row.TaskName)
                  .Should().Equal("Investigate gateway timeouts", "Code review");
        _viewModel.Days[0].TotalText.Should().Be("4 h 00 m");
        _viewModel.IsEmpty.Should().BeFalse();
        _viewModel.StatusText.Should().Be("3 entries ready to push · 6 h 00 m");
    }

    [Test]
    public void Refresh_ReportsThatNothingIsWaiting()
    {
        var synced = Insert("Investigate gateway timeouts", "TEAM-1187", BaseTime);
        synced.SyncState = SyncState.Synced;
        _entries.Update(synced);

        _viewModel.Refresh();

        _viewModel.Days.Should().BeEmpty();
        _viewModel.IsEmpty.Should().BeTrue();
        _viewModel.StatusText.Should().Contain("Nothing to push");
        _viewModel.LocalOnlyText.Should().BeNull();
    }

    [Test]
    public void Refresh_MentionsTheEntriesWithoutAnIssueKey()
    {
        Insert("Investigate gateway timeouts", "TEAM-1187", BaseTime);
        Insert("Meetings", null, BaseTime.AddHours(3));
        Insert("Reading", null, BaseTime.AddHours(5));

        _viewModel.Refresh();

        _viewModel.Days.Should().ContainSingle().Which.Rows.Should().ContainSingle();
        _viewModel.LocalOnlyText.Should().Be("2 entries have no issue key and stay local.");
    }

    [Test]
    public void Refresh_WarnsWhenJiraIsNotConfigured()
    {
        _jira.IsConfigured = false;

        _viewModel.Refresh();

        _viewModel.IsNotConfigured.Should().BeTrue();
    }

    [Test]
    public async Task PushAll_PushesEveryEntryAndKeepsTheRowsWithTheirResult()
    {
        Insert("Investigate gateway timeouts", "TEAM-1187", BaseTime);
        Insert("Standup", "TEAM-3", BaseTime.AddDays(1));
        _viewModel.Refresh();

        await _viewModel.PushAllCommand.ExecuteAsync(null);

        _jira.CreatedWorklogs.Should().HaveCount(2);
        var rows = _viewModel.Days.SelectMany(day => day.Rows).ToList();
        rows.Should().OnlyContain(row => row.IsPushed && !row.IsPushable && !row.HasFailed);
        rows[0].ResultText.Should().Be("Pushed to TEAM-1187.");
        _viewModel.StatusText.Should().Be("All pushed.");
        _viewModel.IsBusy.Should().BeFalse();
    }

    [Test]
    public async Task PushDay_OnlyPushesTheEntriesOfThatDay()
    {
        Insert("Investigate gateway timeouts", "TEAM-1187", BaseTime);
        Insert("Standup", "TEAM-3", BaseTime.AddDays(1));
        _viewModel.Refresh();

        await _viewModel.PushDayCommand.ExecuteAsync(_viewModel.Days[1]);

        _jira.CreatedWorklogs.Should().ContainSingle().Which.IssueKey.Should().Be("TEAM-3");
        _viewModel.Days[0].Rows[0].IsPushed.Should().BeFalse();
        _viewModel.Days[1].Rows[0].IsPushed.Should().BeTrue();
        _viewModel.StatusText.Should().Be("1 entry ready to push · 2 h 00 m");
    }

    [Test]
    public async Task PushEntry_ShowsTheFailureAndKeepsTheEntryPushable()
    {
        Insert("Investigate gateway timeouts", "TEAM-1187", BaseTime);
        _viewModel.Refresh();
        _jira.CreateFailure = new("Jira does not know issue TEAM-1187.");

        await _viewModel.PushEntryCommand.ExecuteAsync(_viewModel.Days[0].Rows[0]);

        var row = _viewModel.Days[0].Rows[0];
        row.HasFailed.Should().BeTrue();
        row.IsPushed.Should().BeFalse();
        row.IsPushable.Should().BeTrue();
        row.ResultText.Should().Be("Jira does not know issue TEAM-1187.");
        _viewModel.StatusText.Should().Be("1 entry left · 1 failed");
        _entries.GetById(row.Entry.Id)!.SyncState.Should().Be(SyncState.Local);
    }

    [Test]
    public async Task LoadIssueSummaries_ShowsTheSummaryAndFlagsAnUnknownKey()
    {
        // Seeded before the entries: writing one refreshes the page, which looks the keys
        // up right away and caches what Jira answered.
        _jira.Issues["TEAM-1187"] = new("TEAM-1187", "Gateway returns 504 under load");
        Insert("Investigate gateway timeouts", "TEAM-1187", BaseTime);
        Insert("Typo", "TEAM-9999", BaseTime.AddHours(3));
        _viewModel.Refresh();

        await _viewModel.LoadIssueSummariesAsync();

        var rows = _viewModel.Days[0].Rows;
        rows[0].IssueSummary.Should().Be("Gateway returns 504 under load");
        rows[0].HasUnknownIssue.Should().BeFalse();
        rows[1].IssueSummary.Should().BeNull();
        rows[1].HasUnknownIssue.Should().BeTrue();
    }

    private TimeEntry Insert(String taskName, String? issueKey, DateTime startedAt)
    {
        var entry = new TimeEntry
        {
            TaskName = taskName,
            JiraIssueKey = issueKey,
            StartedAt = startedAt,
            EndedAt = startedAt.AddHours(2)
        };
        _entries.Insert(entry);
        return entry;
    }
}
