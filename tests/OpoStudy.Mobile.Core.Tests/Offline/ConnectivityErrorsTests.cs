using System.Net;
using System.Net.Sockets;

using OpoStudy.Mobile.Core.Auth;
using OpoStudy.Mobile.Core.Offline;

using Shouldly;

namespace OpoStudy.Mobile.Core.Tests.Offline;

public sealed class ConnectivityErrorsTests
{
    [Fact]
    public void IsConnectivityError_HttpRequestExceptionWithoutStatus_ReturnsTrue() =>
        Check(new HttpRequestException("no route")).ShouldBeTrue();

    [Fact]
    public void IsConnectivityError_HttpRequestExceptionWithStatus_ReturnsFalse() =>
        Check(new HttpRequestException("bad", null, HttpStatusCode.BadGateway)).ShouldBeFalse();

    [Fact]
    public void IsConnectivityError_HttpClientTimeout_ReturnsTrue() =>
        Check(new TaskCanceledException("timeout", new TimeoutException())).ShouldBeTrue();

    [Fact]
    public void IsConnectivityError_CancelledByCaller_ReturnsFalse()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        CheckWithToken(new TaskCanceledException("cancelled", null, cts.Token), cts.Token).ShouldBeFalse();
        CheckWithToken(new OperationCanceledException(cts.Token), cts.Token).ShouldBeFalse();
    }

    [Fact]
    public void IsConnectivityError_CancelledButCallerTokenNotCancelled_IsATimeout() =>
        CheckWithToken(new TaskCanceledException("timeout"), CancellationToken.None).ShouldBeTrue();

    [Fact]
    public void IsConnectivityError_PlainOperationCanceled_ReturnsFalse() =>
        Check(new OperationCanceledException()).ShouldBeFalse();

    [Fact]
    public void IsConnectivityError_InnerSocketException_ReturnsTrue() =>
        Check(new InvalidOperationException("wrapper", new SocketException())).ShouldBeTrue();

    [Fact]
    public void IsConnectivityError_InnerIOException_ReturnsTrue() =>
        Check(new InvalidDataException("wrapper", new IOException("reset"))).ShouldBeTrue();

    [Fact]
    public void IsConnectivityError_AggregateWithSocketException_ReturnsTrue() =>
        Check(new AggregateException(new SocketException())).ShouldBeTrue();

    [Fact]
    public async Task IsConnectivityError_ApiException_ReturnsFalse()
    {
        var exception = await OfflineTestData.ApiExceptionAsync(HttpStatusCode.ServiceUnavailable);

        Check(exception).ShouldBeFalse();
    }

    [Fact]
    public void IsConnectivityError_UnrelatedException_ReturnsFalse() =>
        Check(new InvalidOperationException("bug")).ShouldBeFalse();

    [Fact]
    public void IsConnectivityError_WebExceptionConnectFailure_ReturnsTrue() =>
        Check(new WebException("connect failed", new InvalidOperationException("java.net.ConnectException"), WebExceptionStatus.ConnectFailure, null)).ShouldBeTrue();

    [Fact]
    public void IsConnectivityError_WebExceptionNameResolution_ReturnsTrue() =>
        Check(new WebException("unknown host", WebExceptionStatus.NameResolutionFailure)).ShouldBeTrue();

    [Fact]
    public void IsConnectivityError_WebExceptionWithoutStatusDetails_ReturnsTrue() =>
        Check(new WebException("no route")).ShouldBeTrue();

    [Fact]
    public void IsConnectivityError_WebExceptionProtocolError_ReturnsFalse() =>
        Check(new WebException("500", WebExceptionStatus.ProtocolError)).ShouldBeFalse();

    [Fact]
    public void IsConnectivityError_WebExceptionWrappedInHttpRequestException_ReturnsTrue() =>
        Check(new HttpRequestException("wrapper", new WebException("connect", WebExceptionStatus.ConnectFailure))).ShouldBeTrue();

    [Fact]
    public void IsConnectivityError_FirebaseErrorCode_ReturnsFalse()
    {
        Check(new FirebaseAuthException("INVALID_REFRESH_TOKEN")).ShouldBeFalse();
        Check(new FirebaseAuthException("UNKNOWN")).ShouldBeFalse();
    }

    [Fact]
    public void IsConnectivityError_TokenRefreshTimeout_ReturnsTrue() =>
        Check(new TaskCanceledException("timeout", new TimeoutException())).ShouldBeTrue();

    [Fact]
    public void IsConnectivityError_TokenRefreshJavaFailureWrappedByTheHandler_ReturnsTrue() =>
        Check(new HttpRequestException("Unable to resolve host", new InvalidOperationException("Java.Net.UnknownHostException"))).ShouldBeTrue();

    private static bool Check(Exception exception) =>
        ConnectivityErrors.IsConnectivityError(exception, TestContext.Current.CancellationToken);

    private static bool CheckWithToken(Exception exception, CancellationToken callerToken) =>
        ConnectivityErrors.IsConnectivityError(exception, callerToken);
}
