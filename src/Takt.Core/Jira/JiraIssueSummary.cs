// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.Core.Jira;

/// <summary>
/// A Jira issue as returned by the issue search: its key and its summary line.
/// </summary>
/// <param name="Key">The issue key, for example <c>TEAM-1234</c>.</param>
/// <param name="Summary">The issue summary text.</param>
public sealed record JiraIssueSummary(String Key, String Summary);
