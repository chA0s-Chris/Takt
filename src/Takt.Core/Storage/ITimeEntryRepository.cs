// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.Core.Storage;

using Takt.Core.Domain;

/// <summary>
/// Persistence operations for <see cref="TimeEntry"/> documents.
/// </summary>
public interface ITimeEntryRepository
{
    /// <summary>Deletes the entry with the given identifier. Deleting a missing entry is a no-op.</summary>
    /// <param name="id">The identifier of the entry.</param>
    void Delete(Guid id);

    /// <summary>
    /// Returns the entries whose start time lies within <paramref name="fromUtc"/>
    /// (inclusive) and <paramref name="toUtc"/> (exclusive), ordered by start time.
    /// </summary>
    /// <param name="fromUtc">The inclusive UTC lower bound.</param>
    /// <param name="toUtc">The exclusive UTC upper bound.</param>
    /// <returns>The matching entries in chronological order.</returns>
    IReadOnlyList<TimeEntry> GetBetween(DateTime fromUtc, DateTime toUtc);

    /// <summary>Returns the entry with the given identifier, or <c>null</c>.</summary>
    /// <param name="id">The identifier of the entry.</param>
    /// <returns>The entry, or <c>null</c> when it does not exist.</returns>
    TimeEntry? GetById(Guid id);

    /// <summary>Returns the most recently started entries, newest first.</summary>
    /// <param name="count">The maximum number of entries to return.</param>
    /// <returns>The most recent entries.</returns>
    IReadOnlyList<TimeEntry> GetMostRecent(Int32 count);

    /// <summary>Returns the currently running entry (no end time), or <c>null</c>.</summary>
    /// <returns>The open entry, or <c>null</c> when no timer is running.</returns>
    TimeEntry? GetOpenEntry();

    /// <summary>
    /// Returns the completed entries that carry a Jira issue key and are not in sync
    /// with Jira, ordered by start time.
    /// </summary>
    /// <returns>The entries awaiting a push to Jira.</returns>
    IReadOnlyList<TimeEntry> GetPendingSync();

    /// <summary>Inserts a new entry. A <see cref="Guid.Empty"/> identifier is replaced with a new one.</summary>
    /// <param name="entry">The entry to insert.</param>
    void Insert(TimeEntry entry);

    /// <summary>Updates an existing entry.</summary>
    /// <param name="entry">The entry to update.</param>
    /// <exception cref="InvalidOperationException">Thrown when the entry does not exist.</exception>
    void Update(TimeEntry entry);
}
