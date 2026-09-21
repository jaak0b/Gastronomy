using ErrorOr;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Core.Refusals;

namespace GastronomyApp.Api.Tests.ErrorHandling;

[TestFixture]
public sealed class SystemTextJsonRecordingSerializerTest
{
  private readonly SystemTextJsonRecordingSerializer _serializer = new();

  [Test]
  public void GetRecording_ARefusedResult_WritesTheCodeTheDescriptionAndTheMetadataOfEveryRefusedLine()
  {
    var catalogItemId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    IErrorOr refused = ErrorOrFactory.From<Success>([Refusal.Order.ItemNotAvailable(catalogItemId, "Bratwurst"), Refusal.Order.NoRunningFestival()]);

    var recording = refused.GetRecording(_serializer);

    Assert.Multiple(() =>
                    {
                      Assert.That(recording, Does.Contain("catalog.itemSoldOut"));
                      Assert.That(recording, Does.Contain("sold out at the running festival"));
                      Assert.That(recording, Does.Contain(catalogItemId.ToString()));
                      Assert.That(recording, Does.Contain("Bratwurst"));
                      Assert.That(recording, Does.Contain("order.cannotBeProcessed"));
                    });
  }

  [Test]
  public void GetRecording_AResultCarryingAValue_WritesThatValue()
  {
    IErrorOr accepted = ErrorOrFactory.From(new SettledTable("Tisch 12", 350));

    var recording = accepted.GetRecording(_serializer);

    Assert.Multiple(() =>
                    {
                      Assert.That(recording, Does.Contain("Tisch 12"));
                      Assert.That(recording, Does.Contain("350"));
                    });
  }

  private sealed record SettledTable(string TableName, int PaidPriceCents);
}
