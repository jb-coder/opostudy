using OpoStudy.Infrastructure.Persistencia;
using OpoStudy.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<OpoStudyDbContext>(
    "opostudy", configureDbContextOptions: OpoStudyDbContext.AplicarConvenciones);
builder.Services.AddHostedService<CorreccionesWorker>();

var host = builder.Build();
host.Run();
