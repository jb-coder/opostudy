using System.Text.Json;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using OpoStudy.Mobile.Core.Auth;

using Shouldly;

namespace OpoStudy.Mobile.Core.Tests.Auth;

public sealed class AuthServiceTests
{
    private const string Key = "auth.session";

    private readonly IFirebaseAuthClient _firebase = Substitute.For<IFirebaseAuthClient>();
    private readonly ISecureStore _store = Substitute.For<ISecureStore>();
    private readonly IGoogleSignInProvider _google = Substitute.For<IGoogleSignInProvider>();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task SignInWithEmailAsync_Success_PersistsSession()
    {
        _firebase.SignInWithPasswordAsync("a@b.com", "secret1", Arg.Any<CancellationToken>())
            .Returns(new FirebaseTokens("id-1", "refresh-1", 3600, "a@b.com"));
        var sut = CreateSut();
        var expected = _time.GetUtcNow().AddSeconds(3600);

        await sut.SignInWithEmailAsync("a@b.com", "secret1", TestContext.Current.CancellationToken);

        sut.IsSignedIn.ShouldBeTrue();
        sut.Email.ShouldBe("a@b.com");
        sut.Provider.ShouldBe("password");
        await _store.Received(1).SetAsync(
            Key,
            Arg.Is<string>(json => JsonSerializer.Deserialize<AuthSession>(json, JsonSerializerOptions.Web)!.ExpiresAt == expected));
    }

    [Fact]
    public async Task RestoreSessionAsync_StoredSession_IsSignedIn()
    {
        var sut = await CreateRestoredAsync(TimeSpan.FromHours(1));

        sut.IsSignedIn.ShouldBeTrue();
        sut.Email.ShouldBe("a@b.com");
        await _firebase.DidNotReceiveWithAnyArgs().RefreshAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task RestoreSessionAsync_NothingStored_IsSignedOut()
    {
        _store.GetAsync(Key).Returns((string?)null);
        var sut = CreateSut();

        await sut.RestoreSessionAsync(TestContext.Current.CancellationToken);

        sut.IsSignedIn.ShouldBeFalse();
    }

    [Fact]
    public async Task RestoreSessionAsync_CorruptSession_ClearsAndSignedOut()
    {
        _store.GetAsync(Key).Returns("{not json");
        var sut = CreateSut();

        await sut.RestoreSessionAsync(TestContext.Current.CancellationToken);

        sut.IsSignedIn.ShouldBeFalse();
        _store.Received(1).Remove(Key);
    }

    [Fact]
    public async Task GetIdTokenAsync_NoSession_ReturnsNull()
    {
        var sut = CreateSut();

        (await sut.GetIdTokenAsync(TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    [Fact]
    public async Task GetIdTokenAsync_TokenFresh_DoesNotRefresh()
    {
        var sut = await CreateRestoredAsync(TimeSpan.FromMinutes(30));

        (await sut.GetIdTokenAsync(TestContext.Current.CancellationToken)).ShouldBe("id-old");

        await _firebase.DidNotReceiveWithAnyArgs().RefreshAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetIdTokenAsync_TokenNearExpiry_RefreshesAndPersists()
    {
        var sut = await CreateRestoredAsync(TimeSpan.FromMinutes(4));
        _firebase.RefreshAsync("refresh-old", Arg.Any<CancellationToken>())
            .Returns(new FirebaseTokens("id-new", "refresh-new", 3600, null));

        (await sut.GetIdTokenAsync(TestContext.Current.CancellationToken)).ShouldBe("id-new");

        await _store.Received(1).SetAsync(Key, Arg.Is<string>(json => json.Contains("refresh-new", StringComparison.Ordinal)));
        sut.Email.ShouldBe("a@b.com");
    }

    [Fact]
    public async Task GetIdTokenAsync_ConcurrentCalls_RefreshesOnce()
    {
        var sut = await CreateRestoredAsync(TimeSpan.FromMinutes(1));
        var gate = new TaskCompletionSource<FirebaseTokens>();
        _firebase.RefreshAsync("refresh-old", Arg.Any<CancellationToken>()).Returns(gate.Task);

        var calls = Enumerable.Range(0, 5).Select(_ => sut.GetIdTokenAsync()).ToArray();
        gate.SetResult(new FirebaseTokens("id-new", "refresh-new", 3600, null));
        var tokens = await Task.WhenAll(calls);

        tokens.ShouldAllBe(t => t == "id-new");
        await _firebase.Received(1).RefreshAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("INVALID_REFRESH_TOKEN")]
    [InlineData("TOKEN_EXPIRED")]
    [InlineData("USER_DISABLED")]
    [InlineData("USER_NOT_FOUND")]
    [InlineData("INVALID_ID_TOKEN")]
    public async Task GetIdTokenAsync_RefreshRejected_ClearsSessionAndRaisesExpired(string code)
    {
        var sut = await CreateRestoredAsync(TimeSpan.FromMinutes(1));
        _firebase.RefreshAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).ThrowsAsync(new FirebaseAuthException(code));
        var raised = 0;
        sut.SessionExpired += (_, _) => raised++;

        (await sut.GetIdTokenAsync(TestContext.Current.CancellationToken)).ShouldBeNull();

        sut.IsSignedIn.ShouldBeFalse();
        raised.ShouldBe(1);
        _store.Received(1).Remove(Key);
    }

    [Fact]
    public async Task GetIdTokenAsync_NetworkError_KeepsSession()
    {
        var sut = await CreateRestoredAsync(TimeSpan.FromMinutes(1));
        _firebase.RefreshAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException());

        await Should.ThrowAsync<HttpRequestException>(() => sut.GetIdTokenAsync());

        sut.IsSignedIn.ShouldBeTrue();
        _store.DidNotReceive().Remove(Key);
    }

    [Fact]
    public async Task InvalidateIdToken_Called_ForcesRefreshOnNextCall()
    {
        var sut = await CreateRestoredAsync(TimeSpan.FromHours(1));
        _firebase.RefreshAsync("refresh-old", Arg.Any<CancellationToken>())
            .Returns(new FirebaseTokens("id-new", "refresh-new", 3600, null));

        sut.InvalidateIdToken();

        (await sut.GetIdTokenAsync(TestContext.Current.CancellationToken)).ShouldBe("id-new");
    }

    [Fact]
    public async Task SignInWithGoogleAsync_Cancelled_ReturnsFalse()
    {
        _google.GetIdTokenAsync(Arg.Any<CancellationToken>()).Returns((string?)null);
        var sut = CreateSut();

        (await sut.SignInWithGoogleAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();

        sut.IsSignedIn.ShouldBeFalse();
        await _firebase.DidNotReceiveWithAnyArgs().SignInWithGoogleAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SignInWithGoogleAsync_Success_PersistsGoogleSession()
    {
        _google.GetIdTokenAsync(Arg.Any<CancellationToken>()).Returns("google-token");
        _firebase.SignInWithGoogleAsync("google-token", Arg.Any<CancellationToken>())
            .Returns(new FirebaseTokens("id-1", "refresh-1", 3600, "g@b.com"));
        var sut = CreateSut();

        (await sut.SignInWithGoogleAsync(TestContext.Current.CancellationToken)).ShouldBeTrue();

        sut.Provider.ShouldBe("google.com");
        sut.Email.ShouldBe("g@b.com");
    }

    [Fact]
    public async Task SignOutAsync_ClearsSessionAndGoogleState()
    {
        var sut = await CreateRestoredAsync(TimeSpan.FromHours(1));

        await sut.SignOutAsync();

        sut.IsSignedIn.ShouldBeFalse();
        _store.Received(1).Remove(Key);
        await _google.Received(1).SignOutAsync();
    }

    [Fact]
    public async Task SignOutAsync_GoogleFails_StillSignsOut()
    {
        var sut = await CreateRestoredAsync(TimeSpan.FromHours(1));
        _google.SignOutAsync().ThrowsAsync(new InvalidOperationException());

        await sut.SignOutAsync();

        sut.IsSignedIn.ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteFirebaseAccountAsync_Success_DeletesAndSignsOut()
    {
        var sut = await CreateRestoredAsync(TimeSpan.FromHours(1));

        await sut.DeleteFirebaseAccountAsync(TestContext.Current.CancellationToken);

        await _firebase.Received(1).DeleteAccountAsync("id-old", Arg.Any<CancellationToken>());
        sut.IsSignedIn.ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteFirebaseAccountAsync_CredentialTooOld_SignsOutAndThrows()
    {
        var sut = await CreateRestoredAsync(TimeSpan.FromHours(1));
        _firebase.DeleteAccountAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new FirebaseAuthException("CREDENTIAL_TOO_OLD_LOGIN_AGAIN"));

        var ex = await Should.ThrowAsync<FirebaseAuthException>(() => sut.DeleteFirebaseAccountAsync());

        ex.Code.ShouldBe("CREDENTIAL_TOO_OLD_LOGIN_AGAIN");
        sut.IsSignedIn.ShouldBeFalse();
    }

    private static string Serialize(AuthSession session) => JsonSerializer.Serialize(session, JsonSerializerOptions.Web);

    private AuthService CreateSut() => new(_firebase, _store, _google, _time, NullLogger<AuthService>.Instance);

    private AuthSession StoredSession(TimeSpan validFor) =>
        new("id-old", "refresh-old", _time.GetUtcNow().Add(validFor), "a@b.com", "password");

    private async Task<AuthService> CreateRestoredAsync(TimeSpan validFor)
    {
        _store.GetAsync(Key).Returns(Serialize(StoredSession(validFor)));
        var sut = CreateSut();
        await sut.RestoreSessionAsync();
        return sut;
    }
}