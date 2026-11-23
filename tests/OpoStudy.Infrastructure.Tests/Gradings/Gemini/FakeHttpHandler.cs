using System.Net;
using System.Text;

namespace OpoStudy.Infrastructure.Tests.Gradings.Gemini;

/// <summary>Captures the request and answers with the configured response, or throws the configured exception.</summary>
internal sealed class FakeHttpHandler(Func<HttpRequestMessage, HttpResponseMessage>? respond = null) : HttpMessageHandler
{
    public HttpRequestMessage? Request { get; private set; }

    public string? RequestBody { get; private set; }

    public int Calls { get; private set; }

    public static FakeHttpHandler Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(_ => new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") });

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++;
        Request = request;
        RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        return respond is null ? new HttpResponseMessage(HttpStatusCode.OK) : respond(request);
    }
}