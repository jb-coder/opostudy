namespace OpoStudy.Domain.Comun;

/// <summary>
/// Se lanza cuando una operación incumple una regla del dominio.
/// <see cref="Codigo"/> es estable (p. ej. <c>pregunta.sin_opcion_correcta</c>) para poder traducirlo a ProblemDetails.
/// </summary>
public sealed class ReglaDominioException : Exception
{
    public ReglaDominioException()
        : this("dominio.regla_incumplida", "Se ha incumplido una regla del dominio.")
    {
    }

    public ReglaDominioException(string message)
        : this("dominio.regla_incumplida", message)
    {
    }

    public ReglaDominioException(string message, Exception innerException)
        : base(message, innerException)
    {
        Codigo = "dominio.regla_incumplida";
    }

    public ReglaDominioException(string codigo, string message)
        : base(message)
    {
        Codigo = codigo;
    }

    public string Codigo { get; }
}