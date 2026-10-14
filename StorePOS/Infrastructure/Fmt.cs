using System.Globalization;
using System.Text;

namespace StorePOS.Infrastructure;

/// <summary>Money is whole kyat (long). Quantities are decimal with at most 3 decimal places.</summary>
public static class Fmt
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static string Money(long value) => value.ToString("#,0", Inv);

    /// <summary>A price change: "+1,000" or "−500", with a true minus sign.</summary>
    public static string SignedMoney(long value) =>
        value < 0 ? "−" + Money(-value) : value > 0 ? "+" + Money(value) : Money(0);

    /// <summary>"78,500 Ks"</summary>
    public static string Ks(long value) => Loc.F("Fmt_Ks", Money(value));

    public static string Qty(decimal value) => value.ToString("#,0.###", Inv);

    /// <summary>Weight products show the unit ("2.5 kg"); counted products show the number only.</summary>
    public static string QtyWithUnit(decimal value, string unit, bool soldByWeight) =>
        soldByWeight ? $"{Qty(value)} {unit}" : Qty(value);

    /// <summary>Stock and stock changes: "−2" with a true minus sign, "+36" when the sign is asked for.</summary>
    public static string Stock(decimal value, bool showPlus = false) =>
        value < 0 ? "−" + Qty(-value) : showPlus && value > 0 ? "+" + Qty(value) : Qty(value);

    public static string SaleNo(long id) => "#" + id.ToString("0000", Inv);

    public static long LineTotal(long unitPrice, decimal quantity) =>
        (long)Math.Round(unitPrice * quantity, MidpointRounding.AwayFromZero);

    /// <summary>Turns Burmese digits (၀-၉) into 0-9 and drops spaces, so either keyboard works.</summary>
    public static string NormalizeDigits(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var sb = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            if (ch >= '၀' && ch <= '၉') sb.Append((char)('0' + (ch - '၀')));
            else if (!char.IsWhiteSpace(ch)) sb.Append(ch);
        }
        return sb.ToString();
    }

    /// <summary>Accepts "50000" or "50,000". Whole kyat only.</summary>
    public static bool TryParseMoney(string? text, out long value)
    {
        value = 0;
        var s = NormalizeDigits(text).Replace(",", "");
        if (s.Length == 0 || s.Length > 15) return false;
        foreach (var ch in s) if (ch < '0' || ch > '9') return false;
        return long.TryParse(s, NumberStyles.None, Inv, out value);
    }

    /// <summary>Accepts "2", "2.5", "1,000". At most 3 decimal places. Must be more than zero, unless allowZero (a shelf count).</summary>
    public static bool TryParseQty(string? text, bool allowDecimals, out decimal value, bool allowZero = false)
    {
        value = 0;
        var s = NormalizeDigits(text).Replace(",", "");
        if (s.Length == 0 || s.Length > 12) return false;
        var dot = s.IndexOf('.');
        if (dot >= 0 && (!allowDecimals || s.Length - dot - 1 > 3 || dot != s.LastIndexOf('.'))) return false;
        foreach (var ch in s) if ((ch < '0' || ch > '9') && ch != '.') return false;
        if (!decimal.TryParse(s, NumberStyles.AllowDecimalPoint, Inv, out value)) return false;
        return allowZero || value > 0;
    }

    public static long ToMilli(decimal qty) => (long)(qty * 1000m);
    public static decimal FromMilli(long milli) => milli / 1000m;

    public static string DbTime(DateTime t) => t.ToString("yyyy-MM-dd HH:mm:ss", Inv);
    public static string DbDate(DateTime d) => d.ToString("yyyy-MM-dd", Inv);
    public static DateTime ParseDbTime(string s) => DateTime.ParseExact(s, "yyyy-MM-dd HH:mm:ss", Inv);
    public static DateTime ParseDbDate(string s) => DateTime.ParseExact(s, "yyyy-MM-dd", Inv);

    /// <summary>Key used for case-insensitive matching of product names (Burmese or English).</summary>
    public static string SearchKey(string? text) =>
        string.Join(' ', (text ?? "").Normalize(NormalizationForm.FormC)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();

    /// <summary>Key for matching sizes: "1 L", "1l" and "၁ L" are the same size.</summary>
    public static string SizeKey(string? text) => NormalizeDigits(SearchKey(text));

    public static DateTime WeekStart(DateTime d) => d.Date.AddDays(-(((int)d.DayOfWeek + 6) % 7));
}
