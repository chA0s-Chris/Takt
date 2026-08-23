// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.Core.Tests.Security;

using FluentAssertions;
using NUnit.Framework;
using Takt.Core.Security;

[TestFixture]
public class EncryptedFileCredentialStoreTests
{
    private String _directory;
    private EncryptedFileCredentialStore _store;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"takt-credentials-tests-{Guid.NewGuid():N}");
        _store = new(_directory);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_directory, true);

    [Test]
    public void Get_ReturnsNullForAMissingSecret()
    {
        _store.Get("jira-api-token").Should().BeNull();
    }

    [Test]
    public void Set_RoundTripsTheSecret()
    {
        _store.Set("jira-api-token", "secret-token");

        _store.Get("jira-api-token").Should().Be("secret-token");
    }

    [Test]
    public void Set_ReplacesAnExistingSecret()
    {
        _store.Set("jira-api-token", "old");
        _store.Set("jira-api-token", "new");

        _store.Get("jira-api-token").Should().Be("new");
    }

    [Test]
    public void Secrets_SurviveANewStoreInstance()
    {
        _store.Set("jira-api-token", "secret-token");

        var reopened = new EncryptedFileCredentialStore(_directory);

        reopened.Get("jira-api-token").Should().Be("secret-token");
    }

    [Test]
    public void Remove_DeletesTheSecretAndToleratesMissingOnes()
    {
        _store.Set("jira-api-token", "secret-token");

        _store.Remove("jira-api-token");
        _store.Remove("jira-api-token");

        _store.Get("jira-api-token").Should().BeNull();
    }

    [Test]
    public void TheStoreFileDoesNotContainThePlaintextSecret()
    {
        _store.Set("jira-api-token", "very-secret-token");

        var storeContent = File.ReadAllText(Path.Combine(_directory, "credentials.dat"));

        storeContent.Should().NotContain("very-secret-token");
    }

    [Test]
    public void KeyAndStoreFilesAreRestrictedToTheOwner()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Ignore("Unix file modes are not applicable on Windows.");
            return;
        }

        _store.Set("jira-api-token", "secret-token");

        var expected = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        File.GetUnixFileMode(Path.Combine(_directory, "credentials.key")).Should().Be(expected);
        File.GetUnixFileMode(Path.Combine(_directory, "credentials.dat")).Should().Be(expected);
    }
}
