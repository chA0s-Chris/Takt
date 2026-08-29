// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.App.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Reflection;
using Takt.App.Services;

/// <summary>
/// The main window: the navigation rail and the page currently shown next to it.
/// The pages are long-lived, so switching back and forth keeps their state.
/// </summary>
public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly ThemeService _theme;

    [ObservableProperty]
    private ObservableObject _currentPage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ThemeGlyph))]
    [NotifyPropertyChangedFor(nameof(ThemeToolTip))]
    private Boolean _isDarkTheme;

    [ObservableProperty]
    private Boolean _isOverviewSelected;

    [ObservableProperty]
    private Boolean _isSettingsSelected;

    [ObservableProperty]
    private Boolean _isSyncSelected;

    [ObservableProperty]
    private Boolean _isTemplatesSelected;

    /// <summary>Creates the view model and selects the overview.</summary>
    /// <param name="overview">The overview page.</param>
    /// <param name="sync">The sync page.</param>
    /// <param name="templates">The templates page.</param>
    /// <param name="settings">The settings page.</param>
    /// <param name="theme">The service owning the selected appearance.</param>
    public MainWindowViewModel(
        OverviewViewModel overview,
        SyncViewModel sync,
        TemplatesViewModel templates,
        SettingsViewModel settings,
        ThemeService theme)
    {
        ArgumentNullException.ThrowIfNull(overview);
        ArgumentNullException.ThrowIfNull(sync);
        ArgumentNullException.ThrowIfNull(templates);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(theme);
        _theme = theme;
        _isDarkTheme = theme.IsDark;
        Overview = overview;
        Sync = sync;
        Templates = templates;
        Settings = settings;
        _currentPage = overview;
        _isOverviewSelected = true;

        var version = typeof(MainWindowViewModel).Assembly
                                                 .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                                                 ?.InformationalVersion;
        VersionText = $"Takt {version?.Split('+')[0] ?? "dev"}";
    }

    /// <summary>The overview page.</summary>
    public OverviewViewModel Overview { get; }

    /// <summary>The settings page.</summary>
    public SettingsViewModel Settings { get; }

    /// <summary>The sync page.</summary>
    public SyncViewModel Sync { get; }

    /// <summary>The templates page.</summary>
    public TemplatesViewModel Templates { get; }

    /// <summary>The glyph on the appearance toggle: it shows the appearance the button switches to.</summary>
    public String ThemeGlyph => IsDarkTheme ? "☀" : "☾";

    /// <summary>The tooltip on the appearance toggle.</summary>
    public String ThemeToolTip => IsDarkTheme ? "Switch to the light appearance" : "Switch to the dark appearance";

    /// <summary>The application version, shown at the bottom of the navigation rail.</summary>
    public String VersionText { get; }

    /// <summary>Reloads the current page; called whenever the window is shown.</summary>
    public void Refresh()
    {
        switch (CurrentPage)
        {
            case OverviewViewModel overview:
                overview.Refresh();
                break;
            case SyncViewModel sync:
                sync.Refresh();
                break;
            case TemplatesViewModel templates:
                templates.Refresh();
                break;
            case SettingsViewModel settings:
                settings.Refresh();
                break;
        }
    }

    /// <summary>Advances the running entry's elapsed time; called once per second.</summary>
    public void Tick() => (CurrentPage as OverviewViewModel)?.Tick();

    private void Select(ObservableObject page)
    {
        CurrentPage = page;
        IsOverviewSelected = ReferenceEquals(page, Overview);
        IsSyncSelected = ReferenceEquals(page, Sync);
        IsTemplatesSelected = ReferenceEquals(page, Templates);
        IsSettingsSelected = ReferenceEquals(page, Settings);
        Refresh();
    }

    [RelayCommand]
    private void ShowOverview() => Select(Overview);

    [RelayCommand]
    private void ShowSettings() => Select(Settings);

    [RelayCommand]
    private void ShowSync() => Select(Sync);

    [RelayCommand]
    private void ShowTemplates() => Select(Templates);

    [RelayCommand]
    private void ToggleTheme()
    {
        _theme.Toggle();
        IsDarkTheme = _theme.IsDark;
    }
}
