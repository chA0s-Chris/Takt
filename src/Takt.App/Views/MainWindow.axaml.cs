// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.App.Views;

using Avalonia.Controls;

/// <summary>
/// The full-sized configuration and overview window. Placeholder until Milestone 3.
/// Closing hides the window; the application exits only via the tray menu.
/// </summary>
public sealed partial class MainWindow : Window
{
    /// <summary>Creates the window.</summary>
    public MainWindow()
    {
        InitializeComponent();
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
}
