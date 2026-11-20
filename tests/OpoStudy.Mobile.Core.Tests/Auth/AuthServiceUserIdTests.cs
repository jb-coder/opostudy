using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;

using OpoStudy.Mobile.Core.Auth;

using Shouldly;

namespace OpoStudy.Mobile.Core.Tests.Auth;

public sealed class AuthServiceUserIdTests
{
    private readonly ISecureStore _store = Substitute.For<ISecureStore>();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero));

    private static string Base64Url(string text) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(text)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string Jwt(string payloadJson) => $"{Base64Url("""{"alg":"RS256"}""")}.{Base64Url(payloadJson)}.signature";

    private async Task<AuthService> RestoredAsync(string idToken)
    {
        var session = new AuthSession(idToken, "refresh", _time.GetUtcNow().AddHours(1), "a@b.com", "password");
        _store.GetAsync("auth.session").Returns(JsonSerializer.Serialize(session, JsonSerializerOptions.Web));
        var sut = new AuthService(
            Substitute.For<IFirebaseAuthClient>(),
            _store,
            Substitute.For<IGoogleSignInProvider>(),
            _time,
            NullLogger<AuthService>.Instance);
        await sut.RestoreSessionAsync(TestContext.Current.CancellationToken);
        return sut;
    }

    [Fact]
    public async Task UserId_TokenWithUserIdClaim_ReturnsIt()
    {
        using var sut = await RestoredAsync(Jwt("""{"user_id":"firebase-uid-1","sub":"other"}"""));

        sut.UserId.ShouldBe("firebase-uid-1");
    }

    [Fact]
    public async Task UserId_TokenWithOnlySub_FallsBackToSub()
    {
        using var sut = await RestoredAsync(Jwt("""{"sub":"firebase-uid-2"}"""));

        sut.UserId.ShouldBe("firebase-uid-2");
    }

    [Fact]
    public async Task UserId_PayloadNeedingBase64UrlPadding_IsDecoded()
    {
        // The payload length is not a multiple of 4, so the unpadded base64url form needs padding to be decoded.
        using var sut = await RestoredAsync(Jwt("""{"user_id":"uid-with-ñ-and-ü?>>"}"""));

        sut.UserId.ShouldBe("uid-with-ñ-and-ü?>>");
    }

    [Theory]
    [InlineData("not-a-jwt")]
    [InlineData("a.%%%.c")]
    [InlineData("a.e30.c")]
    public async Task UserId_MalformedToken_ReturnsNull(string token)
    {
        using var sut = await RestoredAsync(token);

        sut.UserId.ShouldBeNull();
    }

    [Fact]
    public void UserId_WithoutSession_ReturnsNull()
    {
        using var sut = new AuthService(
            Substitute.For<IFirebaseAuthClient>(),
            _store,
            Substitute.For<IGoogleSignInProvider>(),
            _time,
            NullLogger<AuthService>.Instance);

        sut.UserId.ShouldBeNull();
    }
}