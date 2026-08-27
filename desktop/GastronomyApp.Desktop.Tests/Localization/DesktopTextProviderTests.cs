using System.Globalization;
using GastronomyApp.Desktop.Localization;
using GastronomyApp.Desktop.Services;

namespace GastronomyApp.Desktop.Tests.Localization;

[TestFixture]
public sealed class DesktopTextProviderTests
{
    private CultureInfo _originalUiCulture = null!;

    [SetUp]
    public void SetUp()
    {
        _originalUiCulture = CultureInfo.CurrentUICulture;
    }

    [TearDown]
    public void TearDown()
    {
        CultureInfo.CurrentUICulture = _originalUiCulture;
    }

    [Test]
    public void Get_OnAGermanWindows_ReturnsTheGermanString()
    {
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de-DE");
        IDesktopTextProvider text = new DesktopTextProvider();

        Assert.That(text.Get("desktop.status.running"), Is.EqualTo("Das Programm nimmt Bestellungen an."));
    }

    [Test]
    public void UseLanguage_OverridesWindowsForTheChosenLanguage()
    {
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
        IDesktopTextProvider text = new DesktopTextProvider();

        text.UseLanguage("de");

        Assert.That(text.Get("desktop.status.running"), Is.EqualTo("Das Programm nimmt Bestellungen an."));
    }

    [Test]
    public void UseLanguage_WithNoChoice_FollowsWindowsAgain()
    {
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
        IDesktopTextProvider text = new DesktopTextProvider();
        text.UseLanguage("de");

        text.UseLanguage(null);

        Assert.That(text.Get("desktop.status.running"), Is.EqualTo("The program is taking orders."));
    }

    [Test]
    public void UseLanguage_TellsEveryReaderThatTheLanguageChanged()
    {
        IDesktopTextProvider text = new DesktopTextProvider();
        int changes = 0;
        text.LanguageChanged += () => changes++;

        text.UseLanguage("de");

        Assert.That(changes, Is.EqualTo(1));
    }

    [Test]
    public void Get_OnAnEnglishWindows_ReturnsTheEnglishString()
    {
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
        IDesktopTextProvider text = new DesktopTextProvider();

        Assert.That(text.Get("desktop.status.running"), Is.EqualTo("The program is taking orders."));
    }
}
