// Audits a `uiautomator dump` file: finds interactive nodes without an accessible name
// ("unlabeled") and touch targets smaller than 48 dp ("small-target").
// Usage: dotnet run tools/a11y/AuditUiDump.cs -- <ui.xml> --density <dpi> [--package <applicationId>]
// Exit codes: 0 no findings, 1 findings, 2 invalid file or arguments.
// PublishAot is off: the AOT compiler package is not needed for a local tool.
#:property PublishAot=false
using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

const double MinDp = 48;
const double RoundingToleranceDp = 0.5;

string? file = null;
double? density = null;
string? package = null;
for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--density" when i + 1 < args.Length:
            if (!double.TryParse(args[++i], NumberStyles.Float, CultureInfo.InvariantCulture, out var d) || d <= 0)
            {
                return Usage("--density must be a positive number");
            }

            density = d;
            break;
        case "--package" when i + 1 < args.Length:
            package = args[++i];
            break;
        default:
            if (file is not null || args[i].StartsWith("--", StringComparison.Ordinal))
            {
                return Usage($"Unexpected argument: {args[i]}");
            }

            file = args[i];
            break;
    }
}

if (file is null || density is null)
{
    return Usage("The dump path and --density are required");
}

package ??= ReadApplicationId();

XDocument doc;
try
{
    doc = XDocument.Load(file);
}
catch (Exception ex) when (ex is XmlException or IOException)
{
    Console.Error.WriteLine($"Invalid dump: {ex.Message}");
    return 2;
}

var nodes = doc.Descendants("node").ToList();
if (nodes.Count == 0)
{
    Console.Error.WriteLine("Invalid dump: it has no <node> elements");
    return 2;
}

var findings = new List<string>();
var audited = 0;
foreach (var node in nodes.Where(n => package is null || (string?)n.Attribute("package") == package))
{
    if (!IsInteractive(node))
    {
        continue;
    }

    audited++;
    if (!HasLabel(node))
    {
        findings.Add(Describe("unlabeled", node, density.Value));
    }

    if (IsSmall(node, density.Value))
    {
        findings.Add(Describe("small-target", node, density.Value));
    }
}

foreach (var line in findings)
{
    Console.WriteLine(line);
}

Console.WriteLine($"Summary: {audited} interactive nodes audited (package {package ?? "any"}), {findings.Count} findings.");
return findings.Count == 0 ? 0 : 1;

static int Usage(string message)
{
    Console.Error.WriteLine(message);
    Console.Error.WriteLine("Usage: dotnet run tools/a11y/AuditUiDump.cs -- <ui.xml> --density <dpi> [--package <applicationId>]");
    return 2;
}

static string? ReadApplicationId()
{
    var csproj = Path.Combine("src", "OpoStudy.Mobile", "OpoStudy.Mobile.csproj");
    return File.Exists(csproj)
        ? XDocument.Load(csproj).Descendants("ApplicationId").FirstOrDefault()?.Value.Trim()
        : null;
}

static bool IsTrue(XElement node, string attribute) =>
    string.Equals((string?)node.Attribute(attribute), "true", StringComparison.OrdinalIgnoreCase);

static bool IsInteractive(XElement node) =>
    IsTrue(node, "clickable") || IsTrue(node, "long-clickable") || IsTrue(node, "checkable");

static bool HasName(XElement node) =>
    !string.IsNullOrWhiteSpace((string?)node.Attribute("text"))
    || !string.IsNullOrWhiteSpace((string?)node.Attribute("content-desc"));

// A node is labeled if it, or any descendant, has text or content-desc.
static bool HasLabel(XElement node) => HasName(node) || node.Descendants("node").Any(HasName);

// Returns the node size in dp, or null when the bounds are missing or malformed.
static (double Width, double Height)? SizeDp(XElement node, double density)
{
    var m = Regex.Match((string?)node.Attribute("bounds") ?? string.Empty, @"^\[(-?\d+),(-?\d+)\]\[(-?\d+),(-?\d+)\]$");
    if (!m.Success)
    {
        return null;
    }

    int V(int g) => int.Parse(m.Groups[g].Value, CultureInfo.InvariantCulture);
    return ((V(3) - V(1)) * 160.0 / density, (V(4) - V(2)) * 160.0 / density);
}

static bool IsSmall(XElement node, double density) =>
    SizeDp(node, density) is { } s && (s.Width < MinDp - RoundingToleranceDp || s.Height < MinDp - RoundingToleranceDp);

static string Describe(string rule, XElement node, double density)
{
    var name = (string?)node.Attribute("text");
    if (string.IsNullOrWhiteSpace(name))
    {
        name = (string?)node.Attribute("content-desc");
    }

    var size = SizeDp(node, density);
    var dp = size is { } s
        ? string.Create(CultureInfo.InvariantCulture, $"{s.Width:F1}x{s.Height:F1} dp")
        : "? dp";
    return $"{rule}\tclass={(string?)node.Attribute("class")}\tresource-id={(string?)node.Attribute("resource-id")}"
        + $"\tname=\"{name}\"\tbounds={(string?)node.Attribute("bounds")}\tsize={dp}";
}
