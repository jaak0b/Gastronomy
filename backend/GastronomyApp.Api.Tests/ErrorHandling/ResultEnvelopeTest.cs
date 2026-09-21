using System.Text.Json;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Contracts;
using GastronomyApp.Core.Refusals;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace GastronomyApp.Api.Tests.ErrorHandling;

[TestFixture]
public sealed class ResultEnvelopeTest
{
  private readonly Guid _catalogItemId = Guid.Parse("11111111-1111-1111-1111-111111111111");

  [Test]
  public async Task Refuse_ARefusalCarryingContext_WritesTheMessageKeyAndTheContextAsParameters()
  {
    var written = await AnswerOf(_envelope.Refuse([Refusal.Order.ItemNotAvailable(_catalogItemId, "Bratwurst")]));

    Assert.Multiple(() =>
                    {
                      Assert.That(written.StatusCode, Is.EqualTo(422));
                      Assert.That(written.Error!.Code, Is.EqualTo("UnprocessableEntity"));
                      Assert.That(written.Error.MessageKey, Is.EqualTo("catalog.itemSoldOut"));
                      Assert.That(written.Error.Parameters["catalogItemId"], Is.EqualTo(_catalogItemId.ToString()));
                      Assert.That(written.Error.Parameters["name"], Is.EqualTo("Bratwurst"));
                    });
  }

  [Test]
  public async Task Refuse_ARefusalAnsweredWithAStatusAlone_WritesNoBody()
  {
    var written = await AnswerOf(_envelope.Refuse([Refusal.StaffMember.StaffMemberNotFound(Guid.NewGuid())]));

    Assert.Multiple(() =>
                    {
                      Assert.That(written.StatusCode, Is.EqualTo(404));
                      Assert.That(written.Error, Is.Null);
                    });
  }

  [Test]
  public async Task Refuse_SeveralRefusedLines_AnswersWithTheFirstOne()
  {
    var written = await AnswerOf(_envelope.Refuse([Refusal.Order.UnknownCatalogItemId(_catalogItemId), Refusal.Order.NoRunningFestival()]));

    Assert.Multiple(() =>
                    {
                      Assert.That(written.StatusCode, Is.EqualTo(422));
                      Assert.That(written.Error!.MessageKey, Is.EqualTo("order.unknownItem"));
                    });
  }

  private readonly ResultEnvelope _envelope = new(new(), new HttpContextAccessor(), NullLogger<ResultEnvelope>.Instance);

  private async Task<WrittenAnswer> AnswerOf(IResult result)
  {
    ServiceCollection services = new();
    services.AddLogging();
    DefaultHttpContext context = new() { RequestServices = services.BuildServiceProvider() };
    using MemoryStream body = new();
    context.Response.Body = body;

    await result.ExecuteAsync(context);

    body.Position = 0;

    if (body.Length == 0)
      return new(context.Response.StatusCode, null);

    return new(context.Response.StatusCode, await JsonSerializer.DeserializeAsync<ApiError>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }));
  }

  private sealed record WrittenAnswer(int StatusCode, ApiError? Error);
}
