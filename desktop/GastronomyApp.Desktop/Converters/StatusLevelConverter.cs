using System.Globalization;
using Avalonia.Data.Converters;
using GastronomyApp.Desktop.ViewModels;

namespace GastronomyApp.Desktop.Converters;

public sealed class StatusLevelConverter : IValueConverter
{
  public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
  {
    return value is StatusLevel level
           && parameter is string wantedName
           && Enum.TryParse(wantedName, out StatusLevel wanted)
           && level == wanted;
  }

  public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
  {
    throw new NotSupportedException("A status level is never written back from the window.");
  }
}
