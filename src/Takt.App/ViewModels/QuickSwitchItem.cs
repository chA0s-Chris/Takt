// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.App.ViewModels;

/// <summary>
/// One entry of the widget's quick-switch list, sourced from a template or a recent task.
/// </summary>
/// <param name="Name">The task name to start.</param>
/// <param name="JiraIssueKey">The Jira issue key the new entry is associated with.</param>
/// <param name="Note">The note the new entry starts with.</param>
/// <param name="IsTemplate">Whether the item stems from a template rather than a recent entry.</param>
public sealed record QuickSwitchItem(String Name, String? JiraIssueKey, String? Note, Boolean IsTemplate);
