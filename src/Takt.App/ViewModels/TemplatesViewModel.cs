// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.App.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using Takt.App.Services;
using Takt.Core.Domain;
using Takt.Core.Jira;
using Takt.Core.Storage;

/// <summary>
/// The template list: the quick-switch entries of the widget, in their display order.
/// Duplicating a template and changing its issue key is the quarterly rollover
/// ("Meetings (Q2)" becomes "Meetings (Q3)"); archiving hides a template without
/// losing it.
/// </summary>
public sealed partial class TemplatesViewModel : ObservableObject
{
    private const String CopySuffix = " (copy)";

    private readonly IJiraClient _jiraClient;
    private readonly ITemplateRepository _templates;

    [ObservableProperty]
    private Boolean _isArchivedVisible;

    /// <summary>Creates the view model and loads the templates.</summary>
    /// <param name="templates">The template repository.</param>
    /// <param name="jiraClient">The Jira client handed to the template editor.</param>
    /// <param name="dataChanges">Announces templates written anywhere, the editor included.</param>
    public TemplatesViewModel(ITemplateRepository templates, IJiraClient jiraClient, DataChangeNotifier dataChanges)
    {
        ArgumentNullException.ThrowIfNull(templates);
        ArgumentNullException.ThrowIfNull(jiraClient);
        ArgumentNullException.ThrowIfNull(dataChanges);
        _templates = templates;
        _jiraClient = jiraClient;
        dataChanges.TemplatesChanged += (_, _) => Refresh();
        Refresh();
    }

    /// <summary>Raised when a template should be edited; the view shows the dialog.</summary>
    public event EventHandler<TemplateEditorViewModel>? EditRequested;

    /// <summary>The header of the archived section, for example <c>Archived (2)</c>.</summary>
    public String ArchivedHeaderText => $"Archived ({ArchivedTemplates.Count})";

    /// <summary>The archived templates.</summary>
    public ObservableCollection<TemplateRowViewModel> ArchivedTemplates { get; } = new();

    /// <summary>Indicates whether any archived template exists.</summary>
    public Boolean HasArchived => ArchivedTemplates.Count > 0;

    /// <summary>Indicates whether no active template exists yet.</summary>
    public Boolean IsEmpty => Templates.Count == 0;

    /// <summary>The active templates, in display order.</summary>
    public ObservableCollection<TemplateRowViewModel> Templates { get; } = new();

    /// <summary>Reloads the templates from the database; called on every stored change.</summary>
    public void Refresh()
    {
        Templates.Clear();
        ArchivedTemplates.Clear();
        foreach (var template in _templates.GetAll())
        {
            var row = new TemplateRowViewModel(template);
            (template.Archived ? ArchivedTemplates : Templates).Add(row);
        }

        OnPropertyChanged(nameof(ArchivedHeaderText));
        OnPropertyChanged(nameof(HasArchived));
        OnPropertyChanged(nameof(IsEmpty));
    }

    [RelayCommand]
    private void Duplicate(TemplateRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        var copy = new Template
        {
            Name = row.Template.Name + CopySuffix,
            DefaultJiraIssueKey = row.Template.DefaultJiraIssueKey,
            DefaultNote = row.Template.DefaultNote,
            SortOrder = row.Template.SortOrder
        };
        _templates.Insert(copy);
        Resequence();
        EditRequested?.Invoke(this, new(copy, _templates, _jiraClient));
    }

    [RelayCommand]
    private void Edit(TemplateRowViewModel? row)
    {
        if (row is not null)
        {
            EditRequested?.Invoke(this, new(row.Template, _templates, _jiraClient));
        }
    }

    private void MoveBy(TemplateRowViewModel? row, Int32 offset)
    {
        if (row is null)
        {
            return;
        }

        var index = Templates.IndexOf(row);
        var targetIndex = index + offset;
        if (index < 0 || targetIndex < 0 || targetIndex >= Templates.Count)
        {
            return;
        }

        var moved = Templates[index].Template;
        var displaced = Templates[targetIndex].Template;
        (moved.SortOrder, displaced.SortOrder) = (displaced.SortOrder, moved.SortOrder);
        _templates.Update(moved);
        _templates.Update(displaced);
    }

    [RelayCommand]
    private void MoveDown(TemplateRowViewModel? row) => MoveBy(row, 1);

    [RelayCommand]
    private void MoveUp(TemplateRowViewModel? row) => MoveBy(row, -1);

    [RelayCommand]
    private void NewTemplate() => EditRequested?.Invoke(this, new(null, _templates, _jiraClient));

    private void Resequence()
    {
        var order = 0;
        foreach (var template in _templates.GetAll())
        {
            template.SortOrder = order++;
            _templates.Update(template);
        }
    }

    [RelayCommand]
    private void ToggleArchive(TemplateRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        row.Template.Archived = !row.Template.Archived;
        _templates.Update(row.Template);
    }

    [RelayCommand]
    private void ToggleArchivedVisible() => IsArchivedVisible = !IsArchivedVisible;
}
