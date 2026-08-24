// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.App.Views;

using Avalonia.Controls;
using Avalonia.Input;
using Takt.App.ViewModels;

/// <summary>
/// The template list page. Double-clicking a row opens the template editor; the
/// dialog is owned by the view, the view model only asks for it.
/// </summary>
public sealed partial class TemplatesView : UserControl
{
    private TemplatesViewModel? _viewModel;

    /// <summary>Creates the view.</summary>
    public TemplatesView()
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

        _viewModel = DataContext as TemplatesViewModel;
        if (_viewModel is not null)
        {
            _viewModel.EditRequested += OnEditRequested;
        }

        base.OnDataContextChanged(e);
    }

    private void OnEditRequested(Object? sender, TemplateEditorViewModel editor) => _ = ShowEditorAsync(editor);

    private void OnRowDoubleTapped(Object? sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: TemplateRowViewModel row })
        {
            _viewModel?.EditCommand.Execute(row);
        }
    }

    private async Task ShowEditorAsync(TemplateEditorViewModel editor)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner)
        {
            return;
        }

        var dialog = new TemplateEditorDialog(editor);
        // No refresh here: saving writes through the repository, which announces it.
        await dialog.ShowDialog(owner);
    }
}
