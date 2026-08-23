// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.Core.Platform;

/// <summary>
/// Resolves the per-user directory that holds the database and other application data.
/// </summary>
public static class AppDataPaths
{
    /// <summary>
    /// Returns the data directory: <c>%LOCALAPPDATA%\Takt</c> on Windows,
    /// <c>$XDG_DATA_HOME/takt</c> (falling back to <c>~/.local/share/takt</c>) elsewhere.
    /// The directory is not created by this method.
    /// </summary>
    /// <returns>The absolute path of the data directory.</returns>
    public static String GetDataDirectory()
    {
        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Takt");
        }

        var xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        var baseDirectory = String.IsNullOrEmpty(xdgDataHome)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share")
            : xdgDataHome;
        return Path.Combine(baseDirectory, "takt");
    }

    /// <summary>
    /// Returns the absolute path of the LiteDB database file inside <see cref="GetDataDirectory"/>.
    /// </summary>
    /// <returns>The absolute path of the database file.</returns>
    public static String GetDatabasePath() => Path.Combine(GetDataDirectory(), "takt.db");
}
