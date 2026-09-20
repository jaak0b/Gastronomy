using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace GastronomyApp.Desktop.Tests.Smoke;

internal sealed class RenderedColourReader
{
  public Color ToColour(IBrush? brush)
  {
    return ((ISolidColorBrush)brush!).Color;
  }

  public Color ReadLabelForeground(Button button)
  {
    return ToColour(Label(button).Foreground);
  }

  public Color ReadLabelBackground(Button button)
  {
    return ToColour(Label(button).Background);
  }

  public Color LabelBackgroundWhile(Button button, string pseudoClass)
  {
    var pseudoClasses = (IPseudoClasses)button.Classes;
    pseudoClasses.Add(pseudoClass);
    Dispatcher.UIThread.RunJobs();
    var colour = ReadLabelBackground(button);
    pseudoClasses.Remove(pseudoClass);
    Dispatcher.UIThread.RunJobs();

    return colour;
  }

  private ContentPresenter Label(Button button)
  {
    return button.GetVisualDescendants()
                 .OfType<ContentPresenter>()
                 .Single(presenter => presenter.Name == "PART_ContentPresenter");
  }
}
