using System.Text;
using StorePOS.Models;

namespace StorePOS.Tests;

public class PriceHistoryTests
{
    [Fact]
    public void Adding_a_product_records_its_first_price_and_editing_records_only_price_changes()
    {
        using var shop = new TestShop();
        var p = shop.AddProduct("Glyphosate", 14000);
        var added = shop.Now;

        shop.Now = new DateTime(2026, 10, 14, 8, 5, 0);
        p.Name = "Glyphosate herbicide";
        shop.S.Products.Save(p);
        p.Price = 15000;
        shop.S.Products.Save(p);

        var history = shop.S.Products.PriceHistory(p.Id);
        Assert.Equal(2, history.Count);
        Assert.Equal((PriceSource.Edited, (long?)14000, 15000L, shop.Now), (history[0].Source, history[0].OldPrice, history[0].NewPrice, history[0].At));
        Assert.Equal((PriceSource.Added, (long?)null, 14000L, added), (history[1].Source, history[1].OldPrice, history[1].NewPrice, history[1].At));
        Assert.Equal(shop.Now, shop.S.Products.Get(p.Id)!.PriceChangedAt);
    }

    [Fact]
    public void A_csv_import_records_new_products_and_changed_prices_with_the_file_name()
    {
        using var shop = new TestShop();
        var glyphosate = shop.AddProduct("Glyphosate", 14000);
        var paraquat = shop.AddProduct("Paraquat", 8500);

        shop.Now = new DateTime(2026, 10, 14, 7, 40, 0);
        var path = Path.Combine(shop.Folder, "prices-oct-2026.csv");
        File.WriteAllText(path, """
            Glyphosate,Herbicide,bottle,15000,no
            Paraquat,Herbicide,bottle,8500,no
            Atrazine,Herbicide,bag,14000,no
            """, new UTF8Encoding(false));
        var preview = shop.S.Import.Preview(path);
        Assert.Equal("prices-oct-2026.csv", preview.FileName);
        Assert.Equal(2, shop.S.Import.Apply(preview));

        var changed = shop.S.Products.PriceHistory(glyphosate.Id)[0];
        Assert.Equal((PriceSource.CsvImport, (long?)14000, 15000L, "prices-oct-2026.csv", shop.Now),
            (changed.Source, changed.OldPrice, changed.NewPrice, changed.FileName, changed.At));
        Assert.Single(shop.S.Products.PriceHistory(paraquat.Id)); // same price: nothing new

        var atrazine = shop.S.Products.List(true).Single(x => x.Name == "Atrazine");
        var first = Assert.Single(shop.S.Products.PriceHistory(atrazine.Id));
        Assert.Equal((PriceSource.CsvImport, (long?)null, 14000L, "prices-oct-2026.csv"),
            (first.Source, first.OldPrice, first.NewPrice, first.FileName));
    }

    [Fact]
    public void An_import_records_the_price_it_actually_replaced_even_if_the_preview_is_out_of_date()
    {
        using var shop = new TestShop();
        var p = shop.AddProduct("Glyphosate", 14000);
        var preview = shop.S.Import.Preview(Encoding.UTF8.GetBytes("Glyphosate,Herbicide,bottle,16000,no\n"));

        p.Price = 15000;
        shop.S.Products.Save(p);
        shop.S.Import.Apply(preview);

        var latest = shop.S.Products.PriceHistory(p.Id)[0];
        Assert.Equal(((long?)15000, 16000L), (latest.OldPrice, latest.NewPrice));
        Assert.Equal(16000, shop.S.Products.Get(p.Id)!.Price);
    }

    [Fact]
    public void A_price_changed_for_one_sale_is_not_a_price_change()
    {
        using var shop = new TestShop();
        var p = shop.AddProduct("Paraquat", 8500, stock: 100);
        shop.Sell(shop.Now.AddHours(1), 8000, null, TestShop.Line(p, 1, price: 8000));

        Assert.Equal(8500, shop.S.Products.Get(p.Id)!.Price);
        Assert.Single(shop.S.Products.PriceHistory(p.Id));
    }
}
