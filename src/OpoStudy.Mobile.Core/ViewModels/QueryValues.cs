namespace OpoStudy.Mobile.Core.ViewModels;

/// <summary>Reads navigation parameters, which Shell may deliver as <see cref="Guid"/> or as text.</summary>
internal static class QueryValues
{
    public static Guid? ReadGuid(IReadOnlyDictionary<string, object> query, string key) =>
        query.TryGetValue(key, out var value)
            ? value switch
            {
                Guid guid => guid,
                string text when Guid.TryParse(text, out var parsed) => parsed,
                _ => null,
            }
            : null;
}