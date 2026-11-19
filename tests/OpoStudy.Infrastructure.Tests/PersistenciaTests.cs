using Microsoft.EntityFrameworkCore;

using Npgsql;

using OpoStudy.Domain.Intentos;
using OpoStudy.Domain.Oposiciones;
using OpoStudy.Domain.Preguntas;
using OpoStudy.Domain.Usuarios;

using Shouldly;

namespace OpoStudy.Infrastructure.Tests;

/// <summary>Cada test crea sus propios datos con slugs y claves únicos, así no se pisan entre sí.</summary>
public sealed class PersistenciaTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Ahora = DateTimeOffset.UtcNow;
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Modelo_NoTieneCambiosPendientesDeMigrar()
    {
        await using var db = postgres.CrearContexto();

        db.Database.HasPendingModelChanges().ShouldBeFalse();
    }

    [Fact]
    public async Task IntentoTest_GuardadoYRecargado_ConservaRespuestasYOpciones()
    {
        var (oposicion, tema, preguntas) = await CrearContenidoAsync(numeroPreguntas: 2);
        var usuario = Usuario.Crear(Unico("uid"), Ahora);
        var intento = IntentoTest.Crear(usuario.Id, oposicion.Id, tema.Id, ModoTest.PorTema, [.. preguntas.Select(p => p.Id)], Ahora);
        intento.Responder(preguntas[0], preguntas[0].OpcionCorrecta.Id, Ahora);
        await using (var db = postgres.CrearContexto())
        {
            db.Usuarios.Add(usuario);
            db.IntentosTest.Add(intento);
            await db.SaveChangesAsync(Ct);
        }

        await using (var db = postgres.CrearContexto())
        {
            var leido = await db.IntentosTest.SingleAsync(i => i.Id == intento.Id, Ct);
            leido.Modo.ShouldBe(ModoTest.PorTema);
            leido.Respuestas.Count.ShouldBe(2);
            leido.Aciertos.ShouldBe(1);
            leido.EstaFinalizado.ShouldBeFalse();

            var pregunta = await db.Preguntas.SingleAsync(p => p.Id == preguntas[1].Id, Ct);
            pregunta.Opciones.Count.ShouldBe(4);
            leido.Responder(pregunta, pregunta.OpcionCorrecta.Id, Ahora);
            await db.SaveChangesAsync(Ct);
        }

        await using (var db = postgres.CrearContexto())
        {
            var final = await db.IntentosTest.SingleAsync(i => i.Id == intento.Id, Ct);
            final.EstaFinalizado.ShouldBeTrue();
            final.Aciertos.ShouldBe(2);
        }
    }

    [Fact]
    public async Task Pregunta_ConClaveDuplicada_ViolaIndiceUnico()
    {
        var (_, tema, preguntas) = await CrearContenidoAsync(numeroPreguntas: 1);
        await using var db = postgres.CrearContexto();
        db.Preguntas.Add(NuevaPregunta(tema.Id, preguntas[0].Clave));

        var ex = await Should.ThrowAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));

        ex.InnerException.ShouldBeOfType<PostgresException>().SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
    }

    [Fact]
    public async Task Usuario_ConFirebaseUidDuplicado_ViolaIndiceUnico()
    {
        var uid = Unico("uid");
        await using var db = postgres.CrearContexto();
        db.Usuarios.AddRange(Usuario.Crear(uid, Ahora), Usuario.Crear(uid, Ahora));

        var ex = await Should.ThrowAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));

        ex.InnerException.ShouldBeOfType<PostgresException>().SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
    }

    [Fact]
    public async Task Usuario_AlBorrarse_BorraSusIntentosYRespuestasEnCascada()
    {
        var (oposicion, _, preguntas) = await CrearContenidoAsync(numeroPreguntas: 1);
        var usuario = Usuario.Crear(Unico("uid"), Ahora);
        var intento = IntentoTest.Crear(usuario.Id, oposicion.Id, null, ModoTest.Aleatorio, [preguntas[0].Id], Ahora);
        await using (var db = postgres.CrearContexto())
        {
            db.Usuarios.Add(usuario);
            db.IntentosTest.Add(intento);
            await db.SaveChangesAsync(Ct);
        }

        await using (var db = postgres.CrearContexto())
        {
            await db.Usuarios.Where(u => u.Id == usuario.Id).ExecuteDeleteAsync(Ct);
        }

        await using (var db = postgres.CrearContexto())
        {
            (await db.IntentosTest.AnyAsync(i => i.Id == intento.Id, Ct)).ShouldBeFalse();
            (await db.Preguntas.AnyAsync(p => p.Id == preguntas[0].Id, Ct)).ShouldBeTrue();
        }
    }

    private async Task<(Oposicion Oposicion, Tema Tema, List<Pregunta> Preguntas)> CrearContenidoAsync(int numeroPreguntas)
    {
        var oposicion = Oposicion.Crear(Unico("opo"), "Programador", "Diputación de Málaga", "C1");
        var tema = Tema.Crear(oposicion.Id, "tema-1", 1, "Constitución", "Bloque I");
        var preguntas = Enumerable.Range(0, numeroPreguntas).Select(_ => NuevaPregunta(tema.Id, Unico("clave"))).ToList();

        await using var db = postgres.CrearContexto();
        db.Oposiciones.Add(oposicion);
        db.Temas.Add(tema);
        db.Preguntas.AddRange(preguntas);
        await db.SaveChangesAsync(Ct);
        return (oposicion, tema, preguntas);
    }

    private static Pregunta NuevaPregunta(Guid temaId, string clave) =>
        Pregunta.Crear(temaId, clave, "¿Enunciado?", "Aclaración", "CE art. 1",
            [new("A", true), new("B", false), new("C", false), new("D", false)]);

    private static string Unico(string prefijo) => $"{prefijo}-{Guid.NewGuid():N}";
}