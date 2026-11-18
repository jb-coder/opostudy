using OpoStudy.Api.Features.Sistema;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapDefaultEndpoints();

var api = app.MapGroup("/api/v1");
api.MapSistemaEndpoints();

app.Run();

/// <summary>Punto de entrada expuesto para los tests de integración (WebApplicationFactory).</summary>
public partial class Program;
