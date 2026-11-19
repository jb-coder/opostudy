using OpoStudy.Domain.Comun;
using OpoStudy.Domain.Preguntas;

namespace OpoStudy.Domain.Intentos;

/// <summary>Un test que hace un usuario: el conjunto ordenado de preguntas y sus respuestas.</summary>
public sealed class IntentoTest
{
    private readonly List<RespuestaUsuario> _respuestas = [];

    private IntentoTest()
    {
    }

    public Guid Id { get; private set; }
    public Guid UsuarioId { get; private set; }
    public Guid OposicionId { get; private set; }

    /// <summary>Tema del test; <c>null</c> en los tests aleatorios.</summary>
    public Guid? TemaId { get; private set; }

    public ModoTest Modo { get; private set; }
    public DateTimeOffset FechaInicio { get; private set; }
    public DateTimeOffset? FechaFin { get; private set; }
    public IReadOnlyList<RespuestaUsuario> Respuestas => _respuestas;

    public bool EstaFinalizado => FechaFin is not null;
    public int Aciertos => _respuestas.Count(r => r.EsCorrecta == true);
    public int Fallos => _respuestas.Count(r => r.EsCorrecta == false);

    public static IntentoTest Crear(
        Guid usuarioId,
        Guid oposicionId,
        Guid? temaId,
        ModoTest modo,
        IReadOnlyList<Guid> preguntaIds,
        DateTimeOffset ahora)
    {
        ArgumentNullException.ThrowIfNull(preguntaIds);

        if (modo == ModoTest.PorTema && temaId is null)
        {
            throw new ReglaDominioException("intento.tema_obligatorio", "Un test por tema necesita un tema.");
        }

        if (modo == ModoTest.Aleatorio && temaId is not null)
        {
            throw new ReglaDominioException("intento.tema_no_permitido", "Un test aleatorio no tiene tema.");
        }

        if (preguntaIds.Count is 0 or > Limites.MaxPreguntasPorTest)
        {
            throw new ReglaDominioException(
                "intento.numero_preguntas_invalido",
                $"Un test debe tener entre 1 y {Limites.MaxPreguntasPorTest} preguntas.");
        }

        if (preguntaIds.Distinct().Count() != preguntaIds.Count)
        {
            throw new ReglaDominioException("intento.preguntas_repetidas", "Un test no puede repetir preguntas.");
        }

        var intento = new IntentoTest
        {
            Id = Guid.CreateVersion7(),
            UsuarioId = usuarioId,
            OposicionId = oposicionId,
            TemaId = temaId,
            Modo = modo,
            FechaInicio = ahora.ToUniversalTime(),
        };
        for (var orden = 0; orden < preguntaIds.Count; orden++)
        {
            intento._respuestas.Add(RespuestaUsuario.Crear(intento.Id, preguntaIds[orden], orden));
        }

        return intento;
    }

    public ResultadoRespuesta Responder(Pregunta pregunta, Guid opcionId, DateTimeOffset ahora)
    {
        ArgumentNullException.ThrowIfNull(pregunta);
        ahora = ahora.ToUniversalTime();

        if (EstaFinalizado)
        {
            throw new ReglaDominioException("intento.finalizado", "El test ya está finalizado.");
        }

        var respuesta = _respuestas.SingleOrDefault(r => r.PreguntaId == pregunta.Id)
            ?? throw new ReglaDominioException("intento.pregunta_ajena", "La pregunta no pertenece a este test.");

        if (respuesta.EstaRespondida)
        {
            throw new ReglaDominioException("intento.pregunta_ya_respondida", "La pregunta ya está respondida.");
        }

        var opcion = pregunta.Opciones.SingleOrDefault(o => o.Id == opcionId)
            ?? throw new ReglaDominioException("intento.opcion_ajena", "La opción no pertenece a la pregunta.");

        respuesta.Registrar(opcion.Id, opcion.EsCorrecta, ahora);
        if (_respuestas.TrueForAll(r => r.EstaRespondida))
        {
            FechaFin = ahora;
        }

        return new ResultadoRespuesta(opcion.EsCorrecta, pregunta.OpcionCorrecta.Id);
    }
}

public sealed record ResultadoRespuesta(bool EsCorrecta, Guid OpcionCorrectaId);