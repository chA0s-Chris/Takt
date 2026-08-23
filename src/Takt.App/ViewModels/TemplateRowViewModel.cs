// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.App.ViewModels;

using Takt.Core.Domain;

/// <summary>
/// One row of the template list.
/// </summary>
public sealed class TemplateRowViewModel
{
    /// <summary>Creates a row for the given template.</summary>
    /// <param name="template">The template to render.</param>
    public TemplateRowViewModel(Template template)
    {
        ArgumentNullException.ThrowIfNull(template);
        Template = template;
    }

    /// <summary>Indicates whether the template carries a default Jira issue key.</summary>
    public Boolean HasIssueKey => !String.IsNullOrEmpty(Template.DefaultJiraIssueKey);

    /// <summary>Indicates whether the template is archived.</summary>
    public Boolean IsArchived => Template.Archived;

    /// <summary>The default Jira issue key, or <c>null</c>.</summary>
    public String? IssueKey => Template.DefaultJiraIssueKey;

    /// <summary>The template name, which becomes the task name.</summary>
    public String Name => Template.Name;

    /// <summary>The default note, or <c>null</c>.</summary>
    public String? Note => Template.DefaultNote;

    /// <summary>The underlying template.</summary>
    public Template Template { get; }
}
