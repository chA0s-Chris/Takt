// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.Core.Tests.TestSupport;

using System.Net;
using System.Text;

/// <summary>
/// An <see cref="HttpMessageHandler"/> stub returning a preconfigured response and
/// capturing the request details needed for assertions.
/// </summary>
public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    public String? AuthorizationParameter { get; private set; }

    public String? AuthorizationScheme { get; private set; }

    public Uri? RequestUri { get; private set; }

    public String ResponseContent { get; set; } = "{}";

    public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        RequestUri = request.RequestUri;
        AuthorizationScheme = request.Headers.Authorization?.Scheme;
        AuthorizationParameter = request.Headers.Authorization?.Parameter;
        var response = new HttpResponseMessage(StatusCode)
        {
            Content = new StringContent(ResponseContent, Encoding.UTF8, "application/json")
        };
        return Task.FromResult(response);
    }
}
