// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.Core.Storage;

using Takt.Core.Domain;

/// <summary>
/// Persistence operations for the single <see cref="AppSettings"/> document.
/// </summary>
public interface ISettingsRepository
{
    /// <summary>Returns the stored settings, or defaults when none have been saved yet.</summary>
    /// <returns>The application settings.</returns>
    AppSettings Get();

    /// <summary>Saves the settings, replacing the stored document.</summary>
    /// <param name="settings">The settings to save.</param>
    void Save(AppSettings settings);
}
