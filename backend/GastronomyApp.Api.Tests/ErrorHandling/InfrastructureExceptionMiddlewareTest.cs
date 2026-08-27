using System.Text.Json;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace GastronomyApp.Api.Tests.ErrorHandling;

[TestFixture]
public sealed class InfrastructureExceptionMiddlewareTest
{
    [Test]
    public async Task InvokeAsync_DatabaseUnavailable_WritesTheServiceUnavailableEnvelope()
    {
        InfrastructureExceptionMiddleware middleware = new(new ResultEnvelope());
        DefaultHttpContext context = new() { RequestServices = BuildRequestServices() };
        using MemoryStream body = new();
        context.Response.Body = body;

        await middleware.InvokeAsync(
            context,
            _ => throw new InfrastructureException(
                InfrastructureFailureReason.DatabaseUnavailable,
                "The database file could not be written."));

        body.Position = 0;
        ApiError? error = await JsonSerializer.DeserializeAsync<ApiError>(
            body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.Multiple(() =>
        {
            Assert.That(context.Response.StatusCode, Is.EqualTo(503));
            Assert.That(error!.Code, Is.EqualTo("DatabaseUnavailable"));
            Assert.That(error.MessageKey, Is.EqualTo("review.sendFailedDatabase"));
        });
    }

    [Test]
    public async Task InvokeAsync_ConflictingChange_WritesARetryableConflictEnvelope()
    {
        InfrastructureExceptionMiddleware middleware = new(new ResultEnvelope());
        DefaultHttpContext context = new() { RequestServices = BuildRequestServices() };
        using MemoryStream body = new();
        context.Response.Body = body;

        await middleware.InvokeAsync(
            context,
            _ => throw new InfrastructureException(
                InfrastructureFailureReason.ConflictingChange,
                "Another write reached the same unique row first."));

        body.Position = 0;
        ApiError? error = await JsonSerializer.DeserializeAsync<ApiError>(
            body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.Multiple(() =>
        {
            Assert.That(context.Response.StatusCode, Is.EqualTo(409));
            Assert.That(error!.Code, Is.EqualTo("ConflictingChange"));
            Assert.That(error.MessageKey, Is.EqualTo("review.conflictingChange"));
        });
    }

    [Test]
    public void InvokeAsync_UnclassifiedException_RethrowsIt()
    {
        InfrastructureExceptionMiddleware middleware = new(new ResultEnvelope());
        DefaultHttpContext context = new();

        Assert.That(
            async () => await middleware.InvokeAsync(context, _ => throw new InvalidOperationException("unclassified")),
            Throws.TypeOf<InvalidOperationException>());
    }

    private ServiceProvider BuildRequestServices()
    {
        ServiceCollection services = new();
        services.AddLogging();
        return services.BuildServiceProvider();
    }
}
