// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.Core.Jira;

/// <summary>
/// A failed Jira request. The message is written for the user rather than for a log,
/// so callers can show it as it is.
/// </summary>
public sealed class JiraException : Exception
{
    /// <summary>Creates the exception.</summary>
    /// <param name="message">The message shown to the user.</param>
    public JiraException(String message)
        : base(message) { }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">The message shown to the user.</param>
    /// <param name="innerException">The underlying failure.</param>
    public JiraException(String message, Exception innerException)
        : base(message, innerException) { }
}
