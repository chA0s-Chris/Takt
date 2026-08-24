// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.App.Tests.Services;

using FluentAssertions;
using NUnit.Framework;
using Takt.App.Services;
using Takt.Core.Storage;
using Takt.Core.Tests.TestSupport;

/// <summary>
/// Sociable unit tests: the notifier listens to the real LiteDB repositories on a
/// temporary database.
/// </summary>
[TestFixture]
public class DataChangeNotifierTests
{
    private DataChangeNotifier _notifier;
    private TempDatabase _tempDatabase;
    private LiteDbTemplateRepository _templates;
    private LiteDbTimeEntryRepository _timeEntries;

    [SetUp]
    public void SetUp()
    {
        _tempDatabase = new();
        _timeEntries = new(_tempDatabase.Database);
        _templates = new(_tempDatabase.Database);
        _notifier = new(_timeEntries, _templates);
    }

    [TearDown]
    public void TearDown() => _tempDatabase.Dispose();

    [Test]
    public void Changes_AreAnnouncedPerRepository()
    {
        var entryChanges = 0;
        var templateChanges = 0;
        _notifier.TimeEntriesChanged += (_, _) => entryChanges++;
        _notifier.TemplatesChanged += (_, _) => templateChanges++;

        _timeEntries.Insert(new()
        {
            TaskName = "Investigate gateway timeouts",
            StartedAt = new(2026, 8, 23, 9, 0, 0, DateTimeKind.Utc)
        });
        _templates.Insert(new()
        {
            Name = "Meetings (Q3)"
        });

        entryChanges.Should().Be(1);
        templateChanges.Should().Be(1);
    }

    [Test]
    public void AWriteFromAnotherThread_IsMovedOntoTheCreatingThread()
    {
        var context = new RecordingSynchronizationContext();
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(context);
        DataChangeNotifier notifier;
        try
        {
            notifier = new(_timeEntries, _templates);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        var raised = 0;
        notifier.TimeEntriesChanged += (_, _) => raised++;

        // What a Jira push does: it writes the pushed entry from a background thread.
        Task.Run(() => _timeEntries.Insert(new()
            {
                TaskName = "Investigate gateway timeouts",
                StartedAt = new(2026, 8, 23, 9, 0, 0, DateTimeKind.Utc)
            }))
            .GetAwaiter()
            .GetResult();

        raised.Should().Be(0, "the handler must not run on the writing thread");
        context.Run();
        raised.Should().Be(1);
    }

    /// <summary>A stand-in for the UI thread: it queues what is posted to it.</summary>
    private sealed class RecordingSynchronizationContext : SynchronizationContext
    {
        private readonly Queue<(SendOrPostCallback Callback, Object? State)> _posted = new();

        /// <inheritdoc/>
        public override void Post(SendOrPostCallback d, Object? state) => _posted.Enqueue((d, state));

        /// <summary>Runs everything that was posted, as the dispatcher loop would.</summary>
        public void Run()
        {
            while (_posted.Count > 0)
            {
                var (callback, state) = _posted.Dequeue();
                callback(state);
            }
        }
    }
}
