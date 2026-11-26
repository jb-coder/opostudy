namespace OpoStudy.Domain.Quizzes;

/// <summary>Scoring of a mock exam: wrong answers subtract a fraction of a correct one and blanks neither add nor subtract.</summary>
public static class MockExamScoring
{
    public static decimal Calculate(int correct, int incorrect, int questionCount, decimal maxScore, decimal penaltyRatio)
    {
        if (questionCount <= 0)
        {
            return 0;
        }

        var net = Math.Max(0m, correct - (incorrect * penaltyRatio));
        return Math.Round(net * maxScore / questionCount, 2, MidpointRounding.AwayFromZero);
    }
}
