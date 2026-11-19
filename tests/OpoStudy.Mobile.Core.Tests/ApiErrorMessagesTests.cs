using System.Net;
using System.Net.Sockets;
using System.Text;

using OpoStudy.Mobile.Core.Api;

using Refit;

using Shouldly;

namespace OpoStudy.Mobile.Core.Tests;

public sealed class ApiErrorMessagesTests
{
    [Fact]
    public async Task From_ProblemDetailsWithDetail_ReturnsDetail()
    {
        var exception = await CreateApiExceptionAsync(
            HttpStatusCode.BadRequest, """{"title":"Solicitud no válida","detail":"El tema no existe."}""");

        ApiErrorMessages.From(exception).ShouldBe("El tema no existe.");
    }

    [Fact]
    public async Task From_ProblemDetailsWithOnlyTitle_ReturnsTitle()
    {
        var exception = await CreateApiExceptionAsync(HttpStatusCode.NotFound, """{"title":"No encontrado"}""");

        ApiErrorMessages.From(exception).ShouldBe("No encontrado");
    }

    [Fact]
    public async Task From_ApiExceptionWithoutBody_ReturnsServerError()
    {
        var exception = await CreateApiExceptionAsync(HttpStatusCode.InternalServerError, null);

        ApiErrorMessages.From(exception).ShouldBe(ApiErrorMessages.ServerError);
    }

    [Fact]
    public async Task From_ApiExceptionWithNonJsonBody_ReturnsServerError()
    {
        var exception = await CreateApiExceptionAsync(HttpStatusCode.BadGateway, "<html>Bad gateway</html>");

        ApiErrorMessages.From(exception).ShouldBe(ApiErrorMessages.ServerError);
    }

    [Fact]
    public void From_HttpRequestException_ReturnsUnreachable() =>
        ApiErrorMessages.From(new HttpRequestException()).ShouldBe(ApiErrorMessages.Unreachable);

    [Fact]
    public void From_TaskCanceledException_ReturnsTimeout() =>
        ApiErrorMessages.From(new TaskCanceledException()).ShouldBe(ApiErrorMessages.Timeout);

    [Fact]
    public void From_TimeoutException_ReturnsTimeout() =>
        ApiErrorMessages.From(new TimeoutException()).ShouldBe(ApiErrorMessages.Timeout);

    [Fact]
    public void From_OtherException_ReturnsUnexpected() =>
        ApiErrorMessages.From(new InvalidOperationException("secret detail")).ShouldBe(ApiErrorMessages.Unexpected);

    [Fact]
    public void From_TaskCanceledWrappedInOtherException_ReturnsTimeout() =>
        ApiErrorMessages.From(new InvalidOperationException("wrapper", new TaskCanceledException("x", new TimeoutException())))
            .ShouldBe(ApiErrorMessages.Timeout);

    [Fact]
    public void From_HttpRequestExceptionWrappedByOther_ReturnsUnreachable() =>
        ApiErrorMessages.From(new InvalidOperationException("wrapper", new HttpRequestException()))
            .ShouldBe(ApiErrorMessages.Unreachable);

    [Fact]
    public void From_InnerSocketException_ReturnsUnreachable() =>
        ApiErrorMessages.From(new InvalidOperationException("wrapper", new SocketException()))
            .ShouldBe(ApiErrorMessages.Unreachable);

    [Fact]
    public void From_InnerIOException_ReturnsUnreachable() =>
        ApiErrorMessages.From(new InvalidOperationException("wrapper", new IOException("connection reset")))
            .ShouldBe(ApiErrorMessages.Unreachable);

    [Fact]
    public void From_AggregateWithConnectionError_ReturnsUnreachable() =>
        ApiErrorMessages.From(new AggregateException(new InvalidOperationException(), new HttpRequestException()))
            .ShouldBe(ApiErrorMessages.Unreachable);

    [Fact]
    public void From_AggregateWithUnknownErrors_ReturnsUnexpected() =>
        ApiErrorMessages.From(new AggregateException(new InvalidOperationException())).ShouldBe(ApiErrorMessages.Unexpected);

    private static async Task<ApiException> CreateApiExceptionAsync(HttpStatusCode status, string? body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/api/v1/version");
        using var response = new HttpResponseMessage(status);
        if (body is not null)
        {
            response.Content = new StringContent(body, Encoding.UTF8, "application/problem+json");
        }

        return await ApiException.Create(request, HttpMethod.Get, response, new RefitSettings());
    }
}