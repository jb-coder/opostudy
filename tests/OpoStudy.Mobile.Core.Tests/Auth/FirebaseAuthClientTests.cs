using System.Net;
using System.Text.Json;

using OpoStudy.Mobile.Core.Auth;

using Shouldly;

namespace OpoStudy.Mobile.Core.Tests.Auth;

public sealed class FirebaseAuthClientTests
{
    private const string SignInBody = """{"idToken":"id-1","refreshToken":"refresh-1","expiresIn":"3600","email":"a@b.com"}""";

    private static readonly FirebaseOptions Options = new("fake-api-key", "123-web.apps.googleusercontent.com", "fake-project");

    private static FirebaseAuthClient CreateSut(StubHttpMessageHandler handler, FirebaseOptions? options = null) =>
        new(new HttpClient(handler), options ?? Options);

    [Fact]
    public async Task SignInWithPasswordAsync_Success_PostsCredentialsAndParsesStringExpiry()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, SignInBody);

        var tokens = await CreateSut(handler).SignInWithPasswordAsync("a@b.com", "secret1", TestContext.Current.CancellationToken);

        handler.Request!.RequestUri!.ToString().ShouldBe("https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key=fake-api-key");
        handler.Request.Method.ShouldBe(HttpMethod.Post);
        using var body = JsonDocument.Parse(handler.RequestBody!);
        body.RootElement.GetProperty("email").GetString().ShouldBe("a@b.com");
        body.RootElement.GetProperty("password").GetString().ShouldBe("secret1");
        body.RootElement.GetProperty("returnSecureToken").GetBoolean().ShouldBeTrue();
        tokens.ShouldBe(new FirebaseTokens("id-1", "refresh-1", 3600, "a@b.com"));
    }

    [Fact]
    public async Task SignUpAsync_Success_UsesSignUpEndpoint()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, SignInBody);

        await CreateSut(handler).SignUpAsync("a@b.com", "secret1", TestContext.Current.CancellationToken);

        handler.Request!.RequestUri!.AbsolutePath.ShouldBe("/v1/accounts:signUp");
    }

    [Fact]
    public async Task SignInWithGoogleAsync_Success_PostsIdpBody()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, SignInBody);

        await CreateSut(handler).SignInWithGoogleAsync("google-token", TestContext.Current.CancellationToken);

        handler.Request!.RequestUri!.AbsolutePath.ShouldBe("/v1/accounts:signInWithIdp");
        using var body = JsonDocument.Parse(handler.RequestBody!);
        body.RootElement.GetProperty("postBody").GetString().ShouldBe("id_token=google-token&providerId=google.com");
        body.RootElement.GetProperty("requestUri").GetString().ShouldBe("http://localhost");
        body.RootElement.GetProperty("returnSecureToken").GetBoolean().ShouldBeTrue();
        body.RootElement.GetProperty("returnIdpCredential").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task RefreshAsync_Success_SendsFormAndReadsSnakeCase()
    {
        var handler = new StubHttpMessageHandler(
            HttpStatusCode.OK,
            """{"id_token":"id-2","refresh_token":"refresh-2","expires_in":"1800","user_id":"u"}""");

        var tokens = await CreateSut(handler).RefreshAsync("refresh-1", TestContext.Current.CancellationToken);

        handler.Request!.RequestUri!.ToString().ShouldBe("https://securetoken.googleapis.com/v1/token?key=fake-api-key");
        handler.Request.Content!.Headers.ContentType!.MediaType.ShouldBe("application/x-www-form-urlencoded");
        handler.RequestBody.ShouldBe("grant_type=refresh_token&refresh_token=refresh-1");
        tokens.IdToken.ShouldBe("id-2");
        tokens.RefreshToken.ShouldBe("refresh-2");
        tokens.ExpiresIn.ShouldBe(1800);
    }

    [Fact]
    public async Task SendPasswordResetAsync_Success_SendsSpanishLocaleHeader()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, """{"email":"a@b.com"}""");

        await CreateSut(handler).SendPasswordResetAsync("a@b.com", TestContext.Current.CancellationToken);

        handler.Request!.RequestUri!.AbsolutePath.ShouldBe("/v1/accounts:sendOobCode");
        handler.Request.Headers.GetValues("X-Firebase-Locale").ShouldBe(["es"]);
        using var body = JsonDocument.Parse(handler.RequestBody!);
        body.RootElement.GetProperty("requestType").GetString().ShouldBe("PASSWORD_RESET");
    }

    [Fact]
    public async Task DeleteAccountAsync_Success_PostsIdToken()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "{}");

        await CreateSut(handler).DeleteAccountAsync("id-1", TestContext.Current.CancellationToken);

        handler.Request!.RequestUri!.AbsolutePath.ShouldBe("/v1/accounts:delete");
        using var body = JsonDocument.Parse(handler.RequestBody!);
        body.RootElement.GetProperty("idToken").GetString().ShouldBe("id-1");
    }

    [Fact]
    public async Task SignUpAsync_ErrorWithDetail_UsesCodeBeforeSeparator()
    {
        var handler = new StubHttpMessageHandler(
            HttpStatusCode.BadRequest,
            """{"error":{"code":400,"message":"WEAK_PASSWORD : Password should be at least 6 characters"}}""");

        var ex = await Should.ThrowAsync<FirebaseAuthException>(() => CreateSut(handler).SignUpAsync("a@b.com", "123"));

        ex.Code.ShouldBe("WEAK_PASSWORD");
    }

    [Fact]
    public async Task SignInWithPasswordAsync_ErrorWithoutDetail_UsesWholeMessage()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.BadRequest, """{"error":{"message":"INVALID_LOGIN_CREDENTIALS"}}""");

        var ex = await Should.ThrowAsync<FirebaseAuthException>(() => CreateSut(handler).SignInWithPasswordAsync("a@b.com", "x"));

        ex.Code.ShouldBe("INVALID_LOGIN_CREDENTIALS");
    }

    [Fact]
    public async Task SignInWithPasswordAsync_NonJsonError_ThrowsUnknown()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.BadGateway, "<html>bad gateway</html>", "text/html");

        var ex = await Should.ThrowAsync<FirebaseAuthException>(() => CreateSut(handler).SignInWithPasswordAsync("a@b.com", "x"));

        ex.Code.ShouldBe("UNKNOWN");
    }

    [Fact]
    public async Task SignInWithPasswordAsync_NotConfigured_ThrowsNotConfiguredWithoutRequest()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, SignInBody);

        var ex = await Should.ThrowAsync<FirebaseAuthException>(
            () => CreateSut(handler, FirebaseOptions.Empty).SignInWithPasswordAsync("a@b.com", "x"));

        ex.Code.ShouldBe("NOT_CONFIGURED");
        handler.Request.ShouldBeNull();
    }
}