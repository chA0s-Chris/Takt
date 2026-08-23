// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.App.Views;

using Avalonia.Controls;
using Avalonia.Interactivity;
using Takt.Core.Domain;

/// <summary>
/// Shown at startup when an open entry survived the previous run. Returns <c>true</c>
/// from <c>ShowDialog&lt;Boolean&gt;</c> when the user chose to stop the timer now;
/// <c>false</c> (or closing the dialog) continues tracking. Precise end-time editing
/// lands in the Milestone 3 entry editor.
/// </summary>
public sealed partial class RecoveryDialog : Window
{
    /// <summary>Creates the dialog for the given open entry.</summary>
    /// <param name="openEntry">The entry that is still running.</param>
    /// <param name="timeProvider">The clock used to render the elapsed time.</param>
    public RecoveryDialog(TimeEntry openEntry, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(openEntry);
        ArgumentNullException.ThrowIfNull(timeProvider);
        InitializeComponent();

        var elapsed = openEntry.GetDuration(timeProvider.GetUtcNow().UtcDateTime);
        var startedLocal = openEntry.StartedAt.ToLocalTime();
        DetailsText.Text =
            $"\"{openEntry.TaskName}\" is running since {startedLocal:g} " +
            $"({(Int32)elapsed.TotalHours:00}:{elapsed.Minutes:00} elapsed). Keep tracking it?";
    }

    private void OnContinueClick(Object? sender, RoutedEventArgs e) => Close(false);

    private void OnStopNowClick(Object? sender, RoutedEventArgs e) => Close(true);
}
