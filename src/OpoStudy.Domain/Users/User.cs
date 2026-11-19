using OpoStudy.Domain.Common;

namespace OpoStudy.Domain.Users;

public sealed class User
{
    private User()
    {
        FirebaseUid = string.Empty;
    }

    public Guid Id { get; private set; }

    /// <summary>User id in Firebase Auth. It is personal data: never write it to logs.</summary>
    public string FirebaseUid { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? ActiveExamId { get; private set; }

    public static User Create(string firebaseUid, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(),
        FirebaseUid = Guard.RequiredText(firebaseUid, Limits.FirebaseUid, "user.invalid_firebase_uid"),
        CreatedAt = now.ToUniversalTime(),
    };

    public void ChooseExam(Guid examId) => ActiveExamId = examId;
}