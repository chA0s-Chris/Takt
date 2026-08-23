// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.Core.Tests.Storage;

using FluentAssertions;
using NUnit.Framework;
using Takt.Core.Storage;

[TestFixture]
public class DatabaseBackupTests
{
    private static readonly DateTime BaseTime = new(2026, 8, 23, 6, 0, 0, DateTimeKind.Utc);
    private String _databasePath;

    private String _directory;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"takt-backup-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
        _databasePath = Path.Combine(_directory, "takt.db");
        File.WriteAllText(_databasePath, "database content");
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_directory, true);

    [Test]
    public void Rotate_CopiesTheDatabaseIntoTheBackupsDirectory()
    {
        DatabaseBackup.Rotate(_databasePath, BaseTime);

        var backups = Directory.GetFiles(Path.Combine(_directory, "backups"));
        backups.Should().ContainSingle();
        File.ReadAllText(backups[0]).Should().Be("database content");
        Path.GetFileName(backups[0]).Should().Be("takt-20260823T060000000Z.db");
    }

    [Test]
    public void Rotate_KeepsOnlyTheNewestBackups()
    {
        for (var i = 0; i < 7; i++)
        {
            DatabaseBackup.Rotate(_databasePath, BaseTime.AddDays(i), 3);
        }

        var backups = Directory.GetFiles(Path.Combine(_directory, "backups"))
                               .Select(Path.GetFileName)
                               .OrderBy(x => x, StringComparer.Ordinal)
                               .ToList();
        backups.Should().HaveCount(3);
        backups[0].Should().Be("takt-20260827T060000000Z.db");
        backups[2].Should().Be("takt-20260829T060000000Z.db");
    }

    [Test]
    public void Rotate_DoesNothingWhenTheDatabaseFileDoesNotExist()
    {
        var missingPath = Path.Combine(_directory, "missing.db");

        DatabaseBackup.Rotate(missingPath, BaseTime);

        Directory.Exists(Path.Combine(_directory, "backups")).Should().BeFalse();
    }

    [Test]
    public void Rotate_RejectsANonPositiveKeepCount()
    {
        var act = () => DatabaseBackup.Rotate(_databasePath, BaseTime, 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
