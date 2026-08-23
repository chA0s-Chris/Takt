// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
[assembly: Avalonia.Headless.AvaloniaTestApplication(typeof(Takt.App.Tests.TestAppBuilder))]

namespace Takt.App.Tests;

using Avalonia;
using Avalonia.Headless;

/// <summary>
/// Boots the real <see cref="App"/> on the headless platform for UI tests. The app's
/// service composition only runs under the classic desktop lifetime, so headless tests
/// construct windows and view models with their own dependencies.
/// </summary>
public sealed class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UseHeadless(new());
}
