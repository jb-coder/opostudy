using System.Net;
using System.Net.Http.Headers;

namespace OpoStudy.Mobile.Core.Auth;

/// <summary>Adds the Firebase ID token as a Bearer token and invalidates it when the API answers 401.</summary>
public sealed class AuthHeaderHandler(IAuthService auth) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await auth.GetIdTokenAsync(cancellationToken);
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var response = await base.SendAsync(request, cancellationToken);
        if (token is not null && response.StatusCode == HttpStatusCode.Unauthorized)
        {
            auth.InvalidateIdToken(token);
        }

        return response;
    }
}