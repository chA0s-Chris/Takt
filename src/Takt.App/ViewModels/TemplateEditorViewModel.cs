// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.App.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Takt.Core.Domain;
using Takt.Core.Jira;
using Takt.Core.Storage;

/// <summary>
/// Creates or edits one template. Deleting is a two-step action; archiving from the
/// list is the reversible alternative.
/// </summary>
public sealed partial class TemplateEditorViewModel : ObservableObject
{
    private readonly Boolean _isNew;
    private readonly ITemplateRepository _templates;

    [ObservableProperty]
    private Boolean _isDeleteConfirmationVisible;

    [ObservableProperty]
    private String? _issueKey;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private String _name = String.Empty;

    [ObservableProperty]
    private String? _note;

    /// <summary>Creates the editor for a new or an existing template.</summary>
    /// <param name="template">The template to edit, or <c>null</c> to create one.</param>
    /// <param name="templates">The template repository.</param>
    /// <param name="jiraClient">The Jira client backing the issue search.</param>
    public TemplateEditorViewModel(Template? template, ITemplateRepository templates, IJiraClient jiraClient)
    {
        ArgumentNullException.ThrowIfNull(templates);
        ArgumentNullException.ThrowIfNull(jiraClient);
        _templates = templates;
        _isNew = template is null;
        Template = template ?? new Template();
        IssueSearch = new(jiraClient);

        Name = Template.Name;
        IssueKey = Template.DefaultJiraIssueKey;
        Note = Template.DefaultNote;
    }

    /// <summary>Raised when the dialog should close; <c>true</c> when the template was saved or deleted.</summary>
    public event EventHandler<Boolean>? CloseRequested;

    /// <summary>Raised after an issue was picked, so the view can close the search flyout.</summary>
    public event EventHandler? IssueAssigned;

    /// <summary>Indicates whether the template can be deleted (existing templates only).</summary>
    public Boolean IsDeletable => !_isNew;

    /// <summary>The Jira issue search behind the issue field.</summary>
    public JiraIssueSearchViewModel IssueSearch { get; }

    /// <summary>The edited template. Only <see cref="Save"/> writes it to the database.</summary>
    public Template Template { get; }

    /// <summary>The dialog title.</summary>
    public String Title => _isNew ? "New template" : "Edit template";

    private static String? Normalize(String? value) => String.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [RelayCommand]
    private void AssignIssue(JiraIssueSummary? issue)
    {
        if (issue is null)
        {
            return;
        }

        IssueKey = issue.Key;
        IssueSearch.Clear();
        IssueAssigned?.Invoke(this, EventArgs.Empty);
    }

    private Boolean CanSave() => !String.IsNullOrWhiteSpace(Name);

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(this, false);

    [RelayCommand]
    private void ClearIssue() => IssueKey = null;

    [RelayCommand]
    private void ConfirmDelete()
    {
        if (_isNew)
        {
            return;
        }

        _templates.Delete(Template.Id);
        CloseRequested?.Invoke(this, true);
    }

    [RelayCommand]
    private void Delete() => IsDeleteConfirmationVisible = true;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        Template.Name = Name.Trim();
        Template.DefaultJiraIssueKey = Normalize(IssueKey);
        Template.DefaultNote = Normalize(Note);

        if (_isNew)
        {
            Template.SortOrder = _templates.GetAll().Select(t => t.SortOrder).DefaultIfEmpty(-1).Max() + 1;
            _templates.Insert(Template);
        }
        else
        {
            _templates.Update(Template);
        }

        CloseRequested?.Invoke(this, true);
    }
}
