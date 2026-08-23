// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.Core.Tests.TestSupport;

using Takt.Core.Jira;

/// <summary>
/// An <see cref="IJiraClient"/> stub returning preconfigured results and recording the
/// calls it received.
/// </summary>
public sealed class StubJiraClient : IJiraClient
{
    public JiraConnectionResult ConnectionResult { get; set; } = new(true, "Connected as tester.");

    public Int32 ConnectionTestCount { get; private set; }

    /// <summary>The failure thrown instead of creating a worklog, if any.</summary>
    public JiraException? CreateFailure { get; set; }

    public List<JiraWorklog> CreatedWorklogs { get; } = [];

    /// <summary>The failure thrown instead of deleting a worklog, if any.</summary>
    public JiraException? DeleteFailure { get; set; }

    public List<(String IssueKey, String WorklogId)> DeletedWorklogs { get; } = [];

    /// <summary>The issues <see cref="GetIssueAsync"/> knows; every other key is unknown.</summary>
    public Dictionary<String, JiraIssueSummary> Issues { get; } = new(StringComparer.OrdinalIgnoreCase);

    public String? LastQuery { get; private set; }

    /// <summary>The identifier handed out by the next successful worklog creation.</summary>
    public String NextWorklogId { get; set; } = "10001";

    public IReadOnlyList<JiraIssueSummary> Results { get; set; } = [];

    public Boolean IsConfigured { get; set; }

    public Task<String> CreateWorklogAsync(JiraWorklog worklog, CancellationToken cancellationToken = default)
    {
        if (CreateFailure is not null)
        {
            throw CreateFailure;
        }

        CreatedWorklogs.Add(worklog);
        return Task.FromResult(NextWorklogId);
    }

    public Task<Boolean> DeleteWorklogAsync(
        String issueKey,
        String worklogId,
        CancellationToken cancellationToken = default)
    {
        if (DeleteFailure is not null)
        {
            throw DeleteFailure;
        }

        DeletedWorklogs.Add((issueKey, worklogId));
        return Task.FromResult(true);
    }

    public Task<JiraIssueSummary?> GetIssueAsync(String issueKey, CancellationToken cancellationToken = default) =>
        Task.FromResult(Issues.GetValueOrDefault(issueKey));

    public Task<IReadOnlyList<JiraIssueSummary>> SearchIssuesAsync(
        String query,
        CancellationToken cancellationToken = default)
    {
        LastQuery = query;
        return Task.FromResult(Results);
    }

    public Task<JiraConnectionResult> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        ConnectionTestCount++;
        return Task.FromResult(ConnectionResult);
    }
}
