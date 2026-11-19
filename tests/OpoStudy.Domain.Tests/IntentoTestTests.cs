using OpoStudy.Domain.Comun;
using OpoStudy.Domain.Intentos;
using OpoStudy.Domain.Preguntas;

using Shouldly;

namespace OpoStudy.Domain.Tests;

public sealed class IntentoTestTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 19, 10, 0, 0, TimeSpan.FromHours(1));
    private static readonly Guid UsuarioId = Guid.CreateVersion7();
    private static readonly Guid OposicionId = Guid.CreateVersion7();

    private readonly Pregunta _p1 = PreguntaTests.Nueva(correcta: 0);
    private readonly Pregunta _p2 = PreguntaTests.Nueva(correcta: 1);

    private IntentoTest NuevoIntento() =>
        IntentoTest.Crear(UsuarioId, OposicionId, null, ModoTest.Aleatorio, [_p1.Id, _p2.Id], Ahora);

    [Fact]
    public void Crear_ConPreguntas_CreaRespuestasVaciasEnOrdenYFechaUtc()
    {
        var intento = NuevoIntento();

        intento.Respuestas.Select(r => (r.PreguntaId, r.Orden)).ShouldBe([(_p1.Id, 0), (_p2.Id, 1)]);
        intento.Respuestas.ShouldAllBe(r => !r.EstaRespondida);
        intento.FechaInicio.Offset.ShouldBe(TimeSpan.Zero);
        intento.FechaInicio.ShouldBe(Ahora);
        intento.EstaFinalizado.ShouldBeFalse();
    }

    [Fact]
    public void Crear_PorTemaSinTema_LanzaReglaDominio()
    {
        var ex = Should.Throw<ReglaDominioException>(() =>
            IntentoTest.Crear(UsuarioId, OposicionId, null, ModoTest.PorTema, [_p1.Id], Ahora));

        ex.Codigo.ShouldBe("intento.tema_obligatorio");
    }

    [Fact]
    public void Crear_AleatorioConTema_LanzaReglaDominio()
    {
        var ex = Should.Throw<ReglaDominioException>(() =>
            IntentoTest.Crear(UsuarioId, OposicionId, Guid.CreateVersion7(), ModoTest.Aleatorio, [_p1.Id], Ahora));

        ex.Codigo.ShouldBe("intento.tema_no_permitido");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(Limites.MaxPreguntasPorTest + 1)]
    public void Crear_ConNumeroDePreguntasFueraDeRango_LanzaReglaDominio(int numero)
    {
        var ids = Enumerable.Range(0, numero).Select(_ => Guid.CreateVersion7()).ToList();

        var ex = Should.Throw<ReglaDominioException>(() =>
            IntentoTest.Crear(UsuarioId, OposicionId, null, ModoTest.Aleatorio, ids, Ahora));

        ex.Codigo.ShouldBe("intento.numero_preguntas_invalido");
    }

    [Fact]
    public void Crear_ConPreguntasRepetidas_LanzaReglaDominio()
    {
        var ex = Should.Throw<ReglaDominioException>(() =>
            IntentoTest.Crear(UsuarioId, OposicionId, null, ModoTest.Aleatorio, [_p1.Id, _p1.Id], Ahora));

        ex.Codigo.ShouldBe("intento.preguntas_repetidas");
    }

    [Fact]
    public void Responder_OpcionCorrecta_DevuelveAcierto()
    {
        var intento = NuevoIntento();

        var resultado = intento.Responder(_p1, _p1.OpcionCorrecta.Id, Ahora);

        resultado.ShouldBe(new ResultadoRespuesta(true, _p1.OpcionCorrecta.Id));
        intento.Aciertos.ShouldBe(1);
        intento.Fallos.ShouldBe(0);
    }

    [Fact]
    public void Responder_OpcionIncorrecta_DevuelveFalloYOpcionCorrecta()
    {
        var intento = NuevoIntento();
        var incorrecta = _p1.Opciones.First(o => !o.EsCorrecta);

        var resultado = intento.Responder(_p1, incorrecta.Id, Ahora);

        resultado.ShouldBe(new ResultadoRespuesta(false, _p1.OpcionCorrecta.Id));
        intento.Fallos.ShouldBe(1);
        var respuesta = intento.Respuestas.Single(r => r.PreguntaId == _p1.Id);
        respuesta.OpcionElegidaId.ShouldBe(incorrecta.Id);
        respuesta.RespondidaEn.ShouldNotBeNull().Offset.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void Responder_PreguntaAjena_LanzaReglaDominio()
    {
        var intento = NuevoIntento();
        var ajena = PreguntaTests.Nueva();

        var ex = Should.Throw<ReglaDominioException>(() => intento.Responder(ajena, ajena.OpcionCorrecta.Id, Ahora));

        ex.Codigo.ShouldBe("intento.pregunta_ajena");
    }

    [Fact]
    public void Responder_DosVeces_LanzaReglaDominio()
    {
        var intento = NuevoIntento();
        intento.Responder(_p1, _p1.OpcionCorrecta.Id, Ahora);

        var ex = Should.Throw<ReglaDominioException>(() => intento.Responder(_p1, _p1.OpcionCorrecta.Id, Ahora));

        ex.Codigo.ShouldBe("intento.pregunta_ya_respondida");
    }

    [Fact]
    public void Responder_OpcionDeOtraPregunta_LanzaReglaDominio()
    {
        var intento = NuevoIntento();

        var ex = Should.Throw<ReglaDominioException>(() => intento.Responder(_p1, _p2.OpcionCorrecta.Id, Ahora));

        ex.Codigo.ShouldBe("intento.opcion_ajena");
    }

    [Fact]
    public void Responder_UltimaPregunta_FinalizaElIntento()
    {
        var intento = NuevoIntento();
        var fin = Ahora.AddMinutes(5);

        intento.Responder(_p1, _p1.OpcionCorrecta.Id, Ahora);
        intento.EstaFinalizado.ShouldBeFalse();
        intento.Responder(_p2, _p2.Opciones.First(o => !o.EsCorrecta).Id, fin);

        intento.EstaFinalizado.ShouldBeTrue();
        intento.FechaFin.ShouldBe(fin);
        intento.Aciertos.ShouldBe(1);
        intento.Fallos.ShouldBe(1);
    }

    [Fact]
    public void Responder_IntentoFinalizado_LanzaReglaDominio()
    {
        var intento = IntentoTest.Crear(UsuarioId, OposicionId, null, ModoTest.Aleatorio, [_p1.Id], Ahora);
        intento.Responder(_p1, _p1.OpcionCorrecta.Id, Ahora);

        var ex = Should.Throw<ReglaDominioException>(() => intento.Responder(_p1, _p1.OpcionCorrecta.Id, Ahora));

        ex.Codigo.ShouldBe("intento.finalizado");
    }
}