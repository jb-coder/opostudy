using Microsoft.Extensions.DependencyInjection.Extensions;

using OpoStudy.Api.Authentication;
using OpoStudy.Api.Errors;
using OpoStudy.Api.Features.Exams;
using OpoStudy.Api.Features.Gradings;
using OpoStudy.Api.Features.PracticalCases;
using OpoStudy.Api.Features.Progress;
using OpoStudy.Api.Features.MockExams;
using OpoStudy.Api.Features.Quizzes;
using OpoStudy.Api.Features.Stats;
using OpoStudy.Api.Features.SystemInfo;
using OpoStudy.Api.Features.Users;
using OpoStudy.Api.OpenApi;
using OpoStudy.Api.RateLimiting;
using OpoStudy.Infrastructure.Gradings;
using OpoStudy.Infrastructure.Persistence;
using OpoStudy.Infrastructure.Progress;

using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<OpoStudyDbContext>(
    "opostudy", configureDbContextOptions: OpoStudyDbContext.ApplyConventions);

builder.AddFirebaseAuthentication();
builder.AddPerUserRateLimiting();

builder.Services.AddGradingOptions(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.TryAddSingleton(TimeProvider.System);
builder.Services.AddScoped<CurrentUserService>();
builder.Services.AddProgressTracking();
builder.Services.AddScoped<ProgressRecorder>();

builder.Services.AddExceptionHandler<DomainRuleExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi(options => options.AddDocumentTransformer<BearerSecuritySchemeTransformer>());

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();

// After authentication so the partition key can use the user, and before authorization so
// anonymous requests are also rate limited (partitioned by IP) before getting a 401.
app.UseRateLimiter();
app.UseAuthorization();

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
api.MapExamsEndpoints();
api.MapPracticalCasesEndpoints();
api.MapQuizzesEndpoints();
api.MapMockExamsEndpoints();
api.MapProgressEndpoints();
api.MapGradingsEndpoints();
api.MapStatsEndpoints();

app.Run();

/// <summary>Entry point exposed for the integration tests (WebApplicationFactory).</summary>
public partial class Program;