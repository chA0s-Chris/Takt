// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.Core.Tests.Jira;

using FluentAssertions;
using NUnit.Framework;
using System.Net;
using System.Text;
using Takt.Core.Jira;
using Takt.Core.Storage;
using Takt.Core.Tests.TestSupport;

[TestFixture]
public class JiraCloudClientTests
{
    private JiraCloudClient _client;
    private InMemoryCredentialStore _credentials;
    private StubHttpMessageHandler _handler;
    private HttpClient _httpClient;
    private LiteDbSettingsRepository _settings;
    private TempDatabase _tempDatabase;

    [SetUp]
    public void SetUp()
    {
        _tempDatabase = new();
        _settings = new(_tempDatabase.Database);
        _credentials = new();
        _handler = new();
        _httpClient = new(_handler);
        _client = new(_httpClient, _settings, _credentials);
    }

    [TearDown]
    public void TearDown()
    {
        _httpClient.Dispose();
        _handler.Dispose();
        _tempDatabase.Dispose();
    }

    [Test]
    public void IsConfigured_IsFalseByDefault()
    {
        _client.IsConfigured.Should().BeFalse();
    }

    [Test]
    public void IsConfigured_IsTrueWithBaseUrlEmailAndToken()
    {
        Configure();

        _client.IsConfigured.Should().BeTrue();
    }

    [Test]
    public void IsConfigured_IsFalseWithoutAToken()
    {
        Configure();
        _credentials.Remove(JiraCloudClient.ApiTokenCredentialName);

        _client.IsConfigured.Should().BeFalse();
    }

    [Test]
    public void IsConfigured_IsFalseWithAnInvalidBaseUrl()
    {
        Configure("not a url");

        _client.IsConfigured.Should().BeFalse();
    }

    [Test]
    public async Task SearchIssues_ThrowsWhenNotConfigured()
    {
        var act = () => _client.SearchIssuesAsync("test");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Test]
    public async Task SearchIssues_SendsBasicAuthenticationAndTheQuery()
    {
        Configure();

        await _client.SearchIssuesAsync("test data");

        _handler.RequestUri.Should().Be(new Uri("https://acme.atlassian.net/rest/api/3/issue/picker?query=test%20data"));
        _handler.AuthorizationScheme.Should().Be("Basic");
        var expected = Convert.ToBase64String(Encoding.UTF8.GetBytes("chris@example.com:secret-token"));
        _handler.AuthorizationParameter.Should().Be(expected);
    }

    [Test]
    public async Task SearchIssues_ParsesSectionsAndRemovesDuplicates()
    {
        Configure();
        _handler.ResponseContent = """
                                   {
                                     "sections": [
                                       { "issues": [
                                           { "key": "TEAM-1234", "summaryText": "Create tests for XXX" },
                                           { "key": "TEAM-2", "summaryText": "Test data generator" }
                                       ]},
                                       { "issues": [
                                           { "key": "TEAM-1234", "summaryText": "Create tests for XXX" }
                                       ]}
                                     ]
                                   }
                                   """;

        var results = await _client.SearchIssuesAsync("test");

        results.Should().Equal(
            new JiraIssueSummary("TEAM-1234", "Create tests for XXX"),
            new JiraIssueSummary("TEAM-2", "Test data generator"));
    }

    [Test]
    public async Task SearchIssues_ReturnsEmptyForAnEmptyPickerResponse()
    {
        Configure();
        _handler.ResponseContent = """{ "sections": [] }""";

        var results = await _client.SearchIssuesAsync("nothing");

        results.Should().BeEmpty();
    }

    [Test]
    public async Task SearchIssues_ThrowsOnAnErrorStatus()
    {
        Configure();
        _handler.StatusCode = HttpStatusCode.Unauthorized;

        var act = () => _client.SearchIssuesAsync("test");

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Test]
    public async Task TestConnection_ReportsMissingConfiguration()
    {
        var result = await _client.TestConnectionAsync();

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("required");
    }

    [Test]
    public async Task TestConnection_ReportsTheAccountOnSuccess()
    {
        Configure();
        _handler.ResponseContent = """{ "displayName": "Chris Flessa", "emailAddress": "chris@example.com" }""";

        var result = await _client.TestConnectionAsync();

        _handler.RequestUri.Should().Be(new Uri("https://acme.atlassian.net/rest/api/3/myself"));
        result.Success.Should().BeTrue();
        result.Message.Should().Be("Connected as Chris Flessa.");
    }

    [Test]
    public async Task TestConnection_ReportsRejectedCredentials()
    {
        Configure();
        _handler.StatusCode = HttpStatusCode.Unauthorized;

        var result = await _client.TestConnectionAsync();

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("rejected the credentials");
    }

    [Test]
    public async Task TestConnection_ReportsAnUnexpectedStatus()
    {
        Configure();
        _handler.StatusCode = HttpStatusCode.ServiceUnavailable;

        var result = await _client.TestConnectionAsync();

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("503");
    }

    private void Configure(String baseUrl = "https://acme.atlassian.net")
    {
        _settings.Save(new()
        {
            JiraBaseUrl = baseUrl,
            JiraEmail = "chris@example.com"
        });
        _credentials.Set(JiraCloudClient.ApiTokenCredentialName, "secret-token");
    }
}
