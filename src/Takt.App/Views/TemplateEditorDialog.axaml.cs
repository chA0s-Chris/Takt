// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.App.Views;

using Avalonia.Controls;
using Avalonia.Threading;
using Takt.App.ViewModels;

/// <summary>
/// The template editor dialog. Typing in the issue search is debounced here; the view
/// model runs the query and owns the results.
/// </summary>
public sealed partial class TemplateEditorDialog : Window
{
    private readonly DispatcherTimer _issueSearchTimer;
    private readonly TemplateEditorViewModel _viewModel;

    /// <summary>Creates the dialog.</summary>
    /// <param name="viewModel">The editor view model.</param>
    public TemplateEditorDialog(TemplateEditorViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();

        _issueSearchTimer = new()
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        _issueSearchTimer.Tick += (_, _) =>
        {
            _issueSearchTimer.Stop();
            _ = _viewModel.IssueSearch.RunAsync();
        };
        _viewModel.IssueSearch.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(JiraIssueSearchViewModel.SearchText))
            {
                _issueSearchTimer.Stop();
                _issueSearchTimer.Start();
            }
        };

        _viewModel.IssueAssigned += (_, _) => IssueSearchButton.Flyout?.Hide();
        _viewModel.CloseRequested += (_, saved) => Close(saved);
    }
}
