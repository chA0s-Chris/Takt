// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.Core.Jira;

/// <summary>
/// The outcome of a connection test against the configured Jira Cloud instance.
/// </summary>
/// <param name="Success">Whether Jira accepted the credentials.</param>
/// <param name="Message">A message ready to be shown next to the settings.</param>
public sealed record JiraConnectionResult(Boolean Success, String Message);
