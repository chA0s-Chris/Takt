// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.Core.Tests.Platform;

using FluentAssertions;
using NUnit.Framework;
using Takt.Core.Platform;

[TestFixture]
[NonParallelizable]
public class AppDataPathsTests
{
    private String? _originalXdgDataHome;

    [SetUp]
    public void SetUp() => _originalXdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");

    [TearDown]
    public void TearDown() => Environment.SetEnvironmentVariable("XDG_DATA_HOME", _originalXdgDataHome);

    [Test]
    public void GetDataDirectory_OnWindows_UsesLocalApplicationData()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Ignore("Windows-only behavior.");
        }

        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Takt");
        AppDataPaths.GetDataDirectory().Should().Be(expected);
    }

    [Test]
    public void GetDataDirectory_OnUnix_HonorsXdgDataHome()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Ignore("Non-Windows behavior.");
        }

        Environment.SetEnvironmentVariable("XDG_DATA_HOME", "/custom/data");

        AppDataPaths.GetDataDirectory().Should().Be("/custom/data/takt");
    }

    [Test]
    public void GetDataDirectory_OnUnix_FallsBackToLocalShare()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Ignore("Non-Windows behavior.");
        }

        Environment.SetEnvironmentVariable("XDG_DATA_HOME", null);

        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".local",
            "share",
            "takt");
        AppDataPaths.GetDataDirectory().Should().Be(expected);
    }

    [Test]
    public void GetDatabasePath_PointsToTaktDbInsideTheDataDirectory()
    {
        var expected = Path.Combine(AppDataPaths.GetDataDirectory(), "takt.db");

        AppDataPaths.GetDatabasePath().Should().Be(expected);
    }
}
