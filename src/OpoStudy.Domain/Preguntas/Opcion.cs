using OpoStudy.Domain.Comun;

namespace OpoStudy.Domain.Preguntas;

/// <summary>Opción de respuesta de una <see cref="Pregunta"/>. Solo se crea y modifica a través de la pregunta.</summary>
public sealed class Opcion
{
    private Opcion()
    {
        Texto = string.Empty;
    }

    public Guid Id { get; private set; }
    public Guid PreguntaId { get; private set; }
    public int Orden { get; private set; }
    public string Texto { get; private set; }
    public bool EsCorrecta { get; private set; }

    internal static Opcion Crear(Guid preguntaId, int orden, DatosOpcion datos) => new()
    {
        Id = Guid.CreateVersion7(),
        PreguntaId = preguntaId,
        Orden = orden,
        Texto = Regla.TextoObligatorio(datos.Texto, Limites.TextoOpcion, "opcion.texto_invalido"),
        EsCorrecta = datos.EsCorrecta,
    };

    internal void Actualizar(DatosOpcion datos)
    {
        Texto = Regla.TextoObligatorio(datos.Texto, Limites.TextoOpcion, "opcion.texto_invalido");
        EsCorrecta = datos.EsCorrecta;
    }
}

/// <summary>Datos de entrada de una opción. El orden lo da su posición en la lista.</summary>
public sealed record DatosOpcion(string Texto, bool EsCorrecta);