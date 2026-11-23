using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace OpoStudy.Infrastructure.Gradings.Gemini;

// REST DTOs of generateContent. Only the fields we use; serialized with camelCase names.

internal sealed record GeminiRequest(
    [property: JsonPropertyName("systemInstruction")] GeminiContent SystemInstruction,
    [property: JsonPropertyName("contents")] IReadOnlyList<GeminiContent> Contents,
    [property: JsonPropertyName("generationConfig")] GeminiGenerationConfig GenerationConfig);

internal sealed record GeminiContent(
    [property: JsonPropertyName("role"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Role,
    [property: JsonPropertyName("parts")] IReadOnlyList<GeminiPart>? Parts);

internal sealed record GeminiPart(
    [property: JsonPropertyName("text")] string? Text,
    [property: JsonPropertyName("thought"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? Thought = null);

internal sealed record GeminiGenerationConfig(
    [property: JsonPropertyName("responseMimeType")] string ResponseMimeType,
    [property: JsonPropertyName("responseJsonSchema")] JsonNode ResponseJsonSchema,
    [property: JsonPropertyName("maxOutputTokens")] int MaxOutputTokens,
    [property: JsonPropertyName("thinkingConfig")] GeminiThinkingConfig ThinkingConfig);

internal sealed record GeminiThinkingConfig(
    [property: JsonPropertyName("thinkingLevel")] string ThinkingLevel);

internal sealed record GeminiResponse(
    [property: JsonPropertyName("candidates")] IReadOnlyList<GeminiCandidate>? Candidates,
    [property: JsonPropertyName("promptFeedback")] GeminiPromptFeedback? PromptFeedback,
    [property: JsonPropertyName("usageMetadata")] GeminiUsage? UsageMetadata,
    [property: JsonPropertyName("modelVersion")] string? ModelVersion);

internal sealed record GeminiCandidate(
    [property: JsonPropertyName("content")] GeminiContent? Content,
    [property: JsonPropertyName("finishReason")] string? FinishReason);

internal sealed record GeminiPromptFeedback(
    [property: JsonPropertyName("blockReason")] string? BlockReason);

internal sealed record GeminiUsage(
    [property: JsonPropertyName("promptTokenCount")] int? PromptTokenCount,
    [property: JsonPropertyName("candidatesTokenCount")] int? CandidatesTokenCount,
    [property: JsonPropertyName("thoughtsTokenCount")] int? ThoughtsTokenCount);

internal sealed record GeminiErrorEnvelope(
    [property: JsonPropertyName("error")] GeminiError? Error);

internal sealed record GeminiError(
    [property: JsonPropertyName("status")] string? Status);

// What the model returns (shape fixed by GeminiSchema) and what we send as the user message.

internal sealed record GeminiGradingResult(
    [property: JsonPropertyName("feedback")] string? Feedback,
    [property: JsonPropertyName("parts")] IReadOnlyList<GeminiGradingPart>? Parts);

internal sealed record GeminiGradingPart(
    [property: JsonPropertyName("feedback")] string? Feedback,
    [property: JsonPropertyName("awarded")] IReadOnlyList<decimal>? Awarded);

internal sealed record GeminiPayload(
    [property: JsonPropertyName("caseStatement")] string CaseStatement,
    [property: JsonPropertyName("parts")] IReadOnlyList<GeminiPayloadPart> Parts);

internal sealed record GeminiPayloadPart(
    [property: JsonPropertyName("order")] int Order,
    [property: JsonPropertyName("statement")] string Statement,
    [property: JsonPropertyName("modelAnswer")] string ModelAnswer,
    [property: JsonPropertyName("criteria")] IReadOnlyList<GeminiPayloadCriterion> Criteria,
    [property: JsonPropertyName("answer")] string Answer);

internal sealed record GeminiPayloadCriterion(
    [property: JsonPropertyName("index")] int Index,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("points")] int Points);