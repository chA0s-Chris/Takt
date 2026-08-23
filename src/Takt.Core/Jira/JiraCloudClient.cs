// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.Core.Jira;

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Encodings.Web;
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

    /// <summary>
    /// The default encoder escapes characters that only matter inside HTML, which would
    /// turn the offset of a worklog start time and every umlaut of a note into an escape
    /// sequence. The payloads are JSON request bodies and never markup.
    /// </summary>
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private const String UnreachableMessage = "Could not reach Jira — check the base URL and your connection.";

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

    /// <summary>
    /// Wraps a note in the Atlassian Document Format the REST v3 comment fields expect:
    /// one paragraph per non-empty line.
    /// </summary>
    private static Object CreateCommentDocument(String comment)
    {
        var paragraphs = comment.ReplaceLineEndings("\n")
                                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                .Select(line => new
                                {
                                    type = "paragraph",
                                    content = new[]
                                    {
                                        new
                                        {
                                            type = "text",
                                            text = line
                                        }
                                    }
                                })
                                .ToArray();

        return new
        {
            type = "doc",
            version = 1,
            content = paragraphs
        };
    }

    private static async Task<String> DescribeFailureAsync(
        HttpResponseMessage response,
        String issueKey,
        CancellationToken cancellationToken)
    {
        var detail = await ReadErrorDetailAsync(response, cancellationToken).ConfigureAwait(false);
        return response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Jira rejected the credentials — check the e-mail and API token.",
            HttpStatusCode.Forbidden => $"Jira refused access to {issueKey} — check the account's permissions.",
            HttpStatusCode.NotFound => $"Jira does not know issue {issueKey}.",
            _ when detail is not null => $"Jira rejected the request: {detail}",
            _ => $"Jira answered with {(Int32)response.StatusCode} {response.ReasonPhrase}."
        };
    }

    /// <summary>
    /// Formats an instant the way the worklog endpoint requires: milliseconds and a
    /// numeric offset without a colon.
    /// </summary>
    private static String FormatStarted(DateTime startedAtUtc) =>
        startedAtUtc.ToUniversalTime()
                    .ToString("yyyy-MM-dd'T'HH:mm:ss.fff'+0000'", CultureInfo.InvariantCulture);

    private static String? ParseAccountName(JsonElement root)
    {
        foreach (var propertyName in new[]
                 {
                     "displayName",
                     "emailAddress"
                 })
        {
            if (root.TryGetProperty(propertyName, out var property)
                && property.ValueKind == JsonValueKind.String
                && property.GetString() is { Length: > 0 } value)
            {
                return value;
            }
        }

        return null;
    }

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

    private static async Task<JsonDocument> ReadDocumentAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            try
            {
                return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
                                         .ConfigureAwait(false);
            }
            catch (JsonException exception)
            {
                throw new JiraException("Jira returned a response Takt could not read.", exception);
            }
        }
    }

    /// <summary>Returns the first message of a Jira error payload, or <c>null</c>.</summary>
    private static async Task<String?> ReadErrorDetailAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (String.IsNullOrWhiteSpace(content))
            {
                return null;
            }

            using var document = JsonDocument.Parse(content);
            if (!document.RootElement.TryGetProperty("errorMessages", out var messages)
                || messages.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var message in messages.EnumerateArray())
            {
                if (message.ValueKind == JsonValueKind.String && message.GetString() is { Length: > 0 } value)
                {
                    return value;
                }
            }

            return null;
        }
        catch (Exception exception) when (exception is JsonException or HttpRequestException)
        {
            return null;
        }
    }

    private HttpRequestMessage CreateRequest(
        HttpMethod method,
        (Uri BaseAddress, String Email, String Token) configuration,
        String path)
    {
        var request = new HttpRequestMessage(method, new Uri(configuration.BaseAddress, path));
        request.Headers.Authorization = CreateBasicAuthentication(configuration.Email, configuration.Token);
        return request;
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

    private (Uri BaseAddress, String Email, String Token) RequireConfiguration() =>
        GetConfiguration()
        ?? throw new InvalidOperationException(
            "Jira is not configured. Set the base URL, e-mail, and API token first.");

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            throw new JiraException(UnreachableMessage, exception);
        }
    }

    /// <inheritdoc/>
    public async Task<String> CreateWorklogAsync(JiraWorklog worklog, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(worklog);
        var configuration = RequireConfiguration();

        Object payload;
        var started = FormatStarted(worklog.StartedAtUtc);
        var timeSpentSeconds = (Int64)worklog.Duration.TotalSeconds;
        if (String.IsNullOrWhiteSpace(worklog.Comment))
        {
            payload = new
            {
                started,
                timeSpentSeconds
            };
        }
        else
        {
            payload = new
            {
                started,
                timeSpentSeconds,
                comment = CreateCommentDocument(worklog.Comment)
            };
        }

        using var request = CreateRequest(
            HttpMethod.Post,
            configuration,
            $"/rest/api/3/issue/{Uri.EscapeDataString(worklog.IssueKey)}/worklog");
        request.Content = new StringContent(
            JsonSerializer.Serialize(payload, SerializerOptions),
            Encoding.UTF8,
            "application/json");

        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new JiraException(
                await DescribeFailureAsync(response, worklog.IssueKey, cancellationToken).ConfigureAwait(false));
        }

        using var document = await ReadDocumentAsync(response, cancellationToken).ConfigureAwait(false);
        if (document.RootElement.TryGetProperty("id", out var id)
            && id.ValueKind == JsonValueKind.String
            && id.GetString() is { Length: > 0 } worklogId)
        {
            return worklogId;
        }

        throw new JiraException("Jira accepted the worklog but did not return its identifier.");
    }

    /// <inheritdoc/>
    public async Task<Boolean> DeleteWorklogAsync(
        String issueKey,
        String worklogId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issueKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(worklogId);
        var configuration = RequireConfiguration();

        using var request = CreateRequest(
            HttpMethod.Delete,
            configuration,
            $"/rest/api/3/issue/{Uri.EscapeDataString(issueKey)}/worklog/{Uri.EscapeDataString(worklogId)}");

        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new JiraException(
                await DescribeFailureAsync(response, issueKey, cancellationToken).ConfigureAwait(false));
        }

        return true;
    }

    /// <inheritdoc/>
    public async Task<JiraIssueSummary?> GetIssueAsync(
        String issueKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issueKey);
        var configuration = RequireConfiguration();

        using var request = CreateRequest(
            HttpMethod.Get,
            configuration,
            $"/rest/api/3/issue/{Uri.EscapeDataString(issueKey.Trim())}?fields=summary");

        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new JiraException(
                await DescribeFailureAsync(response, issueKey, cancellationToken).ConfigureAwait(false));
        }

        using var document = await ReadDocumentAsync(response, cancellationToken).ConfigureAwait(false);
        var root = document.RootElement;
        var key = root.TryGetProperty("key", out var keyProperty) && keyProperty.ValueKind == JsonValueKind.String
            ? keyProperty.GetString() ?? issueKey
            : issueKey;
        var summary = root.TryGetProperty("fields", out var fields)
                      && fields.TryGetProperty("summary", out var summaryProperty)
                      && summaryProperty.ValueKind == JsonValueKind.String
            ? summaryProperty.GetString() ?? String.Empty
            : String.Empty;

        return new(key, summary);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<JiraIssueSummary>> SearchIssuesAsync(
        String query,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        var configuration = RequireConfiguration();

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

    /// <inheritdoc/>
    public async Task<JiraConnectionResult> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (GetConfiguration() is not { } configuration)
        {
            return new(false, "Base URL, e-mail, and API token are required.");
        }

        try
        {
            var requestUri = new Uri(configuration.BaseAddress, "/rest/api/3/myself");
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.Authorization = CreateBasicAuthentication(configuration.Email, configuration.Token);

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                return new(false, "Jira rejected the credentials — check the e-mail and API token.");
            }

            if (!response.IsSuccessStatusCode)
            {
                return new(false, $"Jira answered with {(Int32)response.StatusCode} {response.ReasonPhrase}.");
            }

            var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using (stream.ConfigureAwait(false))
            {
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
                                                       .ConfigureAwait(false);
                return new(true, $"Connected as {ParseAccountName(document.RootElement) ?? configuration.Email}.");
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException)
        {
            return new(false, UnreachableMessage);
        }
    }
}
