// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.Core.Jira;

/// <summary>
/// The work to be logged on a Jira issue.
/// </summary>
/// <param name="IssueKey">The issue the work is logged on, for example <c>TEAM-1234</c>.</param>
/// <param name="StartedAtUtc">The UTC instant the work started at.</param>
/// <param name="Duration">The logged duration; Jira rejects anything below one minute.</param>
/// <param name="Comment">The worklog comment, or <c>null</c> for none.</param>
public sealed record JiraWorklog(String IssueKey, DateTime StartedAtUtc, TimeSpan Duration, String? Comment);
