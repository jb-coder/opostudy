using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(OpoStudy.Api.IntegrationTests.ApiFactory))]

namespace OpoStudy.Api.IntegrationTests;

/// <summary>The API against a real Postgres (Docker), shared by the whole assembly.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();

        // Start the base host now so it applies the migrations once. Otherwise tests that build derived hosts
        // (WithWebHostBuilder) in parallel would all try to migrate the empty database at the same time.
        _ = Services;
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    /// <summary>Client authenticated as <paramref name="uid"/> through the test scheme.</summary>
    public HttpClient CreateClientFor(string uid) => CreateClientFor(this, uid);

    /// <summary>Client for any factory derived from this one (see <c>WithWebHostBuilder</c>).</summary>
    public static HttpClient CreateClientFor(WebApplicationFactory<Program> factory, string uid)
    {
        ArgumentNullException.ThrowIfNull(factory);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, uid);
        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:opostudy", _postgres.GetConnectionString());
        builder.UseSetting("Firebase:ProjectId", "test-project");
        builder.UseSetting("Grading:MonthlyQuota", "2");

        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication(options =>
            {
                options.DefaultScheme = TestAuthHandler.SchemeName;
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        });
    }
}