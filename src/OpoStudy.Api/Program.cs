using Microsoft.Extensions.DependencyInjection.Extensions;

using OpoStudy.Api.Authentication;
using OpoStudy.Api.Features.SystemInfo;
using OpoStudy.Api.Features.Users;
using OpoStudy.Api.OpenApi;
using OpoStudy.Api.RateLimiting;
using OpoStudy.Infrastructure.Persistence;

using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<OpoStudyDbContext>(
    "opostudy", configureDbContextOptions: OpoStudyDbContext.ApplyConventions);

builder.AddFirebaseAuthentication();
builder.AddPerUserRateLimiting();

builder.Services.AddHttpContextAccessor();
builder.Services.TryAddSingleton(TimeProvider.System);
builder.Services.AddScoped<CurrentUserService>();

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi(options => options.AddDocumentTransformer<BearerSecuritySchemeTransformer>());

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();

// After authentication so the partition key can use the user.
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    // On the VPS, migrations will be applied with a migration bundle before startup (v0.5).
    await app.Services.ApplyMigrationsAsync();
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference().AllowAnonymous();
}

app.MapDefaultEndpoints();

var api = app.MapGroup("/api/v1").RequireRateLimiting(RateLimitingExtensions.PerUserPolicy);
api.MapSystemInfoEndpoints();
api.MapUsersEndpoints();

app.Run();

/// <summary>Entry point exposed for the integration tests (WebApplicationFactory).</summary>
public partial class Program;