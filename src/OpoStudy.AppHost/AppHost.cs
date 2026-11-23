var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume("opostudy-pgdata")
    .WithLifetime(ContainerLifetime.Persistent);

var db = postgres.AddDatabase("opostudy");

// Firebase project id; stored in the AppHost user-secrets (Parameters:firebase-project-id).
var firebaseProjectId = builder.AddParameter("firebase-project-id");

var api = builder.AddDotnetProject("api", "../OpoStudy.Api/OpoStudy.Api.csproj")
    .WithReference(db)
    .WaitFor(db)
    .WithEnvironment("Firebase__ProjectId", firebaseProjectId)
    .WithHttpHealthCheck("/health");

// Corrector of the practical cases: "Fake" (default, no key) or "Gemini" (Parameters:grading-provider in the AppHost user-secrets).
var gradingProvider = builder.Configuration["Parameters:grading-provider"] ?? "Fake";

var worker = builder.AddDotnetProject("worker", "../OpoStudy.Worker/OpoStudy.Worker.csproj")
    .WithReference(db)
    .WaitFor(db)
    .WaitFor(api)
    .WithEnvironment("Grading__Provider", gradingProvider);

if (string.Equals(gradingProvider, "Gemini", StringComparison.OrdinalIgnoreCase))
{
    // Secret parameter, stored in the AppHost user-secrets (Parameters:gemini-api-key); only requested when Gemini is chosen.
    var geminiApiKey = builder.AddParameter("gemini-api-key", secret: true);
    worker.WithEnvironment("Gemini__ApiKey", geminiApiKey);
}

// Loads content/ into the database on every start; the import is idempotent.
var contentDirectory = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "..", "content"));
builder.AddDotnetProject("content-importer", "../OpoStudy.ContentImporter/OpoStudy.ContentImporter.csproj")
    .WithReference(db)
    .WaitFor(api)
    .WithArgs("--content", contentDirectory);

builder.Build().Run();
