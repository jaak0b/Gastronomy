using System.Globalization;
using GastronomyApp.Core.Localization;

namespace GastronomyApp.Infrastructure.Printing;

public sealed record SlipLine(int Quantity, string ItemName, string? LineNote);

public sealed record SlipRenderRequest(
  string StationName,
  string LanguageCode,
  int StationOrderNumber,
  int GlobalOrderNumber,
  string TableName,
  string StaffMemberName,
  DateTimeOffset OrderTakenAtUtc,
  TimeZoneInfo DisplayTimeZone,
  IReadOnlyList<SlipLine> Lines,
  string? OrderNote,
  IReadOnlyList<string> AlsoGoesToStationNames);

public sealed record TestSlipRenderRequest(
  string StationName,
  string LanguageCode,
  DateTimeOffset PrintedAtUtc,
  TimeZoneInfo DisplayTimeZone);

public sealed record RenderedSlip(ReadOnlyMemory<byte> Bytes, string RenderedText);

public sealed record SlipTimeFormats(string DateAndTime, string TimeOnly);

public sealed class EscPosSlipRenderer
{
  private const int LineWidth = 48;
  private const int DoubleWidthLineWidth = 24;
  private const int ContinuationIndentWidth = 4;
  private const string ContinuationIndent = "    ";
  private const string LineBreak = "\r\n";
  private readonly byte[] _alignCentre = [0x1B, 0x61, 0x01];
  private readonly byte[] _alignLeft = [0x1B, 0x61, 0x00];
  private readonly byte[] _emphasisOff = [0x1B, 0x45, 0x00];
  private readonly byte[] _emphasisOn = [0x1B, 0x45, 0x01];
  private readonly byte[] _enableAutomaticStatusBack = [0x1D, 0x61, 0x0F];
  private readonly Pc858Encoder _encoder;
  private readonly byte[] _feed = [0x1B, 0x64, 0x04];

  private readonly byte[] _initialise = [0x1B, 0x40];
  private readonly byte[] _partialCut = [0x1D, 0x56, 0x42, 0x03];
  private readonly byte[] _qrErrorCorrection = [0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x45, 0x31];
  private readonly byte[] _qrModuleSize = [0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x43, 0x06];
  private readonly byte[] _qrPrintSymbol = [0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x51, 0x30];
  private readonly byte[] _qrSelectModel = [0x1D, 0x28, 0x6B, 0x04, 0x00, 0x31, 0x41, 0x32, 0x00];
  private readonly byte[] _selectCodePage = [0x1B, 0x74, 0x13];
  private readonly byte[] _sizeDouble = [0x1D, 0x21, 0x11];
  private readonly byte[] _sizeNormal = [0x1D, 0x21, 0x00];

  private readonly ISlipTextProvider _slipTextProvider;

  public EscPosSlipRenderer(ISlipTextProvider slipTextProvider)
  {
    _slipTextProvider = slipTextProvider;
    _encoder = new();
  }

  public RenderedSlip RenderInitialSlip(SlipRenderRequest request)
  {
    return RenderSlip(request, null, null, null);
  }

  public RenderedSlip RenderCopySlip(SlipRenderRequest request,
                                     int copyNumber,
                                     DateTimeOffset copyPrintedAtUtc,
                                     TimeZoneInfo displayTimeZone)
  {
    return RenderSlip(request, copyNumber, copyPrintedAtUtc, displayTimeZone);
  }

  public RenderedSlip RenderTestSlip(TestSlipRenderRequest request)
  {
    var strings = _slipTextProvider.GetStrings(request.LanguageCode);
    var formats = FormatsFor(request.LanguageCode);
    List<SlipSegment> segments =
    [
      new(NormalText(), [MajorSeparator()]),
      new(LargeText(), WrapLarge(request.StationName)),
      new(NormalText(), [MajorSeparator()]),
      new(LargeText(), WrapLarge(strings.TestSlipHeader)),
      new(NormalText(), [_encoder.ToPrintableText(FormatMoment(request.PrintedAtUtc, request.DisplayTimeZone, formats.DateAndTime)), MajorSeparator()])
    ];

    return Compose(segments);
  }

  private RenderedSlip RenderSlip(SlipRenderRequest request,
                                  int? copyNumber,
                                  DateTimeOffset? reprintAtUtc,
                                  TimeZoneInfo? reprintTimeZone)
  {
    var strings = _slipTextProvider.GetStrings(request.LanguageCode);
    var formats = FormatsFor(request.LanguageCode);
    List<SlipSegment> segments = [new(NormalText(), [MajorSeparator()])];

    if (reprintAtUtc is not null && reprintTimeZone is not null)
    {
      segments.Add(new(LargeText(),
                       WrapLarge(string.Format(CultureInfo.InvariantCulture,
                                               strings.ReprintBanner,
                                               copyNumber ?? 0))));
      segments.Add(new(NormalText(),
                       [
                         Wrap($"{strings.ReprintTimePrefix} {FormatMoment(reprintAtUtc.Value, reprintTimeZone, formats.TimeOnly)}")[0],
                         MajorSeparator()
                       ]));
    }

    segments.Add(new(LargeText(), WrapLarge(request.StationName)));
    segments.Add(new(NormalText(), [MajorSeparator()]));
    segments.Add(new(LargeText(), WrapLarge($"{strings.SlipNumberPrefix} {request.StationOrderNumber.ToString("D3", CultureInfo.InvariantCulture)}")));
    segments.Add(new(NormalText(), [MajorSeparator()]));
    segments.Add(new(EmphasisedText(), Wrap($"{strings.OrderNumberPrefix} {request.GlobalOrderNumber.ToString(CultureInfo.InvariantCulture)}")));

    List<string> header =
    [
      .. Wrap($"{strings.TablePrefix} {request.TableName}"),
      .. Wrap($"{strings.StaffMemberPrefix} {request.StaffMemberName}"),
      _encoder.ToPrintableText(FormatMoment(request.OrderTakenAtUtc, request.DisplayTimeZone, formats.DateAndTime)),
      MinorSeparator()
    ];
    segments.Add(new(NormalText(), header));

    List<string> body = [];
    foreach (var line in request.Lines)
    {
      body.AddRange(Wrap($"{line.Quantity.ToString(CultureInfo.InvariantCulture)} x {line.ItemName}"));
      if (line.LineNote is not null)
      {
        body.AddRange(Wrap($"{ContinuationIndent}{strings.NotePrefix} {line.LineNote}"));
      }
    }

    body.Add(MinorSeparator());
    segments.Add(new(NormalText(), body));

    var itemsTotal = request.Lines.Sum(line => line.Quantity);
    List<string> footer = [.. Wrap($"{strings.ItemsTotalPrefix} {itemsTotal.ToString(CultureInfo.InvariantCulture)}")];

    if (request.OrderNote is not null)
    {
      footer.AddRange(Wrap($"{strings.NotePrefix} {request.OrderNote}"));
    }

    if (request.AlsoGoesToStationNames.Count > 0)
    {
      footer.AddRange(Wrap($"{strings.AlsoGoesToPrefix} {string.Join(", ", request.AlsoGoesToStationNames)}"));
    }

    footer.Add(MajorSeparator());
    segments.Add(new(NormalText(), footer));

    return Compose(segments);
  }

  private RenderedSlip Compose(IReadOnlyList<SlipSegment> segments)
  {
    List<byte> bytes = [.. _initialise, .. _selectCodePage, .. _enableAutomaticStatusBack];
    List<string> allLines = [];

    foreach (var segment in segments)
    {
      bytes.AddRange(segment.Commands);
      foreach (var line in segment.Lines)
      {
        bytes.AddRange(_encoder.GetBytes(line));
        bytes.AddRange(_encoder.GetBytes(LineBreak));
        allLines.Add(line);
      }
    }

    bytes.AddRange(_feed);
    bytes.AddRange(_partialCut);

    var renderedText = string.Join(LineBreak, allLines) + LineBreak;
    return new(bytes.ToArray(), renderedText);
  }

  private IReadOnlyList<byte> NormalText()
  {
    return [.. _alignLeft, .. _sizeNormal, .. _emphasisOff];
  }

  private IReadOnlyList<byte> EmphasisedText()
  {
    return [.. _alignLeft, .. _sizeNormal, .. _emphasisOn];
  }

  private IReadOnlyList<byte> LargeText()
  {
    return [.. _alignCentre, .. _sizeDouble, .. _emphasisOn];
  }

  private SlipTimeFormats FormatsFor(string languageCode)
  {
    return languageCode.StartsWith("en", StringComparison.OrdinalIgnoreCase)
             ? new("dd/MM/yyyy, HH:mm", "HH:mm")
             : new SlipTimeFormats("dd.MM.yyyy, HH:mm 'Uhr'", "HH:mm 'Uhr'");
  }

  private string FormatMoment(DateTimeOffset momentUtc, TimeZoneInfo displayTimeZone, string format)
  {
    var local = TimeZoneInfo.ConvertTime(momentUtc, displayTimeZone);
    return local.ToString(format, CultureInfo.InvariantCulture);
  }

  private string MajorSeparator()
  {
    return new('=', LineWidth);
  }

  private string MinorSeparator()
  {
    return new('-', LineWidth);
  }

  private IReadOnlyList<string> SplitParagraph(string paragraph)
  {
    return paragraph.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
  }

  private IReadOnlyList<string> Wrap(string text)
  {
    return WrapTo(text, LineWidth);
  }

  private IReadOnlyList<string> WrapLarge(string text)
  {
    return WrapTo(text, DoubleWidthLineWidth);
  }

  private IReadOnlyList<string> WrapTo(string text, int width)
  {
    var printable = _encoder.ToPrintableText(text);
    if (printable.Length <= width)
    {
      return [printable];
    }

    var continuationWidth = width - ContinuationIndentWidth;
    List<string> lines = [printable[..width]];
    var position = width;
    while (position < printable.Length)
    {
      var take = Math.Min(continuationWidth, printable.Length - position);
      lines.Add(ContinuationIndent + printable.Substring(position, take));
      position += take;
    }

    return lines;
  }

  private sealed record SlipSegment(IReadOnlyList<byte> Commands, IReadOnlyList<string> Lines);
}
