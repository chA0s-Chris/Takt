// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.Core.Storage;

using LiteDB;
using Takt.Core.Domain;

/// <summary>
/// LiteDB-backed implementation of <see cref="ITimeEntryRepository"/>.
/// </summary>
public sealed class LiteDbTimeEntryRepository : ITimeEntryRepository
{
    private readonly ILiteCollection<TimeEntry> _collection;

    /// <summary>Creates a repository operating on the given database.</summary>
    /// <param name="database">The database to operate on.</param>
    public LiteDbTimeEntryRepository(TaktDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        _collection = database.Database.GetCollection<TimeEntry>(TaktDatabase.TimeEntryCollectionName);
    }

    /// <inheritdoc/>
    public void Delete(Guid id) => _collection.Delete(id);

    /// <inheritdoc/>
    public IReadOnlyList<TimeEntry> GetBetween(DateTime fromUtc, DateTime toUtc) =>
        _collection.Query()
                   .Where(x => x.StartedAt >= fromUtc && x.StartedAt < toUtc)
                   .OrderBy(x => x.StartedAt)
                   .ToList();

    /// <inheritdoc/>
    public TimeEntry? GetById(Guid id) => _collection.FindById(id);

    /// <inheritdoc/>
    public IReadOnlyList<TimeEntry> GetMostRecent(Int32 count) =>
        _collection.Query()
                   .OrderByDescending(x => x.StartedAt)
                   .Limit(count)
                   .ToList();

    /// <inheritdoc/>
    public TimeEntry? GetOpenEntry() => _collection.FindOne(x => x.EndedAt == null);

    /// <inheritdoc/>
    public IReadOnlyList<TimeEntry> GetUnsynced() =>
        _collection.Query()
                   .Where(x => x.SyncState != SyncState.Synced && x.EndedAt != null)
                   .OrderBy(x => x.StartedAt)
                   .ToList();

    /// <inheritdoc/>
    public void Insert(TimeEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.Id == Guid.Empty)
        {
            entry.Id = Guid.CreateVersion7();
        }

        _collection.Insert(entry);
    }

    /// <inheritdoc/>
    public void Update(TimeEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (!_collection.Update(entry))
        {
            throw new InvalidOperationException($"Time entry {entry.Id} does not exist and cannot be updated.");
        }
    }
}
