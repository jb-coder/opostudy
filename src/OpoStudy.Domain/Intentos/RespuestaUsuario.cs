namespace OpoStudy.Domain.Intentos;

/// <summary>Pregunta de un <see cref="IntentoTest"/> y lo que respondió el usuario. Solo se modifica a través del intento.</summary>
public sealed class RespuestaUsuario
{
    private RespuestaUsuario()
    {
    }

    public Guid IntentoTestId { get; private set; }
    public Guid PreguntaId { get; private set; }
    public int Orden { get; private set; }
    public Guid? OpcionElegidaId { get; private set; }
    public bool? EsCorrecta { get; private set; }
    public DateTimeOffset? RespondidaEn { get; private set; }

    public bool EstaRespondida => OpcionElegidaId is not null;

    internal static RespuestaUsuario Crear(Guid intentoTestId, Guid preguntaId, int orden) => new()
    {
        IntentoTestId = intentoTestId,
        PreguntaId = preguntaId,
        Orden = orden,
    };

    internal void Registrar(Guid opcionId, bool esCorrecta, DateTimeOffset ahora)
    {
        OpcionElegidaId = opcionId;
        EsCorrecta = esCorrecta;
        RespondidaEn = ahora;
    }
}