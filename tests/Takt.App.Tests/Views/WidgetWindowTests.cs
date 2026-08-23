// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.App.Tests.Views;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using FluentAssertions;
using Takt.App.Tests.TestSupport;
using Takt.App.ViewModels;
using Takt.App.Views;
using Takt.Core.Storage;
using Takt.Core.Tests.TestSupport;
using Takt.Core.Tracking;

/// <summary>
/// Headless UI tests: the widget window runs in-process on the headless Avalonia
/// platform against the real view model and LiteDB storage on a temporary file.
/// </summary>
public class WidgetWindowTests
{
    private static readonly DateTime BaseTime = new(2026, 8, 23, 9, 0, 0, DateTimeKind.Utc);

    [AvaloniaTest]
    public void Widget_RestoresTheSavedPositionOnOpen()
    {
        using var tempDatabase = new TempDatabase();
        var timeEntries = new LiteDbTimeEntryRepository(tempDatabase.Database);
        var settings = new LiteDbSettingsRepository(tempDatabase.Database);
        settings.Save(new()
        {
            WidgetPositionX = 321,
            WidgetPositionY = 45
        });
        var timeProvider = new TestTimeProvider
        {
            UtcNow = BaseTime
        };
        var viewModel = new WidgetViewModel(
            new(timeEntries, timeProvider),
            new LiteDbTemplateRepository(tempDatabase.Database),
            timeEntries,
            settings,
            timeProvider,
            new StubJiraClient());

        var window = new WidgetWindow(viewModel, settings, new());
        window.Show();

        window.Position.Should().Be(new PixelPoint(321, 45));

        window.Hide();
    }

    [AvaloniaTest]
    public void Widget_ShowsTheTrackedTaskAndElapsedTime()
    {
        using var tempDatabase = new TempDatabase();
        var timeEntries = new LiteDbTimeEntryRepository(tempDatabase.Database);
        var settings = new LiteDbSettingsRepository(tempDatabase.Database);
        var timeProvider = new TestTimeProvider
        {
            UtcNow = BaseTime
        };
        var trackingService = new TrackingService(timeEntries, timeProvider);
        var viewModel = new WidgetViewModel(
            trackingService,
            new LiteDbTemplateRepository(tempDatabase.Database),
            timeEntries,
            settings,
            timeProvider,
            new StubJiraClient());
        trackingService.Start("Implement widget", "TEAM-1234");
        timeProvider.UtcNow = BaseTime.AddMinutes(5);

        var window = new WidgetWindow(viewModel, settings, new());
        window.Show();
        viewModel.Tick();

        var taskNameText = window.FindControl<TextBlock>("TaskNameText");
        taskNameText.Should().NotBeNull();
        taskNameText.Text.Should().Be("Implement widget");
        var elapsedText = window.FindControl<TextBlock>("ElapsedText");
        elapsedText.Should().NotBeNull();
        elapsedText.Text.Should().Be("00:05:00");
        var pauseButton = window.FindControl<Button>("PauseButton");
        pauseButton.Should().NotBeNull();
        pauseButton.IsVisible.Should().BeTrue();
        var resumeButton = window.FindControl<Button>("ResumeButton");
        resumeButton.Should().NotBeNull();
        resumeButton.IsVisible.Should().BeFalse();

        window.Hide();
    }
}
