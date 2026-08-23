// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.App.Views;

using Avalonia.Controls;

/// <summary>
/// The sync page. All behaviour lives in the view model; the view only lays out the
/// pending entries and the push buttons.
/// </summary>
public sealed partial class SyncView : UserControl
{
    /// <summary>Creates the view.</summary>
    public SyncView()
    {
        InitializeComponent();
    }
}
