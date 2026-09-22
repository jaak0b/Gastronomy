using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Api.Tests.TestSupport;
using GastronomyApp.Contracts;
using GastronomyApp.Contracts.Admin.Catalog;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GastronomyApp.Api.Tests.ErrorHandling;

[TestFixture]
public sealed class RequestShapeRefusalWriterTest
{
  [SetUp]
  public async Task SetUp()
  {
    _context = await new OrderTestContextBuilder().StartAsync();
  }

  [TearDown]
  public async Task TearDown()
  {
    await _context.DisposeAsync();
  }

  private OrderTestContext _context = null!;

  private async static Task AssertRefusedWithAsync(HttpResponseMessage response, string expectedMessageKey)
  {
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                      Assert.That(body.RootElement.GetProperty("code").GetString(), Is.EqualTo("ValidationFailed"));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(), Is.EqualTo(expectedMessageKey));
                    });
  }

  private object BuildOrderWith(object items)
  {
    return new
    {
      clientOrderId = Guid.NewGuid(),
      tableName = "Tisch 12",
      items
    };
  }

  [Test]
  public async Task PostOrder_TableNameOfOnlySpaces_IsRefusedWithTheOrderMessage()
  {
    using var response = await _context.SendAsync(HttpMethod.Post,
                                                  "/api/orders",
                                                  new
                                                  {
                                                    clientOrderId = Guid.NewGuid(),
                                                    tableName = "   ",
                                                    items = new[]
                                                            {
                                                              new
                                                              {
                                                                catalogItemId = _context.World.BratwurstItemId,
                                                                unitPriceCents = 350
                                                              }
                                                            }
                                                  });

    await AssertRefusedWithAsync(response, "order.cannotBeProcessed");
  }

  [Test]
  public async Task PostOrder_NoItems_IsRefusedWithTheOrderMessage()
  {
    using var response = await _context.SendAsync(HttpMethod.Post, "/api/orders", BuildOrderWith(Array.Empty<object>()));

    await AssertRefusedWithAsync(response, "order.cannotBeProcessed");
  }

  [Test]
  public async Task PostOrder_NegativeUnitPrice_IsRefusedWithTheOrderMessage()
  {
    using var response = await _context.SendAsync(HttpMethod.Post,
                                                  "/api/orders",
                                                  BuildOrderWith(new[]
                                                                 {
                                                                   new
                                                                   {
                                                                     catalogItemId = _context.World.BratwurstItemId,
                                                                     unitPriceCents = -1
                                                                   }
                                                                 }));

    await AssertRefusedWithAsync(response, "order.cannotBeProcessed");
  }

  [Test]
  public async Task PostOrder_SettlementWithoutAnAmount_IsRefusedWithTheSettlementMessage()
  {
    using var response = await _context.SendAsync(HttpMethod.Post,
                                                  "/api/orders",
                                                  BuildOrderWith(new[]
                                                                 {
                                                                   new
                                                                   {
                                                                     catalogItemId = _context.World.BratwurstItemId,
                                                                     unitPriceCents = 350,
                                                                     settlement = new { paidPriceCents = (int?)null }
                                                                   }
                                                                 }));

    await AssertRefusedWithAsync(response, "order.settlementCannotBeProcessed");
  }

  [Test]
  public async Task PostOrder_LessThanThePriceWithoutAReason_IsRefusedWithTheSettlementMessage()
  {
    using var response = await _context.SendAsync(HttpMethod.Post,
                                                  "/api/orders",
                                                  BuildOrderWith(new[]
                                                                 {
                                                                   new
                                                                   {
                                                                     catalogItemId = _context.World.BratwurstItemId,
                                                                     unitPriceCents = 350,
                                                                     settlement = new { paidPriceCents = (int?)100 }
                                                                   }
                                                                 }));

    await AssertRefusedWithAsync(response, "order.settlementCannotBeProcessed");
  }

  [Test]
  public async Task PostSettlement_NoLines_IsRefusedWithTheEmptySelectionMessage()
  {
    using var response = await _context.SendAsync(HttpMethod.Post, "/api/open-items/settle", new { lines = Array.Empty<object>() });

    await AssertRefusedWithAsync(response, "order.settlementNoItemsSelected");
  }

  [Test]
  public async Task PostSettlement_NegativeAmountPaid_IsRefusedWithTheSettlementMessage()
  {
    using var response = await _context.SendAsync(HttpMethod.Post,
                                                  "/api/open-items/settle",
                                                  new
                                                  {
                                                    lines = new[]
                                                            {
                                                              new
                                                              {
                                                                orderItemId = Guid.NewGuid(),
                                                                paidPriceCents = -1
                                                              }
                                                            }
                                                  });

    await AssertRefusedWithAsync(response, "order.settlementCannotBeProcessed");
  }

  [Test]
  public async Task PostFulfillment_NoItemsSelected_IsRefusedWithTheStationMessage()
  {
    var stationToken = await _context.IssueStationTokenAsync(_context.World.KitchenStationId);

    using var response = await _context.SendAsAsync(stationToken, HttpMethod.Post, "/api/station/items/fulfill", new { orderItemIds = Array.Empty<Guid>() });

    await AssertRefusedWithAsync(response, "station.noItemsSelected");
  }

  [Test]
  public async Task PostEnrolmentRedemption_CodeOfOnlySpaces_IsRefusedWithTheMissingCodeMessage()
  {
    using var response = await _context.Client.PostAsJsonAsync("/api/enrolment/redeem", new { code = "   " });

    await AssertRefusedWithAsync(response, "enrolment.codeMissing");
  }

  [Test]
  public async Task PostCategory_NameOfOnlySpaces_IsRefusedWithTheCategoryNameMessage()
  {
    using var response = await _context.Client.PostAsJsonAsync("/api/admin/categories",
                                                               new
                                                               {
                                                                 name = "   ",
                                                                 colourHex = "#6D4C41"
                                                               });

    await AssertRefusedWithAsync(response, "admin.categoryNameMissing");
  }

  [Test]
  public async Task PostCategory_ColourThatIsNotSixHexDigits_IsRefusedWithTheColourMessage()
  {
    using var response = await _context.Client.PostAsJsonAsync("/api/admin/categories",
                                                               new
                                                               {
                                                                 name = "Kaffee",
                                                                 colourHex = "braun"
                                                               });

    await AssertRefusedWithAsync(response, "admin.categoryColourInvalid");
  }

  [Test]
  public async Task PostItem_NameOfOnlySpaces_IsRefusedWithTheItemNameMessage()
  {
    using var response = await _context.Client.PostAsJsonAsync("/api/admin/items",
                                                               new
                                                               {
                                                                 name = "   ",
                                                                 categoryId = _context.World.FoodCategoryId,
                                                                 sortOrder = 1
                                                               });

    await AssertRefusedWithAsync(response, "admin.itemNameMissing");
  }

  [Test]
  public async Task PostStation_NameOfOnlySpaces_IsRefusedWithTheStationNameMessage()
  {
    using var response = await _context.Client.PostAsJsonAsync("/api/admin/stations",
                                                               new
                                                               {
                                                                 name = "   ",
                                                                 sortOrder = 3
                                                               });

    await AssertRefusedWithAsync(response, "admin.stationNameMissing");
  }

  [Test]
  public async Task PutStaffMember_NameOfOnlySpaces_IsRefusedWithTheStaffNameMessage()
  {
    using var response = await _context.Client.PutAsJsonAsync($"/api/admin/staff-members/{_context.World.StaffMemberId}", new { name = "   " });

    await AssertRefusedWithAsync(response, "admin.staff.nameMissing");
  }

  [Test]
  public async Task PostFestival_NameOfOnlySpaces_IsRefusedWithTheFestivalNameMessage()
  {
    using var response = await _context.Client.PostAsJsonAsync("/api/admin/festivals",
                                                               new
                                                               {
                                                                 name = "   ",
                                                                 startsAtUtc = DateTime.UtcNow.AddDays(30),
                                                                 endsAtUtc = DateTime.UtcNow.AddDays(31)
                                                               });

    await AssertRefusedWithAsync(response, "admin.festivalNameMissing");
  }

  [Test]
  public async Task PutFestivalItem_NegativePrice_IsRefusedWithThePriceMessage()
  {
    using var response = await _context.Client.PutAsJsonAsync($"/api/admin/festivals/{_context.World.FestivalId}/items/{_context.World.BratwurstItemId}",
                                                              new
                                                              {
                                                                priceCents = -1,
                                                                stationIds = new[] { _context.World.KitchenStationId }
                                                              });

    await AssertRefusedWithAsync(response, "admin.itemPriceOutOfRange");
  }

  [Test]
  public async Task TryWrite_TwoRefusedMembersListedInReverse_AnswersTheKeyOfTheMemberDeclaredFirst()
  {
    RecordingLogger recordedRefusals = new();

    var written = await WriteRefusedShapeAsync(new HttpValidationProblemDetails(new Dictionary<string, string[]>
    {
      ["ColourHex"] = ["admin.categoryColourInvalid"],
      ["Name"] = ["admin.categoryNameMissing"]
    }),
                                               typeof(SaveCategoryRequest),
                                               recordedRefusals);

    Assert.Multiple(() =>
                    {
                      Assert.That(written.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
                      Assert.That(written.Error!.Code, Is.EqualTo("ValidationFailed"));
                      Assert.That(written.Error.MessageKey, Is.EqualTo("admin.categoryNameMissing"));
                    });
  }

  [Test]
  public void TryWrite_ARefusedShapeNamingNoMember_Throws()
  {
    Assert.ThrowsAsync<InvalidOperationException>(async () => await WriteRefusedShapeAsync(new HttpValidationProblemDetails(new Dictionary<string, string[]>()), typeof(SaveCategoryRequest), new RecordingLogger()));
  }

  [Test]
  public async Task TryWrite_TwoRefusedMembers_LogsEveryRefusedMember()
  {
    RecordingLogger recordedRefusals = new();

    await WriteRefusedShapeAsync(new HttpValidationProblemDetails(new Dictionary<string, string[]>
    {
      ["ColourHex"] = ["admin.categoryColourInvalid"],
      ["Name"] = ["admin.categoryNameMissing"]
    }),
                                 typeof(SaveCategoryRequest),
                                 recordedRefusals);

    Assert.Multiple(() =>
                    {
                      Assert.That(recordedRefusals.Lines, Has.Some.Contains("admin.categoryNameMissing"));
                      Assert.That(recordedRefusals.Lines, Has.Some.Contains("admin.categoryColourInvalid"));
                      Assert.That(recordedRefusals.Lines, Has.Some.Contains("ColourHex"));
                      Assert.That(recordedRefusals.Lines, Has.Some.Contains("Name"));
                    });
  }

  private async static Task<WrittenAnswer> WriteRefusedShapeAsync(HttpValidationProblemDetails refusedShape, Type requestType, RecordingLogger recordedRefusals)
  {
    ServiceCollection services = new();
    services.AddLogging();

    DefaultHttpContext httpContext = new() { RequestServices = services.BuildServiceProvider() };
    httpContext.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(new AcceptedRequestType(requestType)), "the route under test"));

    using MemoryStream body = new();
    httpContext.Response.Body = body;

    RequestShapeRefusalWriter writer = new(new ResultEnvelope(new SystemTextJsonRecordingSerializer(), new HttpContextAccessor(), recordedRefusals));

    await writer.TryWriteAsync(new ProblemDetailsContext
    {
      HttpContext = httpContext,
      ProblemDetails = refusedShape
    });

    body.Position = 0;

    if (body.Length == 0)
      return new(httpContext.Response.StatusCode, null);

    return new(httpContext.Response.StatusCode, await JsonSerializer.DeserializeAsync<ApiError>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }));
  }

  private sealed record WrittenAnswer(int StatusCode, ApiError? Error);

  private sealed class AcceptedRequestType : IAcceptsMetadata
  {
    public AcceptedRequestType(Type requestType)
    {
      RequestType = requestType;
    }

    public IReadOnlyList<string> ContentTypes { get; } = ["application/json"];

    public Type? RequestType { get; }

    public bool IsOptional => false;
  }

  private sealed class RecordingLogger : ILogger<ResultEnvelope>
  {
    public List<string> Lines { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
      return null;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
      return true;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
      ArgumentNullException.ThrowIfNull(formatter);

      Lines.Add(formatter(state, exception));
    }
  }
}
