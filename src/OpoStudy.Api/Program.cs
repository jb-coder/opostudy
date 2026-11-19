using OpoStudy.Api.Features.SystemInfo;
using OpoStudy.Infrastructure.Persistence;

using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<OpoStudyDbContext>(
    "opostudy", configureDbContextOptions: OpoStudyDbContext.ApplyConventions);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    // On the VPS, migrations will be applied with a migration bundle before startup (v0.5).
    await app.Services.ApplyMigrationsAsync();
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapDefaultEndpoints();

var api = app.MapGroup("/api/v1");
api.MapSystemInfoEndpoints();

app.Run();

/// <summary>Entry point exposed for the integration tests (WebApplicationFactory).</summary>
public partial class Program;