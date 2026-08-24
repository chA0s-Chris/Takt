// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.Core.Storage;

using Takt.Core.Domain;

/// <summary>
/// Persistence operations for <see cref="Template"/> documents.
/// </summary>
public interface ITemplateRepository
{
    /// <summary>
    /// Raised after a template was inserted, updated, or deleted. Handlers run on the
    /// thread that wrote.
    /// </summary>
    event EventHandler? Changed;

    /// <summary>Deletes the template with the given identifier. Deleting a missing template is a no-op.</summary>
    /// <param name="id">The identifier of the template.</param>
    void Delete(Guid id);

    /// <summary>Returns the non-archived templates, ordered by sort order and name.</summary>
    /// <returns>The active templates.</returns>
    IReadOnlyList<Template> GetActive();

    /// <summary>Returns all templates, including archived ones, ordered by sort order and name.</summary>
    /// <returns>All templates.</returns>
    IReadOnlyList<Template> GetAll();

    /// <summary>Returns the template with the given identifier, or <c>null</c>.</summary>
    /// <param name="id">The identifier of the template.</param>
    /// <returns>The template, or <c>null</c> when it does not exist.</returns>
    Template? GetById(Guid id);

    /// <summary>Inserts a new template. A <see cref="Guid.Empty"/> identifier is replaced with a new one.</summary>
    /// <param name="template">The template to insert.</param>
    void Insert(Template template);

    /// <summary>Updates an existing template.</summary>
    /// <param name="template">The template to update.</param>
    /// <exception cref="InvalidOperationException">Thrown when the template does not exist.</exception>
    void Update(Template template);
}
