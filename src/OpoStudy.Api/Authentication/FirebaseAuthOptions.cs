using System.ComponentModel.DataAnnotations;

namespace OpoStudy.Api.Authentication;

/// <summary>Firebase Auth settings (section <c>Firebase</c>).</summary>
public sealed class FirebaseAuthOptions
{
    public const string SectionName = "Firebase";

    /// <summary>Firebase project id. It is the audience of the ID tokens and part of the issuer.</summary>
    [Required(AllowEmptyStrings = false)]
    public string ProjectId { get; set; } = string.Empty;
}