using OpoStudy.Mobile.Core.Auth;

using Shouldly;

namespace OpoStudy.Mobile.Core.Tests.Auth;

public sealed class GoogleSignInErrorClassifierTests
{
    [Fact]
    public void Classify_UserCancellation_ReturnsCancelled() =>
        GoogleSignInErrorClassifier.Classify("GetCredentialCancellationException", "activity is cancelled by the user.")
            .ShouldBe(GoogleSignInOutcome.Cancelled);

    [Fact]
    public void Classify_CancellationWithNullMessage_ReturnsCancelled() =>
        GoogleSignInErrorClassifier.Classify("GetCredentialCancellationException", null)
            .ShouldBe(GoogleSignInOutcome.Cancelled);

    [Fact]
    public void Classify_CancellationWithStatusCode_ReturnsFailed() =>
        GoogleSignInErrorClassifier.Classify("GetCredentialCancellationException", "[16] Account reauth failed.")
            .ShouldBe(GoogleSignInOutcome.Failed);

    [Fact]
    public void Classify_NoCredential_ReturnsNoAccount() =>
        GoogleSignInErrorClassifier.Classify("NoCredentialException", "No credentials available")
            .ShouldBe(GoogleSignInOutcome.NoAccount);

    [Theory]
    [InlineData("GetCredentialUnknownException")]
    [InlineData("GetCredentialCustomException")]
    [InlineData("GetCredentialProviderConfigurationException")]
    [InlineData("IllegalStateException")]
    public void Classify_OtherType_ReturnsFailed(string typeName) =>
        GoogleSignInErrorClassifier.Classify(typeName, "anything").ShouldBe(GoogleSignInOutcome.Failed);

    [Theory]
    [InlineData("[16] Account reauth failed.", 16)]
    [InlineData("Error [10]: developer error", 10)]
    [InlineData("activity is cancelled by the user.", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void ExtractStatusCode_Message_ReturnsCodeOrNull(string? message, int? expected) =>
        GoogleSignInErrorClassifier.ExtractStatusCode(message).ShouldBe(expected);
}