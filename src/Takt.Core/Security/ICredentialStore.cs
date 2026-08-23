// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.Core.Security;

/// <summary>
/// Stores secrets (like the Jira API token) outside of the database: in the Windows
/// Credential Manager on Windows, in an encrypted file elsewhere.
/// </summary>
public interface ICredentialStore
{
    /// <summary>Returns the stored secret, or <c>null</c> when none exists.</summary>
    /// <param name="name">The name of the secret.</param>
    /// <returns>The secret, or <c>null</c>.</returns>
    String? Get(String name);

    /// <summary>Removes the secret. Removing a missing secret is a no-op.</summary>
    /// <param name="name">The name of the secret.</param>
    void Remove(String name);

    /// <summary>Stores the secret, replacing any existing value.</summary>
    /// <param name="name">The name of the secret.</param>
    /// <param name="value">The secret value.</param>
    void Set(String name, String value);
}
