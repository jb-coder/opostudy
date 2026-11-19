namespace OpoStudy.Mobile.Core.Auth;

/// <summary>Error returned by Firebase Auth. <see cref="Code"/> is the REST error code (e.g. <c>WEAK_PASSWORD</c>).</summary>
public sealed class FirebaseAuthException : Exception
{
    public FirebaseAuthException(string code)
        : base($"Firebase Auth error: {code}")
    {
        Code = code;
    }

    public string Code { get; }
}