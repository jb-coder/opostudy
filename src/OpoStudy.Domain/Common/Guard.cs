using System.Text.RegularExpressions;

namespace OpoStudy.Domain.Common;

/// <summary>Common validations shared by the entities.</summary>
internal static partial class Guard
{
    public static string RequiredText(string? value, int maxLength, string code)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            throw new DomainRuleException(code, $"El campo es obligatorio ({code}).");
        }

        if (text.Length > maxLength)
        {
            throw new DomainRuleException(code, $"El texto supera los {maxLength} caracteres ({code}).");
        }

        return text;
    }

    public static string? OptionalText(string? value, int maxLength, string code)
    {
        var text = value?.Trim();
        return string.IsNullOrEmpty(text) ? null : RequiredText(text, maxLength, code);
    }

    public static string Slug(string? value, string code)
    {
        var slug = RequiredText(value, Limits.Slug, code);
        if (!SlugRegex().IsMatch(slug))
        {
            throw new DomainRuleException(code, $"'{slug}' no es un slug válido: minúsculas, números y guiones ({code}).");
        }

        return slug;
    }

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex SlugRegex();
}