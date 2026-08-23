// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.App.ViewModels;

using System.Globalization;

/// <summary>
/// Formats durations and times of day for the widget and the overview. Running timers
/// are rendered as a clock (<c>01:12:36</c>), completed spans in the compact form
/// (<c>2 h 25 m</c>).
/// </summary>
public static class TimeFormat
{
    /// <summary>Formats an elapsed time as <c>hh:mm:ss</c>.</summary>
    /// <param name="value">The elapsed time; negative values render as zero.</param>
    /// <returns>The formatted clock text.</returns>
    public static String FormatClock(TimeSpan value)
    {
        value = Clamp(value);
        return $"{(Int32)value.TotalHours:00}:{value.Minutes:00}:{value.Seconds:00}";
    }

    /// <summary>Formats a duration as <c>5 h 05 m</c>, or <c>45 m</c> below one hour.</summary>
    /// <param name="value">The duration; negative values render as zero.</param>
    /// <returns>The formatted duration text.</returns>
    public static String FormatDuration(TimeSpan value)
    {
        value = Clamp(value);
        var hours = (Int32)value.TotalHours;
        return hours > 0 ? $"{hours} h {value.Minutes:00} m" : $"{value.Minutes} m";
    }

    /// <summary>Formats a local time of day as <c>14:05</c>.</summary>
    /// <param name="value">The local time.</param>
    /// <returns>The formatted time of day.</returns>
    public static String FormatTimeOfDay(DateTime value) => value.ToString("HH:mm", CultureInfo.CurrentCulture);

    private static TimeSpan Clamp(TimeSpan value) => value < TimeSpan.Zero ? TimeSpan.Zero : value;
}
