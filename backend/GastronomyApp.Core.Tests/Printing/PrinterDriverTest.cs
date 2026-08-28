using GastronomyApp.Core.Printing;

namespace GastronomyApp.Core.Tests.Printing;

public sealed class PrinterDriverTest
{
  [Test]
  public async Task ConnectAsync_OnItsOwnPrinter_ReceivesItTyped()
  {
    TestPrinterDriverDouble driver = new();
    var printer = APrinter.Test();

    await driver.ConnectAsync(printer, CancellationToken.None);

    Assert.That(driver.LastPrinter, Is.SameAs(printer));
  }

  [Test]
  public void ConnectAsync_OnAPrinterOfAnotherKind_RefusesInsteadOfCrashing()
  {
    TestPrinterDriverDouble driver = new();

    Assert.ThrowsAsync<PrinterDriverMismatchException>(async () => await driver.ConnectAsync(APrinter.Network(), CancellationToken.None));
  }
}
