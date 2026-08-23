// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.App.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Globalization;
using Takt.App.Services;
using Takt.Core.Jira;
using Takt.Core.Platform;
using Takt.Core.Security;
using Takt.Core.Storage;

/// <summary>
/// The settings page: the Jira Cloud connection and the widget preferences. The API
/// token is written to the credential store and never to the database; the token box
/// stays empty and only overwrites the stored token when something is typed into it.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ICredentialStore _credentials;
    private readonly IJiraClient _jiraClient;
    private readonly SettingsNotifier _notifier;
    private readonly ISettingsRepository _settings;

    [ObservableProperty]
    private String? _apiToken;

    [ObservableProperty]
    private String? _connectionStatus;

    [ObservableProperty]
    private Boolean _isConnectionFailed;

    [ObservableProperty]
    private Boolean _isConnectionSuccessful;

    private Boolean _isLoading;

    [ObservableProperty]
    private String? _jiraBaseUrl;

    [ObservableProperty]
    private String? _jiraEmail;

    [ObservableProperty]
    private String? _saveStatus;

    [ObservableProperty]
    private String _tokenPlaceholder = String.Empty;

    [ObservableProperty]
    private Boolean _widgetAlwaysOnTop;

    [ObservableProperty]
    private String _widgetPositionText = String.Empty;

    [ObservableProperty]
    private Boolean _widgetShowIssueKey;

    /// <summary>Creates the view model and loads the stored settings.</summary>
    /// <param name="settings">The settings repository.</param>
    /// <param name="credentials">The credential store holding the API token.</param>
    /// <param name="jiraClient">The Jira client used for the connection test.</param>
    /// <param name="notifier">The notifier telling the widget about saved settings.</param>
    public SettingsViewModel(
        ISettingsRepository settings,
        ICredentialStore credentials,
        IJiraClient jiraClient,
        SettingsNotifier notifier)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNull(jiraClient);
        ArgumentNullException.ThrowIfNull(notifier);
        _settings = settings;
        _credentials = credentials;
        _jiraClient = jiraClient;
        _notifier = notifier;
        Refresh();
    }

    /// <summary>The database location, shown as a hint at the bottom of the page.</summary>
    public String DataPathText => $"Data: {AppDataPaths.GetDatabasePath()} · the last 5 backups are kept";

    /// <summary>Explains where the API token is kept on this operating system.</summary>
    public String TokenHint => OperatingSystem.IsWindows()
        ? "Stored in the Windows Credential Manager, never in the database."
        : "Stored in an encrypted file in the data directory, never in the database.";

    /// <summary>Re-reads the settings; called whenever the page is shown.</summary>
    public void Refresh()
    {
        _isLoading = true;
        var settings = _settings.Get();
        JiraBaseUrl = settings.JiraBaseUrl;
        JiraEmail = settings.JiraEmail;
        WidgetAlwaysOnTop = settings.WidgetAlwaysOnTop;
        WidgetShowIssueKey = settings.WidgetShowIssueKey;
        WidgetPositionText = settings is { WidgetPositionX: { } x, WidgetPositionY: { } y }
            ? String.Format(CultureInfo.CurrentCulture, "Currently at {0}, {1}", x, y)
            : "No position saved yet";
        ApiToken = null;
        SaveStatus = null;
        UpdateTokenPlaceholder();
        _isLoading = false;
    }

    private static String? Normalize(String? value) => String.IsNullOrWhiteSpace(value) ? null : value.Trim();

    partial void OnWidgetAlwaysOnTopChanged(Boolean value) => SaveWidgetPreferences();

    partial void OnWidgetShowIssueKeyChanged(Boolean value) => SaveWidgetPreferences();

    [RelayCommand]
    private void ResetWidgetPosition()
    {
        var settings = _settings.Get();
        settings.WidgetPositionX = null;
        settings.WidgetPositionY = null;
        _settings.Save(settings);
        WidgetPositionText = "No position saved yet";
        _notifier.NotifyChanged();
    }

    [RelayCommand]
    private void Save()
    {
        SaveJiraSettings();
        SaveStatus = "Saved.";
    }

    private void SaveJiraSettings()
    {
        var settings = _settings.Get();
        settings.JiraBaseUrl = Normalize(JiraBaseUrl);
        settings.JiraEmail = Normalize(JiraEmail);
        settings.WidgetAlwaysOnTop = WidgetAlwaysOnTop;
        settings.WidgetShowIssueKey = WidgetShowIssueKey;
        _settings.Save(settings);

        if (Normalize(ApiToken) is { } token)
        {
            _credentials.Set(JiraCloudClient.ApiTokenCredentialName, token);
            ApiToken = null;
        }

        UpdateTokenPlaceholder();
        _notifier.NotifyChanged();
    }

    private void SaveWidgetPreferences()
    {
        if (_isLoading)
        {
            return;
        }

        var settings = _settings.Get();
        settings.WidgetAlwaysOnTop = WidgetAlwaysOnTop;
        settings.WidgetShowIssueKey = WidgetShowIssueKey;
        _settings.Save(settings);
        _notifier.NotifyChanged();
    }

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        SaveJiraSettings();
        SaveStatus = "Saved.";
        ConnectionStatus = "Testing…";
        IsConnectionSuccessful = false;
        IsConnectionFailed = false;

        var result = await _jiraClient.TestConnectionAsync();
        ConnectionStatus = result.Message;
        IsConnectionSuccessful = result.Success;
        IsConnectionFailed = !result.Success;
    }

    private void UpdateTokenPlaceholder() =>
        TokenPlaceholder = String.IsNullOrEmpty(_credentials.Get(JiraCloudClient.ApiTokenCredentialName))
            ? "Paste your Jira API token"
            : "(unchanged)";
}
