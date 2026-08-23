// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.App.Tests.TestSupport;

using Takt.Core.Jira;

/// <summary>
/// An <see cref="IJiraClient"/> stub returning preconfigured search results and
/// capturing the last query.
/// </summary>
public sealed class StubJiraClient : IJiraClient
{
    public String? LastQuery { get; private set; }

    public IReadOnlyList<JiraIssueSummary> Results { get; set; } = [];
    public Boolean IsConfigured { get; set; }

    public Task<IReadOnlyList<JiraIssueSummary>> SearchIssuesAsync(
        String query,
        CancellationToken cancellationToken = default)
    {
        LastQuery = query;
        return Task.FromResult(Results);
    }
}
