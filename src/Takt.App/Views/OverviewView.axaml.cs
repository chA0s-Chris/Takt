// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.App.Views;

using Avalonia.Controls;
using Avalonia.Input;
using Takt.App.ViewModels;

/// <summary>
/// The entry overview page. Double-clicking a row opens the entry editor; the dialog
/// is owned by the view, the view model only asks for it.
/// </summary>
public sealed partial class OverviewView : UserControl
{
    private OverviewViewModel? _viewModel;

    /// <summary>Creates the view.</summary>
    public OverviewView()
    {
        InitializeComponent();
    }

    /// <inheritdoc/>
    protected override void OnDataContextChanged(EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.EditRequested -= OnEditRequested;
        }

        _viewModel = DataContext as OverviewViewModel;
        if (_viewModel is not null)
        {
            _viewModel.EditRequested += OnEditRequested;
        }

        base.OnDataContextChanged(e);
    }

    private void OnEditRequested(Object? sender, EntryEditorViewModel editor) => _ = ShowEditorAsync(editor);

    private void OnRowDoubleTapped(Object? sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: TimeEntryRowViewModel row })
        {
            _viewModel?.Edit(row);
        }
    }

    private async Task ShowEditorAsync(EntryEditorViewModel editor)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner)
        {
            return;
        }

        var dialog = new EntryEditorDialog(editor);
        await dialog.ShowDialog(owner);
        _viewModel?.Refresh();
    }
}
