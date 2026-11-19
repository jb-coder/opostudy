using OpoStudy.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddHostedService<CorreccionesWorker>();

var host = builder.Build();
host.Run();
