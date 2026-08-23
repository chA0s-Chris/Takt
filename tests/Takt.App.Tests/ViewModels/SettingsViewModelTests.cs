// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.App.Tests.ViewModels;

using FluentAssertions;
using NUnit.Framework;
using Takt.App.Services;
using Takt.App.ViewModels;
using Takt.Core.Jira;
using Takt.Core.Storage;
using Takt.Core.Tests.TestSupport;

/// <summary>
/// Sociable unit tests: the settings page runs against the real LiteDB settings
/// repository; the credential store and the Jira client are test doubles.
/// </summary>
[TestFixture]
public class SettingsViewModelTests
{
    private InMemoryCredentialStore _credentials;
    private StubJiraClient _jiraClient;
    private Int32 _notificationCount;
    private SettingsNotifier _notifier;
    private LiteDbSettingsRepository _settings;
    private TempDatabase _tempDatabase;
    private SettingsViewModel _viewModel;

    [SetUp]
    public void SetUp()
    {
        _tempDatabase = new();
        _settings = new(_tempDatabase.Database);
        _credentials = new();
        _jiraClient = new();
        _notifier = new();
        _notificationCount = 0;
        _notifier.Changed += (_, _) => _notificationCount++;
        _viewModel = new(_settings, _credentials, _jiraClient, _notifier);
    }

    [TearDown]
    public void TearDown() => _tempDatabase.Dispose();

    [Test]
    public void Refresh_LoadsTheStoredSettings()
    {
        _settings.Save(new()
        {
            JiraBaseUrl = "https://acme.atlassian.net",
            JiraEmail = "chris@example.com",
            WidgetPositionX = 1720,
            WidgetPositionY = 40,
            WidgetShowIssueKey = false
        });

        _viewModel.Refresh();

        _viewModel.JiraBaseUrl.Should().Be("https://acme.atlassian.net");
        _viewModel.JiraEmail.Should().Be("chris@example.com");
        _viewModel.WidgetAlwaysOnTop.Should().BeTrue();
        _viewModel.WidgetShowIssueKey.Should().BeFalse();
        _viewModel.WidgetPositionText.Should().Be("Currently at 1720, 40");
        _viewModel.ApiToken.Should().BeNull();
    }

    [Test]
    public void Save_StoresTheConnectionAndAnnouncesIt()
    {
        _viewModel.JiraBaseUrl = " https://acme.atlassian.net ";
        _viewModel.JiraEmail = "chris@example.com";

        _viewModel.SaveCommand.Execute(null);

        var stored = _settings.Get();
        stored.JiraBaseUrl.Should().Be("https://acme.atlassian.net");
        stored.JiraEmail.Should().Be("chris@example.com");
        _viewModel.SaveStatus.Should().Be("Saved.");
        _notificationCount.Should().Be(1);
    }

    [Test]
    public void Save_WritesTheTokenToTheCredentialStoreOnly()
    {
        _viewModel.ApiToken = "secret-token";

        _viewModel.SaveCommand.Execute(null);

        _credentials.Get(JiraCloudClient.ApiTokenCredentialName).Should().Be("secret-token");
        _viewModel.ApiToken.Should().BeNull();
        _viewModel.TokenPlaceholder.Should().Be("(unchanged)");
    }

    [Test]
    public void Save_KeepsTheStoredTokenWhenTheBoxStaysEmpty()
    {
        _credentials.Set(JiraCloudClient.ApiTokenCredentialName, "secret-token");
        _viewModel.Refresh();

        _viewModel.SaveCommand.Execute(null);

        _credentials.Get(JiraCloudClient.ApiTokenCredentialName).Should().Be("secret-token");
    }

    [Test]
    public async Task TestConnection_SavesFirstAndReportsTheResult()
    {
        _jiraClient.ConnectionResult = new(true, "Connected as chris@example.com.");
        _viewModel.JiraBaseUrl = "https://acme.atlassian.net";

        await _viewModel.TestConnectionCommand.ExecuteAsync(null);

        _settings.Get().JiraBaseUrl.Should().Be("https://acme.atlassian.net");
        _jiraClient.ConnectionTestCount.Should().Be(1);
        _viewModel.ConnectionStatus.Should().Be("Connected as chris@example.com.");
        _viewModel.IsConnectionSuccessful.Should().BeTrue();
        _viewModel.IsConnectionFailed.Should().BeFalse();
    }

    [Test]
    public async Task TestConnection_ReportsAFailure()
    {
        _jiraClient.ConnectionResult = new(false, "Jira rejected the credentials — check the e-mail and API token.");

        await _viewModel.TestConnectionCommand.ExecuteAsync(null);

        _viewModel.IsConnectionSuccessful.Should().BeFalse();
        _viewModel.IsConnectionFailed.Should().BeTrue();
        _viewModel.ConnectionStatus.Should().Contain("rejected");
    }

    [Test]
    public void WidgetPreferences_ArePersistedImmediately()
    {
        _viewModel.WidgetAlwaysOnTop = false;

        _settings.Get().WidgetAlwaysOnTop.Should().BeFalse();
        _notificationCount.Should().Be(1);

        _viewModel.WidgetShowIssueKey = false;

        _settings.Get().WidgetShowIssueKey.Should().BeFalse();
        _notificationCount.Should().Be(2);
    }

    [Test]
    public void Refresh_DoesNotPersistWhileLoading()
    {
        _settings.Save(new()
        {
            WidgetAlwaysOnTop = false
        });

        _viewModel.Refresh();

        _notificationCount.Should().Be(0);
    }

    [Test]
    public void ResetWidgetPosition_ClearsTheStoredPosition()
    {
        _settings.Save(new()
        {
            WidgetPositionX = 1720,
            WidgetPositionY = 40
        });
        _viewModel.Refresh();

        _viewModel.ResetWidgetPositionCommand.Execute(null);

        var stored = _settings.Get();
        stored.WidgetPositionX.Should().BeNull();
        stored.WidgetPositionY.Should().BeNull();
        _viewModel.WidgetPositionText.Should().Be("No position saved yet");
        _notificationCount.Should().Be(1);
    }
}
