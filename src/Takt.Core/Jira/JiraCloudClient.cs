// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.Core.Jira;

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Takt.Core.Security;
using Takt.Core.Storage;

/// <summary>
/// Jira Cloud REST v3 client using Basic authentication (e-mail + API token). Issue
/// search uses the issue picker endpoint, which matches keys and summary text and is
/// built for autocomplete-style queries.
/// </summary>
public sealed class JiraCloudClient : IJiraClient
{
    /// <summary>The credential store entry name of the Jira API token.</summary>
    public const String ApiTokenCredentialName = "jira-api-token";

    private readonly ICredentialStore _credentials;
    private readonly HttpClient _httpClient;
    private readonly ISettingsRepository _settings;

    /// <summary>Creates the client.</summary>
    /// <param name="httpClient">The HTTP client requests are sent with.</param>
    /// <param name="settings">The settings holding base URL and e-mail.</param>
    /// <param name="credentials">The credential store holding the API token.</param>
    public JiraCloudClient(HttpClient httpClient, ISettingsRepository settings, ICredentialStore credentials)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(credentials);
        _httpClient = httpClient;
        _settings = settings;
        _credentials = credentials;
    }

    /// <inheritdoc/>
    public Boolean IsConfigured => GetConfiguration() is not null;

    private static AuthenticationHeaderValue CreateBasicAuthentication(String email, String token) =>
        new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{email}:{token}")));

    private static List<JiraIssueSummary> ParseIssues(JsonElement root)
    {
        var results = new List<JiraIssueSummary>();
        var seenKeys = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetProperty("sections", out var sections) || sections.ValueKind != JsonValueKind.Array)
        {
            return results;
        }

        foreach (var section in sections.EnumerateArray())
        {
            if (!section.TryGetProperty("issues", out var issues) || issues.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var issue in issues.EnumerateArray())
            {
                if (!issue.TryGetProperty("key", out var keyProperty) || keyProperty.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var key = keyProperty.GetString();
                if (String.IsNullOrEmpty(key) || !seenKeys.Add(key))
                {
                    continue;
                }

                var summary = issue.TryGetProperty("summaryText", out var summaryProperty)
                              && summaryProperty.ValueKind == JsonValueKind.String
                    ? summaryProperty.GetString() ?? String.Empty
                    : String.Empty;
                results.Add(new(key, summary));
            }
        }

        return results;
    }

    private (Uri BaseAddress, String Email, String Token)? GetConfiguration()
    {
        var settings = _settings.Get();
        var token = _credentials.Get(ApiTokenCredentialName);
        if (String.IsNullOrWhiteSpace(settings.JiraBaseUrl)
            || String.IsNullOrWhiteSpace(settings.JiraEmail)
            || String.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        return Uri.TryCreate(settings.JiraBaseUrl, UriKind.Absolute, out var baseAddress)
            ? (baseAddress, settings.JiraEmail, token)
            : null;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<JiraIssueSummary>> SearchIssuesAsync(
        String query,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        var configuration = GetConfiguration()
                            ?? throw new InvalidOperationException(
                                "Jira is not configured. Set the base URL, e-mail, and API token first.");

        var requestUri = new Uri(
            configuration.BaseAddress,
            $"/rest/api/3/issue/picker?query={Uri.EscapeDataString(query.Trim())}");
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Authorization = CreateBasicAuthentication(configuration.Email, configuration.Token);

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
                                                   .ConfigureAwait(false);
            return ParseIssues(document.RootElement);
        }
    }
}
