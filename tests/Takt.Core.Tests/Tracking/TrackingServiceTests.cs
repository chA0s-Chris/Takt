// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.Core.Tests.Tracking;

using FluentAssertions;
using NUnit.Framework;
using Takt.Core.Domain;
using Takt.Core.Storage;
using Takt.Core.Tests.TestSupport;
using Takt.Core.Tracking;

/// <summary>
/// Sociable unit tests: the service runs against the real LiteDB repository on a
/// temporary database file; only the clock is a test double.
/// </summary>
[TestFixture]
public class TrackingServiceTests
{
    private static readonly DateTime BaseTime = new(2026, 8, 23, 9, 0, 0, DateTimeKind.Utc);
    private Int32 _changedCount;
    private LiteDbTimeEntryRepository _repository;
    private TrackingService _service;

    private TempDatabase _tempDatabase;
    private TestTimeProvider _timeProvider;

    [SetUp]
    public void SetUp()
    {
        _tempDatabase = new();
        _repository = new(_tempDatabase.Database);
        _timeProvider = new()
        {
            UtcNow = BaseTime
        };
        _service = new(_repository, _timeProvider);
        _changedCount = 0;
        _repository.Changed += (_, _) => _changedCount++;
    }

    [TearDown]
    public void TearDown() => _tempDatabase.Dispose();

    [Test]
    public void Start_CreatesAndPersistsAnOpenEntry()
    {
        var entry = _service.Start("Implement widget", "TEAM-1234", "First session");

        entry.IsRunning.Should().BeTrue();
        entry.StartedAt.Should().Be(BaseTime);
        entry.SyncState.Should().Be(SyncState.Local);

        var stored = _repository.GetOpenEntry();
        stored.Should().NotBeNull();
        stored.Should().BeEquivalentTo(entry);
        _service.IsTracking.Should().BeTrue();
        _changedCount.Should().Be(1);
    }

    [Test]
    public void Start_TrimsTheTaskNameAndDropsBlankOptionals()
    {
        var entry = _service.Start("  Implement widget  ", " ", "");

        entry.TaskName.Should().Be("Implement widget");
        entry.JiraIssueKey.Should().BeNull();
        entry.Note.Should().BeNull();
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Start_RejectsAnEmptyTaskName(String? taskName)
    {
        var act = () => _service.Start(taskName!);

        act.Should().Throw<ArgumentException>();
    }

    [Test]
    public void Start_ThrowsWhenATimerIsAlreadyRunning()
    {
        _service.Start("First task");

        var act = () => _service.Start("Second task");

        act.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void Stop_ClosesTheRunningEntryAndPersistsTheEndTime()
    {
        _service.Start("Implement widget");
        _timeProvider.UtcNow = BaseTime.AddMinutes(25);

        var stopped = _service.Stop();

        stopped.Should().NotBeNull();
        stopped.EndedAt.Should().Be(BaseTime.AddMinutes(25));
        _repository.GetOpenEntry().Should().BeNull();
        _service.IsTracking.Should().BeFalse();
        _changedCount.Should().Be(2);
    }

    [Test]
    public void Stop_ReturnsNullWhenNoTimerIsRunning()
    {
        var stopped = _service.Stop();

        stopped.Should().BeNull();
        _changedCount.Should().Be(0);
    }

    [Test]
    public void SwitchTo_ClosesTheOldEntryAndOpensANewOne()
    {
        var oldEntry = _service.Start("First task");
        _timeProvider.UtcNow = BaseTime.AddMinutes(40);

        var newEntry = _service.SwitchTo("Second task", "TEAM-1234");

        var storedOldEntry = _repository.GetById(oldEntry.Id);
        storedOldEntry.Should().NotBeNull();
        storedOldEntry.EndedAt.Should().Be(BaseTime.AddMinutes(40));

        newEntry.StartedAt.Should().Be(BaseTime.AddMinutes(40));
        newEntry.IsRunning.Should().BeTrue();

        var openEntry = _repository.GetOpenEntry();
        openEntry.Should().NotBeNull();
        openEntry.Id.Should().Be(newEntry.Id);
        _changedCount.Should().Be(3);
    }

    [Test]
    public void SwitchTo_SimplyStartsWhenNoTimerIsRunning()
    {
        var entry = _service.SwitchTo("Only task");

        entry.IsRunning.Should().BeTrue();
        _service.IsTracking.Should().BeTrue();
        _changedCount.Should().Be(1);
    }

    [Test]
    public void GetElapsed_ReturnsTheRunningDuration()
    {
        _service.Start("Implement widget");
        _timeProvider.UtcNow = BaseTime.AddMinutes(15);

        _service.GetElapsed().Should().Be(TimeSpan.FromMinutes(15));
    }

    [Test]
    public void GetElapsed_ReturnsZeroWhenNoTimerIsRunning()
    {
        _service.GetElapsed().Should().Be(TimeSpan.Zero);
    }
}
