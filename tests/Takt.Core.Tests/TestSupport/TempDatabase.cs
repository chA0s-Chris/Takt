// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.Core.Tests.TestSupport;

using Takt.Core.Storage;

/// <summary>
/// A <see cref="TaktDatabase"/> on a unique temporary file, deleted on dispose.
/// </summary>
public sealed class TempDatabase : IDisposable
{
    public TempDatabase()
    {
        DatabasePath = Path.Combine(Path.GetTempPath(), $"takt-tests-{Guid.NewGuid():N}.db");
        Database = new(DatabasePath);
    }

    public TaktDatabase Database { get; }

    public String DatabasePath { get; }

    public void Dispose()
    {
        Database.Dispose();
        File.Delete(DatabasePath);

        var logPath = Path.ChangeExtension(DatabasePath, null) + "-log.db";
        if (File.Exists(logPath))
        {
            File.Delete(logPath);
        }
    }
}
