// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.App.Services;

using Takt.Core.Storage;

/// <summary>
/// Announces stored changes to every open window: the widget and the main window share
/// one database, so a note written in the widget has to reach the overview and a new
/// template has to reach the quick-switch list without a restart.
/// <para>
/// The repositories raise their events on whichever thread wrote — a Jira push writes
/// from a background thread — so this notifier moves them onto the thread it was created
/// on, which is the UI thread. View models can therefore touch their collections
/// straight from a handler.
/// </para>
/// </summary>
public sealed class DataChangeNotifier
{
    private readonly SynchronizationContext? _context;

    /// <summary>Creates the notifier and subscribes to both repositories.</summary>
    /// <param name="timeEntries">The entry repository.</param>
    /// <param name="templates">The template repository.</param>
    public DataChangeNotifier(ITimeEntryRepository timeEntries, ITemplateRepository templates)
    {
        ArgumentNullException.ThrowIfNull(timeEntries);
        ArgumentNullException.ThrowIfNull(templates);
        _context = SynchronizationContext.Current;
        timeEntries.Changed += (_, _) => Dispatch(() => TimeEntriesChanged?.Invoke(this, EventArgs.Empty));
        templates.Changed += (_, _) => Dispatch(() => TemplatesChanged?.Invoke(this, EventArgs.Empty));
    }

    /// <summary>Raised on the UI thread after a template was inserted, updated, or deleted.</summary>
    public event EventHandler? TemplatesChanged;

    /// <summary>Raised on the UI thread after a time entry was inserted, updated, or deleted.</summary>
    public event EventHandler? TimeEntriesChanged;

    private void Dispatch(Action raise)
    {
        if (_context is null || SynchronizationContext.Current == _context)
        {
            raise();
            return;
        }

        _context.Post(_ => raise(), null);
    }
}
