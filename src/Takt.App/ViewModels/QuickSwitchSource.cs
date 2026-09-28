// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.App.ViewModels;

using Takt.Core.Storage;

/// <summary>
/// Builds the list of tasks to start from: the active templates first, then the recent
/// tasks, each task name only once. The widget's quick-switch list and the new-entry
/// dialog share it, so both offer the same choices.
/// </summary>
public static class QuickSwitchSource
{
    /// <summary>The maximum number of items in the list.</summary>
    public const Int32 MaxItems = 8;

    private const Int32 RecentEntriesToScan = 20;

    /// <summary>Loads the items.</summary>
    /// <param name="templates">The template repository.</param>
    /// <param name="timeEntries">The entry repository feeding the recent tasks.</param>
    /// <param name="excludedTaskName">A task name to leave out, for example the running task. Optional.</param>
    /// <returns>At most <see cref="MaxItems"/> items, templates first.</returns>
    public static IReadOnlyList<QuickSwitchItem> Load(
        ITemplateRepository templates,
        ITimeEntryRepository timeEntries,
        String? excludedTaskName = null)
    {
        ArgumentNullException.ThrowIfNull(templates);
        ArgumentNullException.ThrowIfNull(timeEntries);

        var templateItems = templates.GetActive()
                                     .Select(t => new QuickSwitchItem(t.Name, t.DefaultJiraIssueKey, t.DefaultNote, true));
        var recentItems = timeEntries.GetMostRecent(RecentEntriesToScan)
                                     .Select(e => new QuickSwitchItem(e.TaskName, e.JiraIssueKey, null, false));

        return templateItems
               .Concat(recentItems)
               .DistinctBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
               .Where(i => !String.Equals(i.Name, excludedTaskName, StringComparison.OrdinalIgnoreCase))
               .Take(MaxItems)
               .ToList();
    }
}
