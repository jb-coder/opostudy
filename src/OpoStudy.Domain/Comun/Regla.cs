using System.Text.RegularExpressions;

namespace OpoStudy.Domain.Comun;

/// <summary>Validaciones comunes de las entidades.</summary>
internal static partial class Regla
{
    public static string TextoObligatorio(string? valor, int longitudMaxima, string codigo)
    {
        var texto = valor?.Trim();
        if (string.IsNullOrEmpty(texto))
        {
            throw new ReglaDominioException(codigo, $"El campo es obligatorio ({codigo}).");
        }

        if (texto.Length > longitudMaxima)
        {
            throw new ReglaDominioException(codigo, $"El texto supera los {longitudMaxima} caracteres ({codigo}).");
        }

        return texto;
    }

    public static string? TextoOpcional(string? valor, int longitudMaxima, string codigo)
    {
        var texto = valor?.Trim();
        return string.IsNullOrEmpty(texto) ? null : TextoObligatorio(texto, longitudMaxima, codigo);
    }

    public static string Slug(string? valor, string codigo)
    {
        var slug = TextoObligatorio(valor, Limites.Slug, codigo);
        if (!SlugRegex().IsMatch(slug))
        {
            throw new ReglaDominioException(codigo, $"'{slug}' no es un slug válido: minúsculas, números y guiones ({codigo}).");
        }

        return slug;
    }

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex SlugRegex();
}