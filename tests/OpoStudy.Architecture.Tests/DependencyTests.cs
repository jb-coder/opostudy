using System.Reflection;

using NetArchTest.Rules;

using Shouldly;

using ArchResult = NetArchTest.Rules.TestResult;

namespace OpoStudy.Architecture.Tests;

/// <summary>Dependency rules between layers.</summary>
public sealed class DependencyTests
{
    private static readonly Assembly Domain = Assembly.Load("OpoStudy.Domain");
    private static readonly Assembly Contracts = Assembly.Load("OpoStudy.Contracts");
    private static readonly Assembly Infrastructure = Assembly.Load("OpoStudy.Infrastructure");

    [Fact]
    public void Domain_DoesNotDependOnInfrastructureOrFrameworks()
    {
        var result = Types.InAssembly(Domain)
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

        result.IsSuccessful.ShouldBeTrue(FailingTypes(result));
    }

    [Fact]
    public void Contracts_OnlyContainsDtosWithoutDependencies()
    {
        var result = Types.InAssembly(Contracts)
            .ShouldNot()
            .HaveDependencyOnAny(
                "OpoStudy.Domain",
                "OpoStudy.Infrastructure",
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(FailingTypes(result));
    }

    [Fact]
    public void Infrastructure_DoesNotDependOnApiOrWorker()
    {
        var result = Types.InAssembly(Infrastructure)
            .ShouldNot()
            .HaveDependencyOnAny("OpoStudy.Api", "OpoStudy.Worker")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(FailingTypes(result));
    }

    private static string FailingTypes(ArchResult result) =>
        string.Join(", ", result.FailingTypeNames ?? []);
}