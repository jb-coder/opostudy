using System.Net;
using System.Text;

namespace OpoStudy.Mobile.Core.Tests.Auth;

/// <summary>Captures the request and answers with a canned response.</summary>
internal sealed class StubHttpMessageHandler(HttpStatusCode status, string body, string mediaType = "application/json") : HttpMessageHandler
{
    public HttpRequestMessage? Request { get; private set; }

    public string? RequestBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Request = request;
        RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, mediaType) };
    }
}