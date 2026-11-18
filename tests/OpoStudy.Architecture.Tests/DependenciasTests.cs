using System.Reflection;
using NetArchTest.Rules;
using Shouldly;
using ArchResult = NetArchTest.Rules.TestResult;

namespace OpoStudy.Architecture.Tests;

/// <summary>
/// Reglas de dependencias entre capas. Se cumplen trivialmente mientras los proyectos estén vacíos
/// y empiezan a proteger la arquitectura en cuanto se añade código.
/// </summary>
public sealed class DependenciasTests
{
    private static readonly Assembly Domain = Assembly.Load("OpoStudy.Domain");
    private static readonly Assembly Contracts = Assembly.Load("OpoStudy.Contracts");
    private static readonly Assembly Infrastructure = Assembly.Load("OpoStudy.Infrastructure");

    [Fact]
    public void Domain_NoDependeDeInfraestructuraNiFrameworks()
    {
        var resultado = Types.InAssembly(Domain)
            .ShouldNot()
            .HaveDependencyOnAny(
                "OpoStudy.Infrastructure",
                "OpoStudy.Api",
                "OpoStudy.Worker",
                "OpoStudy.Contracts",
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore",
                "Npgsql")
            .GetResult();

        resultado.IsSuccessful.ShouldBeTrue(Fallos(resultado));
    }

    [Fact]
    public void Contracts_SoloContieneDtosSinDependencias()
    {
        var resultado = Types.InAssembly(Contracts)
            .ShouldNot()
            .HaveDependencyOnAny(
                "OpoStudy.Domain",
                "OpoStudy.Infrastructure",
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore")
            .GetResult();

        resultado.IsSuccessful.ShouldBeTrue(Fallos(resultado));
    }

    [Fact]
    public void Infrastructure_NoDependeDeLaApiNiDelWorker()
    {
        var resultado = Types.InAssembly(Infrastructure)
            .ShouldNot()
            .HaveDependencyOnAny("OpoStudy.Api", "OpoStudy.Worker")
            .GetResult();

        resultado.IsSuccessful.ShouldBeTrue(Fallos(resultado));
    }

    private static string Fallos(ArchResult resultado) =>
        string.Join(", ", resultado.FailingTypeNames ?? []);
}
