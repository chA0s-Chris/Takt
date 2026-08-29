// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.Core.Domain;

/// <summary>
/// The appearance the main window is rendered in. Deliberately UI-framework agnostic:
/// <c>Takt.Core</c> must not reference Avalonia, so the mapping to a theme variant
/// happens at the application edge.
/// </summary>
public enum TaktTheme
{
    /// <summary>
    /// The light appearance. The CLR default, so databases written before this
    /// setting existed keep the appearance they were designed for.
    /// </summary>
    Light = 0,

    /// <summary>The dark appearance.</summary>
    Dark = 1
}
