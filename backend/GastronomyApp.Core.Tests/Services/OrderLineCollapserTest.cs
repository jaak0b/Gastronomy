using GastronomyApp.Core.Services;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class OrderLineCollapserTest
{

  [SetUp]
  public void SetUp()
  {
    collapser = new();
  }

  private sealed record Line(string ItemName, string? Note);

  private OrderLineCollapser collapser = null!;

  private IReadOnlyList<CollapsedOrderLine<Line>> Collapse(params Line[] lines)
  {
    return collapser.Collapse(lines, line => line.ItemName, line => line.Note);
  }

  [Test]
  public void Collapse_IdenticalLinesWithoutANote_CountsThemAsOne()
  {
    IReadOnlyList<CollapsedOrderLine<Line>> collapsed =
      Collapse(new Line("Bier", null), new Line("Bier", null), new Line("Bier", null));

    Assert.Multiple(() =>
                    {
                      Assert.That(collapsed, Has.Count.EqualTo(1));
                      Assert.That(collapsed[0].Quantity, Is.EqualTo(3));
                      Assert.That(collapsed[0].Line.ItemName, Is.EqualTo("Bier"));
                    });
  }

  [Test]
  public void Collapse_SameItemWithADifferentNote_StaysASeparateLine()
  {
    IReadOnlyList<CollapsedOrderLine<Line>> collapsed = Collapse(new Line("Bier", null),
                                                                 new Line("Bier", "ohne Schaum"),
                                                                 new Line("Bier", null));

    Assert.Multiple(() =>
                    {
                      Assert.That(collapsed, Has.Count.EqualTo(2));
                      Assert.That(collapsed[0].Quantity, Is.EqualTo(2));
                      Assert.That(collapsed[0].Line.Note, Is.Null);
                      Assert.That(collapsed[1].Quantity, Is.EqualTo(1));
                      Assert.That(collapsed[1].Line.Note, Is.EqualTo("ohne Schaum"));
                    });
  }

  [Test]
  public void Collapse_LinesWithTheSameNote_CountThemTogether()
  {
    IReadOnlyList<CollapsedOrderLine<Line>> collapsed = Collapse(new Line("Bier", "ohne Schaum"),
                                                                 new Line("Bier", "ohne Schaum"));

    Assert.Multiple(() =>
                    {
                      Assert.That(collapsed, Has.Count.EqualTo(1));
                      Assert.That(collapsed[0].Quantity, Is.EqualTo(2));
                    });
  }

  [Test]
  public void Collapse_DifferentItems_KeepsTheOrderTheyWereAddedIn()
  {
    IReadOnlyList<CollapsedOrderLine<Line>> collapsed = Collapse(new Line("Schnitzel", null),
                                                                 new Line("Bier", null),
                                                                 new Line("Schnitzel", null));

    Assert.Multiple(() =>
                    {
                      Assert.That(collapsed[0].Line.ItemName, Is.EqualTo("Schnitzel"));
                      Assert.That(collapsed[0].Quantity, Is.EqualTo(2));
                      Assert.That(collapsed[1].Line.ItemName, Is.EqualTo("Bier"));
                      Assert.That(collapsed[1].Quantity, Is.EqualTo(1));
                    });
  }

  [Test]
  public void Collapse_AnEmptyNoteAndNoNote_AreTheSameLine()
  {
    IReadOnlyList<CollapsedOrderLine<Line>> collapsed =
      Collapse(new Line("Bier", null), new Line("Bier", string.Empty));

    Assert.Multiple(() =>
                    {
                      Assert.That(collapsed, Has.Count.EqualTo(1));
                      Assert.That(collapsed[0].Quantity, Is.EqualTo(2));
                    });
  }

  [Test]
  public void Collapse_NoLines_ReturnsNothing()
  {
    Assert.That(Collapse(), Is.Empty);
  }
}
