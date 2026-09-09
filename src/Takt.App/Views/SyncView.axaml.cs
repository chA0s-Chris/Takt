// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.App.Views;

using Avalonia.Controls;
using Takt.App.ViewModels;

/// <summary>
/// The sync page. The view owns the entry editor dialog while the view model owns its state.
/// </summary>
public sealed partial class SyncView : UserControl
{
    private SyncViewModel? _viewModel;

    /// <summary>Creates the view.</summary>
    public SyncView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.EditRequested -= OnEditRequested;
        }

        _viewModel = DataContext as SyncViewModel;
        if (_viewModel is not null)
        {
            _viewModel.EditRequested += OnEditRequested;
        }

        base.OnDataContextChanged(e);
    }

    private void OnEditRequested(Object? sender, EntryEditorViewModel editor) => _ = ShowEditorAsync(editor);

    private async Task ShowEditorAsync(EntryEditorViewModel editor)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner)
        {
            return;
        }

        var dialog = new EntryEditorDialog(editor);
        await dialog.ShowDialog(owner);
    }
}
