namespace OpoStudy.Mobile.Core.Auth;

public enum GoogleSignInFailure
{
    NoAccount,
    Failed,
}

/// <summary>Google sign-in could not be completed (cancelling is not an error: the provider returns null).</summary>
public sealed class GoogleSignInException : Exception
{
    public GoogleSignInException(GoogleSignInFailure failure, Exception? innerException = null)
        : base($"Google sign-in failed: {failure}", innerException)
    {
        Failure = failure;
    }

    public GoogleSignInFailure Failure { get; }
}