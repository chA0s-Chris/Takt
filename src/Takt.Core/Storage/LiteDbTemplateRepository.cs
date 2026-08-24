// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.Core.Storage;

using LiteDB;
using Takt.Core.Domain;

/// <summary>
/// LiteDB-backed implementation of <see cref="ITemplateRepository"/>.
/// </summary>
public sealed class LiteDbTemplateRepository : ITemplateRepository
{
    private readonly ILiteCollection<Template> _collection;

    /// <summary>Creates a repository operating on the given database.</summary>
    /// <param name="database">The database to operate on.</param>
    public LiteDbTemplateRepository(TaktDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        _collection = database.Database.GetCollection<Template>(TaktDatabase.TemplateCollectionName);
    }

    /// <inheritdoc/>
    public event EventHandler? Changed;

    private static List<Template> Sort(IEnumerable<Template> templates) =>
        templates
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);

    /// <inheritdoc/>
    public void Delete(Guid id)
    {
        if (_collection.Delete(id))
        {
            OnChanged();
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<Template> GetActive() => Sort(_collection.Find(x => !x.Archived));

    /// <inheritdoc/>
    public IReadOnlyList<Template> GetAll() => Sort(_collection.FindAll());

    /// <inheritdoc/>
    public Template? GetById(Guid id) => _collection.FindById(id);

    /// <inheritdoc/>
    public void Insert(Template template)
    {
        ArgumentNullException.ThrowIfNull(template);
        if (template.Id == Guid.Empty)
        {
            template.Id = Guid.NewGuid();
        }

        _collection.Insert(template);
        OnChanged();
    }

    /// <inheritdoc/>
    public void Update(Template template)
    {
        ArgumentNullException.ThrowIfNull(template);
        if (!_collection.Update(template))
        {
            throw new InvalidOperationException($"Template {template.Id} does not exist and cannot be updated.");
        }

        OnChanged();
    }
}
