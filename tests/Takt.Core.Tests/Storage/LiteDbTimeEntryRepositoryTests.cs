// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.Core.Tests.Storage;

using FluentAssertions;
using NUnit.Framework;
using Takt.Core.Domain;
using Takt.Core.Storage;
using Takt.Core.Tests.TestSupport;

[TestFixture]
public class LiteDbTimeEntryRepositoryTests
{
    private static readonly DateTime BaseTime = new(2026, 8, 23, 10, 15, 30, 123, DateTimeKind.Utc);
    private LiteDbTimeEntryRepository _repository;

    private TempDatabase _tempDatabase;

    [SetUp]
    public void SetUp()
    {
        _tempDatabase = new();
        _repository = new(_tempDatabase.Database);
    }

    [TearDown]
    public void TearDown() => _tempDatabase.Dispose();

    [Test]
    public void Insert_AssignsIdAndRoundTripsAllValues()
    {
        var entry = CreateClosedEntry(BaseTime);

        _repository.Insert(entry);
        var stored = _repository.GetById(entry.Id);

        entry.Id.Should().NotBe(Guid.Empty);
        stored.Should().NotBeNull();
        stored.Should().BeEquivalentTo(entry);
    }

    [Test]
    public void Insert_KeepsAProvidedId()
    {
        var entry = CreateClosedEntry(BaseTime);
        entry.Id = Guid.NewGuid();
        var providedId = entry.Id;

        _repository.Insert(entry);

        entry.Id.Should().Be(providedId);
        _repository.GetById(providedId).Should().NotBeNull();
    }

    [Test]
    public void StoredTimestampsStayUtcInstants()
    {
        var entry = CreateClosedEntry(BaseTime);

        _repository.Insert(entry);
        var stored = _repository.GetById(entry.Id);

        stored.Should().NotBeNull();
        stored.StartedAt.Kind.Should().Be(DateTimeKind.Utc);
        stored.StartedAt.Should().Be(BaseTime);
        stored.EndedAt.Should().NotBeNull();
        stored.EndedAt.Value.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Test]
    public void SyncStateIsStoredAsString()
    {
        var entry = CreateClosedEntry(BaseTime);
        entry.SyncState = SyncState.LocallyModified;

        _repository.Insert(entry);

        var rawDocument = _tempDatabase.Database.Database
                                       .GetCollection(TaktDatabase.TimeEntryCollectionName)
                                       .FindAll()
                                       .Single();
        rawDocument["SyncState"].IsString.Should().BeTrue();
        rawDocument["SyncState"].AsString.Should().Be(nameof(SyncState.LocallyModified));
    }

    [Test]
    public void GetOpenEntry_ReturnsTheRunningEntry()
    {
        _repository.Insert(CreateClosedEntry(BaseTime));
        var openEntry = CreateOpenEntry(BaseTime.AddHours(2));
        _repository.Insert(openEntry);

        var result = _repository.GetOpenEntry();

        result.Should().NotBeNull();
        result.Id.Should().Be(openEntry.Id);
    }

    [Test]
    public void GetOpenEntry_ReturnsNullWhenAllEntriesAreClosed()
    {
        _repository.Insert(CreateClosedEntry(BaseTime));

        _repository.GetOpenEntry().Should().BeNull();
    }

    [Test]
    public void GetBetween_FiltersByStartTimeAndOrdersChronologically()
    {
        var second = CreateClosedEntry(BaseTime.AddHours(1));
        var first = CreateClosedEntry(BaseTime);
        var beforeRange = CreateClosedEntry(BaseTime.AddHours(-1));
        var atUpperBound = CreateClosedEntry(BaseTime.AddHours(2));
        _repository.Insert(second);
        _repository.Insert(first);
        _repository.Insert(beforeRange);
        _repository.Insert(atUpperBound);

        var result = _repository.GetBetween(BaseTime, BaseTime.AddHours(2));

        result.Select(x => x.Id).Should().Equal(first.Id, second.Id);
    }

    [Test]
    public void GetPendingSync_ReturnsClosedUnsyncedEntriesWithIssueKey()
    {
        var pendingLocal = CreateClosedEntry(BaseTime);
        var pendingModified = CreateClosedEntry(BaseTime.AddHours(1));
        pendingModified.SyncState = SyncState.LocallyModified;
        pendingModified.JiraWorklogId = "10001";
        var alreadySynced = CreateClosedEntry(BaseTime.AddHours(2));
        alreadySynced.SyncState = SyncState.Synced;
        var withoutIssueKey = CreateClosedEntry(BaseTime.AddHours(3));
        withoutIssueKey.JiraIssueKey = null;
        var stillRunning = CreateOpenEntry(BaseTime.AddHours(4));
        _repository.Insert(pendingLocal);
        _repository.Insert(pendingModified);
        _repository.Insert(alreadySynced);
        _repository.Insert(withoutIssueKey);
        _repository.Insert(stillRunning);

        var result = _repository.GetPendingSync();

        result.Select(x => x.Id).Should().Equal(pendingLocal.Id, pendingModified.Id);
    }

    [Test]
    public void Update_PersistsChanges()
    {
        var entry = CreateClosedEntry(BaseTime);
        _repository.Insert(entry);

        entry.TaskName = "Renamed task";
        entry.SyncState = SyncState.Synced;
        entry.JiraWorklogId = "10001";
        _repository.Update(entry);

        var stored = _repository.GetById(entry.Id);
        stored.Should().NotBeNull();
        stored.Should().BeEquivalentTo(entry);
    }

    [Test]
    public void Update_ThrowsForAMissingEntry()
    {
        var entry = CreateClosedEntry(BaseTime);
        entry.Id = Guid.NewGuid();

        var act = () => _repository.Update(entry);

        act.Should().Throw<InvalidOperationException>().WithMessage($"*{entry.Id}*");
    }

    [Test]
    public void Delete_RemovesTheEntry()
    {
        var entry = CreateClosedEntry(BaseTime);
        _repository.Insert(entry);

        _repository.Delete(entry.Id);

        _repository.GetById(entry.Id).Should().BeNull();
    }

    [Test]
    public void Delete_IsANoOpForAMissingEntry()
    {
        var act = () => _repository.Delete(Guid.NewGuid());

        act.Should().NotThrow();
    }

    private static TimeEntry CreateClosedEntry(DateTime startedAtUtc) =>
        new()
        {
            TaskName = "Implement storage layer",
            JiraIssueKey = "TEAM-1234",
            Note = "Pairing session",
            StartedAt = startedAtUtc,
            EndedAt = startedAtUtc.AddMinutes(30),
            SyncState = SyncState.Local
        };

    private static TimeEntry CreateOpenEntry(DateTime startedAtUtc) =>
        new()
        {
            TaskName = "Running task",
            StartedAt = startedAtUtc,
            SyncState = SyncState.Local
        };
}
