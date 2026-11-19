using System.Security.Claims;

namespace OpoStudy.Api.Authentication;

internal static class ClaimsPrincipalExtensions
{
    /// <summary>Claim that holds the Firebase uid in an ID token.</summary>
    public const string SubjectClaimType = "sub";

    /// <summary>Returns the Firebase uid (the <c>sub</c> claim) of the authenticated user.</summary>
    public static string GetFirebaseUid(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        var uid = principal.FindFirst(SubjectClaimType)?.Value;
        return string.IsNullOrWhiteSpace(uid)
            ? throw new InvalidOperationException("The authenticated principal has no 'sub' claim.")
            : uid;
    }
}