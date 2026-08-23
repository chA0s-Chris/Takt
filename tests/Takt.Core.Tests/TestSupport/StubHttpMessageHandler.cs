// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.Core.Tests.TestSupport;

using System.Net;
using System.Text;

/// <summary>
/// An <see cref="HttpMessageHandler"/> stub returning preconfigured responses and
/// capturing the requests needed for assertions. A single response can be set through
/// <see cref="StatusCode"/> and <see cref="ResponseContent"/>; call <see cref="Enqueue"/>
/// once per request when a call sequence has to answer differently each time.
/// </summary>
public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly List<StubRequest> _requests = [];
    private readonly Queue<(HttpStatusCode StatusCode, String Content)> _responses = new();

    public String? AuthorizationParameter { get; private set; }

    public String? AuthorizationScheme { get; private set; }

    public Uri? RequestUri { get; private set; }

    /// <summary>The requests received so far, in order.</summary>
    public IReadOnlyList<StubRequest> Requests => _requests;

    public String ResponseContent { get; set; } = "{}";

    public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;

    /// <summary>Adds the answer to the next request not yet answered by an earlier call.</summary>
    /// <param name="statusCode">The status code to return.</param>
    /// <param name="content">The response body.</param>
    public void Enqueue(HttpStatusCode statusCode, String content = "{}") => _responses.Enqueue((statusCode, content));

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var body = request.Content is null
            ? String.Empty
            : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        _requests.Add(new(request.Method, request.RequestUri, body));
        RequestUri = request.RequestUri;
        AuthorizationScheme = request.Headers.Authorization?.Scheme;
        AuthorizationParameter = request.Headers.Authorization?.Parameter;

        var (statusCode, content) = _responses.Count > 0 ? _responses.Dequeue() : (StatusCode, ResponseContent);
        return new(statusCode)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        };
    }
}
