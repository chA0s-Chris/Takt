// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.App;

using Avalonia;
using Takt.App.Services;

internal static class Program
{
    // Avalonia configuration, don't remove; also used by the visual designer.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
                  .UsePlatformDetect()
                  .WithInterFont()
                  .LogToTrace();

    [STAThread]
    public static void Main(String[] args)
    {
        using var singleInstance = new SingleInstanceGuard();
        if (!singleInstance.TryAcquire())
        {
            singleInstance.SignalRunningInstance();
            return;
        }

        App.SingleInstance = singleInstance;
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }
}
