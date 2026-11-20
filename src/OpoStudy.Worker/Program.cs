using OpoStudy.Infrastructure.Persistence;
using OpoStudy.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<OpoStudyDbContext>(
    "opostudy", configureDbContextOptions: OpoStudyDbContext.ApplyConventions);
builder.Services.AddHostedService<GradingWorker>();

var host = builder.Build();
host.Run();