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
    public void UpdateExam_WithInvalidGroup_LeavesExamUntouched()
    {
        var exam = Exam.Create("malaga-programador-c1", "Programador", "Diputación de Málaga", "C1");

        var ex = Should.Throw<DomainRuleException>(() => exam.Update("New name", "New organization", new string('x', Limits.Group + 1)));

        ex.Code.ShouldBe("exam.invalid_group");
        exam.Name.ShouldBe("Programador");
        exam.Organization.ShouldBe("Diputación de Málaga");
        exam.Group.ShouldBe("C1");
    }

    [Fact]
    public void UpdateTopic_WithInvalidTitle_LeavesTopicUntouched()
    {
        var topic = Topic.Create(Guid.CreateVersion7(), "topic-1", 1, "Constitución", "Bloque I");

        var ex = Should.Throw<DomainRuleException>(() => topic.Update(5, " ", "Bloque II"));

        ex.Code.ShouldBe("topic.invalid_title");
        topic.Number.ShouldBe(1);
        topic.Title.ShouldBe("Constitución");
        topic.Section.ShouldBe("Bloque I");
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