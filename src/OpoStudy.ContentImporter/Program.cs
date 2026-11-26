using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using OpoStudy.Infrastructure.Content;
using OpoStudy.Infrastructure.Persistence;

// Exit codes: 0 = OK, 1 = invalid content (or wrong usage), 2 = the import failed.
const int Ok = 0;
const int InvalidContent = 1;
const int ImportFailed = 2;

string? contentPath = null;
var dryRun = false;
var validateOnly = false;
var verbose = false;
for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--content" when i + 1 < args.Length:
            contentPath = args[++i];
            break;
        case "--dry-run":
            dryRun = true;
            break;
        case "--validate-only":
            validateOnly = true;
            break;
        case "--verbose":
            verbose = true;
            break;
        default:
            Console.Error.WriteLine($"Argumento no reconocido o incompleto: {args[i]}");
            PrintUsage();
            return InvalidContent;
    }
}

if (string.IsNullOrWhiteSpace(contentPath))
{
    PrintUsage();
    return InvalidContent;
}

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true; // let the import roll back cleanly instead of killing the process
    cts.Cancel();
};

try
{
    var read = ContentReader.Read(contentPath);
    if (read.Errors.Count > 0)
    {
        Console.Error.WriteLine($"Contenido no válido: {read.Errors.Count} error(es).");
        foreach (var error in read.Errors)
        {
            Console.Error.WriteLine(error);
        }

        return InvalidContent;
    }

    var topicCount = read.Exams.Sum(e => e.Topics.Count);
    var questionCount = read.Exams.Sum(e => e.Topics.Sum(t => t.Questions.Count));
    var caseCount = read.Exams.Sum(e => e.PracticalCases.Count);
    Console.WriteLine($"Contenido válido: {read.Exams.Count} oposición(es), {topicCount} tema(s), {questionCount} pregunta(s), {caseCount} supuesto(s).");
    if (validateOnly)
    {
        return Ok;
    }

    // No command-line args: the connection string comes from Aspire (environment) or user-secrets, never from here.
    var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { Args = [] });
    builder.AddServiceDefaults();
    builder.AddNpgsqlDbContext<OpoStudyDbContext>(
        "opostudy", configureDbContextOptions: OpoStudyDbContext.ApplyConventions);
    builder.Services.AddContentImport();

    using var host = builder.Build();
    using var scope = host.Services.CreateScope();
    var importer = scope.ServiceProvider.GetRequiredService<ContentImporter>();
    var report = await importer.ImportAsync(read.Exams, dryRun, cts.Token);

    Console.WriteLine(dryRun ? "Simulación (--dry-run): no se ha guardado nada." : "Importación completada.");
    PrintCounts("Oposiciones", report.Exams);
    PrintCounts("Temas", report.Topics);
    PrintCounts("Preguntas", report.Questions);
    PrintCounts("Supuestos", report.PracticalCases);
    if (report.Exercises is not null)
    {
        PrintCounts("Ejercicios", report.Exercises);
    }
    foreach (var warning in report.Warnings)
    {
        Console.WriteLine($"Aviso: {warning}");
    }

    return Ok;
}
catch (OperationCanceledException) when (cts.IsCancellationRequested)
{
    Console.Error.WriteLine("Operación cancelada: no se ha guardado nada.");
    return ImportFailed;
}
catch (Exception ex)
{
    Console.Error.WriteLine("La importación ha fallado:");
    PrintException(ex, verbose);
    return ImportFailed;
}

static void PrintException(Exception ex, bool verbose)
{
    // One line per level of the InnerException chain; the stack trace only with --verbose.
    for (var current = ex; current is not null; current = current.InnerException)
    {
        Console.Error.WriteLine($"  {current.GetType().Name}: {current.Message}");
        if (verbose)
        {
            Console.Error.WriteLine(current.StackTrace);
        }
    }
}

static void PrintCounts(string label, ImportCounts counts) =>
    Console.WriteLine(
        $"  {label}: {counts.Created} altas, {counts.Updated} actualizaciones, {counts.Deactivated} desactivaciones, {counts.Reactivated} reactivaciones");

static void PrintUsage() =>
    Console.Error.WriteLine("Uso: OpoStudy.ContentImporter --content <ruta> [--dry-run] [--validate-only] [--verbose]");