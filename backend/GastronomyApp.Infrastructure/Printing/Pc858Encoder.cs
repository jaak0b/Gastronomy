using System.Globalization;
using System.Text;

namespace GastronomyApp.Infrastructure.Printing;

public sealed class Pc858Encoder
{
    private const char Substitute = '?';

    private readonly IReadOnlyDictionary<char, byte> highRange;
    private readonly IReadOnlyDictionary<char, string> transliterations;

    public Pc858Encoder()
    {
        highRange = new Dictionary<char, byte>
        {
            ['ü'] = 0x81,
            ['é'] = 0x82,
            ['â'] = 0x83,
            ['ä'] = 0x84,
            ['à'] = 0x85,
            ['ç'] = 0x87,
            ['ê'] = 0x88,
            ['ë'] = 0x89,
            ['è'] = 0x8A,
            ['ï'] = 0x8B,
            ['î'] = 0x8C,
            ['Ä'] = 0x8E,
            ['ô'] = 0x93,
            ['ö'] = 0x94,
            ['û'] = 0x96,
            ['ù'] = 0x97,
            ['Ö'] = 0x99,
            ['Ü'] = 0x9A,
            ['£'] = 0x9C,
            ['á'] = 0xA0,
            ['í'] = 0xA1,
            ['ó'] = 0xA2,
            ['ú'] = 0xA3,
            ['ñ'] = 0xA4,
            ['Ñ'] = 0xA5,
            ['€'] = 0xD5,
            ['ß'] = 0xE1,
            ['µ'] = 0xE6,
            ['°'] = 0xF8,
        };

        transliterations = new Dictionary<char, string>
        {
            ['ł'] = "l",
            ['Ł'] = "L",
            ['đ'] = "d",
            ['Đ'] = "D",
            ['ø'] = "o",
            ['Ø'] = "O",
            ['æ'] = "ae",
            ['Æ'] = "AE",
            ['œ'] = "oe",
            ['Œ'] = "OE",
            ['\u2013'] = "-",
            ['\u2014'] = "-",
            ['\u2018'] = "'",
            ['\u2019'] = "'",
            ['\u201A'] = "'",
            ['\u2026'] = "...",
            ['\u00A0'] = " ",
            ['\u2022'] = "-",
            ['\u201C'] = "\"",
            ['\u201D'] = "\"",
            ['\u201E'] = "\"",
        };
    }

    public string ToPrintableText(string text)
    {
        StringBuilder builder = new(text.Length);
        foreach (char character in text)
        {
            if (IsRepresentable(character))
            {
                builder.Append(character);
                continue;
            }

            builder.Append(Transliterate(character));
        }

        return builder.ToString();
    }

    public byte[] GetBytes(string text)
    {
        string printable = ToPrintableText(text);
        byte[] bytes = new byte[printable.Length];
        for (int index = 0; index < printable.Length; index++)
        {
            bytes[index] = Encode(printable[index]);
        }

        return bytes;
    }

    private byte Encode(char character)
    {
        if (character <= 0x7F)
        {
            return (byte)character;
        }

        return highRange.TryGetValue(character, out byte mapped) ? mapped : (byte)Substitute;
    }

    private bool IsRepresentable(char character)
    {
        return character <= 0x7F || highRange.ContainsKey(character);
    }

    private string Transliterate(char character)
    {
        if (transliterations.TryGetValue(character, out string? replacement))
        {
            return replacement;
        }

        string decomposed = character.ToString().Normalize(NormalizationForm.FormD);
        StringBuilder stripped = new(decomposed.Length);
        foreach (char part in decomposed)
        {
            bool isMark = CharUnicodeInfo.GetUnicodeCategory(part) == UnicodeCategory.NonSpacingMark;
            if (!isMark && IsRepresentable(part))
            {
                stripped.Append(part);
            }
        }

        return stripped.Length == 0 ? Substitute.ToString() : stripped.ToString();
    }
}
