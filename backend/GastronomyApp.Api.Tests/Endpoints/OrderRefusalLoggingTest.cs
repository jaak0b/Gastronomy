using System.Net;
using System.Text.Json;
using GastronomyApp.Api.Tests.Logging;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class OrderRefusalLoggingTest
{

  [SetUp]
  public async Task SetUp()
  {
    _log = new();
    _context = await new OrderTestContext.Builder().StartAsync();
  }

  [TearDown]
  public async Task TearDown()
  {
    await _context.DisposeAsync();
    _log.Dispose();
  }

  private OrderTestContext _context = null!;
  private RecordedLog _log = null!;

  private OrderBody WithoutATableName()
  {
    return new(Guid.NewGuid(),
               string.Empty,
               null,
               [new(_context.World.BratwurstItemId, 350, null, null)]);
  }

  private OrderBody WithAnItemTheLaptopNeverHeardOf()
  {
    return new(Guid.NewGuid(),
               "Tisch 12",
               null,
               [new(Guid.NewGuid(), 350, null, null)]);
  }

  private static async Task<string> MessageKeyOfAsync(HttpResponseMessage response)
  {
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    return body.RootElement.GetProperty("messageKey").GetString()!;
  }

  [Test]
  public async Task PostOrder_SomethingTheScreenAlreadyPrevents_AnswersWithTheOneSharedMessage()
  {
    using var response = await _context.PostOrderAsync(WithoutATableName());
    var messageKey = await MessageKeyOfAsync(response);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                      Assert.That(messageKey, Is.EqualTo("order.cannotBeProcessed"));
                    });
  }

  [Test]
  public async Task PostOrder_SomethingTheScreenAlreadyPrevents_WritesTheExactReasonToTheLog()
  {
    using var response = await _context.PostOrderAsync(WithoutATableName());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                      Assert.That(_log.RenderedMessages, Has.Some.Contains("TableNameMissing"));
                    });
  }

  [Test]
  public async Task PostOrder_ItemThatLeftTheMenu_KeepsTheMessageTheWaiterCanActOn()
  {
    using var response = await _context.PostOrderAsync(WithAnItemTheLaptopNeverHeardOf());
    var messageKey = await MessageKeyOfAsync(response);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
                      Assert.That(messageKey, Is.EqualTo("order.unknownItem"));
                    });
  }
}
