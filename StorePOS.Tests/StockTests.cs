using StorePOS.Infrastructure;
using StorePOS.Models;
using StorePOS.ViewModels;

namespace StorePOS.Tests;

public class StockTests
{
    private static decimal OnHand(TestShop shop, Product p) => shop.S.Stock.OnHand(p.Id);

    [Fact]
    public void Opening_count_delivery_sales_and_a_void_all_show_in_stock_and_its_history()
    {
        using var shop = new TestShop();
        var glyphosate = shop.AddProduct("Glyphosate herbicide 1 L", 15000);
        var urea = shop.AddProduct("Urea fertilizer (loose)", 2200, weight: true);

        shop.Now = new DateTime(2026, 10, 12, 17, 0, 0);
        shop.S.Stock.Adjust(glyphosate.Id, 4, AdjustReason.CountWrong, "Opening count");
        shop.S.Stock.Adjust(urea.Id, 50, AdjustReason.CountWrong, null);

        shop.Now = new DateTime(2026, 10, 13, 8, 0, 0);
        shop.S.Stock.RecordStockIn(shop.Now.Date, "Shwe Pyi Trading", "Invoice SP-1189",
            [new(glyphosate.Id, 36), new(urea.Id, 25.5m)]);

        shop.Sell(new DateTime(2026, 10, 13, 9, 0, 0), 30000, null, TestShop.Line(glyphosate, 2));
        var voided = shop.Sell(new DateTime(2026, 10, 13, 10, 0, 0), 20500, null, TestShop.Line(glyphosate, 1), TestShop.Line(urea, 2.5m));
        shop.Sell(new DateTime(2026, 10, 13, 11, 0, 0), 45000, null, TestShop.Line(glyphosate, 3));
        shop.Now = new DateTime(2026, 10, 13, 12, 0, 0);
        shop.S.Sales.Void(voided, "Wrong quantity entered");

        Assert.Equal(35, OnHand(shop, glyphosate));
        Assert.Equal(75.5m, OnHand(shop, urea));

        var moves = shop.S.Stock.Movements(glyphosate.Id);
        Assert.Equal(
            [StockMoveKind.Void, StockMoveKind.Sale, StockMoveKind.Sale, StockMoveKind.Sale, StockMoveKind.StockIn, StockMoveKind.Adjustment],
            moves.Select(m => m.Kind));
        Assert.Equal([1m, -3, -1, -2, 36, 4], moves.Select(m => m.Change));
        Assert.Equal([35m, 34, 37, 38, 40, 4], moves.Select(m => m.After));
        Assert.Equal("Shwe Pyi Trading", moves.Single(m => m.Kind == StockMoveKind.StockIn).Supplier);
        Assert.Equal(voided, moves[0].SaleId);
    }

    [Fact]
    public void A_sale_cannot_take_more_than_is_in_stock()
    {
        using var shop = new TestShop();
        var paraquat = shop.AddProduct("Paraquat", 8500, stock: 1);
        var urea = shop.AddProduct("Urea (loose)", 2200, weight: true, stock: 2.5m);

        var ex = Assert.Throws<DomainException>(() => shop.Sell(shop.Now, 17000, null, TestShop.Line(paraquat, 2)));
        Assert.Contains("1 bottle", ex.Message);
        // Two lines of one product, one at a changed price, count together.
        Assert.Throws<DomainException>(() => shop.Sell(shop.Now, 16500, null,
            TestShop.Line(paraquat, 1), TestShop.Line(paraquat, 1, price: 8000)));
        Assert.Throws<DomainException>(() => shop.Sell(shop.Now, 6000, null, TestShop.Line(urea, 2.501m)));

        // Nothing of a refused sale is saved.
        Assert.Empty(shop.S.Sales.SalesBetween(shop.Now.Date, shop.Now.Date.AddDays(1)));
        Assert.Equal(1, OnHand(shop, paraquat));

        shop.Sell(shop.Now, 14000, null, TestShop.Line(paraquat, 1), TestShop.Line(urea, 2.5m));
        Assert.All(shop.S.Stock.Levels(), l => Assert.Equal(0, l.OnHand));
        Assert.Throws<DomainException>(() => shop.Sell(shop.Now, 8500, null, TestShop.Line(paraquat, 1)));
    }

    [Fact]
    public void The_sales_screen_will_not_save_a_cart_that_takes_more_than_is_in_stock()
    {
        using var shop = new TestShop();
        var p = shop.AddProduct("Paraquat", 8500, stock: 1);
        var sales = new MainViewModel(shop.S).SalesPage;
        sales.Activate();
        var row = sales.ProductsView.Cast<ProductRow>().Single();
        sales.AddOne(row);
        sales.AddOne(row);
        sales.PaidText = "17000";

        Assert.False(sales.CanSave);
        Assert.StartsWith("Not enough Paraquat in stock: the app has 1 bottle.", sales.Problem);

        // The shelf had more: once the count is corrected, the same cart can be saved.
        shop.S.Stock.Adjust(p.Id, 5, AdjustReason.CountWrong, null);
        sales.Activate();
        Assert.True(sales.CanSave);
        sales.SaveCommand.Execute(null);
        Assert.Equal(3, OnHand(shop, p));
    }

    [Fact]
    public void A_product_never_counted_is_out_of_stock()
    {
        using var shop = new TestShop();
        shop.AddProduct("Butachlor", 13500);
        var level = shop.S.Stock.Levels().Single();
        Assert.Equal(0, level.OnHand);
        Assert.True(level.IsOut);
    }

    [Fact]
    public void Adjust_count_records_only_the_difference_and_rejects_a_count_that_changes_nothing()
    {
        using var shop = new TestShop();
        var p = shop.AddProduct("Cypermethrin", 6000);
        shop.S.Stock.Adjust(p.Id, 27, AdjustReason.CountWrong, null);

        shop.Now = shop.Now.AddHours(2);
        Assert.Equal(-2, shop.S.Stock.Adjust(p.Id, 25, AdjustReason.Damaged, " 2 bottles cracked "));
        Assert.Throws<DomainException>(() => shop.S.Stock.Adjust(p.Id, 25, AdjustReason.Other, null));

        var latest = shop.S.Stock.Movements(p.Id)[0];
        Assert.Equal(AdjustReason.Damaged, latest.Reason);
        Assert.Equal("2 bottles cracked", latest.Note);
        Assert.Equal(25, latest.After);
        Assert.Equal(2, shop.S.Stock.Movements(p.Id).Count);
    }

    [Fact]
    public void Counts_are_whole_numbers_except_for_products_sold_by_weight()
    {
        using var shop = new TestShop();
        var bottle = shop.AddProduct("Imidacloprid", 4500);
        var loose = shop.AddProduct("NPK (loose)", 2600, weight: true);

        Assert.Throws<DomainException>(() => shop.S.Stock.Adjust(bottle.Id, 2.5m, AdjustReason.CountWrong, null));
        Assert.Throws<DomainException>(() => shop.S.Stock.Adjust(bottle.Id, -1, AdjustReason.CountWrong, null));
        Assert.Throws<DomainException>(() => shop.S.Stock.RecordStockIn(shop.Now, "", "", [new(bottle.Id, 1.5m)]));
        Assert.Throws<DomainException>(() => shop.S.Stock.RecordStockIn(shop.Now, "", "", [new(bottle.Id, 0)]));
        Assert.Throws<DomainException>(() => shop.S.Stock.RecordStockIn(shop.Now, "", "", [new(loose.Id, 1.2345m)]));

        shop.S.Stock.Adjust(loose.Id, 37.5m, AdjustReason.CountWrong, null);
        shop.S.Stock.RecordStockIn(shop.Now, "", "", [new(loose.Id, 0.25m)]);
        Assert.Equal(37.75m, OnHand(shop, loose));
    }

    [Fact]
    public void One_delivery_saves_every_line_with_stock_before_and_after()
    {
        using var shop = new TestShop();
        var a = shop.AddProduct("Glyphosate", 15000, company: "Green Valley", size: "1 L");
        var b = shop.AddProduct("Paraquat", 8500);
        shop.S.Stock.Adjust(a.Id, 4, AdjustReason.CountWrong, null);

        shop.Now = shop.Now.AddHours(1);
        var id = shop.S.Stock.RecordStockIn(shop.Now.Date, "  Shwe Pyi Trading ", "Invoice SP-1189",
            [new(a.Id, 30), new(b.Id, 24), new(a.Id, 6)]);

        var record = shop.S.Stock.StockIns().Single();
        Assert.Equal(id, record.Id);
        Assert.Equal("Shwe Pyi Trading", record.Supplier);
        Assert.Equal("Invoice SP-1189", record.Note);
        Assert.Equal(shop.Now, record.ReceivedAt);
        var line = record.Lines.Single(l => l.ProductId == a.Id);
        Assert.Equal("Glyphosate 1 L (Green Valley)", line.Name);
        Assert.Equal((36m, 4m, 40m), (line.Quantity, line.Before, line.After));
        Assert.Equal(24, record.Lines.Single(l => l.ProductId == b.Id).After);
        Assert.Equal(shop.Now, shop.S.Stock.Levels().Single(l => l.Product.Id == a.Id).LastStockIn);
        Assert.Equal(["Shwe Pyi Trading"], shop.S.Stock.Suppliers());
    }

    [Fact]
    public void A_delivery_cannot_be_empty_or_in_the_future_and_an_earlier_day_keeps_that_day()
    {
        using var shop = new TestShop();
        var p = shop.AddProduct("Rice seed", 3500);

        Assert.Throws<DomainException>(() => shop.S.Stock.RecordStockIn(shop.Now.Date, "", "", []));
        Assert.Throws<DomainException>(() => shop.S.Stock.RecordStockIn(shop.Now.Date.AddDays(1), "", "", [new(p.Id, 10)]));

        shop.S.Stock.RecordStockIn(shop.Now.Date.AddDays(-2), "", "", [new(p.Id, 150)]);
        var record = shop.S.Stock.StockIns().Single();
        Assert.Equal(shop.Now.AddDays(-2), record.ReceivedAt);
        Assert.Equal("", record.Supplier);
    }

    [Fact]
    public void Voiding_a_sale_saved_before_stock_was_tracked_returns_nothing()
    {
        using var shop = new TestShop();
        var p = shop.AddProduct("Mancozeb", 11000, stock: 1);
        var sale = shop.Sell(shop.Now, 11000, null, TestShop.Line(p, 1));
        using (var s = shop.S.Db.Write())
        {
            // As if the sale had been saved by version 2, which kept no stock movements.
            s.Exec("DELETE FROM stock_movements WHERE sale_id = $id", ("$id", sale));
            s.Commit();
        }

        shop.S.Sales.Void(sale, "Entered twice");

        // Only the opening count is left.
        Assert.Equal(1, OnHand(shop, p));
        Assert.Equal(StockMoveKind.Adjustment, Assert.Single(shop.S.Stock.Movements(p.Id)).Kind);
    }
}
