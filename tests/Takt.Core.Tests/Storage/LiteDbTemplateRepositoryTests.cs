// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.Core.Tests.Storage;

using FluentAssertions;
using NUnit.Framework;
using Takt.Core.Domain;
using Takt.Core.Storage;
using Takt.Core.Tests.TestSupport;

[TestFixture]
public class LiteDbTemplateRepositoryTests
{
    private LiteDbTemplateRepository _repository;
    private TempDatabase _tempDatabase;

    [SetUp]
    public void SetUp()
    {
        _tempDatabase = new();
        _repository = new(_tempDatabase.Database);
    }

    [TearDown]
    public void TearDown() => _tempDatabase.Dispose();

    [Test]
    public void Changed_IsRaisedForEveryWriteAndNotForADeleteThatHitsNothing()
    {
        var changes = 0;
        _repository.Changed += (_, _) => changes++;
        var template = new Template
        {
            Name = "Meetings (Q3)"
        };

        _repository.Insert(template);
        template.Archived = true;
        _repository.Update(template);
        _repository.Delete(Guid.NewGuid());
        _repository.Delete(template.Id);

        changes.Should().Be(3);
    }

    [Test]
    public void Insert_AssignsIdAndRoundTripsAllValues()
    {
        var template = new Template
        {
            Name = "Meetings (Q2)",
            DefaultJiraIssueKey = "TEAM-1234",
            DefaultNote = "Quarterly meeting slot",
            SortOrder = 3
        };

        _repository.Insert(template);
        var stored = _repository.GetById(template.Id);

        template.Id.Should().NotBe(Guid.Empty);
        stored.Should().NotBeNull();
        stored.Should().BeEquivalentTo(template);
    }

    [Test]
    public void GetActive_ExcludesArchivedTemplatesAndSortsBySortOrderThenName()
    {
        var meetings = new Template
        {
            Name = "Meetings (Q3)",
            SortOrder = 1
        };
        var breakTemplate = new Template
        {
            Name = "break",
            SortOrder = 0
        };
        var adminTasks = new Template
        {
            Name = "Admin",
            SortOrder = 1
        };
        var archived = new Template
        {
            Name = "Meetings (Q2)",
            SortOrder = 1,
            Archived = true
        };
        _repository.Insert(meetings);
        _repository.Insert(breakTemplate);
        _repository.Insert(adminTasks);
        _repository.Insert(archived);

        var result = _repository.GetActive();

        result.Select(x => x.Name).Should().Equal("break", "Admin", "Meetings (Q3)");
    }

    [Test]
    public void GetAll_IncludesArchivedTemplates()
    {
        _repository.Insert(new()
        {
            Name = "Active"
        });
        _repository.Insert(new()
        {
            Name = "Archived",
            Archived = true
        });

        _repository.GetAll().Should().HaveCount(2);
    }

    [Test]
    public void Update_PersistsChanges()
    {
        var template = new Template
        {
            Name = "Meetings (Q2)",
            DefaultJiraIssueKey = "TEAM-1234"
        };
        _repository.Insert(template);

        template.Name = "Meetings (Q3)";
        template.DefaultJiraIssueKey = "TEAM-2345";
        _repository.Update(template);

        var stored = _repository.GetById(template.Id);
        stored.Should().NotBeNull();
        stored.Should().BeEquivalentTo(template);
    }

    [Test]
    public void Update_ThrowsForAMissingTemplate()
    {
        var template = new Template
        {
            Id = Guid.NewGuid(),
            Name = "Missing"
        };

        var act = () => _repository.Update(template);

        act.Should().Throw<InvalidOperationException>().WithMessage($"*{template.Id}*");
    }

    [Test]
    public void Delete_RemovesTheTemplate()
    {
        var template = new Template
        {
            Name = "Meetings (Q2)"
        };
        _repository.Insert(template);

        _repository.Delete(template.Id);

        _repository.GetById(template.Id).Should().BeNull();
    }
}
