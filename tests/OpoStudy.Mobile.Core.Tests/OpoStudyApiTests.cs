using System.Net;
using System.Text;

using OpoStudy.Mobile.Core.Api;

using Refit;

using Shouldly;

namespace OpoStudy.Mobile.Core.Tests;

public sealed class OpoStudyApiTests
{
    [Fact]
    public async Task GetVersionAsync_ValidResponse_CallsVersionRouteAndDeserializes()
    {
        using var handler = new StubHandler("""{"version":"0.1.0"}""");
        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5054") };
        var api = RestService.ForGenerated<IOpoStudyApi>(client, ApiJsonContext.Default);

        var response = await api.GetVersionAsync(TestContext.Current.CancellationToken);

        response.Version.ShouldBe("0.1.0");
        handler.LastRequestUri.ShouldBe(new Uri("http://localhost:5054/api/v1/version"));
    }

    private sealed class StubHandler(string json) : HttpMessageHandler
    {
        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
        }
    }
}