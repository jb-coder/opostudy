using System.Text.Json.Nodes;

namespace OpoStudy.Infrastructure.Gradings.Gemini;

/// <summary>Builds the JSON schema of the output for one request, so the model gets the exact number of parts and criteria and the maximum of each.</summary>
internal static class GeminiSchema
{
    public static JsonObject For(GradingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var parts = new JsonArray();
        foreach (var part in request.Parts)
        {
            var awarded = new JsonArray();
            foreach (var criterion in part.Criteria)
            {
                awarded.Add(new JsonObject
                {
                    ["type"] = "number",
                    ["minimum"] = 0,
                    ["maximum"] = criterion.Points,
                });
            }

            parts.Add(new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["feedback"] = new JsonObject { ["type"] = "string" },
                    ["awarded"] = new JsonObject
                    {
                        ["type"] = "array",
                        ["minItems"] = part.Criteria.Count,
                        ["maxItems"] = part.Criteria.Count,
                        ["prefixItems"] = awarded,
                    },
                },
                ["required"] = new JsonArray("feedback", "awarded"),
            });
        }

        return new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["feedback"] = new JsonObject { ["type"] = "string" },
                ["parts"] = new JsonObject
                {
                    ["type"] = "array",
                    ["minItems"] = request.Parts.Count,
                    ["maxItems"] = request.Parts.Count,
                    ["prefixItems"] = parts,
                },
            },
            ["required"] = new JsonArray("feedback", "parts"),
        };
    }
}