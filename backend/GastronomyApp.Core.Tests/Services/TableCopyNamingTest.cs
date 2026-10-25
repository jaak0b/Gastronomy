using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class TableCopyNamingTest
{
  [SetUp]
  public void SetUp()
  {
    _naming = new();
  }

  private readonly Guid _festivalId = Guid.Parse("eeeeeeee-0000-0000-0000-000000000001");
  private readonly Guid _areaId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");

  private TableCopyNaming _naming = null!;

  [TestCase("A1", "A2")]
  [TestCase("B4", "B5")]
  [TestCase("Tisch 1", "Tisch 2")]
  [TestCase("A09", "A10")]
  [TestCase("A99", "A100")]
  [TestCase("A099", "A100")]
  public void NamesFor_ASourceNameEndingInDigits_CountsTheRunUpKeepingItsLength(string sourceName, string expected)
  {
    IReadOnlyList<TableCopyName> names = _naming.NamesFor([BuildTable(1, sourceName)], [sourceName]);

    Assert.That(names.Single().Name, Is.EqualTo(expected));
  }

  [Test]
  public void NamesFor_ASourceNameWithoutADigit_AppendsTheFirstFreeNumber()
  {
    IReadOnlyList<TableCopyName> names = _naming.NamesFor([BuildTable(1, "Theke")], ["Theke"]);

    Assert.That(names.Single().Name, Is.EqualTo("Theke 1"));
  }

  [Test]
  public void NamesFor_AnAppendedNumberAlreadyTaken_CountsOnToTheNextFreeOne()
  {
    IReadOnlyList<TableCopyName> names = _naming.NamesFor([BuildTable(1, "Theke")],
                                                          [
                                                            "Theke",
                                                            "Theke 1",
                                                            "theke 2"
                                                          ]);

    Assert.That(names.Single().Name, Is.EqualTo("Theke 3"));
  }

  [Test]
  public void NamesFor_ANameInOtherCapitalsAlreadyTaken_IsSkipped()
  {
    IReadOnlyList<TableCopyName> names = _naming.NamesFor([BuildTable(1, "A1")],
                                                          [
                                                            "A1",
                                                            "a2"
                                                          ]);

    Assert.That(names.Single().Name, Is.EqualTo("A3"));
  }

  [Test]
  public void NamesFor_ASourceNameWithItsOwnSpellingAndCapitals_KeepsThemExactly()
  {
    IReadOnlyList<TableCopyName> names = _naming.NamesFor([BuildTable(1, "tIsCh 09")], ["tIsCh 09"]);

    Assert.That(names.Single().Name, Is.EqualTo("tIsCh 10"));
  }

  [Test]
  public void NamesFor_AVeryLongDigitRun_CountsWithoutOverflowing()
  {
    var sourceName = $"A{new string('9', 30)}";

    IReadOnlyList<TableCopyName> names = _naming.NamesFor([BuildTable(1, sourceName)], [sourceName]);

    Assert.That(names.Single().Name, Is.EqualTo($"A1{new string('0', 30)}"));
  }

  [Test]
  public void NamesFor_APasteOfSeveralTables_AllocatesInAscendingSourceNameAndKeepsEveryNameDistinct()
  {
    IReadOnlyList<TableCopyName> names = _naming.NamesFor([
                                                            BuildTable(2, "B1"),
                                                            BuildTable(1, "A1")
                                                          ],
                                                          [
                                                            "A1",
                                                            "B1"
                                                          ]);

    Assert.That(names.Select(copy => copy.Name),
                Is.EqualTo(new[]
                           {
                             "A2",
                             "B2"
                           }));
  }

  [Test]
  public void NamesFor_TwoSourceNamesThatDifferOnlyInCapitals_AreOrderedByIdAndKeepTheirOwnCase()
  {
    IReadOnlyList<TableCopyName> names = _naming.NamesFor([
                                                            BuildTable(2, "A1"),
                                                            BuildTable(1, "a1")
                                                          ],
                                                          [
                                                            "a1",
                                                            "A1",
                                                            "a2"
                                                          ]);

    Assert.That(names.Select(copy => copy.Name),
                Is.EqualTo(new[]
                           {
                             "a3",
                             "A4"
                           }));
  }

  [Test]
  public void NamesFor_TwoTablesThatWouldNeedTheSameName_HandsTheSecondTheNextFreeOne()
  {
    IReadOnlyList<TableCopyName> names = _naming.NamesFor([
                                                            BuildTable(1, "Theke"),
                                                            BuildTable(2, "Theke")
                                                          ],
                                                          ["Theke"]);

    Assert.That(names.Select(copy => copy.Name),
                Is.EqualTo(new[]
                           {
                             "Theke 1",
                             "Theke 2"
                           }));
  }

  private Table BuildTable(int id, string name)
  {
    return new()
           {
             Id = Guid.Parse($"cccccccc-0000-0000-0000-{id:D12}"),
             FestivalId = _festivalId,
             AreaId = _areaId,
             Name = name,
             IsHidden = false
           };
  }
}
