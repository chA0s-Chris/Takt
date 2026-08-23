// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.Core.Storage;

using LiteDB;
using Takt.Core.Domain;

/// <summary>
/// LiteDB-backed implementation of <see cref="ISettingsRepository"/>.
/// </summary>
public sealed class LiteDbSettingsRepository : ISettingsRepository
{
    private readonly ILiteCollection<AppSettings> _collection;

    /// <summary>Creates a repository operating on the given database.</summary>
    /// <param name="database">The database to operate on.</param>
    public LiteDbSettingsRepository(TaktDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        _collection = database.Database.GetCollection<AppSettings>(TaktDatabase.SettingsCollectionName);
    }

    /// <inheritdoc/>
    public AppSettings Get() => _collection.FindById(1) ?? new AppSettings();

    /// <inheritdoc/>
    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Id = 1;
        _collection.Upsert(settings);
    }
}
