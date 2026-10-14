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

    /// <summary>PRD: current stock for all products loads within one second with a year of stock movements.</summary>
    [Fact]
    public void Current_stock_loads_under_a_second_with_a_year_of_movements()
    {
        using var shop = new TestShop();
        var csv = new StringBuilder();
        for (var i = 0; i < 5000; i++) csv.AppendLine($"Product {i},Pesticide,bottle,{1000 + i},no");
        shop.S.Import.Apply(shop.S.Import.Preview(Encoding.UTF8.GetBytes(csv.ToString())));
        var ids = shop.S.Products.List(includeInactive: true).Select(p => p.Id).ToArray();

        // A busy year: about 270 movements a day.
        var rng = new Random(3);
        var start = new DateTime(2025, 10, 13, 8, 0, 0);
        using (var s = shop.S.Db.Write())
        {
            for (var i = 0; i < 100_000; i++)
            {
                var stockIn = i % 20 == 0;
                s.Exec("INSERT INTO stock_movements(product_id, moved_at, change_milli, kind) VALUES($p, $t, $q, $k)",
                    ("$p", ids[rng.Next(ids.Length)]), ("$t", Infrastructure.Fmt.DbTime(start.AddMinutes(i * 5.25))),
                    ("$q", stockIn ? 24_000 : -1_000 * rng.Next(1, 4)), ("$k", stockIn ? "stock_in" : "sale"));
            }
            s.Commit();
        }

        var main = new MainViewModel(shop.S);
        var sw = Stopwatch.StartNew();
        main.Navigate(Section.Stock);
        var load = sw.Elapsed;
        Assert.Equal(5000, main.StockPage.View.Count);

        sw.Restart();
        main.StockPage.Selected = main.StockPage.View.Cast<StockRow>().First();
        var history = sw.Elapsed;

        output.WriteLine($"Stock screen load: {load.TotalMilliseconds:F0} ms, one product's history: {history.TotalMilliseconds:F0} ms");
        Assert.True(load < TimeSpan.FromSeconds(1), $"Stock screen took {load.TotalMilliseconds} ms");
        Assert.True(history < TimeSpan.FromMilliseconds(500), $"Stock history took {history.TotalMilliseconds} ms");
    }
}
