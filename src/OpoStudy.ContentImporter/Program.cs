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
Console.WriteLine($"Contenido válido: {read.Exams.Count} oposición(es), {topicCount} tema(s), {questionCount} pregunta(s).");
if (validateOnly)
{
    return Ok;
}

try
{
    // No command-line args: the connection string comes from Aspire (environment) or user-secrets, never from here.
    var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { Args = [] });
    builder.AddServiceDefaults();
    builder.AddNpgsqlDbContext<OpoStudyDbContext>(
        "opostudy", configureDbContextOptions: OpoStudyDbContext.ApplyConventions);
    builder.Services.AddContentImport();

    using var host = builder.Build();
    using var scope = host.Services.CreateScope();
    var importer = scope.ServiceProvider.GetRequiredService<ContentImporter>();
    var report = await importer.ImportAsync(read.Exams, dryRun, CancellationToken.None);

    Console.WriteLine(dryRun ? "Simulación (--dry-run): no se ha guardado nada." : "Importación completada.");
    PrintCounts("Oposiciones", report.Exams);
    PrintCounts("Temas", report.Topics);
    PrintCounts("Preguntas", report.Questions);
    foreach (var warning in report.Warnings)
    {
        Console.WriteLine($"Aviso: {warning}");
    }

    return Ok;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"La importación ha fallado: {ex.Message}");
    return ImportFailed;
}

static void PrintCounts(string label, ImportCounts counts) =>
    Console.WriteLine(
        $"  {label}: {counts.Created} altas, {counts.Updated} actualizaciones, {counts.Deactivated} desactivaciones, {counts.Reactivated} reactivaciones");

static void PrintUsage() =>
    Console.Error.WriteLine("Uso: OpoStudy.ContentImporter --content <ruta> [--dry-run] [--validate-only]");