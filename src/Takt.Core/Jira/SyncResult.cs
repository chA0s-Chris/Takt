// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.Core.Jira;

/// <summary>
/// The outcome of pushing one time entry to Jira.
/// </summary>
/// <param name="EntryId">The identifier of the pushed entry.</param>
/// <param name="TaskName">The task name of the entry, so results read without a lookup.</param>
/// <param name="Success">Indicates whether Jira accepted the worklog.</param>
/// <param name="Message">The outcome in words, shown next to the entry.</param>
public sealed record SyncResult(Guid EntryId, String TaskName, Boolean Success, String Message);
