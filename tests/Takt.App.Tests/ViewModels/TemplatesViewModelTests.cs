// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.App.Tests.ViewModels;

using FluentAssertions;
using NUnit.Framework;
using Takt.App.ViewModels;
using Takt.Core.Domain;
using Takt.Core.Storage;
using Takt.Core.Tests.TestSupport;

/// <summary>
/// Sociable unit tests: the template list and its editor run against the real LiteDB
/// repository on a temporary database.
/// </summary>
[TestFixture]
public class TemplatesViewModelTests
{
    private StubJiraClient _jiraClient;
    private TempDatabase _tempDatabase;
    private LiteDbTemplateRepository _templates;
    private TemplatesViewModel _viewModel;

    [SetUp]
    public void SetUp()
    {
        _tempDatabase = new();
        _templates = new(_tempDatabase.Database);
        _jiraClient = new();
        _viewModel = new(_templates, _jiraClient,
                         new(new LiteDbTimeEntryRepository(_tempDatabase.Database), _templates));
    }

    [TearDown]
    public void TearDown() => _tempDatabase.Dispose();

    [Test]
    public void Refresh_SplitsActiveAndArchivedTemplates()
    {
        Insert("Meetings (Q3)", 0, "TEAM-1234");
        Insert("Support & incidents", 1, "TEAM-1100");
        Insert("Meetings (Q2)", 2, "TEAM-1233", true);

        _viewModel.Refresh();

        _viewModel.Templates.Select(row => row.Name).Should().Equal("Meetings (Q3)", "Support & incidents");
        _viewModel.ArchivedTemplates.Select(row => row.Name).Should().Equal("Meetings (Q2)");
        _viewModel.ArchivedHeaderText.Should().Be("Archived (1)");
        _viewModel.HasArchived.Should().BeTrue();
        _viewModel.IsEmpty.Should().BeFalse();
    }

    [Test]
    public void Duplicate_CopiesTheTemplateAndOpensItForEditing()
    {
        Insert("Meetings (Q2)", 0, "TEAM-1233", note: "Regular team meetings");
        _viewModel.Refresh();
        TemplateEditorViewModel? requested = null;
        _viewModel.EditRequested += (_, editor) => requested = editor;

        _viewModel.DuplicateCommand.Execute(_viewModel.Templates[0]);

        _viewModel.Templates.Select(row => row.Name).Should().Equal("Meetings (Q2)", "Meetings (Q2) (copy)");
        var copy = _viewModel.Templates[1];
        copy.IssueKey.Should().Be("TEAM-1233");
        copy.Note.Should().Be("Regular team meetings");
        requested.Should().NotBeNull();
        requested.Name.Should().Be("Meetings (Q2) (copy)");
    }

    [Test]
    public void ToggleArchive_MovesATemplateInAndOutOfTheArchive()
    {
        Insert("Meetings (Q2)", 0);
        _viewModel.Refresh();

        _viewModel.ToggleArchiveCommand.Execute(_viewModel.Templates[0]);

        _viewModel.Templates.Should().BeEmpty();
        _viewModel.ArchivedTemplates.Should().ContainSingle();
        _templates.GetActive().Should().BeEmpty();

        _viewModel.ToggleArchiveCommand.Execute(_viewModel.ArchivedTemplates[0]);

        _viewModel.Templates.Should().ContainSingle();
        _viewModel.ArchivedTemplates.Should().BeEmpty();
    }

    [Test]
    public void MoveDown_ReordersTheTemplatesPersistently()
    {
        Insert("Meetings (Q3)", 0);
        Insert("Support & incidents", 1);
        _viewModel.Refresh();

        _viewModel.MoveDownCommand.Execute(_viewModel.Templates[0]);

        _viewModel.Templates.Select(row => row.Name).Should().Equal("Support & incidents", "Meetings (Q3)");
        _templates.GetActive().Select(template => template.Name)
                  .Should().Equal("Support & incidents", "Meetings (Q3)");
    }

    [Test]
    public void MoveUp_IgnoresTheFirstTemplate()
    {
        Insert("Meetings (Q3)", 0);
        Insert("Support & incidents", 1);
        _viewModel.Refresh();

        _viewModel.MoveUpCommand.Execute(_viewModel.Templates[0]);

        _viewModel.Templates.Select(row => row.Name).Should().Equal("Meetings (Q3)", "Support & incidents");
    }

    [Test]
    public void NewTemplate_RequestsAnEditorForAnUnsavedTemplate()
    {
        TemplateEditorViewModel? requested = null;
        _viewModel.EditRequested += (_, editor) => requested = editor;

        _viewModel.NewTemplateCommand.Execute(null);

        requested.Should().NotBeNull();
        requested.Title.Should().Be("New template");
        requested.IsDeletable.Should().BeFalse();
        _templates.GetAll().Should().BeEmpty();
    }

    [Test]
    public void Editor_SavesANewTemplateAtTheEnd()
    {
        Insert("Meetings (Q3)", 0);
        var editor = new TemplateEditorViewModel(null, _templates, _jiraClient);
        editor.SaveCommand.CanExecute(null).Should().BeFalse();

        editor.Name = "Internal / admin";
        editor.Note = "Non-billable internal work";
        editor.SaveCommand.Execute(null);

        _templates.GetActive().Select(template => template.Name)
                  .Should().Equal("Meetings (Q3)", "Internal / admin");
    }

    [Test]
    public void Editor_UpdatesAnExistingTemplate()
    {
        var template = Insert("Meetings (Q2)", 0, "TEAM-1233");
        var editor = new TemplateEditorViewModel(template, _templates, _jiraClient);

        editor.Name = "Meetings (Q3)";
        editor.AssignIssueCommand.Execute(new("TEAM-1234", "Quarterly team meetings (Q3)"));
        editor.SaveCommand.Execute(null);

        var stored = _templates.GetById(template.Id);
        stored.Should().NotBeNull();
        stored.Name.Should().Be("Meetings (Q3)");
        stored.DefaultJiraIssueKey.Should().Be("TEAM-1234");
    }

    [Test]
    public void Editor_DeletesOnlyAfterConfirmation()
    {
        var template = Insert("Meetings (Q2)", 0);
        var editor = new TemplateEditorViewModel(template, _templates, _jiraClient);
        var closed = false;
        editor.CloseRequested += (_, result) => closed = result;

        editor.DeleteCommand.Execute(null);

        editor.IsDeleteConfirmationVisible.Should().BeTrue();
        _templates.GetById(template.Id).Should().NotBeNull();

        editor.ConfirmDeleteCommand.Execute(null);

        closed.Should().BeTrue();
        _templates.GetById(template.Id).Should().BeNull();
    }

    private Template Insert(String name, Int32 sortOrder, String? issueKey = null, Boolean archived = false, String? note = null)
    {
        var template = new Template
        {
            Name = name,
            SortOrder = sortOrder,
            DefaultJiraIssueKey = issueKey,
            DefaultNote = note,
            Archived = archived
        };
        _templates.Insert(template);
        return template;
    }
}
