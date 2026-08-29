// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.App.Services;

using Avalonia;
using Avalonia.Styling;
using Takt.Core.Domain;
using Takt.Core.Storage;

/// <summary>
/// Owns the appearance of the main window: it maps the stored <see cref="TaktTheme"/>
/// onto Avalonia's theme variant and persists the change. The floating widget is
/// hand-coloured and deliberately unaffected; only the surfaces it opens follow the
/// selected appearance.
/// </summary>
public sealed class ThemeService
{
    private readonly ISettingsRepository _settings;

    /// <summary>Creates the service and reads the stored appearance.</summary>
    /// <param name="settings">The settings repository holding the appearance.</param>
    public ThemeService(ISettingsRepository settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _settings = settings;
        CurrentTheme = _settings.Get().Theme;
    }

    /// <summary>The appearance currently selected.</summary>
    public TaktTheme CurrentTheme { get; private set; }

    /// <summary>Indicates whether the dark appearance is selected.</summary>
    public Boolean IsDark => CurrentTheme == TaktTheme.Dark;

    /// <summary>
    /// Applies the stored appearance to the running application. Called during startup
    /// before the first window is shown, so no frame is drawn in the wrong appearance.
    /// </summary>
    public void ApplyStoredTheme() => Apply(CurrentTheme);

    /// <summary>Selects an appearance, applies it, and persists it.</summary>
    /// <param name="theme">The appearance to select.</param>
    public void SetTheme(TaktTheme theme)
    {
        CurrentTheme = theme;
        Apply(theme);

        // Deliberately not announced through SettingsNotifier: its subscribers react to a
        // changed Jira connection and widget preferences, and a cosmetic change would make
        // them discard the Jira issue cache and re-apply widget settings for nothing.
        var settings = _settings.Get();
        settings.Theme = theme;
        _settings.Save(settings);
    }

    /// <summary>Switches to the other appearance and persists the choice.</summary>
    public void Toggle() => SetTheme(CurrentTheme == TaktTheme.Dark ? TaktTheme.Light : TaktTheme.Dark);

    private static void Apply(TaktTheme theme)
    {
        if (Application.Current is { } application)
        {
            application.RequestedThemeVariant = theme == TaktTheme.Dark
                ? ThemeVariant.Dark
                : ThemeVariant.Light;
        }
    }
}
