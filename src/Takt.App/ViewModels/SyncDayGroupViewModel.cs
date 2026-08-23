// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.App.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Globalization;

/// <summary>
/// The entries of one local day in the sync list, with the day's total. Pushing a whole
/// day at once is the usual gesture: work is reviewed and logged day by day.
/// </summary>
public sealed partial class SyncDayGroupViewModel : ObservableObject
{
    [ObservableProperty]
    private String _totalText = String.Empty;

    /// <summary>Creates a group for the given day.</summary>
    /// <param name="date">The local date.</param>
    public SyncDayGroupViewModel(DateOnly date)
    {
        HeaderText = date.ToDateTime(TimeOnly.MinValue).ToString("dddd, MMM d", CultureInfo.CurrentCulture);
    }

    /// <summary>The day header, for example <c>Friday, Aug 21</c>.</summary>
    public String HeaderText { get; }

    /// <summary>The rows of this day, in chronological order.</summary>
    public ObservableCollection<SyncRowViewModel> Rows { get; } = new();

    /// <summary>Recomputes the day's total from the rows that are still to be pushed.</summary>
    public void UpdateTotal() =>
        TotalText = TimeFormat.FormatDuration(Rows.Aggregate(TimeSpan.Zero, (total, row) => total + row.Duration));
}
