// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.App.Tests.Services;

using FluentAssertions;
using NUnit.Framework;
using Takt.App.Services;

[TestFixture]
public class SingleInstanceGuardTests
{
    private static String UniquePipeName() => $"takt-test-{Guid.NewGuid():N}";

    [Test]
    public void TryAcquire_SucceedsForTheFirstInstance()
    {
        using var guard = new SingleInstanceGuard(UniquePipeName());

        guard.TryAcquire().Should().BeTrue();
    }

    [Test]
    public void TryAcquire_FailsWhileAnotherInstanceHoldsThePipe()
    {
        var pipeName = UniquePipeName();
        using var first = new SingleInstanceGuard(pipeName);
        first.TryAcquire().Should().BeTrue();

        using var second = new SingleInstanceGuard(pipeName);

        second.TryAcquire().Should().BeFalse();
    }

    [Test]
    public void TryAcquire_SucceedsAgainAfterTheFirstInstanceIsDisposed()
    {
        var pipeName = UniquePipeName();
        var first = new SingleInstanceGuard(pipeName);
        first.TryAcquire().Should().BeTrue();
        first.Dispose();

        using var second = new SingleInstanceGuard(pipeName);

        second.TryAcquire().Should().BeTrue();
    }

    [Test]
    public void SignalRunningInstance_RaisesActivationRequestedOnTheRunningInstance()
    {
        var pipeName = UniquePipeName();
        using var running = new SingleInstanceGuard(pipeName);
        running.TryAcquire().Should().BeTrue();
        // Not disposed on purpose: a SemaphoreSlim without a wait handle needs no disposal,
        // and the pipe listener thread may release it after the test method returns.
        var activated = new SemaphoreSlim(0);
        running.ActivationRequested += (_, _) => activated.Release();

        using var second = new SingleInstanceGuard(pipeName);
        second.TryAcquire().Should().BeFalse();
        second.SignalRunningInstance().Should().BeTrue();

        activated.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue();
    }

    [Test]
    public void SignalRunningInstance_ReturnsFalseWhenNoInstanceIsRunning()
    {
        using var guard = new SingleInstanceGuard(UniquePipeName());

        guard.SignalRunningInstance().Should().BeFalse();
    }
}
