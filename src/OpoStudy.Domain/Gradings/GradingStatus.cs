namespace OpoStudy.Domain.Gradings;

/// <summary>Where a <see cref="Grading"/> is in the correction queue. Stored as text.</summary>
public enum GradingStatus
{
    /// <summary>Waiting for the worker (also after a transient failure, until <see cref="Grading.NextAttemptAt"/>).</summary>
    Pending,

    /// <summary>Claimed by a worker.</summary>
    Processing,

    /// <summary>Corrected: scores and feedback are available.</summary>
    Completed,

    /// <summary>Given up: see <see cref="Grading.FailureCode"/>.</summary>
    Failed,
}
