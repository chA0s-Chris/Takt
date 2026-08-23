// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.App.Tests.Views;

using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using Takt.App.Tests.TestSupport;
using Takt.App.ViewModels;
using Takt.App.Views;
using Takt.Core.Domain;
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
    public void MainWindow_SwitchesBetweenThePages()
    {
        using var context = new TestContext();
        var window = context.CreateWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();

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
            MainViewModel = new(
                new(TimeEntries, trackingService, JiraClient, TimeProvider),
                new(Templates, JiraClient),
                new(Settings, new InMemoryCredentialStore(), JiraClient, new()));
        }

        public StubJiraClient JiraClient { get; }

        public MainWindowViewModel MainViewModel { get; }

        public LiteDbSettingsRepository Settings { get; }

        public LiteDbTemplateRepository Templates { get; }

        public LiteDbTimeEntryRepository TimeEntries { get; }

        public TestTimeProvider TimeProvider { get; }

        public MainWindow CreateWindow() => new(MainViewModel);

        public void Dispose() => _tempDatabase.Dispose();
    }
}
