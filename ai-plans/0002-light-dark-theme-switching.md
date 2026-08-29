# Light/dark theme switching for the main window

> Issue: [#2](https://github.com/chA0s-Chris/Takt/issues/2)

## Rationale

Takt pins the light theme in `App.axaml` while the floating widget is hand-coloured dark, so the application is visually inconsistent with itself. `IMPLEMENTATION_PLAN.md` records the pinned light theme as a deliberate Milestone 3 deviation rather than a decision anyone defended.

Users should be able to switch the main window between a light and a dark appearance at runtime. The widget pill is deliberately excluded: it is an always-on-top overlay, dark is the right treatment for it, and a light variant would be a regression. The flyouts it opens are ordinary surfaces and follow the selected appearance.

## Acceptance Criteria

- [ ] Outside the theme dictionaries that define the tokens, no colour literal appears in `App.axaml` or in any view except `WidgetWindow.axaml`; every colour used by the main window and its dialogs resolves through a named `Takt*` resource.
- [ ] The application resources define both a light and a dark value for every `Takt*` colour token.
- [ ] A sun/moon button at the bottom of the navigation rail switches the main window and every open dialog between the light and the dark appearance immediately, without restarting the application.
- [ ] The selected appearance is persisted and restored on the next start.
- [ ] The widget pill renders identically regardless of the selected appearance, while the flyouts it opens — issue search, note editor and quick-switch — follow the selected appearance.
- [ ] Automated tests cover the toggle changing the application theme variant, the persisted appearance being restored, and a widget flyout resolving against the selected appearance.

## Technical Details

### Persisted appearance

A new `TaktTheme` enum in `Takt.Core.Domain` with `Light = 0` and `Dark = 1`, and a new `AppSettings.Theme` property. `Takt.Core` must not reference Avalonia, so the mapping to Avalonia's `ThemeVariant` belongs in `Takt.App`. `Light = 0` keeps existing databases working without a migration, because LiteDB's `BsonMapper` leaves a missing field at the CLR default.

Two values only. "Follow the operating system" is deliberately out of scope: a two-state button cannot express a third state, and Avalonia's OS-preference detection on Linux/X11 is unverified. The enum leaves room to add it later without changing the persisted type.

### Applying the variant

A new `ThemeService` in `Takt.App/Services/`, registered as a singleton in `App.BuildServices`. It reads the stored `TaktTheme`, applies it by assigning `Application.Current.RequestedThemeVariant`, persists changes through `ISettingsRepository`, and raises `SettingsNotifier.NotifyChanged()` — the Get/Save/notify pattern `SettingsViewModel` already uses for the widget preferences.

`App.OnFrameworkInitializationCompleted` must apply the stored appearance after `BuildServices()` and before `_widgetWindow.Show()`; otherwise the first frame appears in the wrong one. That body runs only under `IClassicDesktopStyleApplicationLifetime`, which the headless harness does not provide, so the ordering is verified by observation on a real desktop session rather than by an automated test.

`MainWindowViewModel` currently receives only page view models and gains the `ThemeService` dependency plus the toggle command. The button sits beside the version text at the bottom of the rail in `MainWindow.axaml` and uses a plain BMP glyph, matching the existing icon set (`✎`, `⧉`, `▾`, `▶`) rather than a colour emoji. The system title bar is left alone: placing the control there would mean owning the window chrome, title, buttons, drag region and resize borders across both shipped platforms.

### Resource restructuring

`StaticResource` resolves exactly once and never re-evaluates, so the switch cannot work against the resources as they stand:

- Move the 19 `Takt*` brushes from `Application.Resources` into `ThemeDictionaries` keyed `Light` and `Dark`. `TaktMonoFontFamily` is not theme-dependent and stays in `Application.Resources`; every key placed in a theme dictionary must be defined in both variants.
- Convert the 43 `StaticResource` brush references to `DynamicResource`; the two `TaktMonoFontFamily` references can stay `StaticResource`. This includes `Background="{StaticResource TaktPageBrush}"` on the `MainWindow` element itself.
- Every colour literal outside `WidgetWindow.axaml` moves to a token. `#F7F8FA` is an exact duplicate of `TaktPageBrush` and should simply use it. `#F4F6FA` (the row backgrounds) and `#FAFBFC` (nested surfaces) have no exact counterpart — `TaktCardBrush` is pure white — so each needs a deliberate call between snapping to the nearest existing token and gaining its own. The green Overview highlights (`#EAF6ED`, `#E8F5EC`) and the Templates accent set (`#EDF0FA`, `#D8DEF3`, `#3D4A85`) carry meaning the palette does not yet name and need new tokens. `Button.primary`'s white foreground becomes a new `TaktOnAccentBrush`, white in both variants, so the rule holds without exceptions.

### The widget

`WidgetWindow.axaml` needs no change. The pill is hand-coloured with literals and therefore renders dark under either variant, while its three flyouts — issue search, note editor and quick-switch — contain no colour literals and resolve against FluentTheme, so they follow the selected appearance on their own.

Pinning `RequestedThemeVariant="Dark"` on the widget window was considered and rejected: it would lock those flyouts dark as well. Searching for an issue or editing a note should match the rest of the application; only the pill stays dark.

### Dark palette

The soft tints cannot be inverted mechanically. `TaktAccentSoftBrush`, `TaktRunningSoftBrush` and `TaktWarningBrush` are near-white washes that carry meaning and need hand-picked desaturated counterparts. The semantic colours — `TaktDangerBrush`, `TaktRunningBrush` and the warning amber — need a contrast check against the dark ground.

### Tests

Headless tests beside the existing `Views/MainWindowTests.cs` and `Views/WidgetWindowTests.cs`, using `Avalonia.Headless.NUnit`, FluentAssertions and hand-written test doubles per `tests/AGENTS.md`. Cover the toggle command changing `Application.Current.RequestedThemeVariant`, `ThemeService` restoring the stored appearance, and a widget flyout resolving to the selected variant.
