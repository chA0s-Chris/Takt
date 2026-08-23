// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.App.Tests.ViewModels;

using FluentAssertions;
using NUnit.Framework;
using Takt.App.ViewModels;
using Takt.Core.Domain;
using Takt.Core.Storage;
using Takt.Core.Tests.TestSupport;
using Takt.Core.Tracking;

/// <summary>
/// Sociable unit tests against the real LiteDB repository; the clock is fixed to a
/// Friday in UTC so the local-time conversions are deterministic.
/// </summary>
[TestFixture]
public class EntryEditorViewModelTests
{
    private static readonly DateOnly BaseDate = new(2026, 8, 21);
    private static readonly DateTime BaseTime = new(2026, 8, 21, 9, 0, 0, DateTimeKind.Utc);

    private StubJiraClient _jiraClient;
    private TempDatabase _tempDatabase;
    private LiteDbTimeEntryRepository _timeEntries;
    private TestTimeProvider _timeProvider;

    [SetUp]
    public void SetUp()
    {
        _tempDatabase = new();
        _timeEntries = new(_tempDatabase.Database);
        _timeProvider = new()
        {
            UtcNow = BaseTime
        };
        _jiraClient = new();
    }

    [TearDown]
    public void TearDown() => _tempDatabase.Dispose();

    [Test]
    public void NewEntry_StartsAsAHalfHourEndingNow()
    {
        var editor = CreateEditor(null);

        editor.Title.Should().Be("New entry");
        editor.IsDeletable.Should().BeFalse();
        editor.StartDate.Should().Be(BaseDate.ToDateTime(TimeOnly.MinValue));
        editor.StartTimeText.Should().Be("08:30");
        editor.EndTimeText.Should().Be("09:00");
        editor.DurationText.Should().Be("30 m");
        editor.SaveCommand.CanExecute(null).Should().BeFalse();
    }

    [Test]
    public void Save_InsertsANewEntry()
    {
        var editor = CreateEditor(null);
        var saved = false;
        editor.CloseRequested += (_, result) => saved = result;
        editor.TaskName = "Code review: worklog push";
        editor.IssueKey = "TEAM-1187";
        editor.Note = "Second pass";

        editor.SaveCommand.Execute(null);

        saved.Should().BeTrue();
        var stored = _timeEntries.GetMostRecent(1).Single();
        stored.TaskName.Should().Be("Code review: worklog push");
        stored.JiraIssueKey.Should().Be("TEAM-1187");
        stored.Note.Should().Be("Second pass");
        stored.StartedAt.Should().Be(BaseTime.AddMinutes(-30));
        stored.EndedAt.Should().Be(BaseTime);
        stored.SyncState.Should().Be(SyncState.Local);
    }

    [Test]
    public void Save_WritesTheEditedTimesBackAsUtc()
    {
        var entry = Insert("Meetings (Q3)", BaseTime, BaseTime.AddHours(1));
        var editor = CreateEditor(entry);

        editor.StartTimeText = "12:00";
        editor.EndTimeText = "14:05";
        editor.SaveCommand.Execute(null);

        var stored = _timeEntries.GetById(entry.Id);
        stored.Should().NotBeNull();
        stored.StartedAt.Should().Be(new(2026, 8, 21, 12, 0, 0, DateTimeKind.Utc));
        stored.EndedAt.Should().Be(new(2026, 8, 21, 14, 5, 0, DateTimeKind.Utc));
        editor.DurationText.Should().Be("2 h 05 m");
    }

    [Test]
    public void Save_FlipsASyncedEntryBackToModified()
    {
        var entry = Insert("Meetings (Q3)", BaseTime, BaseTime.AddHours(1), SyncState.Synced);
        var editor = CreateEditor(entry);

        editor.TaskName = "Meetings (Q3) — extended";
        editor.SaveCommand.Execute(null);

        _timeEntries.GetById(entry.Id)!.SyncState.Should().Be(SyncState.LocallyModified);
    }

    [Test]
    public void Validation_RequiresATaskName()
    {
        var editor = CreateEditor(Insert("Code review", BaseTime, BaseTime.AddMinutes(30)));

        editor.TaskName = "  ";

        editor.ValidationError.Should().Contain("task name");
        editor.SaveCommand.CanExecute(null).Should().BeFalse();
    }

    [Test]
    public void Validation_RejectsAnUnparsableTime()
    {
        var editor = CreateEditor(Insert("Code review", BaseTime, BaseTime.AddMinutes(30)));

        editor.EndTimeText = "half past";

        editor.ValidationError.Should().Contain("end");
        editor.SaveCommand.CanExecute(null).Should().BeFalse();
    }

    [Test]
    public void Validation_RejectsAnEndBeforeTheStart()
    {
        var editor = CreateEditor(Insert("Code review", BaseTime, BaseTime.AddMinutes(30)));

        editor.EndTimeText = "08:00";

        editor.ValidationError.Should().Contain("must not be before");
        editor.SaveCommand.CanExecute(null).Should().BeFalse();
    }

    [Test]
    public void Overlap_IsReportedButDoesNotBlockSaving()
    {
        Insert("Code review: worklog push", BaseTime.AddMinutes(40), BaseTime.AddMinutes(70));
        var editor = CreateEditor(Insert("Meetings (Q3)", BaseTime, BaseTime.AddMinutes(30)));

        editor.EndTimeText = "10:05";

        editor.OverlapWarning.Should().Contain("Code review: worklog push").And.Contain("09:40 – 10:10");
        editor.ValidationError.Should().BeNull();
        editor.SaveCommand.CanExecute(null).Should().BeTrue();
    }

    [Test]
    public void Overlap_IgnoresTheEditedEntryItself()
    {
        var editor = CreateEditor(Insert("Meetings (Q3)", BaseTime, BaseTime.AddMinutes(30)));

        editor.EndTimeText = "09:45";

        editor.OverlapWarning.Should().BeNull();
    }

    [Test]
    public void Delete_AsksForConfirmationFirst()
    {
        var entry = Insert("Code review", BaseTime, BaseTime.AddMinutes(30));
        var editor = CreateEditor(entry);
        var closed = false;
        editor.CloseRequested += (_, result) => closed = result;

        editor.DeleteCommand.Execute(null);

        editor.IsDeleteConfirmationVisible.Should().BeTrue();
        closed.Should().BeFalse();
        _timeEntries.GetById(entry.Id).Should().NotBeNull();

        editor.ConfirmDeleteCommand.Execute(null);

        closed.Should().BeTrue();
        _timeEntries.GetById(entry.Id).Should().BeNull();
    }

    [Test]
    public void RunningEntry_KeepsItsOpenEnd()
    {
        var trackingService = new TrackingService(_timeEntries, _timeProvider);
        var entry = trackingService.Start("Implement entry editor");
        var editor = CreateEditor(entry);

        editor.IsRunning.Should().BeTrue();
        editor.IsEndEditable.Should().BeFalse();
        editor.TaskName = "Implement the entry editor";
        editor.SaveCommand.Execute(null);

        var stored = _timeEntries.GetById(entry.Id);
        stored.Should().NotBeNull();
        stored.EndedAt.Should().BeNull();
        stored.TaskName.Should().Be("Implement the entry editor");
    }

    [Test]
    public void AssignIssue_SetsTheKeyAndClearsTheSearch()
    {
        var editor = CreateEditor(Insert("Code review", BaseTime, BaseTime.AddMinutes(30)));
        var assignedCount = 0;
        editor.IssueAssigned += (_, _) => assignedCount++;
        editor.IssueSearch.SearchText = "test";

        editor.AssignIssueCommand.Execute(new("TEAM-1234", "Create tests for XXX"));

        editor.IssueKey.Should().Be("TEAM-1234");
        editor.IssueSearch.SearchText.Should().BeNull();
        assignedCount.Should().Be(1);

        editor.ClearIssueCommand.Execute(null);

        editor.IssueKey.Should().BeNull();
    }

    [Test]
    public void Cancel_ClosesWithoutWriting()
    {
        var entry = Insert("Code review", BaseTime, BaseTime.AddMinutes(30));
        var editor = CreateEditor(entry);
        var saved = true;
        editor.CloseRequested += (_, result) => saved = result;

        editor.TaskName = "Changed but discarded";
        editor.CancelCommand.Execute(null);

        saved.Should().BeFalse();
        _timeEntries.GetById(entry.Id)!.TaskName.Should().Be("Code review");
    }

    private EntryEditorViewModel CreateEditor(TimeEntry? entry) =>
        new(entry, _timeEntries, _jiraClient, _timeProvider, BaseDate);

    private TimeEntry Insert(
        String taskName,
        DateTime startedAt,
        DateTime endedAt,
        SyncState syncState = SyncState.Local)
    {
        var entry = new TimeEntry
        {
            TaskName = taskName,
            StartedAt = startedAt,
            EndedAt = endedAt,
            SyncState = syncState
        };
        _timeEntries.Insert(entry);
        return entry;
    }
}
