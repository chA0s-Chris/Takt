// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.App.Views;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Takt.App.Services;
using Takt.App.ViewModels;
using Takt.Core.Storage;

/// <summary>
/// The floating always-on-top widget: current task, live elapsed time, pause/resume,
/// the quick-switch flyout, and the Jira issue search. Dragging anywhere outside a
/// button moves the window; the position is persisted. Closing hides the widget
/// instead of exiting.
/// </summary>
public sealed partial class WidgetWindow : Window
{
    private readonly DispatcherTimer _issueSearchTimer;
    private readonly DispatcherTimer _savePositionTimer;
    private readonly ISettingsRepository _settings;
    private readonly DispatcherTimer _tickTimer;
    private readonly WidgetViewModel _viewModel;

    /// <summary>Creates the widget window.</summary>
    /// <param name="viewModel">The view model driving the widget.</param>
    /// <param name="settings">The settings repository the window position is persisted in.</param>
    /// <param name="notifier">The notifier announcing changed widget preferences.</param>
    public WidgetWindow(WidgetViewModel viewModel, ISettingsRepository settings, SettingsNotifier notifier)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(notifier);
        _viewModel = viewModel;
        _settings = settings;
        notifier.Changed += (_, _) => ApplySettings();
        DataContext = viewModel;
        InitializeComponent();

        _tickTimer = new()
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _tickTimer.Tick += (_, _) => _viewModel.Tick();

        _savePositionTimer = new()
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _savePositionTimer.Tick += (_, _) =>
        {
            _savePositionTimer.Stop();
            SavePosition();
        };

        _issueSearchTimer = new()
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        _issueSearchTimer.Tick += (_, _) =>
        {
            _issueSearchTimer.Stop();
            _ = _viewModel.IssueSearch.RunAsync();
        };

        _viewModel.IssueAssigned += (_, _) => IssueButton.Flyout?.Hide();
        _viewModel.NoteSaved += (_, _) => NoteButton.Flyout?.Hide();
        _viewModel.SwitchCompleted += (_, _) => SwitchButton.Flyout?.Hide();
        _viewModel.IssueSearch.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(JiraIssueSearchViewModel.SearchText))
            {
                _issueSearchTimer.Stop();
                _issueSearchTimer.Start();
            }
        };
        PositionChanged += (_, _) =>
        {
            if (IsVisible)
            {
                _savePositionTimer.Stop();
                _savePositionTimer.Start();
            }
        };
    }

    /// <inheritdoc/>
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (!App.IsShutdownInProgress)
        {
            e.Cancel = true;
            Hide();
        }

        base.OnClosing(e);
    }

    /// <inheritdoc/>
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        RestorePosition();
        ApplySettings();
        _viewModel.Refresh();
        _tickTimer.Start();
    }

    /// <inheritdoc/>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed
            && e.Source is Visual source
            && source.FindAncestorOfType<Button>(true) is null)
        {
            BeginMoveDrag(e);
        }
    }

    private void ApplySettings()
    {
        var settings = _settings.Get();
        Topmost = settings.WidgetAlwaysOnTop;
        if (settings is { WidgetPositionX: null, WidgetPositionY: null })
        {
            MoveToDefaultPosition();
        }

        _viewModel.Refresh();
    }

    private void MoveToDefaultPosition()
    {
        if (Screens.Primary is not { } screen)
        {
            return;
        }

        var workingArea = screen.WorkingArea;
        var width = (Int32)(Bounds.Width * screen.Scaling);
        Position = new(workingArea.X + Math.Max(0, workingArea.Width - width - 40), workingArea.Y + 40);
    }

    private void OnNewTaskKeyDown(Object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _viewModel.StartNewTaskCommand.CanExecute(null))
        {
            _viewModel.StartNewTaskCommand.Execute(null);
            e.Handled = true;
        }
    }

    /// <summary>
    /// Enter saves the note, Shift+Enter starts a new line: the box accepts returns
    /// because a note may have several lines, but saving should not need the mouse.
    /// </summary>
    private void OnNoteKeyDown(Object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            _viewModel.SaveNoteCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            NoteButton.Flyout?.Hide();
            e.Handled = true;
        }
    }

    private void RestorePosition()
    {
        var settings = _settings.Get();
        if (settings.WidgetPositionX is { } x && settings.WidgetPositionY is { } y)
        {
            Position = new(x, y);
        }
    }

    private void SavePosition()
    {
        var settings = _settings.Get();
        settings.WidgetPositionX = Position.X;
        settings.WidgetPositionY = Position.Y;
        _settings.Save(settings);
    }
}
