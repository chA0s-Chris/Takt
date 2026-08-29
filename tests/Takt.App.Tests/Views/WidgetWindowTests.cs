// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.App.Tests.Views;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless.NUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
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
    public void Widget_KeepsThePillDarkWhileItsFlyoutFollowsTheAppearance()
    {
        using var tempDatabase = new TempDatabase();
        var timeEntries = new LiteDbTimeEntryRepository(tempDatabase.Database);
        var settings = new LiteDbSettingsRepository(tempDatabase.Database);
        var timeProvider = new TestTimeProvider
        {
            UtcNow = BaseTime
        };
        var templates = new LiteDbTemplateRepository(tempDatabase.Database);
        var viewModel = new WidgetViewModel(
            new(timeEntries, timeProvider),
            templates,
            timeEntries,
            settings,
            timeProvider,
            new StubJiraClient(),
            new(timeEntries, templates));

        var window = new WidgetWindow(viewModel, settings, new());
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var rootBorder = window.FindControl<Border>("RootBorder");
        rootBorder.Should().NotBeNull();
        var pillColour = rootBorder.Background.Should().BeAssignableTo<ISolidColorBrush>().Subject.Color;

        try
        {
            Application.Current.Should().NotBeNull();
            Application.Current.RequestedThemeVariant = ThemeVariant.Dark;
            Dispatcher.UIThread.RunJobs();

            // The pill is hand-coloured, so the selected appearance must not reach it.
            rootBorder.Background.Should().BeAssignableTo<ISolidColorBrush>()
                      .Which.Color.Should().Be(pillColour);

            // What the widget opens is an ordinary surface and follows the appearance.
            var switchButton = window.FindControl<Button>("SwitchButton");
            switchButton.Should().NotBeNull();
            var flyout = switchButton.Flyout.Should().BeOfType<Flyout>().Subject;
            flyout.ShowAt(switchButton);
            Dispatcher.UIThread.RunJobs();

            var flyoutContent = flyout.Content.Should().BeAssignableTo<Control>().Subject;
            flyoutContent.ActualThemeVariant.Should().Be(ThemeVariant.Dark);

            // The light direction is the one that matters: pinning the widget window to
            // the dark variant would keep the flyout dark here and break issue search
            // and note editing for anyone using the light appearance.
            Application.Current.RequestedThemeVariant = ThemeVariant.Light;
            Dispatcher.UIThread.RunJobs();
            flyoutContent.ActualThemeVariant.Should().Be(ThemeVariant.Light);

            rootBorder.Background.Should().BeAssignableTo<ISolidColorBrush>()
                      .Which.Color.Should().Be(pillColour);

            flyout.Hide();
        }
        finally
        {
            Application.Current!.RequestedThemeVariant = ThemeVariant.Light;
        }

        window.Hide();
    }

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
        var templates = new LiteDbTemplateRepository(tempDatabase.Database);
        var viewModel = new WidgetViewModel(
            new(timeEntries, timeProvider),
            templates,
            timeEntries,
            settings,
            timeProvider,
            new StubJiraClient(),
            new(timeEntries, templates));

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
        var templates = new LiteDbTemplateRepository(tempDatabase.Database);
        var viewModel = new WidgetViewModel(
            trackingService,
            templates,
            timeEntries,
            settings,
            timeProvider,
            new StubJiraClient(),
            new(timeEntries, templates));
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
        // Two drawn bars, not a character: the pause glyph has no font to render it.
        pauseButton.Content.Should().BeOfType<StackPanel>()
                   .Which.Children.Should().AllBeOfType<Rectangle>().And.HaveCount(2);
        var resumeButton = window.FindControl<Button>("ResumeButton");
        resumeButton.Should().NotBeNull();
        resumeButton.IsVisible.Should().BeFalse();
        var noteButton = window.FindControl<Button>("NoteButton");
        noteButton.Should().NotBeNull();
        noteButton.IsVisible.Should().BeTrue();
        noteButton.Flyout.Should().NotBeNull();
        noteButton.GetVisualDescendants().OfType<TextBlock>().Single().Text.Should().Be("+ note");

        window.Hide();
    }
}
