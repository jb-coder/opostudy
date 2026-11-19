using System.Net;

using NSubstitute;

using OpoStudy.Mobile.Core.Auth;

using Shouldly;

namespace OpoStudy.Mobile.Core.Tests.Auth;

public sealed class AuthHeaderHandlerTests
{
    private readonly IAuthService _auth = Substitute.For<IAuthService>();

    [Fact]
    public async Task SendAsync_WithSession_AddsBearerHeader()
    {
        _auth.GetIdTokenAsync(Arg.Any<CancellationToken>()).Returns("token-1");
        var (client, inner) = CreateClient();

        await client.GetAsync("/x", TestContext.Current.CancellationToken);

        inner.Request!.Headers.Authorization!.Scheme.ShouldBe("Bearer");
        inner.Request.Headers.Authorization.Parameter.ShouldBe("token-1");
    }

    [Fact]
    public async Task SendAsync_WithoutSession_AddsNoHeader()
    {
        _auth.GetIdTokenAsync(Arg.Any<CancellationToken>()).Returns((string?)null);
        var (client, inner) = CreateClient();

        await client.GetAsync("/x", TestContext.Current.CancellationToken);

        inner.Request!.Headers.Authorization.ShouldBeNull();
        _auth.DidNotReceive().InvalidateIdToken();
    }

    [Fact]
    public async Task SendAsync_Unauthorized_InvalidatesToken()
    {
        _auth.GetIdTokenAsync(Arg.Any<CancellationToken>()).Returns("token-1");
        var (client, _) = CreateClient(HttpStatusCode.Unauthorized);

        await client.GetAsync("/x", TestContext.Current.CancellationToken);

        _auth.Received(1).InvalidateIdToken();
    }

    [Fact]
    public async Task SendAsync_UnauthorizedWithoutToken_DoesNotInvalidate()
    {
        _auth.GetIdTokenAsync(Arg.Any<CancellationToken>()).Returns((string?)null);
        var (client, _) = CreateClient(HttpStatusCode.Unauthorized);

        await client.GetAsync("/x", TestContext.Current.CancellationToken);

        _auth.DidNotReceive().InvalidateIdToken();
    }

    private (HttpClient Client, StubHttpMessageHandler Inner) CreateClient(HttpStatusCode status = HttpStatusCode.OK)
    {
        var inner = new StubHttpMessageHandler(status, "{}");
        var handler = new AuthHeaderHandler(_auth) { InnerHandler = inner };
        return (new HttpClient(handler) { BaseAddress = new Uri("http://localhost") }, inner);
    }
}