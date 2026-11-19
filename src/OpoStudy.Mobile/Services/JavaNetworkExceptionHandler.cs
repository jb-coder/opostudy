#if ANDROID
namespace OpoStudy.Mobile.Services;

/// <summary>
/// The Android HTTP stack surfaces Java exceptions (<c>Java.Net.ConnectException</c>, <c>Java.IO.IOException</c>...)
/// that are not <see cref="HttpRequestException"/>. This handler rethrows them as standard .NET exceptions
/// (keeping the original as inner exception) so callers can classify them without platform types.
/// </summary>
internal sealed class JavaNetworkExceptionHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return await base.SendAsync(request, cancellationToken);
        }
        catch (Java.Net.SocketTimeoutException ex)
        {
            throw new TimeoutException(ex.Message, ex);
        }
        catch (Java.IO.IOException ex)
        {
            throw new HttpRequestException(ex.Message, ex);
        }
    }
}
#endif
