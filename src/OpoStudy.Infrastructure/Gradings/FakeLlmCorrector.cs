using OpoStudy.Domain.Gradings;

namespace OpoStudy.Infrastructure.Gradings;

/// <summary>
/// Deterministic corrector without network or cost: the score depends only on how long each answer is
/// (300 characters without whitespace earn the maximum). It lets the whole queue be exercised until a real provider exists.
/// </summary>
public sealed class FakeLlmCorrector : ILlmCorrector
{
    public const string Name = "fake";

    private const decimal FullScoreCharacters = 300m;
    private const string Prefix = "Corrección de prueba (sin LLM)";

    public Task<GradingOutcome> GradeAsync(GradingRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var parts = new List<PartOutcome>(request.Parts.Count);
        foreach (var part in request.Parts)
        {
            var characters = part.Answer.Count(c => !char.IsWhiteSpace(c));
            var ratio = Math.Min(1m, characters / FullScoreCharacters);
            var awarded = part.Criteria.Select(c => Math.Floor(c.Points * ratio * 2m) / 2m).ToList();
            parts.Add(new PartOutcome(awarded, $"{Prefix}: {characters} caracteres evaluados."));
        }

        var outcome = new GradingOutcome(
            parts,
            $"{Prefix}: la nota depende solo de la longitud de las respuestas.",
            Name,
            null,
            null);
        return Task.FromResult(outcome);
    }
}