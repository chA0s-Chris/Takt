// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.App.Views;

using Avalonia.Controls;
using Avalonia.Threading;
using Takt.App.ViewModels;

/// <summary>
/// The full-sized window holding the overview, the templates, and the settings.
/// Closing hides the window; the application exits only via the tray menu.
/// </summary>
public sealed partial class MainWindow : Window
{
    private readonly DispatcherTimer _tickTimer;
    private readonly MainWindowViewModel _viewModel;

    /// <summary>Creates the window.</summary>
    /// <param name="viewModel">The view model driving the window.</param>
    public MainWindow(MainWindowViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();

        _tickTimer = new()
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _tickTimer.Tick += (_, _) => _viewModel.Tick();
    }

    /// <summary>Shows the window, brings it to the front, and reloads the current page.</summary>
    public void ShowAndActivate()
    {
        Show();
        Activate();
        _viewModel.Refresh();
        _tickTimer.Start();
    }

    /// <inheritdoc/>
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (!App.IsShutdownInProgress)
        {
            e.Cancel = true;
            Hide();
        }

        _tickTimer.Stop();
        base.OnClosing(e);
    }

    /// <inheritdoc/>
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        _viewModel.Refresh();
        _tickTimer.Start();
    }
}
