using System.Net;
using System.Text.Json;
using GastronomyApp.Api.Tests.TestSupport;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class OrderRefusalLoggingTest
{
  [SetUp]
  public async Task SetUp()
  {
    _log = new();
    _context = await new OrderTestContextBuilder().StartAsync();
  }

  [TearDown]
  public async Task TearDown()
  {
    await _context.DisposeAsync();
    _log.Dispose();
  }

  private OrderTestContext _context = null!;
  private RecordedLog _log = null!;

  private readonly Guid _tableThePlanDoesNotHold = Guid.Parse("99999999-0000-0000-0000-000000000001");

  private OrderBody AtATableThePlanDoesNotHold()
  {
    return new(Guid.NewGuid(), _tableThePlanDoesNotHold, [new(_context.World.BratwurstItemId, 350, null, null)]);
  }

  private OrderBody BuildOrderBodyWithAnUnknownItem()
  {
    return new(Guid.NewGuid(), _context.World.TableId, [new(Guid.NewGuid(), 350, null, null)]);
  }

  private async static Task<string> ReadMessageKeyAsync(HttpResponseMessage response)
  {
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    return body.RootElement.GetProperty("messageKey").GetString()!;
  }

  [Test]
  public async Task PostOrder_SomethingTheScreenAlreadyPrevents_AnswersWithTheOneSharedMessage()
  {
    using var response = await _context.PostOrderAsync(AtATableThePlanDoesNotHold());
    var messageKey = await ReadMessageKeyAsync(response);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
                      Assert.That(messageKey, Is.EqualTo("errors.order.cannotBeProcessed"));
                    });
  }

  [Test]
  public async Task PostOrder_SomethingTheScreenAlreadyPrevents_WritesTheExactReasonToTheLog()
  {
    using var response = await _context.PostOrderAsync(AtATableThePlanDoesNotHold());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
                      Assert.That(_log.ReadRenderedMessages(), Has.Some.Contains(_tableThePlanDoesNotHold.ToString()));
                    });
  }

  [Test]
  public async Task PostOrder_ItemThatLeftTheMenu_KeepsTheMessageTheWaiterCanActOn()
  {
    using var response = await _context.PostOrderAsync(BuildOrderBodyWithAnUnknownItem());
    var messageKey = await ReadMessageKeyAsync(response);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
                      Assert.That(messageKey, Is.EqualTo("errors.order.unknownItem"));
                    });
  }
}
