using OpoStudy.Domain.Common;

namespace OpoStudy.Domain.Exams;

/// <summary>A topic (tema) of an exam syllabus.</summary>
public sealed class Topic
{
    private Topic()
    {
        Slug = Title = string.Empty;
    }

    public Guid Id { get; private set; }
    public Guid ExamId { get; private set; }
    public string Slug { get; private set; }
    public int Number { get; private set; }
    public string Title { get; private set; }

    /// <summary>Optional syllabus block (e.g. "Bloque I: Materias comunes").</summary>
    public string? Section { get; private set; }

    public static Topic Create(Guid examId, string slug, int number, string title, string? section)
    {
        var topic = new Topic
        {
            Id = Guid.CreateVersion7(),
            ExamId = examId,
            Slug = Guard.Slug(slug, "topic.invalid_slug"),
        };
        topic.Update(number, title, section);
        return topic;
    }

    public void Update(int number, string title, string? section)
    {
        if (number <= 0)
        {
            throw new DomainRuleException("topic.invalid_number", "El número de tema debe ser mayor que 0.");
        }

        Number = number;
        Title = Guard.RequiredText(title, Limits.Name, "topic.invalid_title");
        Section = Guard.OptionalText(section, Limits.Name, "topic.invalid_section");
    }
}