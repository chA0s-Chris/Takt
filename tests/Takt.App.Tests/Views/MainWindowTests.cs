// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.App.Tests.Views;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless.NUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using Takt.App.Services;
using Takt.App.ViewModels;
using Takt.App.Views;
using Takt.Core.Domain;
using Takt.Core.Jira;
using Takt.Core.Storage;
using Takt.Core.Tests.TestSupport;
using Takt.Core.Tracking;

/// <summary>
/// Headless UI tests: the main window and both editor dialogs are loaded on the
/// headless Avalonia platform against real view models and LiteDB storage, so broken
/// XAML or bindings fail the build rather than the first run.
/// </summary>
public class MainWindowTests
{
    private static readonly DateTime BaseTime = new(2026, 8, 21, 9, 0, 0, DateTimeKind.Utc);

    [AvaloniaTest]
    public void EntryEditorDialog_LoadsTheEditedEntry()
    {
        using var context = new TestContext();
        var entry = new TimeEntry
        {
            TaskName = "Meetings (Q3)",
            StartedAt = BaseTime,
            EndedAt = BaseTime.AddHours(2)
        };
        context.TimeEntries.Insert(entry);
        var editor = new EntryEditorViewModel(entry, context.TimeEntries, context.JiraClient, context.TimeProvider,
                                              new(2026, 8, 21));

        var dialog = new EntryEditorDialog(editor);
        dialog.Show();
        Dispatcher.UIThread.RunJobs();

        dialog.FindControl<TextBox>("TaskNameTextBox")!.Text.Should().Be("Meetings (Q3)");
        dialog.FindControl<TextBox>("StartTimeTextBox")!.Text.Should().Be("09:00");
        dialog.FindControl<Button>("SaveButton")!.IsEnabled.Should().BeTrue();

        dialog.Close();
    }

    [AvaloniaTest]
    public void MainWindow_CentresTheNavigationLabelsInTheirButtons()
    {
        using var context = new TestContext();
        var window = context.CreateWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var navButton = window.FindControl<Button>("OverviewNavButton");
        navButton.Should().NotBeNull();
        var presenter = navButton.GetVisualDescendants().OfType<ContentPresenter>().First();
        presenter.VerticalContentAlignment.Should().Be(VerticalAlignment.Center);
        presenter.Bounds.Height.Should().BeApproximately(navButton.Bounds.Height, 1);

        window.Hide();
    }

    [AvaloniaTest]
    public void MainWindow_OffersEditingAnEntryWithoutADoubleClick()
    {
        using var context = new TestContext();
        context.TimeEntries.Insert(new()
        {
            TaskName = "Code review",
            StartedAt = BaseTime,
            EndedAt = BaseTime.AddMinutes(30)
        });
        var window = context.CreateWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        // The row's edit button reaches the page's command through the template; a broken
        // binding would leave it without one. It is visible at all times, not on hover.
        var editButton = window.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "✎"));
        editButton.IsVisible.Should().BeTrue();
        editButton.Command.Should().NotBeNull();
        editButton.CommandParameter.Should()
                  .BeOfType<TimeEntryRowViewModel>()
                  .Which.TaskName.Should()
                  .Be("Code review");

        // Right-click offers the same, plus deleting.
        var row = editButton.FindAncestorOfType<Border>()!;
        while (row.ContextFlyout is null && row.FindAncestorOfType<Border>() is { } parent)
        {
            row = parent;
        }

        var flyout = row.ContextFlyout.Should().BeOfType<MenuFlyout>().Subject;
        flyout.Items.OfType<MenuItem>().Select(item => item.Header).Should().Equal("Edit…", "Delete…");

        flyout.ShowAt(row);
        Dispatcher.UIThread.RunJobs();

        // The menu items act on the row they were opened over; they inherit it.
        flyout.Items.OfType<MenuItem>()
              .Should()
              .OnlyContain(item => item.DataContext is TimeEntryRowViewModel);

        flyout.Hide();
        window.Hide();
    }

    [AvaloniaTest]
    public void MainWindow_OpensCentredAndKeepsAMovedPosition()
    {
        using var context = new TestContext();
        var window = context.CreateWindow();

        window.ShowAndActivate();
        Dispatcher.UIThread.RunJobs();

        if (window.Screens.ScreenFromWindow(window) is { } screen)
        {
            var expected = screen.WorkingArea.Center;
            var centre = window.Position + new PixelVector(
                (Int32)(window.Bounds.Width / 2),
                (Int32)(window.Bounds.Height / 2));
            centre.X.Should().BeCloseTo(expected.X, 40);
            centre.Y.Should().BeCloseTo(expected.Y, 40);
        }

        window.Position = new(120, 90);
        window.Hide();
        window.ShowAndActivate();
        Dispatcher.UIThread.RunJobs();

        window.Position.Should().Be(new PixelPoint(120, 90));

        window.Hide();
    }

    [AvaloniaTest]
    public void MainWindow_ShowsTheOverviewWithItsEntries()
    {
        using var context = new TestContext();
        context.TimeEntries.Insert(new()
        {
            TaskName = "Investigate gateway timeouts",
            StartedAt = BaseTime,
            EndedAt = BaseTime.AddHours(2),
            JiraIssueKey = "TEAM-1187"
        });
        var window = context.CreateWindow();

        window.Show();
        Dispatcher.UIThread.RunJobs();

        window.GetVisualDescendants().OfType<OverviewView>().Should().ContainSingle();
        var texts = window.GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text).ToList();
        texts.Should().Contain("Investigate gateway timeouts");
        texts.Should().Contain("TEAM-1187");
        texts.Should().Contain("09:00 – 11:00");

        window.Hide();
    }

    [AvaloniaTest]
    public void MainWindow_ShowsThePendingEntriesOnTheSyncPage()
    {
        using var context = new TestContext();
        context.JiraClient.IsConfigured = true;
        context.TimeEntries.Insert(new()
        {
            TaskName = "Investigate gateway timeouts",
            StartedAt = BaseTime,
            EndedAt = BaseTime.AddHours(2),
            JiraIssueKey = "TEAM-1187"
        });
        var window = context.CreateWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        context.MainViewModel.ShowSyncCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        var texts = window.GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text).ToList();
        texts.Should().Contain("Investigate gateway timeouts");
        texts.Should().Contain("TEAM-1187");
        texts.Should().Contain("1 entry ready to push · 2 h 00 m");

        // The row's push button reaches the page's command through the template; a broken
        // binding would leave it without one.
        var pushButton = window.GetVisualDescendants()
                               .OfType<Button>()
                               .Single(button => Equals(button.Content, "Push"));
        pushButton.Command.Should().NotBeNull();
        pushButton.Command.Execute(pushButton.CommandParameter);
        Dispatcher.UIThread.RunJobs();

        context.JiraClient.CreatedWorklogs.Should().ContainSingle().Which.IssueKey.Should().Be("TEAM-1187");

        window.Hide();
    }

    [AvaloniaTest]
    public void MainWindow_SwitchesBetweenThePages()
    {
        using var context = new TestContext();
        var window = context.CreateWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        context.MainViewModel.ShowSyncCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        context.MainViewModel.IsSyncSelected.Should().BeTrue();
        window.GetVisualDescendants().OfType<SyncView>().Should().ContainSingle();

        context.MainViewModel.ShowTemplatesCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        context.MainViewModel.IsTemplatesSelected.Should().BeTrue();
        window.GetVisualDescendants().OfType<TemplatesView>().Should().ContainSingle();

        context.MainViewModel.ShowSettingsCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        context.MainViewModel.IsSettingsSelected.Should().BeTrue();
        window.GetVisualDescendants().OfType<SettingsView>().Should().ContainSingle();

        window.Hide();
    }

    [AvaloniaTest]
    public void TemplateEditorDialog_LoadsTheEditedTemplate()
    {
        using var context = new TestContext();
        var template = new Template
        {
            Name = "Meetings (Q3)",
            DefaultJiraIssueKey = "TEAM-1234"
        };
        context.Templates.Insert(template);
        var editor = new TemplateEditorViewModel(template, context.Templates, context.JiraClient);

        var dialog = new TemplateEditorDialog(editor);
        dialog.Show();
        Dispatcher.UIThread.RunJobs();

        dialog.FindControl<TextBox>("NameTextBox")!.Text.Should().Be("Meetings (Q3)");
        dialog.FindControl<TextBox>("IssueKeyTextBox")!.Text.Should().Be("TEAM-1234");

        dialog.Close();
    }

    [AvaloniaTest]
    public void ThemeService_RestoresTheStoredAppearanceOnStartup()
    {
        using var tempDatabase = new TempDatabase();
        var settings = new LiteDbSettingsRepository(tempDatabase.Database);
        settings.Save(new()
        {
            Theme = TaktTheme.Dark
        });

        try
        {
            // A fresh service stands in for the next application start.
            var theme = new ThemeService(settings);
            theme.IsDark.Should().BeTrue();

            theme.ApplyStoredTheme();

            Application.Current.Should().NotBeNull();
            Application.Current.RequestedThemeVariant.Should().Be(ThemeVariant.Dark);
        }
        finally
        {
            Application.Current!.RequestedThemeVariant = ThemeVariant.Light;
        }
    }

    [AvaloniaTest]
    public void ThemeSwitch_ReResolvesTheWindowBrushes()
    {
        using var context = new TestContext();
        var window = context.CreateWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            // Guards the DynamicResource conversion: a StaticResource reference resolves
            // once and would keep its light colour while the variant says otherwise.
            var light = window.Background.Should().BeAssignableTo<ISolidColorBrush>().Subject.Color;

            context.MainViewModel.ToggleThemeCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            window.Background.Should().BeAssignableTo<ISolidColorBrush>()
                  .Which.Color.Should().NotBe(light);
        }
        finally
        {
            Application.Current!.RequestedThemeVariant = ThemeVariant.Light;
            window.Close();
        }
    }

    [AvaloniaTest]
    public void ThemeToggleButton_IsWiredIntoTheNavigationRail()
    {
        using var context = new TestContext();
        var window = context.CreateWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            var toggle = window.FindControl<Button>("ThemeToggleButton");
            toggle.Should().NotBeNull();
            toggle.IsVisible.Should().BeTrue();
            toggle.Content.Should().Be("\u263e");

            toggle.Command.Should().NotBeNull();
            toggle.Command.Execute(toggle.CommandParameter);
            Dispatcher.UIThread.RunJobs();

            Application.Current.Should().NotBeNull();
            Application.Current.RequestedThemeVariant.Should().Be(ThemeVariant.Dark);
            toggle.Content.Should().Be("\u2600");
        }
        finally
        {
            Application.Current!.RequestedThemeVariant = ThemeVariant.Light;
            window.Close();
        }
    }

    [AvaloniaTest]
    public void ThemeToggle_SwitchesTheApplicationVariantAndTheGlyph()
    {
        using var context = new TestContext();
        try
        {
            context.MainViewModel.IsDarkTheme.Should().BeFalse();
            context.MainViewModel.ThemeGlyph.Should().Be("\u263e");

            context.MainViewModel.ToggleThemeCommand.Execute(null);

            Application.Current.Should().NotBeNull();
            Application.Current.RequestedThemeVariant.Should().Be(ThemeVariant.Dark);
            context.MainViewModel.IsDarkTheme.Should().BeTrue();
            context.MainViewModel.ThemeGlyph.Should().Be("\u2600");
            context.Settings.Get().Theme.Should().Be(TaktTheme.Dark);

            context.MainViewModel.ToggleThemeCommand.Execute(null);

            Application.Current.RequestedThemeVariant.Should().Be(ThemeVariant.Light);
            context.Settings.Get().Theme.Should().Be(TaktTheme.Light);
        }
        finally
        {
            Application.Current!.RequestedThemeVariant = ThemeVariant.Light;
        }
    }

    private sealed class TestContext : IDisposable
    {
        private readonly TempDatabase _tempDatabase;

        public TestContext()
        {
            _tempDatabase = new();
            TimeEntries = new(_tempDatabase.Database);
            Templates = new(_tempDatabase.Database);
            Settings = new(_tempDatabase.Database);
            TimeProvider = new()
            {
                UtcNow = BaseTime
            };
            JiraClient = new();
            var trackingService = new TrackingService(TimeEntries, TimeProvider);
            var syncService = new SyncService(TimeEntries, JiraClient);
            var dataChanges = new DataChangeNotifier(TimeEntries, Templates);
            Theme = new(Settings);
            MainViewModel = new(
                new(TimeEntries, trackingService, JiraClient, TimeProvider, dataChanges),
                new(syncService, new(JiraClient), JiraClient, TimeProvider, new(), dataChanges),
                new(Templates, JiraClient, dataChanges),
                new(Settings, new InMemoryCredentialStore(), JiraClient, new()),
                Theme);
        }

        public StubJiraClient JiraClient { get; }

        public MainWindowViewModel MainViewModel { get; }

        public LiteDbSettingsRepository Settings { get; }

        public LiteDbTemplateRepository Templates { get; }

        public ThemeService Theme { get; }

        public LiteDbTimeEntryRepository TimeEntries { get; }

        public TestTimeProvider TimeProvider { get; }

        public MainWindow CreateWindow() => new(MainViewModel);

        public void Dispose() => _tempDatabase.Dispose();
    }
}
