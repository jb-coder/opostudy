var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume("opostudy-pgdata")
    .WithLifetime(ContainerLifetime.Persistent);

var db = postgres.AddDatabase("opostudy");

var api = builder.AddDotnetProject("api", "../OpoStudy.Api/OpoStudy.Api.csproj")
    .WithReference(db)
    .WaitFor(db)
    .WithHttpHealthCheck("/health");

builder.AddDotnetProject("worker", "../OpoStudy.Worker/OpoStudy.Worker.csproj")
    .WithReference(db)
    .WaitFor(db)
    .WaitFor(api);

builder.Build().Run();
