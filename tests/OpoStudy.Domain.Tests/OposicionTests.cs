using OpoStudy.Domain.Comun;
using OpoStudy.Domain.Oposiciones;
using OpoStudy.Domain.Usuarios;

using Shouldly;

namespace OpoStudy.Domain.Tests;

public sealed class OposicionTests
{
    [Theory]
    [InlineData("Malaga C1")]
    [InlineData("malaga--c1")]
    [InlineData("-malaga")]
    [InlineData("")]
    public void Crear_ConSlugInvalido_LanzaReglaDominio(string slug)
    {
        var ex = Should.Throw<ReglaDominioException>(() =>
            Oposicion.Crear(slug, "Programador", "Diputación de Málaga", "C1"));

        ex.Codigo.ShouldBe("oposicion.slug_invalido");
    }

    [Fact]
    public void Crear_ConDatosValidos_QuedaActivaYRecortaTextos()
    {
        var oposicion = Oposicion.Crear("malaga-programador-c1", "  Programador  ", "Diputación de Málaga", "C1");

        oposicion.Activa.ShouldBeTrue();
        oposicion.Nombre.ShouldBe("Programador");
    }

    [Fact]
    public void CrearTema_ConNumeroNoPositivo_LanzaReglaDominio()
    {
        var ex = Should.Throw<ReglaDominioException>(() =>
            Tema.Crear(Guid.CreateVersion7(), "tema-1", 0, "Constitución", null));

        ex.Codigo.ShouldBe("tema.numero_invalido");
    }

    [Fact]
    public void CrearUsuario_SinFirebaseUid_LanzaReglaDominio()
    {
        var ex = Should.Throw<ReglaDominioException>(() => Usuario.Crear(" ", DateTimeOffset.UtcNow));

        ex.Codigo.ShouldBe("usuario.firebase_uid_invalido");
    }
}