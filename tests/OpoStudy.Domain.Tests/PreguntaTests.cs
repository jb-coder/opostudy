using OpoStudy.Domain.Comun;
using OpoStudy.Domain.Preguntas;

using Shouldly;

namespace OpoStudy.Domain.Tests;

public sealed class PreguntaTests
{
    private static readonly Guid TemaId = Guid.CreateVersion7();

    internal static DatosOpcion[] Opciones(int numero = 4, int correcta = 0) =>
        [.. Enumerable.Range(0, numero).Select(i => new DatosOpcion($"Opción {i}", i == correcta))];

    internal static Pregunta Nueva(int numeroOpciones = 4, int correcta = 0) =>
        Pregunta.Crear(TemaId, "test/t01/0001", "¿Enunciado?", "Aclaración", "CE art. 1", Opciones(numeroOpciones, correcta));

    [Fact]
    public void Crear_ConUnaCorrecta_CreaOpcionesEnOrden()
    {
        var pregunta = Nueva(correcta: 2);

        pregunta.Activa.ShouldBeTrue();
        pregunta.Opciones.Select(o => o.Orden).ShouldBe([0, 1, 2, 3]);
        pregunta.Opciones.ShouldAllBe(o => o.PreguntaId == pregunta.Id);
        pregunta.OpcionCorrecta.Orden.ShouldBe(2);
    }

    [Fact]
    public void Crear_SinOpcionCorrecta_LanzaReglaDominio()
    {
        DatosOpcion[] opciones = [new("A", false), new("B", false)];

        var ex = Should.Throw<ReglaDominioException>(() =>
            Pregunta.Crear(TemaId, "c", "e", "a", "r", opciones));

        ex.Codigo.ShouldBe("pregunta.sin_opcion_correcta");
    }

    [Fact]
    public void Crear_ConDosCorrectas_LanzaReglaDominio()
    {
        DatosOpcion[] opciones = [new("A", true), new("B", true), new("C", false)];

        var ex = Should.Throw<ReglaDominioException>(() =>
            Pregunta.Crear(TemaId, "c", "e", "a", "r", opciones));

        ex.Codigo.ShouldBe("pregunta.varias_opciones_correctas");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    public void Crear_ConNumeroDeOpcionesFueraDeRango_LanzaReglaDominio(int numero)
    {
        var ex = Should.Throw<ReglaDominioException>(() => Nueva(numeroOpciones: numero));

        ex.Codigo.ShouldBe("pregunta.numero_opciones_invalido");
    }

    [Theory]
    [InlineData("", "a", "r", "pregunta.enunciado_invalido")]
    [InlineData("e", "  ", "r", "pregunta.aclaracion_invalida")]
    [InlineData("e", "a", "", "pregunta.referencia_legal_invalida")]
    public void Crear_ConTextoVacio_LanzaReglaDominio(string enunciado, string aclaracion, string referencia, string codigo)
    {
        var ex = Should.Throw<ReglaDominioException>(() =>
            Pregunta.Crear(TemaId, "c", enunciado, aclaracion, referencia, Opciones()));

        ex.Codigo.ShouldBe(codigo);
    }

    [Fact]
    public void Crear_ConAclaracionDemasiadoLarga_LanzaReglaDominio()
    {
        var aclaracion = new string('x', Limites.Aclaracion + 1);

        var ex = Should.Throw<ReglaDominioException>(() =>
            Pregunta.Crear(TemaId, "c", "e", aclaracion, "r", Opciones()));

        ex.Codigo.ShouldBe("pregunta.aclaracion_invalida");
    }

    [Fact]
    public void Actualizar_ConMismoNumeroDeOpciones_ConservaIdsYCambiaCorrecta()
    {
        var pregunta = Nueva(correcta: 0);
        var ids = pregunta.Opciones.Select(o => o.Id).ToList();

        pregunta.Actualizar(TemaId, "Nuevo", "Nueva aclaración", "CE art. 2", Opciones(correcta: 3));

        pregunta.Opciones.Select(o => o.Id).ShouldBe(ids);
        pregunta.OpcionCorrecta.Orden.ShouldBe(3);
        pregunta.Enunciado.ShouldBe("Nuevo");
    }

    [Fact]
    public void Actualizar_CambiandoNumeroDeOpciones_LanzaReglaDominio()
    {
        var pregunta = Nueva(numeroOpciones: 4);

        var ex = Should.Throw<ReglaDominioException>(() =>
            pregunta.Actualizar(TemaId, "e", "a", "r", Opciones(numero: 3)));

        ex.Codigo.ShouldBe("pregunta.cambio_numero_opciones");
    }
}