using System.Diagnostics;
using System.Text;
using StorePOS.ViewModels;
using Xunit.Abstractions;

namespace StorePOS.Tests;

public class SpeedTests(ITestOutputHelper output)
{
    /// <summary>PRD: search results appear within half a second with at least 5,000 products.</summary>
    [Fact]
    public void Searching_5000_products_stays_under_half_a_second_per_keystroke()
    {
        using var shop = new TestShop();
        var csv = new StringBuilder();
        var rng = new Random(1);
        for (var i = 0; i < 5000; i++)
        {
            var kind = i % 3 == 0 ? "herbicide" : i % 3 == 1 ? "ပိုးသတ်ဆေး" : "fertilizer";
            csv.AppendLine($"Product {i} {kind} {rng.Next(1000)} ml,Pesticide,bottle,{1000 + i},no");
        }

        var sw = Stopwatch.StartNew();
        var preview = shop.S.Import.Preview(Encoding.UTF8.GetBytes(csv.ToString()));
        Assert.Equal(5000, shop.S.Import.Apply(preview));
        output.WriteLine($"Import of 5,000 rows: {sw.ElapsedMilliseconds} ms");

        var main = new MainViewModel(shop.S);
        string[] typing = ["h", "he", "her", "herb", "ပိုး", "12", "product 4999"];

        sw.Restart();
        main.SalesPage.Activate();
        output.WriteLine($"Sales screen load: {sw.ElapsedMilliseconds} ms");
        sw.Restart();
        foreach (var q in typing) main.SalesPage.SearchText = q;
        var sales = sw.Elapsed / typing.Length;
        Assert.Equal(1, main.SalesPage.ProductsView.Count);

        sw.Restart();
        main.PricesPage.Activate();
        output.WriteLine($"Prices screen load: {sw.ElapsedMilliseconds} ms");
        sw.Restart();
        foreach (var q in typing) main.PricesPage.SearchText = q;
        var prices = sw.Elapsed / typing.Length;
        Assert.Equal(1, main.PricesPage.View.Count);

        output.WriteLine($"Per keystroke: Sales {sales.TotalMilliseconds:F1} ms, Prices {prices.TotalMilliseconds:F1} ms");
        Assert.True(sales < TimeSpan.FromMilliseconds(500), $"Sales search took {sales.TotalMilliseconds} ms");
        Assert.True(prices < TimeSpan.FromMilliseconds(500), $"Prices search took {prices.TotalMilliseconds} ms");
    }
}
