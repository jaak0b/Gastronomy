using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace GastronomyApp.Desktop.Tests.Smoke;

internal static class RenderedColours
{
  public static Color Of(IBrush? brush)
  {
    return ((ISolidColorBrush)brush!).Color;
  }

  public static Color LabelForegroundOf(Button button)
  {
    return Of(Label(button).Foreground);
  }

  public static Color LabelBackgroundOf(Button button)
  {
    return Of(Label(button).Background);
  }

  public static Color LabelBackgroundWhile(Button button, string pseudoClass)
  {
    var pseudoClasses = (IPseudoClasses)button.Classes;
    pseudoClasses.Add(pseudoClass);
    Dispatcher.UIThread.RunJobs();
    var colour = LabelBackgroundOf(button);
    pseudoClasses.Remove(pseudoClass);
    Dispatcher.UIThread.RunJobs();

    return colour;
  }

  private static ContentPresenter Label(Button button)
  {
    return button.GetVisualDescendants()
                 .OfType<ContentPresenter>()
                 .Single(presenter => presenter.Name == "PART_ContentPresenter");
  }
}
