using System.Globalization;
using GastronomyApp.Core.Localization;

namespace GastronomyApp.Infrastructure.Printing;

public sealed record SlipLine(int Quantity, string ItemName, string? LineNote);

public sealed record SlipRenderRequest(
    string LocationName,
    string LanguageCode,
    int LocationSequenceNumber,
    int GlobalOrderNumber,
    string TableName,
    string StaffMemberName,
    DateTimeOffset OrderTakenAtUtc,
    TimeZoneInfo DisplayTimeZone,
    IReadOnlyList<SlipLine> Lines,
    string? OrderNote,
    IReadOnlyList<string> AlsoGoesToStationNames,
    string? ChosenStationNameIfDifferent);

public sealed record TestSlipRenderRequest(
    string LocationName,
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

    private readonly ISlipTextProvider slipTextProvider;
    private readonly Pc858Encoder encoder;

    private readonly byte[] initialise = [0x1B, 0x40];
    private readonly byte[] selectCodePage = [0x1B, 0x74, 0x13];
    private readonly byte[] enableAutomaticStatusBack = [0x1D, 0x61, 0x0F];
    private readonly byte[] alignCentre = [0x1B, 0x61, 0x01];
    private readonly byte[] alignLeft = [0x1B, 0x61, 0x00];
    private readonly byte[] sizeDouble = [0x1D, 0x21, 0x11];
    private readonly byte[] sizeNormal = [0x1D, 0x21, 0x00];
    private readonly byte[] emphasisOn = [0x1B, 0x45, 0x01];
    private readonly byte[] emphasisOff = [0x1B, 0x45, 0x00];
    private readonly byte[] feed = [0x1B, 0x64, 0x04];
    private readonly byte[] partialCut = [0x1D, 0x56, 0x42, 0x03];
    private readonly byte[] qrSelectModel = [0x1D, 0x28, 0x6B, 0x04, 0x00, 0x31, 0x41, 0x32, 0x00];
    private readonly byte[] qrModuleSize = [0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x43, 0x06];
    private readonly byte[] qrErrorCorrection = [0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x45, 0x31];
    private readonly byte[] qrPrintSymbol = [0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x51, 0x30];

    public EscPosSlipRenderer(ISlipTextProvider slipTextProvider)
    {
        this.slipTextProvider = slipTextProvider;
        encoder = new Pc858Encoder();
    }

    public RenderedSlip RenderInitialSlip(SlipRenderRequest request)
    {
        return RenderSlip(request, null, null);
    }

    public RenderedSlip RenderReprintSlip(SlipRenderRequest request, DateTimeOffset reprintAtUtc, TimeZoneInfo displayTimeZone)
    {
        return RenderSlip(request, reprintAtUtc, displayTimeZone);
    }

    public RenderedSlip RenderTestSlip(TestSlipRenderRequest request)
    {
        SlipStrings strings = slipTextProvider.GetStrings(request.LanguageCode);
        SlipTimeFormats formats = FormatsFor(request.LanguageCode);
        List<SlipSegment> segments =
        [
            new SlipSegment(NormalText(), [MajorSeparator()]),
            new SlipSegment(LargeText(), WrapLarge(request.LocationName)),
            new SlipSegment(NormalText(), [MajorSeparator()]),
            new SlipSegment(LargeText(), WrapLarge(strings.TestSlipHeader)),
            new SlipSegment(NormalText(), [encoder.ToPrintableText(FormatMoment(request.PrintedAtUtc, request.DisplayTimeZone, formats.DateAndTime)), MajorSeparator()]),
        ];

        return Compose(segments);
    }

    private RenderedSlip RenderSlip(SlipRenderRequest request, DateTimeOffset? reprintAtUtc, TimeZoneInfo? reprintTimeZone)
    {
        SlipStrings strings = slipTextProvider.GetStrings(request.LanguageCode);
        SlipTimeFormats formats = FormatsFor(request.LanguageCode);
        List<SlipSegment> segments = [new SlipSegment(NormalText(), [MajorSeparator()])];

        if (reprintAtUtc is not null && reprintTimeZone is not null)
        {
            segments.Add(new SlipSegment(LargeText(), WrapLarge(strings.ReprintBanner)));
            segments.Add(new SlipSegment(
                NormalText(),
                [
                    Wrap($"{strings.ReprintTimePrefix} {FormatMoment(reprintAtUtc.Value, reprintTimeZone, formats.TimeOnly)}")[0],
                    MajorSeparator(),
                ]));
        }

        segments.Add(new SlipSegment(LargeText(), WrapLarge(request.LocationName)));
        segments.Add(new SlipSegment(NormalText(), [MajorSeparator()]));
        segments.Add(new SlipSegment(LargeText(), WrapLarge($"{strings.SlipNumberPrefix} {request.LocationSequenceNumber.ToString("D3", CultureInfo.InvariantCulture)}")));
        segments.Add(new SlipSegment(NormalText(), [MajorSeparator()]));
        segments.Add(new SlipSegment(EmphasisedText(), Wrap($"{strings.OrderNumberPrefix} {request.GlobalOrderNumber.ToString(CultureInfo.InvariantCulture)}")));

        List<string> header =
        [
            .. Wrap($"{strings.TablePrefix} {request.TableName}"),
            .. Wrap($"{strings.StaffMemberPrefix} {request.StaffMemberName}"),
            encoder.ToPrintableText(FormatMoment(request.OrderTakenAtUtc, request.DisplayTimeZone, formats.DateAndTime)),
            MinorSeparator(),
        ];
        segments.Add(new SlipSegment(NormalText(), header));

        List<string> body = [];
        foreach (SlipLine line in request.Lines)
        {
            body.AddRange(Wrap($"{line.Quantity.ToString(CultureInfo.InvariantCulture)} x {line.ItemName}"));
            if (line.LineNote is not null)
            {
                body.AddRange(Wrap($"{ContinuationIndent}{strings.NotePrefix} {line.LineNote}"));
            }
        }

        body.Add(MinorSeparator());
        segments.Add(new SlipSegment(NormalText(), body));

        int itemsTotal = request.Lines.Sum(line => line.Quantity);
        List<string> footer = [.. Wrap($"{strings.ItemsTotalPrefix} {itemsTotal.ToString(CultureInfo.InvariantCulture)}")];

        if (request.OrderNote is not null)
        {
            footer.AddRange(Wrap($"{strings.NotePrefix} {request.OrderNote}"));
        }

        if (request.AlsoGoesToStationNames.Count > 0)
        {
            footer.AddRange(Wrap($"{strings.AlsoGoesToPrefix} {string.Join(", ", request.AlsoGoesToStationNames)}"));
        }

        if (request.ChosenStationNameIfDifferent is not null)
        {
            footer.AddRange(Wrap($"{strings.ChosenStationWasPrefix} {request.ChosenStationNameIfDifferent}"));
        }

        footer.Add(MajorSeparator());
        segments.Add(new SlipSegment(NormalText(), footer));

        return Compose(segments);
    }

    private RenderedSlip Compose(IReadOnlyList<SlipSegment> segments)
    {
        List<byte> bytes = [.. initialise, .. selectCodePage, .. enableAutomaticStatusBack];
        List<string> allLines = [];

        foreach (SlipSegment segment in segments)
        {
            bytes.AddRange(segment.Commands);
            foreach (string line in segment.Lines)
            {
                bytes.AddRange(encoder.GetBytes(line));
                bytes.AddRange(encoder.GetBytes(LineBreak));
                allLines.Add(line);
            }
        }

        bytes.AddRange(feed);
        bytes.AddRange(partialCut);

        string renderedText = string.Join(LineBreak, allLines) + LineBreak;
        return new RenderedSlip(bytes.ToArray(), renderedText);
    }

    private IReadOnlyList<byte> NormalText()
    {
        return [.. alignLeft, .. sizeNormal, .. emphasisOff];
    }

    private IReadOnlyList<byte> EmphasisedText()
    {
        return [.. alignLeft, .. sizeNormal, .. emphasisOn];
    }

    private IReadOnlyList<byte> LargeText()
    {
        return [.. alignCentre, .. sizeDouble, .. emphasisOn];
    }

    private SlipTimeFormats FormatsFor(string languageCode)
    {
        return languageCode.StartsWith("en", StringComparison.OrdinalIgnoreCase)
            ? new SlipTimeFormats("dd/MM/yyyy, HH:mm", "HH:mm")
            : new SlipTimeFormats("dd.MM.yyyy, HH:mm 'Uhr'", "HH:mm 'Uhr'");
    }

    private string FormatMoment(DateTimeOffset momentUtc, TimeZoneInfo displayTimeZone, string format)
    {
        DateTimeOffset local = TimeZoneInfo.ConvertTime(momentUtc, displayTimeZone);
        return local.ToString(format, CultureInfo.InvariantCulture);
    }

    private string MajorSeparator()
    {
        return new string('=', LineWidth);
    }

    private string MinorSeparator()
    {
        return new string('-', LineWidth);
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
        string printable = encoder.ToPrintableText(text);
        if (printable.Length <= width)
        {
            return [printable];
        }

        int continuationWidth = width - ContinuationIndentWidth;
        List<string> lines = [printable[..width]];
        int position = width;
        while (position < printable.Length)
        {
            int take = Math.Min(continuationWidth, printable.Length - position);
            lines.Add(ContinuationIndent + printable.Substring(position, take));
            position += take;
        }

        return lines;
    }

    private sealed record SlipSegment(IReadOnlyList<byte> Commands, IReadOnlyList<string> Lines);
}
