using OpoStudy.Domain.Comun;

namespace OpoStudy.Domain.Preguntas;

/// <summary>
/// Pregunta tipo test con su aclaración. No se borra nunca: se desactiva para conservar el historial de respuestas.
/// </summary>
public sealed class Pregunta
{
    private readonly List<Opcion> _opciones = [];

    private Pregunta()
    {
        Clave = Enunciado = Aclaracion = ReferenciaLegal = string.Empty;
    }

    public Guid Id { get; private set; }
    public Guid TemaId { get; private set; }

    /// <summary>Identificador estable que viene del contenido versionado (p. ej. <c>malaga-c1/t01/0001</c>).</summary>
    public string Clave { get; private set; }

    public string Enunciado { get; private set; }
    public string Aclaracion { get; private set; }
    public string ReferenciaLegal { get; private set; }
    public bool Activa { get; private set; }
    public IReadOnlyList<Opcion> Opciones => _opciones;

    public Opcion OpcionCorrecta => _opciones.Single(o => o.EsCorrecta);

    public static Pregunta Crear(
        Guid temaId,
        string clave,
        string enunciado,
        string aclaracion,
        string referenciaLegal,
        IReadOnlyList<DatosOpcion> opciones)
    {
        ArgumentNullException.ThrowIfNull(opciones);
        ValidarOpciones(opciones);

        var pregunta = new Pregunta
        {
            Id = Guid.CreateVersion7(),
            TemaId = temaId,
            Clave = Regla.TextoObligatorio(clave, Limites.Clave, "pregunta.clave_invalida"),
            Activa = true,
        };
        pregunta.AsignarTextos(enunciado, aclaracion, referenciaLegal);
        for (var orden = 0; orden < opciones.Count; orden++)
        {
            pregunta._opciones.Add(Opcion.Crear(pregunta.Id, orden, opciones[orden]));
        }

        return pregunta;
    }

    /// <summary>
    /// Actualiza los textos y las opciones (emparejadas por orden). El número de opciones no puede cambiar,
    /// porque las respuestas ya dadas apuntan a ellas: para eso se crea otra pregunta y se desactiva esta.
    /// </summary>
    public void Actualizar(
        Guid temaId,
        string enunciado,
        string aclaracion,
        string referenciaLegal,
        IReadOnlyList<DatosOpcion> opciones)
    {
        ArgumentNullException.ThrowIfNull(opciones);
        ValidarOpciones(opciones);
        if (opciones.Count != _opciones.Count)
        {
            throw new ReglaDominioException(
                "pregunta.cambio_numero_opciones",
                "No se puede cambiar el número de opciones de una pregunta existente; crea otra y desactiva esta.");
        }

        TemaId = temaId;
        AsignarTextos(enunciado, aclaracion, referenciaLegal);
        foreach (var opcion in _opciones)
        {
            opcion.Actualizar(opciones[opcion.Orden]);
        }
    }

    public void Activar() => Activa = true;

    public void Desactivar() => Activa = false;

    private void AsignarTextos(string enunciado, string aclaracion, string referenciaLegal)
    {
        Enunciado = Regla.TextoObligatorio(enunciado, Limites.Enunciado, "pregunta.enunciado_invalido");
        Aclaracion = Regla.TextoObligatorio(aclaracion, Limites.Aclaracion, "pregunta.aclaracion_invalida");
        ReferenciaLegal = Regla.TextoObligatorio(referenciaLegal, Limites.ReferenciaLegal, "pregunta.referencia_legal_invalida");
    }

    private static void ValidarOpciones(IReadOnlyList<DatosOpcion> opciones)
    {
        if (opciones.Count is < Limites.MinOpciones or > Limites.MaxOpciones)
        {
            throw new ReglaDominioException(
                "pregunta.numero_opciones_invalido",
                $"Una pregunta debe tener entre {Limites.MinOpciones} y {Limites.MaxOpciones} opciones.");
        }

        var correctas = opciones.Count(o => o.EsCorrecta);
        if (correctas == 0)
        {
            throw new ReglaDominioException("pregunta.sin_opcion_correcta", "La pregunta no tiene ninguna opción correcta.");
        }

        if (correctas > 1)
        {
            throw new ReglaDominioException("pregunta.varias_opciones_correctas", "La pregunta tiene más de una opción correcta.");
        }
    }
}