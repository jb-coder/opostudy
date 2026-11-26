// Computes WCAG 2.2 contrast ratios for the palette in Colors.xaml (light and dark).
// Usage: dotnet run tools/a11y/ContrastCheck.cs [-- <path to Colors.xaml>]
// Exit codes: 0 all pairs pass, 1 some pair fails, 2 invalid input or unknown token.
// PublishAot is off: the AOT compiler package is not needed for a local tool.
#:property PublishAot=false
using System.Globalization;
using System.Xml.Linq;

var path = args.Length > 0 ? args[0] : Path.Combine("src", "OpoStudy.Mobile", "Resources", "Styles", "Colors.xaml");
if (!File.Exists(path))
{
    Console.Error.WriteLine($"File not found: {path}");
    return 2;
}

XNamespace x = "http://schemas.microsoft.com/winfx/2009/xaml";
Dictionary<string, string> colors;
try
{
    colors = XDocument.Load(path).Descendants()
        .Where(e => e.Name.LocalName == "Color" && e.Attribute(x + "Key") is not null)
        .ToDictionary(e => e.Attribute(x + "Key")!.Value, e => e.Value.Trim());
}
catch (System.Xml.XmlException ex)
{
    Console.Error.WriteLine($"Invalid XAML: {ex.Message}");
    return 2;
}

// (foreground, background, minimum, kind)
(string Fg, string Bg, double Min, string Kind)[] pairs =
[
    ("OnPrimary", "Primary", 4.5, "Text"),
    ("OnSurface", "Background", 4.5, "Text"),
    ("OnSurface", "Surface", 4.5, "Text"),
    ("OnSurfaceVariant", "Background", 4.5, "Text"),
    ("OnSurfaceVariant", "Surface", 4.5, "Text"),
    ("Primary", "Background", 4.5, "Text"),
    ("Primary", "Surface", 4.5, "Text"),
    ("Success", "Background", 4.5, "Text"),
    ("Success", "Surface", 4.5, "Text"),
    ("Error", "Background", 4.5, "Text"),
    ("Error", "Surface", 4.5, "Text"),
    ("Warning", "Background", 4.5, "Text"),
    ("Warning", "Surface", 4.5, "Text"),
    ("Primary", "Background", 3.0, "Graphic"),
    ("Primary", "Surface", 3.0, "Graphic"),
    ("Success", "Background", 3.0, "Graphic"),
    ("Success", "Surface", 3.0, "Graphic"),
    ("Error", "Background", 3.0, "Graphic"),
    ("Error", "Surface", 3.0, "Graphic"),
    ("Warning", "Background", 3.0, "Graphic"),
    ("Warning", "Surface", 3.0, "Graphic"),
];

var failed = false;
foreach (var kind in new[] { "Text", "Graphic" })
{
    Console.WriteLine(kind == "Text"
        ? "Texto (4,5:1)"
        : "Elementos gráficos y bordes de componentes (3:1)");
    Console.WriteLine();
    Console.WriteLine("| Par (primer plano sobre fondo) | Mínimo | Claro | Oscuro | Resultado |");
    Console.WriteLine("|---|---|---|---|---|");
    foreach (var (fg, bg, min, _) in pairs.Where(p => p.Kind == kind))
    {
        double[] ratios;
        try
        {
            ratios = [Ratio(Get("Light", fg), Get("Light", bg)), Ratio(Get("Dark", fg), Get("Dark", bg))];
        }
        catch (KeyNotFoundException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 2;
        }
        var ok = ratios.All(r => r >= min);
        failed |= !ok;
        Console.WriteLine($"| `{fg}` sobre `{bg}` | {Fmt(min, min % 1 == 0 ? 0 : 1)}:1 | {Fmt(ratios[0], 2)}:1 | {Fmt(ratios[1], 2)}:1 | {(ok ? "OK" : "FALLA")} |");
    }
    Console.WriteLine();
}

return failed ? 1 : 0;

string Get(string theme, string token)
{
    var key = token + theme;
    return colors.TryGetValue(key, out var value)
        ? value
        : throw new KeyNotFoundException($"Unknown color token: {key} (not found in {path})");
}

static string Fmt(double value, int decimals) =>
    value.ToString("F" + decimals, CultureInfo.GetCultureInfo("es-ES"));

static double Ratio(string hexA, string hexB)
{
    var a = Luminance(hexA);
    var b = Luminance(hexB);
    return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
}

static double Luminance(string hex)
{
    var h = hex.TrimStart('#');
    var r = Channel(Convert.ToInt32(h[..2], 16));
    var g = Channel(Convert.ToInt32(h.Substring(2, 2), 16));
    var b = Channel(Convert.ToInt32(h.Substring(4, 2), 16));
    return 0.2126 * r + 0.7152 * g + 0.0722 * b;
}

static double Channel(int value)
{
    var c = value / 255.0;
    return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
}
