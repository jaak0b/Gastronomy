using GastronomyApp.Core.Printing;

namespace GastronomyApp.Core.Tests.Printing;

public sealed class PrinterDriverRegistryTest
{
  [Test]
  public void For_OnPrinterWithARegisteredDriver_ReturnsThatDriver()
  {
    TestPrinterDriverDouble testDriver = new();
    PrinterDriverRegistry registry = new([testDriver, new NetworkPrinterDriverDouble()]);

    IPrinterDriver found = registry.For(APrinter.Test());

    Assert.That(found, Is.SameAs(testDriver));
  }

  [Test]
  public void For_OnPrinterWhoseDriverIsNotInThisBuild_SaysWhichPrinterCannotBeUsed()
  {
    PrinterDriverRegistry registry = new([new TestPrinterDriverDouble()]);

    UnknownPrinterDriverException thrown = Assert.Throws<UnknownPrinterDriverException>(
        () => registry.For(APrinter.Network()))!;

    Assert.That(thrown.Message, Does.Contain("Drucker Küche"));
  }

  [Test]
  public void Constructor_OnTwoDriversForOnePrinter_RefusesToStart()
  {
    Assert.Throws<DuplicatePrinterDriverException>(
        () => _ = new PrinterDriverRegistry([new TestPrinterDriverDouble(), new TestPrinterDriverDouble()]));
  }
}
