// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.Core.Tests.Jira;

using FluentAssertions;
using NUnit.Framework;
using Takt.Core.Jira;
using Takt.Core.Tests.TestSupport;

[TestFixture]
public class JiraIssueCacheTests
{
    private JiraIssueCache _cache;
    private StubJiraClient _jira;

    [SetUp]
    public void SetUp()
    {
        _jira = new()
        {
            IsConfigured = true
        };
        _jira.Issues["TEAM-1187"] = new("TEAM-1187", "Investigate gateway timeouts");
        _cache = new(_jira);
    }

    [Test]
    public async Task Get_ReturnsTheIssueAndKeepsIt()
    {
        var first = await _cache.GetAsync("TEAM-1187");
        _jira.Issues.Clear();
        var second = await _cache.GetAsync("team-1187");

        first.Should().Be(new JiraIssueSummary("TEAM-1187", "Investigate gateway timeouts"));
        second.Should().Be(first);
        _cache.GetCached("TEAM-1187").Should().Be(first);
    }

    [Test]
    public async Task Get_RemembersAnUnknownKey()
    {
        var unknown = await _cache.GetAsync("TEAM-9999");
        _jira.Issues["TEAM-9999"] = new("TEAM-9999", "Added later");

        unknown.Should().BeNull();
        (await _cache.GetAsync("TEAM-9999")).Should().BeNull();
    }

    [Test]
    public void GetCached_ReturnsNullBeforeALookup()
    {
        _cache.GetCached("TEAM-1187").Should().BeNull();
    }

    [Test]
    public async Task Clear_ForgetsTheLookups()
    {
        await _cache.GetAsync("TEAM-1187");

        _cache.Clear();

        _cache.GetCached("TEAM-1187").Should().BeNull();
    }
}
