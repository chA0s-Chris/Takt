// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.App.Tests.ViewModels;

using FluentAssertions;
using NUnit.Framework;
using Takt.App.ViewModels;
using Takt.Core.Storage;
using Takt.Core.Tests.TestSupport;
using Takt.Core.Tracking;

/// <summary>
/// Sociable unit tests: the view model runs against the real tracking service and
/// LiteDB repositories on a temporary database file; only the clock is a test double.
/// </summary>
[TestFixture]
public class WidgetViewModelTests
{
    private static readonly DateTime BaseTime = new(2026, 8, 23, 9, 0, 0, DateTimeKind.Utc);

    private TempDatabase _tempDatabase;
    private LiteDbTemplateRepository _templates;
    private LiteDbTimeEntryRepository _timeEntries;
    private TestTimeProvider _timeProvider;
    private TrackingService _trackingService;
    private WidgetViewModel _viewModel;

    [SetUp]
    public void SetUp()
    {
        _tempDatabase = new();
        _timeEntries = new(_tempDatabase.Database);
        _templates = new(_tempDatabase.Database);
        _timeProvider = new()
        {
            UtcNow = BaseTime
        };
        _trackingService = new(_timeEntries, _timeProvider);
        _viewModel = new(_trackingService, _templates, _timeEntries, _timeProvider);
    }

    [TearDown]
    public void TearDown() => _tempDatabase.Dispose();

    [Test]
    public void InitialState_ShowsNotTracking()
    {
        _viewModel.IsTracking.Should().BeFalse();
        _viewModel.CurrentTaskName.Should().Be("Not tracking");
        _viewModel.ElapsedText.Should().Be("00:00:00");
        _viewModel.CanResume.Should().BeFalse();
    }

    [Test]
    public void StartItemCommand_StartsTrackingTheItem()
    {
        var item = new QuickSwitchItem("Meetings (Q3)", "TEAM-1234", "Weekly sync", true);

        _viewModel.StartItemCommand.Execute(item);

        _viewModel.IsTracking.Should().BeTrue();
        _viewModel.CurrentTaskName.Should().Be("Meetings (Q3)");
        _viewModel.CurrentIssueKey.Should().Be("TEAM-1234");
        var openEntry = _timeEntries.GetOpenEntry();
        openEntry.Should().NotBeNull();
        openEntry.Note.Should().Be("Weekly sync");
    }

    [Test]
    public void StartNewTaskCommand_RequiresATaskName()
    {
        _viewModel.StartNewTaskCommand.CanExecute(null).Should().BeFalse();

        _viewModel.NewTaskName = "Spike LiteDB";

        _viewModel.StartNewTaskCommand.CanExecute(null).Should().BeTrue();
    }

    [Test]
    public void StartNewTaskCommand_StartsTrackingAndClearsTheInput()
    {
        var switchCompletedCount = 0;
        _viewModel.SwitchCompleted += (_, _) => switchCompletedCount++;
        _viewModel.NewTaskName = "Spike LiteDB";

        _viewModel.StartNewTaskCommand.Execute(null);

        _viewModel.IsTracking.Should().BeTrue();
        _viewModel.CurrentTaskName.Should().Be("Spike LiteDB");
        _viewModel.NewTaskName.Should().BeNull();
        switchCompletedCount.Should().Be(1);
    }

    [Test]
    public void PauseCommand_PausesAndKeepsTheTaskResumable()
    {
        _trackingService.Start("Implement widget", "TEAM-1210");
        _timeProvider.UtcNow = BaseTime.AddMinutes(25);

        _viewModel.PauseCommand.Execute(null);

        _viewModel.IsTracking.Should().BeFalse();
        _viewModel.CanResume.Should().BeTrue();
        _viewModel.CurrentTaskName.Should().Be("Implement widget");
        _viewModel.CurrentIssueKey.Should().Be("TEAM-1210");
        _viewModel.ElapsedText.Should().Be("00:25:00");
    }

    [Test]
    public void Tick_KeepsThePausedElapsedTimeFrozen()
    {
        _trackingService.Start("Implement widget");
        _timeProvider.UtcNow = BaseTime.AddMinutes(25);
        _viewModel.PauseCommand.Execute(null);
        _timeProvider.UtcNow = BaseTime.AddMinutes(55);

        _viewModel.Tick();

        _viewModel.ElapsedText.Should().Be("00:25:00");
    }

    [Test]
    public void ResumeCommand_StartsANewEntryOnTheLastTask()
    {
        _trackingService.Start("Implement widget", "TEAM-1210", "Editor work");
        _timeProvider.UtcNow = BaseTime.AddMinutes(25);
        _viewModel.PauseCommand.Execute(null);
        _timeProvider.UtcNow = BaseTime.AddMinutes(40);

        _viewModel.ResumeCommand.Execute(null);

        _viewModel.IsTracking.Should().BeTrue();
        var openEntry = _timeEntries.GetOpenEntry();
        openEntry.Should().NotBeNull();
        openEntry.TaskName.Should().Be("Implement widget");
        openEntry.JiraIssueKey.Should().Be("TEAM-1210");
        openEntry.Note.Should().Be("Editor work");
        openEntry.StartedAt.Should().Be(BaseTime.AddMinutes(40));
        _viewModel.ElapsedText.Should().Be("00:25:00");
    }

    [Test]
    public void Tick_ContinuesAccumulatingAcrossStintsOfTheSameTask()
    {
        _trackingService.Start("Implement widget");
        _timeProvider.UtcNow = BaseTime.AddMinutes(25);
        _viewModel.PauseCommand.Execute(null);
        _timeProvider.UtcNow = BaseTime.AddMinutes(40);
        _viewModel.ResumeCommand.Execute(null);
        _timeProvider.UtcNow = BaseTime.AddMinutes(50);

        _viewModel.Tick();

        _viewModel.ElapsedText.Should().Be("00:35:00");
    }

    [Test]
    public void Tick_CountsOnlyTheDisplayedTask()
    {
        _trackingService.Start("Implement widget");
        _timeProvider.UtcNow = BaseTime.AddMinutes(25);
        _trackingService.SwitchTo("Code review");
        _timeProvider.UtcNow = BaseTime.AddMinutes(35);

        _viewModel.Tick();

        _viewModel.ElapsedText.Should().Be("00:10:00");
    }

    [Test]
    public void ResumeCommand_IsAvailableOnlyWhilePaused()
    {
        _viewModel.ResumeCommand.CanExecute(null).Should().BeFalse();

        _trackingService.Start("Implement widget");
        _viewModel.ResumeCommand.CanExecute(null).Should().BeFalse();

        _viewModel.PauseCommand.Execute(null);
        _viewModel.ResumeCommand.CanExecute(null).Should().BeTrue();
    }

    [Test]
    public void Tick_RendersTheElapsedTimeOfTheRunningEntry()
    {
        _trackingService.Start("Implement widget");
        _timeProvider.UtcNow = BaseTime.AddMinutes(90).AddSeconds(5);

        _viewModel.Tick();

        _viewModel.ElapsedText.Should().Be("01:30:05");
    }

    [Test]
    public void Refresh_ReflectsTrackingStartedOutsideTheViewModel()
    {
        _trackingService.SwitchTo("Started elsewhere", "TEAM-9");

        _viewModel.IsTracking.Should().BeTrue();
        _viewModel.CurrentTaskName.Should().Be("Started elsewhere");
        _viewModel.CurrentIssueKey.Should().Be("TEAM-9");
    }

    [Test]
    public void QuickSwitchItems_MergeTemplatesAndRecentTasksWithoutDuplicates()
    {
        _templates.Insert(new()
        {
            Name = "Meetings (Q3)",
            DefaultJiraIssueKey = "TEAM-1234",
            SortOrder = 0
        });
        _trackingService.SwitchTo("Bugfix", "TEAM-77");
        _timeProvider.UtcNow = BaseTime.AddHours(1);
        _trackingService.SwitchTo("Meetings (Q3)", "TEAM-1234");
        _timeProvider.UtcNow = BaseTime.AddHours(2);
        _trackingService.Stop();

        _viewModel.QuickSwitchItems.Select(i => i.Name).Should().Equal("Meetings (Q3)", "Bugfix");
        _viewModel.QuickSwitchItems[0].IsTemplate.Should().BeTrue();
    }

    [Test]
    public void QuickSwitchItems_ExcludeTheCurrentlyTrackedTask()
    {
        _trackingService.SwitchTo("Bugfix", "TEAM-77");
        _timeProvider.UtcNow = BaseTime.AddHours(1);
        _trackingService.SwitchTo("Review");

        _viewModel.QuickSwitchItems.Select(i => i.Name).Should().Equal("Bugfix");
    }
}
