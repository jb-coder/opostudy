using System.Net;
using System.Net.Sockets;

using Refit;

namespace OpoStudy.Mobile.Core.Offline;

/// <summary>Tells apart "the server could not be reached" from every other failure.</summary>
public static class ConnectivityErrors
{
    /// <summary>
    /// True when the exception (or an inner one) is a transport failure: an <see cref="HttpRequestException"/> without
    /// status code, an <see cref="IOException"/>, a <see cref="SocketException"/>, a <see cref="WebException"/> that is not an HTTP answer, or the timeout of the HttpClient.
    /// The token refresh of Firebase goes through the same checks: a transport failure is connectivity, while a Firebase
    /// answer with an error code (<c>FirebaseAuthException</c>) is not.
    /// False when the server answered (<see cref="ApiException"/>) or when the caller cancelled the operation.
    /// </summary>
    /// <param name="exception">Exception thrown by the API call.</param>
    /// <param name="callerToken">Token the caller passed to the call: its cancellation is not a connectivity error.</param>
    public static bool IsConnectivityError(Exception exception, CancellationToken callerToken = default)
    {
        ArgumentNullException.ThrowIfNull(exception);

        switch (exception)
        {
            case ApiException:
                return false;
            case OperationCanceledException when callerToken.IsCancellationRequested:
                return false;
            // The HttpClient timeout surfaces as a TaskCanceledException the caller did not ask for.
            case TaskCanceledException or TimeoutException:
                return true;
            case OperationCanceledException:
                return false;
            case HttpRequestException http when http.StatusCode is not null:
                return false;
            // The Android HTTP stack can raise a WebException (an InvalidOperationException) around the Java network error.
            case WebException web:
                return web.Status is not (WebExceptionStatus.ProtocolError or WebExceptionStatus.RequestCanceled);
            case HttpRequestException or SocketException or IOException:
                return true;
            case AggregateException aggregate:
                return aggregate.InnerExceptions.Any(inner => IsConnectivityError(inner, callerToken));
        }

        return exception.InnerException is not null && IsConnectivityError(exception.InnerException, callerToken);
    }
}