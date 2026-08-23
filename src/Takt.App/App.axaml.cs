// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.App;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Takt.App.Services;
using Takt.App.ViewModels;
using Takt.App.Views;
using Takt.Core.Jira;
using Takt.Core.Platform;
using Takt.Core.Security;
using Takt.Core.Storage;
using Takt.Core.Tracking;

/// <summary>
/// The Avalonia application: composes the services, shows the floating widget, owns
/// the tray icon, and enforces the lifetime rules (closing windows never exits; only
/// the tray menu does).
/// </summary>
public sealed partial class App : Application
{
    private JiraSettingsDialog? _jiraSettingsDialog;
    private MainWindow? _mainWindow;
    private ServiceProvider? _serviceProvider;
    private TrayIcon? _trayIcon;
    private WidgetWindow? _widgetWindow;

    /// <summary>Indicates that the application is shutting down and windows may really close.</summary>
    public static Boolean IsShutdownInProgress { get; private set; }

    /// <summary>The single-instance guard owned by <c>Program.Main</c>; set before the app starts.</summary>
    public static SingleInstanceGuard? SingleInstance { get; set; }

    /// <inheritdoc/>
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <inheritdoc/>
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            _serviceProvider = BuildServices();
            desktop.Exit += (_, _) => _serviceProvider.Dispose();

            _widgetWindow = _serviceProvider.GetRequiredService<WidgetWindow>();
            _widgetWindow.Show();

            SetUpTrayIcon(desktop);
            HookSingleInstanceActivation();
            _ = OfferRecoveryAsync(_serviceProvider, _widgetWindow);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static ServiceProvider BuildServices()
    {
        var dataDirectory = AppDataPaths.GetDataDirectory();
        Directory.CreateDirectory(dataDirectory);
        var databasePath = AppDataPaths.GetDatabasePath();
        DatabaseBackup.Rotate(databasePath, DateTime.UtcNow);

        var services = new ServiceCollection();
        services.AddSingleton(_ => new TaktDatabase(databasePath));
        services.AddSingleton<ITimeEntryRepository, LiteDbTimeEntryRepository>();
        services.AddSingleton<ITemplateRepository, LiteDbTemplateRepository>();
        services.AddSingleton<ISettingsRepository, LiteDbSettingsRepository>();
        services.AddSingleton<ICredentialStore>(_ => OperatingSystem.IsWindows()
                                                    ? new WindowsCredentialStore()
                                                    : new EncryptedFileCredentialStore(dataDirectory));
        services.AddSingleton(_ => new HttpClient());
        services.AddSingleton<IJiraClient, JiraCloudClient>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<TrackingService>();
        services.AddSingleton<SettingsNotifier>();
        services.AddSingleton<WidgetViewModel>();
        services.AddSingleton<WidgetWindow>();
        services.AddSingleton<OverviewViewModel>();
        services.AddSingleton<TemplatesViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindow>();
        return services.BuildServiceProvider();
    }

    private static async Task OfferRecoveryAsync(ServiceProvider serviceProvider, WidgetWindow owner)
    {
        var trackingService = serviceProvider.GetRequiredService<TrackingService>();
        var openEntry = trackingService.CurrentEntry;
        if (openEntry is null)
        {
            return;
        }

        var timeProvider = serviceProvider.GetRequiredService<TimeProvider>();
        var dialog = new RecoveryDialog(openEntry, timeProvider);
        var stopNow = await dialog.ShowDialog<Boolean>(owner);
        if (stopNow)
        {
            trackingService.Stop();
        }
    }

    private void HookSingleInstanceActivation()
    {
        if (SingleInstance is { } guard)
        {
            guard.ActivationRequested += (_, _) => Dispatcher.UIThread.Post(ShowWidget);
        }
    }

    private void SetUpTrayIcon(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var showWidgetItem = new NativeMenuItem("Show widget");
        showWidgetItem.Click += (_, _) => ShowWidget();
        var openMainItem = new NativeMenuItem("Open Takt…");
        openMainItem.Click += (_, _) => ShowMainWindow();
        var jiraSettingsItem = new NativeMenuItem("Jira settings…");
        jiraSettingsItem.Click += (_, _) => ShowJiraSettings();
        var exitItem = new NativeMenuItem("Exit");
        exitItem.Click += (_, _) => Shutdown(desktop);

        var menu = new NativeMenu();
        menu.Items.Add(showWidgetItem);
        menu.Items.Add(openMainItem);
        menu.Items.Add(jiraSettingsItem);
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(exitItem);

        _trayIcon = new()
        {
            Icon = new(AssetLoader.Open(new("avares://Takt.App/Assets/takt-icon.png"))),
            ToolTipText = "Takt",
            Menu = menu
        };
        _trayIcon.Clicked += (_, _) => ShowWidget();
        TrayIcon.SetIcons(this, new()
        {
            _trayIcon
        });
    }

    private void ShowJiraSettings()
    {
        if (_jiraSettingsDialog is { IsVisible: true })
        {
            _jiraSettingsDialog.Activate();
            return;
        }

        if (_serviceProvider is not { } serviceProvider)
        {
            return;
        }

        _jiraSettingsDialog = new(
            serviceProvider.GetRequiredService<ISettingsRepository>(),
            serviceProvider.GetRequiredService<ICredentialStore>(),
            serviceProvider.GetRequiredService<SettingsNotifier>());
        _jiraSettingsDialog.Closed += (_, _) => _jiraSettingsDialog = null;
        _jiraSettingsDialog.Show();
    }

    private void ShowMainWindow()
    {
        if (_serviceProvider is not { } serviceProvider)
        {
            return;
        }

        _mainWindow ??= serviceProvider.GetRequiredService<MainWindow>();
        _mainWindow.ShowAndActivate();
    }

    private void ShowWidget()
    {
        _widgetWindow?.Show();
        _widgetWindow?.Activate();
    }

    private void Shutdown(IClassicDesktopStyleApplicationLifetime desktop)
    {
        IsShutdownInProgress = true;
        _trayIcon?.Dispose();
        desktop.Shutdown();
    }
}
