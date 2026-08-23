// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.Core.Domain;

/// <summary>
/// Describes the relationship between a locally recorded <see cref="TimeEntry"/> and its Jira worklog.
/// </summary>
public enum SyncState
{
    /// <summary>The entry exists only in the local database.</summary>
    Local = 0,

    /// <summary>The entry has been pushed to Jira and is unchanged since.</summary>
    Synced = 1,

    /// <summary>The entry was pushed to Jira but has been edited locally afterwards.</summary>
    LocallyModified = 2
}
