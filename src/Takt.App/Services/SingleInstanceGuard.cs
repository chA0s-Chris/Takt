// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.App.Services;

using System.IO.Pipes;

/// <summary>
/// Ensures only one Takt instance runs per user. A named pipe serves both as the
/// exclusive lock (only one server may exist) and as the activation channel through
/// which a second launch asks the running instance to show itself.
/// </summary>
public sealed class SingleInstanceGuard : IDisposable
{
    private readonly CancellationTokenSource _cancellation = new();
    private readonly String _pipeName;
    private NamedPipeServerStream? _server;

    /// <summary>Creates the guard.</summary>
    /// <param name="pipeName">Overrides the per-user pipe name; intended for tests.</param>
    public SingleInstanceGuard(String? pipeName = null)
    {
        _pipeName = pipeName ?? $"takt-{Environment.UserName}-single-instance";
    }

    /// <summary>
    /// Raised when another launch requests activation. The event is raised on a
    /// background thread; marshal to the UI thread before touching windows.
    /// </summary>
    public event EventHandler? ActivationRequested;

    /// <summary>
    /// Asks the running instance to activate itself. Call this after <see cref="TryAcquire"/>
    /// returned <c>false</c>.
    /// </summary>
    /// <returns><c>true</c> when the running instance received the request.</returns>
    public Boolean SignalRunningInstance()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out);
            client.Connect(2000);
            client.WriteByte(1);
            client.Flush();
            return true;
        }
        catch (Exception exception) when (exception is IOException or TimeoutException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// Attempts to become the single running instance.
    /// </summary>
    /// <returns><c>true</c> when this process now holds the lock; <c>false</c> when another instance already runs.</returns>
    public Boolean TryAcquire()
    {
        try
        {
            _server = new(
                _pipeName,
                PipeDirection.In,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }

        _ = ListenAsync(_server);
        return true;
    }

    private async Task ListenAsync(NamedPipeServerStream server)
    {
        var buffer = new Byte[1];
        while (!_cancellation.IsCancellationRequested)
        {
            try
            {
                await server.WaitForConnectionAsync(_cancellation.Token).ConfigureAwait(false);
                var bytesRead = await server.ReadAsync(buffer.AsMemory(), _cancellation.Token).ConfigureAwait(false);
                if (bytesRead > 0)
                {
                    ActivationRequested?.Invoke(this, EventArgs.Empty);
                }
            }
            catch (Exception exception) when (exception is OperationCanceledException or ObjectDisposedException)
            {
                return;
            }
            catch (IOException)
            {
                // The client vanished mid-handshake; fall through and listen again.
            }

            if (server.IsConnected)
            {
                try
                {
                    server.Disconnect();
                }
                catch (InvalidOperationException)
                {
                    // Already disconnected by the client.
                }
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _cancellation.Cancel();
        _server?.Dispose();
        _cancellation.Dispose();
    }
}
