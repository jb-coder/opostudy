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
    private static readonly Assembly Api = Assembly.Load("OpoStudy.Api");
    private static readonly Assembly MobileCore = Assembly.Load("OpoStudy.Mobile.Core");

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

    [Fact]
    public void Api_DoesNotReferenceTheLlmCorrector()
    {
        var result = Types.InAssembly(Api)
            .ShouldNot()
            .HaveDependencyOnAny(
                "OpoStudy.Infrastructure.Gradings.ILlmCorrector",
                "OpoStudy.Infrastructure.Gradings.FakeLlmCorrector",
                "OpoStudy.Infrastructure.Gradings.Gemini",
                "OpoStudy.Infrastructure.Gradings.GradingProcessor")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(FailingTypes(result));
    }

    [Fact]
    public void LlmCorrector_LivesInInfrastructure()
    {
        var result = Types.InAssembly(Infrastructure)
            .That()
            .HaveName("ILlmCorrector")
            .Should()
            .ResideInNamespace("OpoStudy.Infrastructure.Gradings")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(FailingTypes(result));
        Types.InAssembly(Domain).That().HaveName("ILlmCorrector").GetTypes().ShouldBeEmpty();
    }

    [Fact]
    public void MobileCore_ShouldNot_DependOnMaui()
    {
        var result = Types.InAssembly(MobileCore)
            .ShouldNot()
            .HaveDependencyOnAny("Microsoft.Maui", "Android", "Xamarin")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(FailingTypes(result));
    }

    private static string FailingTypes(ArchResult result) =>
        string.Join(", ", result.FailingTypeNames ?? []);
}