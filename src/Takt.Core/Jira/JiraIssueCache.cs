// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.Core.Jira;

using System.Collections.Concurrent;

/// <summary>
/// Remembers issue summaries for the lifetime of the process. The sync view asks for the
/// same handful of keys over and over; without the cache every refresh would be a request
/// per entry. A key Jira does not know is remembered as such, so a typo is not retried on
/// every refresh either.
/// </summary>
public sealed class JiraIssueCache
{
    private readonly ConcurrentDictionary<String, JiraIssueSummary?> _cache =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly IJiraClient _jira;

    /// <summary>Creates the cache.</summary>
    /// <param name="jira">The client used for lookups that miss.</param>
    public JiraIssueCache(IJiraClient jira)
    {
        ArgumentNullException.ThrowIfNull(jira);
        _jira = jira;
    }

    /// <summary>Forgets everything, for example after the Jira settings changed.</summary>
    public void Clear() => _cache.Clear();

    /// <summary>
    /// Returns the issue for the given key, looking it up once and reusing the answer.
    /// </summary>
    /// <param name="issueKey">The issue key.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The issue, or <c>null</c> when Jira does not know the key.</returns>
    /// <exception cref="JiraException">Thrown when the lookup fails; nothing is cached then.</exception>
    public async Task<JiraIssueSummary?> GetAsync(String issueKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issueKey);
        var key = issueKey.Trim();
        if (_cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var summary = await _jira.GetIssueAsync(key, cancellationToken).ConfigureAwait(false);
        _cache[key] = summary;
        return summary;
    }

    /// <summary>Returns an already known issue without contacting Jira.</summary>
    /// <param name="issueKey">The issue key.</param>
    /// <returns>The issue, or <c>null</c> when it was not looked up yet or is unknown.</returns>
    public JiraIssueSummary? GetCached(String issueKey) =>
        String.IsNullOrWhiteSpace(issueKey)
            ? null
            : _cache.TryGetValue(issueKey.Trim(), out var summary)
                ? summary
                : null;
}
