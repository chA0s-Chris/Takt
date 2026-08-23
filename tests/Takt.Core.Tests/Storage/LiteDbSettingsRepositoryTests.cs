// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.Core.Tests.Storage;

using FluentAssertions;
using NUnit.Framework;
using Takt.Core.Domain;
using Takt.Core.Storage;
using Takt.Core.Tests.TestSupport;

[TestFixture]
public class LiteDbSettingsRepositoryTests
{
    private LiteDbSettingsRepository _repository;
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
    public void Get_ReturnsDefaultsWhenNothingWasSaved()
    {
        var settings = _repository.Get();

        settings.JiraBaseUrl.Should().BeNull();
        settings.JiraEmail.Should().BeNull();
    }

    [Test]
    public void Save_RoundTripsTheSettings()
    {
        var settings = new AppSettings
        {
            JiraBaseUrl = "https://example.atlassian.net",
            JiraEmail = "chris@example.com",
            WidgetPositionX = 1720,
            WidgetPositionY = 40
        };

        _repository.Save(settings);
        var stored = _repository.Get();

        stored.Should().BeEquivalentTo(settings);
    }

    [Test]
    public void Save_ReplacesThePreviousDocument()
    {
        _repository.Save(new()
        {
            JiraEmail = "old@example.com"
        });
        _repository.Save(new()
        {
            JiraEmail = "new@example.com"
        });

        var stored = _repository.Get();

        stored.JiraEmail.Should().Be("new@example.com");
    }
}
