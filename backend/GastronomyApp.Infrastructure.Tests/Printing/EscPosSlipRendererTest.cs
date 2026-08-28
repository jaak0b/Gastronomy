using System.Text;
using GastronomyApp.Infrastructure.Localization;
using GastronomyApp.Infrastructure.Printing;

namespace GastronomyApp.Infrastructure.Tests.Printing;

public class EscPosSlipRendererTest
{
  private EscPosSlipRenderer renderer = null!;

  [SetUp]
  public void SetUp()
  {
    renderer = new(new ResxSlipTextProvider());
  }

  private string Joined(params string[] lines)
  {
    return string.Join("\r\n", lines) + "\r\n";
  }

  private SlipRenderRequest GermanFixture()
  {
    return new("KÜCHE",
               "de",
               42,
               137,
               "12",
               "Anna",
               new(2026, 8, 26, 19, 42, 0, TimeSpan.Zero),
               TimeZoneInfo.Utc,
               [
                 new(2, "Bratwurst mit Brot", null),
                 new(1, "Pommes groß", "ohne Ketchup"),
                 new(3, "Kartoffelsalat", null)
               ],
               "Ein Teller extra für ein Kind.",
               ["Theke"]);
  }

  private SlipRenderRequest EnglishFixture()
  {
    return GermanFixture() with
           {
             StationName = "KITCHEN",
             LanguageCode = "en",
             Lines =
             [
               new(2, "Sausage with bread", null),
               new(1, "Chips, large", "no ketchup"),
               new(3, "Potato salad", null)
             ],
             OrderNote = "One extra plate for a child.",
             AlsoGoesToStationNames = ["Bar"]
           };
  }

  private bool ContainsSequence(ReadOnlyMemory<byte> haystack, byte[] needle, int startIndex, out int foundAt)
  {
    ReadOnlySpan<byte> span = haystack.Span;
    for (var index = startIndex; index + needle.Length <= span.Length; index++)
    {
      if (span.Slice(index, needle.Length).SequenceEqual(needle))
      {
        foundAt = index;
        return true;
      }
    }

    foundAt = -1;
    return false;
  }

  private void AssertSequencesInOrder(ReadOnlyMemory<byte> bytes, params byte[][] sequences)
  {
    var cursor = 0;
    foreach (var sequence in sequences)
    {
      var found = ContainsSequence(bytes, sequence, cursor, out var foundAt);
      Assert.That(found, Is.True, $"missing sequence {Convert.ToHexString(sequence)} after index {cursor}");
      cursor = foundAt + sequence.Length;
    }
  }

  [Test]
  public void RenderInitialSlip_GermanFixture_MatchesSpecExample()
  {
    var slip = renderer.RenderInitialSlip(GermanFixture());

    Assert.That(slip.RenderedText,
                Is.EqualTo(Joined("================================================",
                                  "KÜCHE",
                                  "================================================",
                                  "BON 042",
                                  "================================================",
                                  "Bestellung 137",
                                  "Tisch 12",
                                  "Kellner: Anna",
                                  "26.08.2026, 19:42 Uhr",
                                  "------------------------------------------------",
                                  "2 x Bratwurst mit Brot",
                                  "1 x Pommes groß",
                                  "    Hinweis: ohne Ketchup",
                                  "3 x Kartoffelsalat",
                                  "------------------------------------------------",
                                  "Artikel gesamt: 6",
                                  "Hinweis: Ein Teller extra für ein Kind.",
                                  "Diese Bestellung geht auch an: Theke",
                                  "================================================")));

    AssertSequencesInOrder(slip.Bytes,
                           [0x1B, 0x40],
                           [0x1B, 0x74, 0x13],
                           [0x1D, 0x61, 0x0F],
                           [0x1B, 0x61, 0x01],
                           [0x1D, 0x21, 0x11],
                           [0x1B, 0x45, 0x01],
                           [0x1B, 0x61, 0x00],
                           [0x1B, 0x64, 0x04],
                           [0x1D, 0x56, 0x42, 0x03]);
  }

  [Test]
  public void RenderInitialSlip_EnglishFixture_MatchesSpecExample()
  {
    var slip = renderer.RenderInitialSlip(EnglishFixture());

    Assert.That(slip.RenderedText,
                Is.EqualTo(Joined("================================================",
                                  "KITCHEN",
                                  "================================================",
                                  "SLIP 042",
                                  "================================================",
                                  "Order 137",
                                  "Table 12",
                                  "Waiter: Anna",
                                  "26/08/2026, 19:42",
                                  "------------------------------------------------",
                                  "2 x Sausage with bread",
                                  "1 x Chips, large",
                                  "    Note: no ketchup",
                                  "3 x Potato salad",
                                  "------------------------------------------------",
                                  "Items in total: 6",
                                  "Note: One extra plate for a child.",
                                  "This order also goes to: Bar",
                                  "================================================")));
  }

  [Test]
  public void RenderInitialSlip_Umlauts_EncodedAsPc858()
  {
    var request = GermanFixture() with
                  {
                    Lines = [new(1, "äöüÄÖÜß€", null)]
                  };

    var slip = renderer.RenderInitialSlip(request);

    AssertSequencesInOrder(slip.Bytes,
                           [0x84, 0x94, 0x81, 0x8E, 0x99, 0x9A, 0xE1, 0xD5]);
    Assert.That(ContainsSequence(slip.Bytes, Encoding.UTF8.GetBytes("ä"), 0, out _), Is.False);
  }

  [Test]
  public void RenderInitialSlip_LongItemName_WrapsIndentedContinuationLine()
  {
    string longName = new('A', 60);
    var request = GermanFixture() with { Lines = [new(1, longName, null)] };

    var slip = renderer.RenderInitialSlip(request);

    Assert.That(slip.RenderedText, Does.Contain("1 x " + new string('A', 44) + "\r\n    " + new string('A', 16) + "\r\n"));
  }

  [Test]
  public void RenderInitialSlip_LongTableOrStaffMemberName_WrapsSameWay()
  {
    var request = GermanFixture() with
                  {
                    TableName = new('T', 60),
                    StaffMemberName = new('S', 60)
                  };

    var slip = renderer.RenderInitialSlip(request);

    Assert.That(slip.RenderedText, Does.Contain("Tisch " + new string('T', 42) + "\r\n    " + new string('T', 18)));
    Assert.That(slip.RenderedText, Does.Contain("Kellner: " + new string('S', 39) + "\r\n    " + new string('S', 21)));
  }


  [Test]
  public void RenderCopySlip_German_PrependsReprintBannerWithReprintTime()
  {
    var slip = renderer.RenderCopySlip(GermanFixture(),
                                       1,
                                       new(2026, 8, 26, 20, 31, 0, TimeSpan.Zero),
                                       TimeZoneInfo.Utc);

    Assert.That(slip.RenderedText,
                Does.StartWith(Joined("================================================",
                                      "NACHDRUCK Nr. 1",
                                      "Nachdruck um 20:31 Uhr",
                                      "================================================",
                                      "KÜCHE",
                                      "================================================",
                                      "BON 042",
                                      "================================================",
                                      "Bestellung 137",
                                      "Tisch 12")));
    Assert.That(slip.RenderedText, Does.Contain("26.08.2026, 19:42 Uhr"));
    Assert.That(slip.RenderedText, Does.Not.Contain("26.08.2026, 20:31 Uhr"));
  }

  [Test]
  public void RenderInitialSlip_FooterCountsUnits_NotLines()
  {
    var slip = renderer.RenderInitialSlip(GermanFixture());

    Assert.That(slip.RenderedText, Does.Contain("Artikel gesamt: 6"));
  }

  private TestSlipRenderRequest GermanTestSlipFixture()
  {
    return new("KÜCHE",
               "de",
               new(2026, 8, 26, 17, 5, 0, TimeSpan.Zero),
               TimeZoneInfo.Utc);
  }

  [Test]
  public void RenderTestSlip_German_NamesTheStationAndCarriesNoAddress()
  {
    var slip = renderer.RenderTestSlip(GermanTestSlipFixture());

    Assert.That(slip.RenderedText,
                Is.EqualTo(Joined("================================================",
                                  "KÜCHE",
                                  "================================================",
                                  "TESTBON",
                                  "26.08.2026, 17:05 Uhr",
                                  "================================================")));
  }

  [Test]
  public void RenderTestSlip_English_NamesTheStationAndCarriesNoAddress()
  {
    var slip = renderer.RenderTestSlip(GermanTestSlipFixture() with { StationName = "KITCHEN", LanguageCode = "en" });

    Assert.That(slip.RenderedText,
                Is.EqualTo(Joined("================================================",
                                  "KITCHEN",
                                  "================================================",
                                  "TEST SLIP",
                                  "26/08/2026, 17:05",
                                  "================================================")));
  }

  [Test]
  public void RenderInitialSlip_LongStationName_WrapsAtTwentyFourColumnsBecauseTheRegionIsDoubleWidth()
  {
    var request = GermanFixture() with { StationName = new('K', 30) };

    var slip = renderer.RenderInitialSlip(request);

    Assert.That(slip.RenderedText, Does.Contain(new string('K', 24) + "\r\n    " + new string('K', 6) + "\r\n"));
  }

  [Test]
  public void RenderCopySlip_NormalSizeRegions_StillWrapAtFortyEightColumns()
  {
    var request = GermanFixture() with { TableName = new('T', 60) };

    var slip = renderer.RenderCopySlip(request,
                                       1,
                                       new(2026, 8, 26, 20, 31, 0, TimeSpan.Zero),
                                       TimeZoneInfo.Utc);

    Assert.That(slip.RenderedText, Does.Contain("Tisch " + new string('T', 42) + "\r\n    " + new string('T', 18)));
  }

  [Test]
  public void RenderInitialSlip_CharacterOutsidePc858_TransliteratesRatherThanDroppingIt()
  {
    var request = GermanFixture() with
                  {
                    Lines = [new(1, "Pierogi \u0142ososiowe \u2013 Cr\u0113me", null)]
                  };

    var slip = renderer.RenderInitialSlip(request);

    Assert.That(slip.RenderedText, Does.Contain("Pierogi lososiowe - Creme"));
    Assert.That(ContainsSequence(slip.Bytes, [0x3F], 0, out _), Is.False);
  }

  [Test]
  public void RenderInitialSlip_CharacterWithNoTransliteration_FallsBackToQuestionMark()
  {
    var request = GermanFixture() with
                  {
                    Lines = [new(1, "Wasabi \u3042", null)]
                  };

    var slip = renderer.RenderInitialSlip(request);

    Assert.That(slip.RenderedText, Does.Contain("Wasabi ?"));
  }
}
