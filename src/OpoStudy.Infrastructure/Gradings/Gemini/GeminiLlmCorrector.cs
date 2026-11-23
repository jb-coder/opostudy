using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using OpoStudy.Domain.Common;
using OpoStudy.Domain.Gradings;

namespace OpoStudy.Infrastructure.Gradings.Gemini;

/// <summary>
/// Corrects a practical case with the Gemini REST API (no SDK). One HTTP call per try: the timeout is set by
/// <see cref="GradingProcessor"/> and the retries by the queue. Never log the prompt, the answers, the comments or the key.
/// </summary>
public sealed partial class GeminiLlmCorrector(
    HttpClient httpClient,
    IOptions<GeminiOptions> options,
    ILogger<GeminiLlmCorrector> logger) : ILlmCorrector
{
    private const string ApiKeyHeader = "x-goog-api-key";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly GeminiOptions _options = options.Value;

    public async Task<GradingOutcome> GradeAsync(GradingRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var body = new GeminiRequest(
            new GeminiContent(null, [new GeminiPart(GeminiPrompt.SystemInstruction)]),
            [new GeminiContent("user", [new GeminiPart(GeminiPrompt.BuildPayload(request))])],
            new GeminiGenerationConfig(
                "application/json",
                GeminiSchema.For(request),
                _options.MaxOutputTokens,
                new GeminiThinkingConfig(_options.ThinkingLevel)));

        using var message = new HttpRequestMessage(HttpMethod.Post, new Uri($"models/{_options.Model}:generateContent", UriKind.Relative))
        {
            Content = JsonContent.Create(body, options: JsonOptions),
        };
        message.Headers.Add(ApiKeyHeader, _options.ApiKey);

        var startedAt = Stopwatch.GetTimestamp();
        string responseText;
        HttpStatusCode statusCode;
        try
        {
            // An OperationCanceledException (timeout of the processor or shutdown) is not caught: the processor handles it.
            using var response = await httpClient.SendAsync(message, cancellationToken);
            statusCode = response.StatusCode;
            responseText = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode is null)
        {
            throw new LlmTransientException("Network error calling Gemini.", ex);
        }

        try
        {
            if ((int)statusCode is < 200 or > 299)
            {
                throw MapHttpError(statusCode, responseText);
            }

            return ParseOutcome(request, responseText, Stopwatch.GetElapsedTime(startedAt));
        }
        catch (GeminiResponseException ex)
        {
            // The exception holds only the codes and a short reason, so it is safe to log.
            LogRejected(logger, ex.HttpStatusCode, ex.GoogleStatus, ex.Reason ?? ex.Message);
            throw;
        }
    }

    private static Exception MapHttpError(HttpStatusCode statusCode, string responseText)
    {
        var googleStatus = TryReadGoogleStatus(responseText);
        var code = (int)statusCode;
        if (code is 408 or 429 or 500 or 502 or 503 or 504)
        {
            return new LlmTransientException($"Gemini unavailable (http={code}, status={googleStatus ?? "-"}).");
        }

        return new GeminiResponseException(code, googleStatus, "request rejected");
    }

    private static string? TryReadGoogleStatus(string responseText)
    {
        try
        {
            var status = JsonSerializer.Deserialize<GeminiErrorEnvelope>(responseText, JsonOptions)?.Error?.Status;
            // The status is a short enum-like word; anything else is not worth keeping.
            return status is { Length: > 0 and <= 40 } && status.All(c => char.IsAsciiLetterUpper(c) || c == '_') ? status : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private GradingOutcome ParseOutcome(GradingRequest request, string responseText, TimeSpan elapsed)
    {
        GeminiResponse? response;
        try
        {
            response = JsonSerializer.Deserialize<GeminiResponse>(responseText, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new GeminiResponseException("The response is not valid JSON.", ex);
        }

        if (response is null)
        {
            throw new GeminiResponseException("The response is empty.");
        }

        var candidate = response.Candidates is { Count: > 0 } candidates ? candidates[0] : null;
        LogCall(
            logger,
            _options.Model,
            response.ModelVersion,
            candidate?.FinishReason,
            response.UsageMetadata?.PromptTokenCount,
            response.UsageMetadata?.CandidatesTokenCount,
            response.UsageMetadata?.ThoughtsTokenCount,
            (long)elapsed.TotalMilliseconds);

        if (response.PromptFeedback?.BlockReason is { Length: > 0 } blockReason)
        {
            throw new GeminiResponseException(null, null, $"prompt blocked ({Sanitize(blockReason)})");
        }

        if (candidate is null)
        {
            throw new GeminiResponseException(null, null, "no candidates");
        }

        if (!string.Equals(candidate.FinishReason, "STOP", StringComparison.Ordinal))
        {
            throw new GeminiResponseException(null, null, $"finish:{Sanitize(candidate.FinishReason ?? "none")}");
        }

        var text = JoinText(candidate);
        if (text.Length == 0)
        {
            throw new GeminiResponseException(null, null, "no text in the candidate");
        }

        GeminiGradingResult? result;
        try
        {
            result = JsonSerializer.Deserialize<GeminiGradingResult>(text, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new GeminiResponseException("The model output is not valid JSON.", ex);
        }

        return ToOutcome(request, result, response);
    }

    private GradingOutcome ToOutcome(GradingRequest request, GeminiGradingResult? result, GeminiResponse response)
    {
        if (result?.Parts is null || result.Parts.Count != request.Parts.Count)
        {
            throw new GeminiResponseException(null, null, "the number of parts does not match");
        }

        var parts = new List<PartOutcome>(request.Parts.Count);
        for (var i = 0; i < request.Parts.Count; i++)
        {
            var criteria = request.Parts[i].Criteria;
            var awarded = result.Parts[i].Awarded;
            if (awarded is null || awarded.Count != criteria.Count)
            {
                throw new GeminiResponseException(null, null, "the number of criteria does not match");
            }

            var points = awarded
                .Select((value, j) => Math.Clamp(Math.Round(value * 2m, MidpointRounding.AwayFromZero) / 2m, 0m, criteria[j].Points))
                .ToList();
            parts.Add(new PartOutcome(points, Truncate(result.Parts[i].Feedback, Limits.GradingFeedback)));
        }

        var usage = response.UsageMetadata;
        var inputTokens = usage?.PromptTokenCount;
        int? outputTokens = usage is null ? null : (usage.CandidatesTokenCount ?? 0) + (usage.ThoughtsTokenCount ?? 0);
        var corrector = Truncate("gemini:" + (response.ModelVersion ?? _options.Model), Limits.Corrector);

        return new GradingOutcome(parts, Truncate(result.Feedback, Limits.GradingFeedback), corrector, inputTokens, outputTokens);
    }

    private static string JoinText(GeminiCandidate candidate)
    {
        var builder = new StringBuilder();
        foreach (var part in candidate.Content?.Parts ?? [])
        {
            if (part.Thought != true && part.Text is not null)
            {
                builder.Append(part.Text);
            }
        }

        return builder.ToString();
    }

    private static string Truncate(string? text, int maxLength)
    {
        var trimmed = (text ?? string.Empty).Trim();
        return trimmed.Length <= maxLength ? trimmed : string.Concat(trimmed.AsSpan(0, maxLength - 1).TrimEnd(), "…");
    }

    /// <summary>Reasons come from Google as short enum words; keep only that so nothing else can end in an exception or a log.</summary>
    private static string Sanitize(string value) =>
        value.Length <= 40 && value.All(c => char.IsAsciiLetterOrDigit(c) || c == '_') ? value : "UNKNOWN";

    [LoggerMessage(Level = LogLevel.Information, Message = "Gemini call: model={Model} modelVersion={ModelVersion} finishReason={FinishReason} inputTokens={InputTokens} outputTokens={OutputTokens} thoughtTokens={ThoughtTokens} durationMs={DurationMs}")]
    private static partial void LogCall(ILogger logger, string model, string? modelVersion, string? finishReason, int? inputTokens, int? outputTokens, int? thoughtTokens, long durationMs);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Gemini rejected the correction: http={HttpStatus} status={Status} reason={Reason}")]
    private static partial void LogRejected(ILogger logger, int? httpStatus, string? status, string reason);
}