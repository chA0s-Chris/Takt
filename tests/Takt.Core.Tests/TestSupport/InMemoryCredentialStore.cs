// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.Core.Tests.TestSupport;

using Takt.Core.Security;

/// <summary>
/// An <see cref="ICredentialStore"/> fake holding secrets in a dictionary.
/// </summary>
public sealed class InMemoryCredentialStore : ICredentialStore
{
    private readonly Dictionary<String, String> _values = new(StringComparer.Ordinal);

    public String? Get(String name) => _values.TryGetValue(name, out var value) ? value : null;

    public void Remove(String name) => _values.Remove(name);

    public void Set(String name, String value) => _values[name] = value;
}
