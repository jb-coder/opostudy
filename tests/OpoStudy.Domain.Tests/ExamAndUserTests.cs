using OpoStudy.Domain.Common;
using OpoStudy.Domain.Exams;
using OpoStudy.Domain.Users;

using Shouldly;

namespace OpoStudy.Domain.Tests;

public sealed class ExamAndUserTests
{
    [Theory]
    [InlineData("Malaga C1")]
    [InlineData("malaga--c1")]
    [InlineData("-malaga")]
    [InlineData("")]
    public void CreateExam_WithInvalidSlug_ThrowsDomainRule(string slug)
    {
        var ex = Should.Throw<DomainRuleException>(() => Exam.Create(slug, "Programador", "Diputación de Málaga", "C1"));

        ex.Code.ShouldBe("exam.invalid_slug");
    }

    [Fact]
    public void CreateExam_WithValidData_IsActiveAndTrimsTexts()
    {
        var exam = Exam.Create("malaga-programador-c1", "  Programador  ", "Diputación de Málaga", "C1");

        exam.IsActive.ShouldBeTrue();
        exam.Name.ShouldBe("Programador");
    }

    [Fact]
    public void CreateTopic_WithNonPositiveNumber_ThrowsDomainRule()
    {
        var ex = Should.Throw<DomainRuleException>(() => Topic.Create(Guid.CreateVersion7(), "topic-1", 0, "Constitución", null));

        ex.Code.ShouldBe("topic.invalid_number");
    }

    [Fact]
    public void CreateUser_WithoutFirebaseUid_ThrowsDomainRule()
    {
        var ex = Should.Throw<DomainRuleException>(() => User.Create(" ", DateTimeOffset.UtcNow));

        ex.Code.ShouldBe("user.invalid_firebase_uid");
    }
}