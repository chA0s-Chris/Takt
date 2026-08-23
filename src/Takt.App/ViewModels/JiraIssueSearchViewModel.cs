// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.App.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using Takt.Core.Jira;

/// <summary>
/// The shared Jira issue search: free text matched against issue keys and summaries.
/// Used by the widget, the entry editor, and the template editor. The view debounces
/// typing and calls <see cref="RunAsync"/>; a newer search cancels the previous one.
/// </summary>
public sealed partial class JiraIssueSearchViewModel : ObservableObject
{
    private const Int32 MinimumQueryLength = 2;

    private readonly IJiraClient _jiraClient;

    private CancellationTokenSource? _cancellation;

    [ObservableProperty]
    private String? _searchText;

    [ObservableProperty]
    private String? _status;

    /// <summary>Creates the search view model.</summary>
    /// <param name="jiraClient">The Jira client the search runs against.</param>
    public JiraIssueSearchViewModel(IJiraClient jiraClient)
    {
        ArgumentNullException.ThrowIfNull(jiraClient);
        _jiraClient = jiraClient;
    }

    /// <summary>The issues matching the current query, in Jira's relevance order.</summary>
    public ObservableCollection<JiraIssueSummary> Results { get; } = new();

    /// <summary>Clears the query, the results, and the status message.</summary>
    public void Clear()
    {
        _cancellation?.Cancel();
        SearchText = null;
        Results.Clear();
        Status = null;
    }

    /// <summary>
    /// Runs the search for the current <see cref="SearchText"/>. Queries shorter than
    /// two characters only clear the results. Failures are reported through
    /// <see cref="Status"/> rather than thrown.
    /// </summary>
    /// <returns>A task that completes when the results are updated.</returns>
    public async Task RunAsync()
    {
        _cancellation?.Cancel();
        _cancellation?.Dispose();
        _cancellation = new();
        var cancellationToken = _cancellation.Token;

        var query = SearchText?.Trim();
        if (query is null || query.Length < MinimumQueryLength)
        {
            Results.Clear();
            Status = null;
            return;
        }

        if (!_jiraClient.IsConfigured)
        {
            Results.Clear();
            Status = "Jira is not configured yet — open \"Jira settings\" in the tray menu.";
            return;
        }

        Status = "Searching…";
        try
        {
            var issues = await _jiraClient.SearchIssuesAsync(query, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            Results.Clear();
            foreach (var issue in issues)
            {
                Results.Add(issue);
            }

            Status = issues.Count == 0 ? "No matching issues." : null;
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer search.
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
        {
            Results.Clear();
            Status = "Search failed — check the Jira settings and your connection.";
        }
    }
}
