// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.App.Views;

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Takt.App.ViewModels;

/// <summary>
/// The entry overview page. The edit button, the context menu, and a double-click all
/// open the entry editor; the dialog is owned by the view, the view model only asks for
/// it.
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

    /// <summary>
    /// Runs an action for the row a control belongs to. The row buttons and the context
    /// menu both inherit the row as their data context, so this is the one place that
    /// has to know how a control maps back to an entry.
    /// </summary>
    private static void Invoke(Object? sender, Action<TimeEntryRowViewModel> action)
    {
        if (sender is Control { DataContext: TimeEntryRowViewModel row })
        {
            action(row);
        }
    }

    private void OnDeleteMenuItemClick(Object? sender, RoutedEventArgs e) =>
        Invoke(sender, row => _viewModel?.DeleteCommand.Execute(row));

    private void OnEditMenuItemClick(Object? sender, RoutedEventArgs e) =>
        Invoke(sender, row => _viewModel?.EditCommand.Execute(row));

    private void OnEditRequested(Object? sender, EntryEditorViewModel editor) => _ = ShowEditorAsync(editor);

    private void OnRowDoubleTapped(Object? sender, TappedEventArgs e) =>
        Invoke(sender, row => _viewModel?.EditCommand.Execute(row));

    private async Task ShowEditorAsync(EntryEditorViewModel editor)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner)
        {
            return;
        }

        var dialog = new EntryEditorDialog(editor);
        // No refresh here: saving writes through the repository, which announces it.
        await dialog.ShowDialog(owner);
    }
}
