// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.Core.Domain;

/// <summary>
/// The application settings, persisted as a single document. The Jira API token is
/// deliberately not part of this type; it lives in the credential store.
/// </summary>
public sealed class AppSettings
{
    /// <summary>The fixed identifier of the settings document.</summary>
    public Int32 Id { get; set; } = 1;

    /// <summary>The base URL of the Jira Cloud instance, for example <c>https://example.atlassian.net</c>.</summary>
    public String? JiraBaseUrl { get; set; }

    /// <summary>The e-mail address used for Jira Basic authentication.</summary>
    public String? JiraEmail { get; set; }

    /// <summary>Indicates whether the floating widget stays above other windows.</summary>
    public Boolean WidgetAlwaysOnTop { get; set; } = true;

    /// <summary>The saved horizontal screen position of the floating widget.</summary>
    public Int32? WidgetPositionX { get; set; }

    /// <summary>The saved vertical screen position of the floating widget.</summary>
    public Int32? WidgetPositionY { get; set; }

    /// <summary>Indicates whether the widget shows the Jira issue key of the current task.</summary>
    public Boolean WidgetShowIssueKey { get; set; } = true;
}
