// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.Core.Tests.TestSupport;

/// <summary>A request captured by <see cref="StubHttpMessageHandler"/>.</summary>
/// <param name="Method">The HTTP method.</param>
/// <param name="Uri">The requested URI.</param>
/// <param name="Body">The request body, empty when there was none.</param>
public sealed record StubRequest(HttpMethod Method, Uri? Uri, String Body);
