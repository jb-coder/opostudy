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

builder.AddDotnetProject("worker", "../OpoStudy.Worker/OpoStudy.Worker.csproj")
    .WithReference(db)
    .WaitFor(db)
    .WaitFor(api);

// Loads content/ into the database on every start; the import is idempotent.
var contentDirectory = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "..", "content"));
builder.AddDotnetProject("content-importer", "../OpoStudy.ContentImporter/OpoStudy.ContentImporter.csproj")
    .WithReference(db)
    .WaitFor(api)
    .WithArgs("--content", contentDirectory);

builder.Build().Run();
