// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.App.Views;

using Avalonia.Controls;
using Avalonia.Interactivity;
using Takt.Core.Jira;
using Takt.Core.Security;
using Takt.Core.Storage;

/// <summary>
/// Minimal Jira Cloud configuration: base URL and e-mail go into the settings
/// document, the API token into the credential store. Reachable from the tray menu,
/// so the widget-first workflow never needs the main window.
/// </summary>
public sealed partial class JiraSettingsDialog : Window
{
    private readonly ICredentialStore _credentials;
    private readonly ISettingsRepository _settings;

    /// <summary>Creates the dialog and loads the current configuration.</summary>
    /// <param name="settings">The settings repository.</param>
    /// <param name="credentials">The credential store holding the API token.</param>
    public JiraSettingsDialog(ISettingsRepository settings, ICredentialStore credentials)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(credentials);
        _settings = settings;
        _credentials = credentials;
        InitializeComponent();

        var current = _settings.Get();
        BaseUrlTextBox.Text = current.JiraBaseUrl;
        EmailTextBox.Text = current.JiraEmail;

        var hasToken = !String.IsNullOrEmpty(_credentials.Get(JiraCloudClient.ApiTokenCredentialName));
        TokenTextBox.PlaceholderText = hasToken ? "(unchanged)" : null;
        TokenHintText.Text = OperatingSystem.IsWindows()
            ? "Stored in the Windows Credential Manager, never in the database."
            : "Stored in an encrypted file in the data directory, never in the database.";
    }

    private static String? Normalize(String? value) =>
        String.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void OnCancelClick(Object? sender, RoutedEventArgs e) => Close();

    private void OnSaveClick(Object? sender, RoutedEventArgs e)
    {
        var settings = _settings.Get();
        settings.JiraBaseUrl = Normalize(BaseUrlTextBox.Text);
        settings.JiraEmail = Normalize(EmailTextBox.Text);
        _settings.Save(settings);

        var token = Normalize(TokenTextBox.Text);
        if (token is not null)
        {
            _credentials.Set(JiraCloudClient.ApiTokenCredentialName, token);
        }

        Close();
    }
}
