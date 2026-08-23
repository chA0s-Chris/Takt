// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.App.Services;

/// <summary>
/// Announces saved settings to the rest of the application, so the floating widget
/// picks up preference changes without a restart.
/// </summary>
public sealed class SettingsNotifier
{
    /// <summary>Raised after the settings were saved.</summary>
    public event EventHandler? Changed;

    /// <summary>Raises <see cref="Changed"/>.</summary>
    public void NotifyChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
