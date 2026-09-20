using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace GastronomyApp.Desktop.Tests.Smoke;

internal static class RenderedColours
{
  public static Color ToColour(IBrush? brush)
  {
    return ((ISolidColorBrush)brush!).Color;
  }

  public static Color ReadLabelForeground(Button button)
  {
    return ToColour(Label(button).Foreground);
  }

  public static Color ReadLabelBackground(Button button)
  {
    return ToColour(Label(button).Background);
  }

  public static Color LabelBackgroundWhile(Button button, string pseudoClass)
  {
    var pseudoClasses = (IPseudoClasses)button.Classes;
    pseudoClasses.Add(pseudoClass);
    Dispatcher.UIThread.RunJobs();
    var colour = ReadLabelBackground(button);
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
