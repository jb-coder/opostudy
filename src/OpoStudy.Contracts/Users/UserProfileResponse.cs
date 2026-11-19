namespace OpoStudy.Contracts.Users;

/// <summary>Profile of the authenticated user. It never includes the Firebase uid.</summary>
/// <param name="Id">Internal user id.</param>
/// <param name="CreatedAt">Moment the account was created (UTC).</param>
/// <param name="ActiveExamId">Exam the user is currently preparing, if any.</param>
public sealed record UserProfileResponse(Guid Id, DateTimeOffset CreatedAt, Guid? ActiveExamId);