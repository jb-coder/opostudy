using OpoStudy.Api.Features.Sistema;
using OpoStudy.Infrastructure.Persistencia;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<OpoStudyDbContext>(
    "opostudy", configureDbContextOptions: OpoStudyDbContext.AplicarConvenciones);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    // En el VPS las migraciones se aplicarán con un migration bundle antes de arrancar (v0.5).
    await app.Services.AplicarMigracionesAsync();
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapDefaultEndpoints();

var api = app.MapGroup("/api/v1");
api.MapSistemaEndpoints();

app.Run();

/// <summary>Punto de entrada expuesto para los tests de integración (WebApplicationFactory).</summary>
public partial class Program;
