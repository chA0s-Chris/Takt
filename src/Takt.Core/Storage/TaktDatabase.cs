// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.Core.Storage;

using LiteDB;
using Takt.Core.Domain;

/// <summary>
/// Owns the LiteDB database connection, configures the BSON mapping (UTC dates,
/// enums as strings), and ensures the required indexes exist.
/// </summary>
public sealed class TaktDatabase : IDisposable
{
    internal const String SettingsCollectionName = "settings";
    internal const String TemplateCollectionName = "templates";
    internal const String TimeEntryCollectionName = "timeEntries";

    /// <summary>
    /// Opens (or creates) the database at <paramref name="databasePath"/> with a direct,
    /// single-process connection.
    /// </summary>
    /// <param name="databasePath">The absolute path of the database file.</param>
    public TaktDatabase(String databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        var mapper = new BsonMapper
        {
            EnumAsInteger = false
        };
        var connectionString = new ConnectionString
        {
            Filename = databasePath,
            Connection = ConnectionType.Direct
        };
        Database = new(connectionString, mapper);
        Database.Pragma("UTC_DATE", true);

        var timeEntries = Database.GetCollection<TimeEntry>(TimeEntryCollectionName);
        timeEntries.EnsureIndex(x => x.StartedAt);
        timeEntries.EnsureIndex(x => x.SyncState);
    }

    internal LiteDatabase Database { get; }

    /// <inheritdoc/>
    public void Dispose() => Database.Dispose();
}
