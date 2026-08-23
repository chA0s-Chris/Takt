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
