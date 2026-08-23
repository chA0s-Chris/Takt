// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.App.Views;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Takt.App.ViewModels;
using Takt.Core.Storage;

/// <summary>
/// The floating always-on-top widget: current task, live elapsed time, stop button,
/// and the quick-switch flyout. Dragging anywhere outside a button moves the window;
/// the position is persisted. Closing hides the widget instead of exiting.
/// </summary>
public sealed partial class WidgetWindow : Window
{
    private readonly DispatcherTimer _savePositionTimer;
    private readonly ISettingsRepository _settings;
    private readonly DispatcherTimer _tickTimer;
    private readonly WidgetViewModel _viewModel;

    /// <summary>Creates the widget window.</summary>
    /// <param name="viewModel">The view model driving the widget.</param>
    /// <param name="settings">The settings repository the window position is persisted in.</param>
    public WidgetWindow(WidgetViewModel viewModel, ISettingsRepository settings)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(settings);
        _viewModel = viewModel;
        _settings = settings;
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

        _viewModel.SwitchCompleted += (_, _) => SwitchButton.Flyout?.Hide();
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

    private void OnNewTaskKeyDown(Object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _viewModel.StartNewTaskCommand.CanExecute(null))
        {
            _viewModel.StartNewTaskCommand.Execute(null);
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
