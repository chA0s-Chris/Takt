// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.Core.Tests.TestSupport;

/// <summary>
/// A <see cref="TimeProvider"/> stub returning a settable instant.
/// </summary>
public sealed class TestTimeProvider : TimeProvider
{
    public override TimeZoneInfo LocalTimeZone => Zone;
    public DateTimeOffset UtcNow { get; set; }

    public TimeZoneInfo Zone { get; set; } = TimeZoneInfo.Utc;

    public override DateTimeOffset GetUtcNow() => UtcNow;
}
