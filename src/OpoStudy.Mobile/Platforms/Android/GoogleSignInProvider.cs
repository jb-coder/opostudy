using Android.OS;
using Android.Runtime;

using AndroidX.Credentials;
using AndroidX.Credentials.Exceptions;

using Java.Util.Concurrent;

using Microsoft.Extensions.Logging;

using OpoStudy.Mobile.Core.Auth;

using Xamarin.GoogleAndroid.Libraries.Identity.GoogleId;

namespace OpoStudy.Mobile.Platforms.Android;

/// <summary>Google sign-in through the Android Credential Manager ("Sign in with Google" option).</summary>
internal sealed partial class GoogleSignInProvider(FirebaseOptions options, ILogger<GoogleSignInProvider> logger) : IGoogleSignInProvider
{
    private static readonly TimeSpan SignOutTimeout = TimeSpan.FromSeconds(5);

    private static readonly IExecutor Executor = Executors.NewSingleThreadExecutor()!;

    public async Task<string?> GetIdTokenAsync(CancellationToken cancellationToken = default)
    {
        var activity = Platform.CurrentActivity
            ?? throw new GoogleSignInException(GoogleSignInFailure.Failed);

        if (string.IsNullOrWhiteSpace(options.GoogleWebClientId))
        {
            throw new GoogleSignInException(GoogleSignInFailure.Failed);
        }

        var googleOption = new GetSignInWithGoogleOption.Builder(options.GoogleWebClientId).Build();
        var request = new GetCredentialRequest.Builder().AddCredentialOption(googleOption).Build();
        var manager = CredentialManager.Create(activity);

        var completion = new TaskCompletionSource<GetCredentialResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var signal = new CancellationSignal();
        await using var registration = cancellationToken.Register(signal.Cancel);

        manager.GetCredentialAsync(activity, request, signal, Executor, new Callback(completion));

        try
        {
            var response = await completion.Task;
            var credential = response.Credential;
            if (credential is CustomCredential custom
                && custom.Type == GoogleIdTokenCredential.TypeGoogleIdTokenCredential)
            {
                return GoogleIdTokenCredential.CreateFrom(custom.Data).IdToken;
            }

            throw new GoogleSignInException(GoogleSignInFailure.Failed);
        }
        catch (GoogleSignInException)
        {
            throw;
        }
        catch (Java.Lang.Throwable ex)
        {
            // Includes GoogleIdTokenParsingException (always a failure) and every GetCredentialException subtype.
            var typeName = ex.Class?.SimpleName ?? "Unknown";
            var outcome = GoogleSignInErrorClassifier.Classify(typeName, ex.Message);
            if (outcome == GoogleSignInOutcome.Cancelled)
            {
                return null;
            }

            // Only the exception type and the Google status code are logged: the message may contain account data.
            LogSignInFailed(typeName, GoogleSignInErrorClassifier.ExtractStatusCode(ex.Message));
            throw new GoogleSignInException(
                outcome == GoogleSignInOutcome.NoAccount ? GoogleSignInFailure.NoAccount : GoogleSignInFailure.Failed,
                ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Google sign-in failed: {ExceptionType}, status code {StatusCode}")]
    private partial void LogSignInFailed(string exceptionType, int? statusCode);

    public async Task SignOutAsync()
    {
        var activity = Platform.CurrentActivity;
        if (activity is null)
        {
            return;
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var signal = new CancellationSignal();
        CredentialManager.Create(activity).ClearCredentialStateAsync(
            new ClearCredentialStateRequest(),
            signal,
            Executor,
            new ClearCallback(completion));

        try
        {
            await completion.Task.WaitAsync(SignOutTimeout);
        }
        catch (System.TimeoutException)
        {
            // Best effort: the local session is already cleared and AuthService ignores sign-out failures.
            signal.Cancel();
        }
    }

    private sealed class Callback(TaskCompletionSource<GetCredentialResponse> completion) : Java.Lang.Object, ICredentialManagerCallback
    {
        public void OnResult(Java.Lang.Object? result)
        {
            if (result is GetCredentialResponse response)
            {
                completion.TrySetResult(response);
            }
            else
            {
                completion.TrySetException(new GoogleSignInException(GoogleSignInFailure.Failed));
            }
        }

        public void OnError(Java.Lang.Object error)
        {
            // The Credential Manager reports the failure as a Java exception (GetCredentialException subclasses).
            if (error.JavaCast<Java.Lang.Throwable>() is { } throwable)
            {
                completion.TrySetException(throwable);
            }
            else
            {
                completion.TrySetException(new GoogleSignInException(GoogleSignInFailure.Failed));
            }
        }
    }

    private sealed class ClearCallback(TaskCompletionSource completion) : Java.Lang.Object, ICredentialManagerCallback
    {
        public void OnResult(Java.Lang.Object? result) => completion.TrySetResult();

        public void OnError(Java.Lang.Object error) => completion.TrySetResult();
    }
}