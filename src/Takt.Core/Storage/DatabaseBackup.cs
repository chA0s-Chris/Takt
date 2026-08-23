// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.Core.Storage;

using System.Globalization;

/// <summary>
/// Creates rotating copies of the database file. Intended to run at application start,
/// before the database is opened.
/// </summary>
public static class DatabaseBackup
{
    /// <summary>
    /// Copies <paramref name="databasePath"/> into a <c>backups</c> subdirectory next to it,
    /// stamped with <paramref name="utcNow"/>, and deletes the oldest backups beyond
    /// <paramref name="keep"/>. Does nothing when the database file does not exist.
    /// </summary>
    /// <param name="databasePath">The absolute path of the database file.</param>
    /// <param name="utcNow">The current UTC instant, used for the backup file name.</param>
    /// <param name="keep">The number of backups to retain.</param>
    public static void Rotate(String databasePath, DateTime utcNow, Int32 keep = 5)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        ArgumentOutOfRangeException.ThrowIfLessThan(keep, 1);

        if (!File.Exists(databasePath))
        {
            return;
        }

        var parentDirectory = Path.GetDirectoryName(databasePath);
        if (String.IsNullOrEmpty(parentDirectory))
        {
            parentDirectory = ".";
        }

        var backupDirectory = Path.Combine(parentDirectory, "backups");
        Directory.CreateDirectory(backupDirectory);

        var baseName = Path.GetFileNameWithoutExtension(databasePath);
        var timestamp = utcNow.ToString("yyyyMMdd'T'HHmmssfff'Z'", CultureInfo.InvariantCulture);
        var backupPath = Path.Combine(backupDirectory, $"{baseName}-{timestamp}.db");
        File.Copy(databasePath, backupPath, true);

        // The timestamp format sorts lexicographically, so ordinal file name order is age order.
        var obsolete = Directory.GetFiles(backupDirectory, $"{baseName}-*.db")
                                .OrderByDescending(Path.GetFileName, StringComparer.Ordinal)
                                .Skip(keep);
        foreach (var file in obsolete)
        {
            File.Delete(file);
        }
    }
}
