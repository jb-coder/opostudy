using OpoStudy.Domain.Common;

namespace OpoStudy.Domain.Exams;

/// <summary>A civil-service exam (oposición), e.g. "Programador Informático C1 — Diputación de Málaga".</summary>
public sealed class Exam
{
    private Exam()
    {
        Slug = Name = Organization = Group = string.Empty;
    }

    public Guid Id { get; private set; }
    public string Slug { get; private set; }
    public string Name { get; private set; }
    public string Organization { get; private set; }

    /// <summary>Classification group of the position (e.g. "C1").</summary>
    public string Group { get; private set; }

    public bool IsActive { get; private set; }

    public static Exam Create(string slug, string name, string organization, string group)
    {
        var exam = new Exam
        {
            Id = Guid.CreateVersion7(),
            Slug = Guard.Slug(slug, "exam.invalid_slug"),
            IsActive = true,
        };
        exam.Update(name, organization, group);
        return exam;
    }

    public void Update(string name, string organization, string group)
    {
        Name = Guard.RequiredText(name, Limits.Name, "exam.invalid_name");
        Organization = Guard.RequiredText(organization, Limits.Name, "exam.invalid_organization");
        Group = Guard.RequiredText(group, Limits.Group, "exam.invalid_group");
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}