// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.App.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Globalization;

/// <summary>
/// The entries of one local day in the overview, with the day's total. In the day view
/// a single group is shown without its header; the week view stacks seven of them.
/// </summary>
public sealed partial class DayGroupViewModel : ObservableObject
{
    [ObservableProperty]
    private String _totalText = String.Empty;

    /// <summary>Creates a group for the given day.</summary>
    /// <param name="date">The local date.</param>
    /// <param name="isHeaderVisible">Whether the day header is shown (week view).</param>
    public DayGroupViewModel(DateOnly date, Boolean isHeaderVisible)
    {
        IsHeaderVisible = isHeaderVisible;
        HeaderText = date.ToDateTime(TimeOnly.MinValue).ToString("dddd, MMM d", CultureInfo.CurrentCulture);
    }

    /// <summary>The rows of this day, in chronological order.</summary>
    public ObservableCollection<TimeEntryRowViewModel> Entries { get; } = new();

    /// <summary>The day header, for example <c>Friday, Aug 21</c>.</summary>
    public String HeaderText { get; }

    /// <summary>Whether the day header is shown.</summary>
    public Boolean IsHeaderVisible { get; }

    /// <summary>Recomputes the day's total from the current row durations.</summary>
    public void UpdateTotal() =>
        TotalText = TimeFormat.FormatDuration(Entries.Aggregate(TimeSpan.Zero, (total, row) => total + row.Duration));
}
