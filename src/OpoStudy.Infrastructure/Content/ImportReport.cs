namespace OpoStudy.Infrastructure.Content;

/// <summary>Number of rows touched for one kind of entity.</summary>
public sealed record ImportCounts(int Created, int Updated, int Deactivated, int Reactivated);

/// <summary>What an import did (or would do when it was a dry run).</summary>
public sealed record ImportReport(
    ImportCounts Exams,
    ImportCounts Topics,
    ImportCounts Questions,
    ImportCounts PracticalCases,
    IReadOnlyList<string> Warnings,
    bool DryRun);

/// <summary>The import cannot be applied (e.g. a question changes its number of options). Nothing is saved.</summary>
public sealed class ContentImportException : Exception
{
    public ContentImportException()
    {
    }

    public ContentImportException(string message)
        : base(message)
    {
    }

    public ContentImportException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
