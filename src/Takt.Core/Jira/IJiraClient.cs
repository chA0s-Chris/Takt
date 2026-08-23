// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.Core.Jira;

/// <summary>
/// Operations against the configured Jira Cloud instance.
/// </summary>
public interface IJiraClient
{
    /// <summary>
    /// Indicates whether base URL, e-mail, and API token are all configured, so calls
    /// can be attempted.
    /// </summary>
    Boolean IsConfigured { get; }

    /// <summary>
    /// Logs work on an issue.
    /// </summary>
    /// <param name="worklog">The work to log.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The identifier of the created Jira worklog.</returns>
    /// <exception cref="InvalidOperationException">Thrown when Jira is not configured.</exception>
    /// <exception cref="JiraException">Thrown when Jira rejects the worklog or cannot be reached.</exception>
    Task<String> CreateWorklogAsync(JiraWorklog worklog, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a worklog. A worklog Jira no longer knows is reported rather than thrown,
    /// so a re-push does not fail over work somebody removed in Jira.
    /// </summary>
    /// <param name="issueKey">The issue the worklog was created on.</param>
    /// <param name="worklogId">The identifier of the worklog.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns><c>true</c> when the worklog was deleted, <c>false</c> when Jira did not know it.</returns>
    /// <exception cref="InvalidOperationException">Thrown when Jira is not configured.</exception>
    /// <exception cref="JiraException">Thrown when Jira refuses the deletion or cannot be reached.</exception>
    Task<Boolean> DeleteWorklogAsync(String issueKey, String worklogId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Looks up a single issue by key, which doubles as the check that the key exists.
    /// </summary>
    /// <param name="issueKey">The issue key, for example <c>TEAM-1234</c>.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The issue, or <c>null</c> when Jira does not know the key.</returns>
    /// <exception cref="InvalidOperationException">Thrown when Jira is not configured.</exception>
    /// <exception cref="JiraException">Thrown when the lookup fails for any other reason.</exception>
    Task<JiraIssueSummary?> GetIssueAsync(String issueKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches issues by free text, matching issue keys and summaries
    /// (for example, <c>"test"</c> finds <c>TEAM-1234 "Create tests for XXX"</c>).
    /// </summary>
    /// <param name="query">The search text; at least one non-whitespace character.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The matching issues in relevance order, without duplicates.</returns>
    /// <exception cref="InvalidOperationException">Thrown when Jira is not configured.</exception>
    /// <exception cref="HttpRequestException">Thrown when the request fails or Jira rejects it.</exception>
    Task<IReadOnlyList<JiraIssueSummary>> SearchIssuesAsync(String query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies the stored configuration against Jira and reports the outcome. Never
    /// throws for an unreachable or rejecting server; the failure is part of the result.
    /// </summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The outcome of the connection test.</returns>
    Task<JiraConnectionResult> TestConnectionAsync(CancellationToken cancellationToken = default);
}
