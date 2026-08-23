// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.Core.Tests.Jira;

using FluentAssertions;
using NUnit.Framework;
using Takt.Core.Domain;
using Takt.Core.Jira;
using Takt.Core.Storage;
using Takt.Core.Tests.TestSupport;

[TestFixture]
public class SyncServiceTests
{
    private static readonly DateTime BaseTime = new(2026, 8, 21, 9, 0, 0, DateTimeKind.Utc);

    private LiteDbTimeEntryRepository _entries;
    private StubJiraClient _jira;
    private SyncService _service;
    private TempDatabase _tempDatabase;

    [SetUp]
    public void SetUp()
    {
        _tempDatabase = new();
        _entries = new(_tempDatabase.Database);
        _jira = new()
        {
            IsConfigured = true
        };
        _service = new(_entries, _jira);
    }

    [TearDown]
    public void TearDown() => _tempDatabase.Dispose();

    [Test]
    public void GetPending_ReturnsUnsyncedEntriesWithAnIssueKey()
    {
        var pending = Insert("Investigate gateway timeouts", "TEAM-1187");
        Insert("Meetings", null);
        Insert("Already pushed", "TEAM-2", SyncState.Synced);

        _service.GetPending().Should().ContainSingle().Which.Id.Should().Be(pending.Id);
    }

    [Test]
    public void GetLocalOnly_ReturnsUnsyncedEntriesWithoutAnIssueKey()
    {
        Insert("Investigate gateway timeouts", "TEAM-1187");
        var local = Insert("Meetings", null);
        Insert("Whitespace instead of a key", "   ");

        _service.GetLocalOnly().Select(entry => entry.TaskName).Should().Equal(local.TaskName, "Whitespace instead of a key");
    }

    [Test]
    public async Task Push_CreatesAWorklogAndMarksTheEntrySynced()
    {
        var entry = Insert("Investigate gateway timeouts", "TEAM-1187", note: "Checked the gateway logs");
        _jira.NextWorklogId = "45001";

        var result = await _service.PushAsync(entry);

        result.Success.Should().BeTrue();
        result.Message.Should().Contain("TEAM-1187");
        _jira.CreatedWorklogs.Should().ContainSingle().Which.Should().Be(
            new JiraWorklog("TEAM-1187", BaseTime, TimeSpan.FromHours(2), "Checked the gateway logs"));
        var stored = _entries.GetById(entry.Id)!;
        stored.SyncState.Should().Be(SyncState.Synced);
        stored.JiraWorklogId.Should().Be("45001");
        stored.JiraWorklogIssueKey.Should().Be("TEAM-1187");
    }

    [Test]
    public async Task Push_DeletesThePreviousWorklogFromTheIssueItWasCreatedOn()
    {
        var entry = Insert("Investigate gateway timeouts", "TEAM-2000", SyncState.LocallyModified);
        entry.JiraWorklogId = "45001";
        entry.JiraWorklogIssueKey = "TEAM-1187";
        _entries.Update(entry);
        _jira.NextWorklogId = "45002";

        var result = await _service.PushAsync(entry);

        result.Success.Should().BeTrue();
        _jira.DeletedWorklogs.Should().ContainSingle().Which.Should().Be(("TEAM-1187", "45001"));
        _jira.CreatedWorklogs.Should().ContainSingle().Which.IssueKey.Should().Be("TEAM-2000");
        var stored = _entries.GetById(entry.Id)!;
        stored.SyncState.Should().Be(SyncState.Synced);
        stored.JiraWorklogId.Should().Be("45002");
        stored.JiraWorklogIssueKey.Should().Be("TEAM-2000");
    }

    [Test]
    public async Task Push_KeepsTheEntryPendingWhenJiraRejectsTheWorklog()
    {
        var entry = Insert("Investigate gateway timeouts", "TEAM-1187");
        _jira.CreateFailure = new("Jira does not know issue TEAM-1187.");

        var result = await _service.PushAsync(entry);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Jira does not know issue TEAM-1187.");
        var stored = _entries.GetById(entry.Id)!;
        stored.SyncState.Should().Be(SyncState.Local);
        stored.JiraWorklogId.Should().BeNull();
    }

    [Test]
    public async Task Push_KeepsTheWorklogWhenItsDeletionFails()
    {
        var entry = Insert("Investigate gateway timeouts", "TEAM-1187", SyncState.LocallyModified);
        entry.JiraWorklogId = "45001";
        entry.JiraWorklogIssueKey = "TEAM-1187";
        _entries.Update(entry);
        _jira.DeleteFailure = new("Jira refused access to TEAM-1187 — check the account's permissions.");

        var result = await _service.PushAsync(entry);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("refused access");
        _jira.CreatedWorklogs.Should().BeEmpty();
        var stored = _entries.GetById(entry.Id)!;
        stored.JiraWorklogId.Should().Be("45001");
        stored.SyncState.Should().Be(SyncState.LocallyModified);
    }

    [Test]
    public async Task Push_ForgetsTheWorklogWhenTheCreationFailsAfterTheDeletion()
    {
        var entry = Insert("Investigate gateway timeouts", "TEAM-1187", SyncState.LocallyModified);
        entry.JiraWorklogId = "45001";
        entry.JiraWorklogIssueKey = "TEAM-1187";
        _entries.Update(entry);
        _jira.CreateFailure = new("Could not reach Jira — check the base URL and your connection.");

        var result = await _service.PushAsync(entry);

        result.Success.Should().BeFalse();
        var stored = _entries.GetById(entry.Id)!;
        stored.JiraWorklogId.Should().BeNull();
        stored.JiraWorklogIssueKey.Should().BeNull();
        stored.SyncState.Should().Be(SyncState.Local);
    }

    [Test]
    public async Task Push_RefusesAnEntryWithoutAnIssueKey()
    {
        var entry = Insert("Meetings", null);

        var result = await _service.PushAsync(entry);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("No issue key");
        _jira.CreatedWorklogs.Should().BeEmpty();
    }

    [Test]
    public async Task Push_RefusesARunningEntry()
    {
        var entry = new TimeEntry
        {
            TaskName = "Investigate gateway timeouts",
            JiraIssueKey = "TEAM-1187",
            StartedAt = BaseTime
        };
        _entries.Insert(entry);

        var result = await _service.PushAsync(entry);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Still running");
        _jira.CreatedWorklogs.Should().BeEmpty();
    }

    [Test]
    public async Task Push_RefusesAnEntryShorterThanAMinute()
    {
        var entry = Insert("Investigate gateway timeouts", "TEAM-1187", duration: TimeSpan.FromSeconds(45));

        var result = await _service.PushAsync(entry);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Shorter than a minute");
        _jira.CreatedWorklogs.Should().BeEmpty();
    }

    [Test]
    public async Task Push_ReportsThatJiraIsNotConfigured()
    {
        _jira.IsConfigured = false;
        var entry = Insert("Investigate gateway timeouts", "TEAM-1187");

        var result = await _service.PushAsync(entry);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not configured");
    }

    [Test]
    public async Task Push_NamesTheEntryInTheResult()
    {
        var entry = Insert("Investigate gateway timeouts", "TEAM-1187");

        var result = await _service.PushAsync(entry);

        result.EntryId.Should().Be(entry.Id);
        result.TaskName.Should().Be("Investigate gateway timeouts");
    }

    private TimeEntry Insert(
        String taskName,
        String? issueKey,
        SyncState syncState = SyncState.Local,
        String? note = null,
        TimeSpan? duration = null)
    {
        var entry = new TimeEntry
        {
            TaskName = taskName,
            JiraIssueKey = issueKey,
            Note = note,
            StartedAt = BaseTime,
            EndedAt = BaseTime + (duration ?? TimeSpan.FromHours(2)),
            SyncState = syncState
        };
        _entries.Insert(entry);
        return entry;
    }
}
