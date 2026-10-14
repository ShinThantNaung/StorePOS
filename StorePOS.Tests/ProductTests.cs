using System.Text;
using StorePOS.Data;
using StorePOS.Infrastructure;
using StorePOS.Models;
using StorePOS.Services;
using StorePOS.ViewModels;

namespace StorePOS.Tests;

public class ProductTests
{
    [Fact]
    public void The_same_name_may_exist_for_another_company_or_size()
    {
        using var shop = new TestShop();
        shop.AddProduct("Glyphosate", 15000, company: "Green Valley", size: "1 L");
        shop.AddProduct("Glyphosate", 14500, company: "Golden Harvest", size: "1 L");
        shop.AddProduct("Glyphosate", 8000, company: "Green Valley", size: "500 ml");
        shop.AddProduct("Glyphosate", 9000);

        // Capitals, spacing and Burmese digits do not make a different product.
        Assert.Throws<DomainException>(() => shop.AddProduct("glyphosate", 1, company: "GREEN  valley", size: "1l"));
        Assert.Throws<DomainException>(() => shop.AddProduct("Glyphosate", 1, company: "Green Valley", size: "၁ L"));
        Assert.Throws<DomainException>(() => shop.AddProduct("GLYPHOSATE", 1));
        Assert.Equal(4, shop.S.Products.List(true).Count);
    }

    [Fact]
    public void Company_and_size_are_saved_trimmed_and_shown_in_the_full_name()
    {
        using var shop = new TestShop();
        var p = shop.AddProduct("Paraquat", 8500, company: "  Shwe Lal ", size: " 500 cc ");
        var saved = shop.S.Products.Get(p.Id)!;
        Assert.Equal("Shwe Lal", saved.Company);
        Assert.Equal("500 cc", saved.Size);
        Assert.Equal("Paraquat 500 cc (Shwe Lal)", saved.FullName);
        Assert.Equal("Paraquat 500 cc", Product.FormatFullName("Paraquat", "500 cc", ""));
        Assert.Equal("Paraquat (Shwe Lal)", Product.FormatFullName("Paraquat", "", "Shwe Lal"));
        Assert.Equal("Paraquat", Product.FormatFullName("Paraquat", "", ""));
    }

    [Fact]
    public void Reports_name_products_with_their_size_and_company()
    {
        using var shop = new TestShop();
        var a = shop.AddProduct("Glyphosate", 15000, company: "Green Valley", size: "1 L", stock: 100);
        var b = shop.AddProduct("Glyphosate", 14500, company: "Golden Harvest", size: "1 L", stock: 100);
        shop.Sell(shop.Now, 29500, null, TestShop.Line(a, 1), TestShop.Line(b, 1));

        var names = shop.S.Reports.ProductSales(shop.Now.Date, shop.Now.Date.AddDays(1)).Select(p => p.Name).Order();
        Assert.Equal(["Glyphosate 1 L (Golden Harvest)", "Glyphosate 1 L (Green Valley)"], names);
    }

    [Fact]
    public void A_version_1_database_gains_company_and_size_and_keeps_its_products()
    {
        using var shop = new TestShop();
        var db = new Db(Path.Combine(shop.Folder, "old.db"));
        using (var s = db.Write())
        {
            s.Exec("""
                CREATE TABLE products(
                    id INTEGER PRIMARY KEY, name TEXT NOT NULL, name_key TEXT NOT NULL UNIQUE,
                    category TEXT NOT NULL, unit TEXT NOT NULL, price INTEGER NOT NULL,
                    sold_by_weight INTEGER NOT NULL DEFAULT 0, active INTEGER NOT NULL DEFAULT 1);
                INSERT INTO products(name, name_key, category, unit, price) VALUES('Urea', 'urea', 'fertilizer', 'bag', 105000);
                PRAGMA user_version=1;
                """);
            s.Commit();
        }

        db.EnsureCreated();
        db.EnsureCreated(); // the next start must not add the columns again

        var products = new ProductService(db, () => shop.Now);
        var urea = Assert.Single(products.List(true));
        Assert.Equal(("Urea", "", ""), (urea.Name, urea.Company, urea.Size));
        // Price history starts with the upgrade: no made-up first price or date.
        Assert.Null(urea.PriceChangedAt);
        Assert.Empty(products.PriceHistory(urea.Id));
        Assert.Throws<DomainException>(() => products.Save(new Product { Name = "UREA", Unit = "bag", Price = 1 }));
        products.Save(new Product { Name = "Urea", Company = "Green Valley", Unit = "bag", Price = 1 });
        using var check = db.Read();
        Assert.Equal(Db.SchemaVersion, check.Scalar<long>("PRAGMA user_version"));
    }

    [Fact]
    public void Csv_reads_optional_company_and_size_and_matches_on_all_three()
    {
        using var shop = new TestShop();
        shop.AddProduct("Glyphosate", 15000, cat: Category.Herbicide, company: "Green Valley", size: "1 L");
        shop.AddProduct("Paraquat", 8500, cat: Category.Herbicide);

        var preview = shop.S.Import.Preview(new UTF8Encoding(true).GetBytes("""
            name,category,unit,price,sold by weight,company,size
            Glyphosate,Herbicide,bottle,16000,no,green valley,1L
            Glyphosate,Herbicide,bottle,14500,no,Golden Harvest,1 L
            Glyphosate,Herbicide,bottle,8000,,Green Valley,500 ml
            Paraquat,Herbicide,bottle,8500
            Paraquat,Herbicide,bottle,8500,no,Shwe Lal
            Glyphosate,Herbicide,bottle,1,no,Golden Harvest,1 l
            Atrazine,Herbicide,pack,1,no,A,1 kg,extra
            """));

        Assert.Equal(1, preview.ChangeCount);
        Assert.Equal(3, preview.NewCount);
        Assert.Equal(1, preview.SameCount);
        Assert.Equal([7, 8], preview.Errors.Select(e => e.Row));
        Assert.Contains("row 3", preview.Errors[0].Message);
        Assert.Contains("8 columns", preview.Errors[1].Message);
        Assert.Equal("", preview.Rows.Single(r => r.Company == "Shwe Lal").Size);

        Assert.Equal(4, shop.S.Import.Apply(preview));
        var all = shop.S.Products.List(true);
        Assert.Equal(5, all.Count);
        Assert.Equal(16000, all.Single(p => p is { Company: "Green Valley", Size: "1 L" }).Price);
        Assert.Equal("500 ml", all.Single(p => p.Price == 8000).Size);
    }

    [Fact]
    public void Sales_and_prices_filter_by_company_and_size_together_with_the_name()
    {
        using var shop = new TestShop();
        shop.AddProduct("Glyphosate", 15000, company: "Green Valley", size: "1 L");
        shop.AddProduct("Glyphosate", 14500, company: "Golden Harvest", size: "1l");
        shop.AddProduct("Glyphosate", 8000, company: "Green Valley", size: "500 ml");
        shop.AddProduct("Paraquat", 8500, company: "Shwe Lal", size: "500 ml");
        shop.AddProduct("Urea (loose)", 2200, weight: true);
        var main = new MainViewModel(shop.S);

        var sales = main.SalesPage;
        sales.Activate();
        // "1 L" and "1l" are one size, and sizes are listed in number order.
        Assert.Equal(["", "1l", "500ml"], sales.Sizes.Select(o => o.Key));
        Assert.Equal(5, sales.ProductsView.Count);

        sales.CompanyText = "green";
        Assert.Equal(2, sales.ProductsView.Count);
        sales.SelectedSize = sales.Sizes.Single(o => o.Key == "500ml");
        Assert.Equal(8000, Assert.Single(sales.ProductsView.Cast<ProductRow>()).Price);
        sales.CompanyText = "";
        sales.SearchText = "para";
        Assert.Equal("Paraquat 500 ml (Shwe Lal)", Assert.Single(sales.ProductsView.Cast<ProductRow>()).FullName);

        var prices = main.PricesPage;
        prices.Activate();
        prices.CompanyText = "GOLDEN";
        Assert.Equal(14500, Assert.Single(prices.View.Cast<ProductRow>()).Price);
        prices.CompanyText = "";
        prices.SelectedSize = prices.Sizes.Single(o => o.Key == "1l");
        Assert.Equal(2, prices.View.Count);
        prices.Category = "Fertilizer";
        Assert.True(prices.IsEmpty);
    }

    [Fact]
    public void The_chosen_size_is_kept_when_the_list_reloads_and_reset_when_it_is_gone()
    {
        using var shop = new TestShop();
        var big = shop.AddProduct("Glyphosate", 15000, size: "1 L");
        shop.AddProduct("Glyphosate", 8000, size: "500 ml");
        var prices = new MainViewModel(shop.S).PricesPage;
        prices.Activate();
        prices.SelectedSize = prices.Sizes.Single(o => o.Key == "1l");

        prices.Activate();
        Assert.Equal("1l", prices.SelectedSize!.Key);
        Assert.Equal(1, prices.View.Count);

        big.Size = "";
        shop.S.Products.Save(big);
        prices.Activate();
        Assert.Equal("", prices.SelectedSize!.Key);
        Assert.Equal(2, prices.View.Count);
    }

    [Fact]
    public void Units_are_shown_by_name_and_older_typed_units_as_they_were()
    {
        using var shop = new TestShop();
        Assert.Equal("Bottle", Units.Display(Units.Bottle));
        Assert.Equal("Bag", Units.Display("BAG"));
        Assert.Equal("kg", Units.Display("kg"));

        Assert.True(Units.TryParse(" Bottle ", out var unit));
        Assert.Equal(Units.Bottle, unit);
        Assert.False(Units.TryParse("pack", out _));
    }
}
