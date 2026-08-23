// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.App;

using Avalonia;

internal static class Program
{
    // Avalonia configuration, don't remove; also used by the visual designer.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
                  .UsePlatformDetect()
                  .WithInterFont()
                  .LogToTrace();

    [STAThread]
    public static void Main(String[] args) =>
        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
}
