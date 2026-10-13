using StorePOS.Infrastructure;

namespace StorePOS.Tests;

public class FmtTests
{
    [Theory]
    [InlineData("50000", 50000)]
    [InlineData("50,000", 50000)]
    [InlineData(" 78,500 ", 78500)]
    [InlineData("၅၀,၀၀၀", 50000)]
    [InlineData("0", 0)]
    public void Money_accepts_commas_spaces_and_Burmese_digits(string text, long expected)
    {
        Assert.True(Fmt.TryParseMoney(text, out var value));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("12.5")]
    [InlineData("-5")]
    [InlineData("12,5OO")]
    [InlineData("abc")]
    public void Money_rejects_anything_but_whole_kyat(string text) =>
        Assert.False(Fmt.TryParseMoney(text, out _));

    [Fact]
    public void Quantity_allows_decimals_only_for_weight_products()
    {
        Assert.True(Fmt.TryParseQty("2.5", allowDecimals: true, out var q));
        Assert.Equal(2.5m, q);
        Assert.False(Fmt.TryParseQty("2.5", allowDecimals: false, out _));
        Assert.True(Fmt.TryParseQty("3", allowDecimals: false, out _));
        Assert.False(Fmt.TryParseQty("0", allowDecimals: true, out _));
        Assert.False(Fmt.TryParseQty("1.2345", allowDecimals: true, out _));
        Assert.True(Fmt.TryParseQty("၂.၅", allowDecimals: true, out q));
        Assert.Equal(2.5m, q);
    }

    [Theory]
    [InlineData(2200, "2.5", 5500)]
    [InlineData(2600, "0.333", 866)] // 865.8 rounds to 866
    [InlineData(1500, "0.333", 500)] // 499.5 rounds away from zero
    public void Line_total_rounds_to_whole_kyat(long price, string qty, long expected) =>
        Assert.Equal(expected, Fmt.LineTotal(price, decimal.Parse(qty, System.Globalization.CultureInfo.InvariantCulture)));

    [Fact]
    public void Money_uses_thousands_separators() => Assert.Equal("1,244,000", Fmt.Money(1244000));

    [Fact]
    public void Search_key_ignores_case_spacing_and_unicode_form()
    {
        Assert.Equal("glyphosate herbicide 1 l", Fmt.SearchKey("  Glyphosate   Herbicide 1 L "));
        Assert.Equal(Fmt.SearchKey("ယူရီးယား"), Fmt.SearchKey("ယူရီးယား".Normalize(System.Text.NormalizationForm.FormD)));
    }

    [Fact]
    public void Week_runs_Monday_to_Sunday()
    {
        Assert.Equal(new DateTime(2026, 10, 12), Fmt.WeekStart(new DateTime(2026, 10, 13)));
        Assert.Equal(new DateTime(2026, 10, 12), Fmt.WeekStart(new DateTime(2026, 10, 18)));
        Assert.Equal(new DateTime(2026, 10, 19), Fmt.WeekStart(new DateTime(2026, 10, 19)));
    }
}
