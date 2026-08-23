// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.Core.Domain;

/// <summary>
/// A user-created task template, for example "Meetings (Q2)" with a default Jira issue.
/// Starting a task from a template copies its values into a new <see cref="TimeEntry"/>.
/// </summary>
public sealed class Template
{
    /// <summary>Indicates whether the template is hidden from quick-switch lists.</summary>
    public Boolean Archived { get; set; }

    /// <summary>The Jira issue key entries created from this template are associated with. Optional.</summary>
    public String? DefaultJiraIssueKey { get; set; }

    /// <summary>The note entries created from this template start with. Optional.</summary>
    public String? DefaultNote { get; set; }

    /// <summary>The unique identifier of the template.</summary>
    public Guid Id { get; set; }

    /// <summary>The display name of the template and the task name it produces.</summary>
    public String Name { get; set; } = String.Empty;

    /// <summary>The position of the template in quick-switch lists.</summary>
    public Int32 SortOrder { get; set; }
}
