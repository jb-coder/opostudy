namespace OpoStudy.Mobile.Core.Auth;

/// <summary>Why the session was closed: the Firebase error code that rejected the renewal.</summary>
public sealed class SessionExpiredEventArgs(string code) : EventArgs
{
    public string Code { get; } = code;
}