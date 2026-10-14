using StorePOS.Infrastructure;
using StorePOS.Models;

namespace StorePOS.Tests;

public class SaleAndDebtTests
{
    [Fact]
    public void Paid_in_full_sale_needs_no_customer_and_creates_no_debt()
    {
        using var shop = new TestShop();
        var gly = shop.AddProduct("Glyphosate herbicide 1 L", 15000, stock: 100);
        var id = shop.Sell(shop.Now, 50000, null, TestShop.Line(gly, 2));

        var sale = shop.S.Sales.SalesBetween(shop.Now.Date, shop.Now.Date.AddDays(1)).Single();
        Assert.Equal(id, sale.Id);
        Assert.Equal(30000, sale.Total);
        Assert.Equal(30000, sale.AmountPaid); // change was given back
        Assert.Empty(shop.S.Debts.Balances());
    }

    [Fact]
    public void Part_payment_without_customer_is_refused()
    {
        using var shop = new TestShop();
        var gly = shop.AddProduct("Glyphosate herbicide 1 L", 15000);
        Assert.Throws<DomainException>(() => shop.Sell(shop.Now, 10000, null, TestShop.Line(gly, 2)));
        Assert.Empty(shop.S.Sales.SalesBetween(shop.Now.Date, shop.Now.Date.AddDays(1)));
    }

    [Fact]
    public void Whole_number_products_refuse_decimals()
    {
        using var shop = new TestShop();
        var gly = shop.AddProduct("Glyphosate herbicide 1 L", 15000);
        Assert.Throws<DomainException>(() => shop.Sell(shop.Now, 50000, null, TestShop.Line(gly, 1.5m)));
    }

    [Fact]
    public void Prices_are_copied_so_later_price_changes_never_alter_past_sales()
    {
        using var shop = new TestShop();
        var urea = shop.AddProduct("Urea fertilizer (loose)", 2200, weight: true, stock: 100);
        var para = shop.AddProduct("Paraquat herbicide 500 ml", 8500, stock: 100);
        // TX3: negotiated price for this sale only.
        shop.Sell(shop.Now, 13500, null, TestShop.Line(urea, 2.5m), TestShop.Line(para, 1, price: 8000));

        urea.Price = 9999;
        shop.S.Products.Save(urea);

        var sale = shop.S.Sales.SalesBetween(shop.Now.Date, shop.Now.Date.AddDays(1)).Single();
        Assert.Equal(13500, sale.Total);
        Assert.Equal(2.5m, sale.Items[0].Quantity);
        Assert.Equal(2200, sale.Items[0].UnitPrice);
        Assert.Equal(8000, sale.Items[1].UnitPrice);
        Assert.Equal(8500, shop.S.Products.Get(para.Id)!.Price); // list price unchanged
    }

    /// <summary>PRD Feature 3 "done when".</summary>
    [Fact]
    public void Part_paid_sale_creates_debt_and_two_repayments_clear_it()
    {
        using var shop = new TestShop();
        var gly = shop.AddProduct("Glyphosate herbicide 1 L", 15000, stock: 100);
        var rice = shop.AddProduct("Rice seed 1 kg pack", 3500, stock: 100);
        var aung = shop.AddCustomer("U Aung Myint");

        var saleId = shop.Sell(new DateTime(2026, 10, 1, 9, 0, 0), 50000, aung,
            TestShop.Line(gly, 2), TestShop.Line(rice, 10)); // 65,000 total, 15,000 owed
        Assert.Equal(15000, shop.S.Debts.Balance(aung)!.Balance);

        shop.Now = new DateTime(2026, 10, 5, 10, 0, 0);
        shop.S.Debts.RecordRepayment(aung, 10000, shop.Now.Date);
        Assert.Equal(5000, shop.S.Debts.Balance(aung)!.Balance);

        shop.Now = new DateTime(2026, 10, 9, 10, 0, 0);
        shop.S.Debts.RecordRepayment(aung, 5000, shop.Now.Date);
        Assert.Equal(0, shop.S.Debts.Balance(aung)!.Balance);

        var ledger = shop.S.Debts.Ledger(aung);
        Assert.Equal(3, ledger.Count);
        Assert.Equal([LedgerKind.Repayment, LedgerKind.Repayment, LedgerKind.CreditSale], ledger.Select(e => e.Kind));
        Assert.Equal([0L, 5000L, 15000L], ledger.Select(e => e.BalanceAfter));
        Assert.Equal(saleId, ledger[2].Id);
        Assert.Contains("Rice seed 1 kg pack × 10", ledger[2].ItemsText);
    }

    [Fact]
    public void One_repayment_is_spread_across_debts_oldest_first()
    {
        using var shop = new TestShop();
        var gly = shop.AddProduct("Glyphosate herbicide 1 L", 15000, stock: 100);
        var khin = shop.AddCustomer("Daw Khin Mya");
        shop.Sell(new DateTime(2026, 9, 1, 9, 0, 0), 0, khin, TestShop.Line(gly, 1));  // owes 15,000
        shop.Sell(new DateTime(2026, 9, 20, 9, 0, 0), 0, khin, TestShop.Line(gly, 2)); // owes 30,000

        shop.Now = new DateTime(2026, 10, 1, 9, 0, 0);
        shop.S.Debts.RecordRepayment(khin, 20000, shop.Now.Date);

        using var s = shop.S.Db.Read();
        var allocations = s.Query("""
            SELECT d.original_amount, a.amount FROM repayment_allocations a JOIN debts d ON d.id = a.debt_id ORDER BY d.id
            """, r => (Original: r.GetInt64(0), Paid: r.GetInt64(1)));
        Assert.Equal([(15000L, 15000L), (30000L, 5000L)], allocations);
        Assert.Equal(25000, shop.S.Debts.Balance(khin)!.Balance);
    }

    [Fact]
    public void Repayment_larger_than_balance_is_refused()
    {
        using var shop = new TestShop();
        var gly = shop.AddProduct("Glyphosate herbicide 1 L", 15000, stock: 100);
        var c = shop.AddCustomer("Ko Htet Aung");
        shop.Sell(shop.Now, 5000, c, TestShop.Line(gly, 1));
        Assert.Throws<DomainException>(() => shop.S.Debts.RecordRepayment(c, 10001, shop.Now.Date));
        Assert.Throws<DomainException>(() => shop.S.Debts.RecordRepayment(c, 100, shop.Now.Date.AddDays(1)));
    }

    [Fact]
    public void Reversed_repayment_stays_in_history_and_the_debt_returns()
    {
        using var shop = new TestShop();
        var gly = shop.AddProduct("Glyphosate herbicide 1 L", 15000, stock: 100);
        var c = shop.AddCustomer("U Myo Win");
        shop.Sell(shop.Now, 0, c, TestShop.Line(gly, 2));
        var rep = shop.S.Debts.RecordRepayment(c, 20000, shop.Now.Date);
        Assert.Equal(10000, shop.S.Debts.Balance(c)!.Balance);

        shop.S.Debts.ReverseRepayment(rep, "Entered twice by mistake");
        Assert.Equal(30000, shop.S.Debts.Balance(c)!.Balance);

        var ledger = shop.S.Debts.Ledger(c);
        var reversed = ledger.Single(e => e.Kind == LedgerKind.Repayment);
        Assert.True(reversed.Cancelled);
        Assert.Equal("Entered twice by mistake", reversed.Reason);
        Assert.Equal(30000, ledger[0].BalanceAfter);
        Assert.Throws<DomainException>(() => shop.S.Debts.ReverseRepayment(rep, "again"));
    }

    [Fact]
    public void Voiding_a_credit_sale_removes_its_debt()
    {
        using var shop = new TestShop();
        var gly = shop.AddProduct("Glyphosate herbicide 1 L", 15000, stock: 100);
        var c = shop.AddCustomer("Daw Thida");
        var id = shop.Sell(shop.Now, 5000, c, TestShop.Line(gly, 1));
        Assert.Throws<DomainException>(() => shop.S.Sales.Void(id, "  "));

        shop.S.Sales.Void(id, "Wrong quantity entered");
        Assert.Equal(0, shop.S.Debts.Balance(c)!.Balance);
        var sale = shop.S.Sales.SalesBetween(shop.Now.Date, shop.Now.Date.AddDays(1)).Single();
        Assert.True(sale.Voided);
        Assert.Equal("Wrong quantity entered", sale.VoidReason);
        Assert.Throws<DomainException>(() => shop.S.Sales.Void(id, "twice"));
    }

    [Fact]
    public void Voiding_a_sale_that_was_already_repaid_is_blocked_and_nothing_changes()
    {
        using var shop = new TestShop();
        var gly = shop.AddProduct("Glyphosate herbicide 1 L", 15000, stock: 100);
        var c = shop.AddCustomer("U Tun Tun");
        var id = shop.Sell(shop.Now, 0, c, TestShop.Line(gly, 1));
        shop.S.Debts.RecordRepayment(c, 10000, shop.Now.Date);

        Assert.Throws<DomainException>(() => shop.S.Sales.Void(id, "mistake"));
        Assert.False(shop.S.Sales.SalesBetween(shop.Now.Date, shop.Now.Date.AddDays(1)).Single().Voided);
        Assert.Equal(5000, shop.S.Debts.Balance(c)!.Balance);
    }

    [Fact]
    public void Product_names_are_unique_ignoring_case()
    {
        using var shop = new TestShop();
        shop.AddProduct("Glyphosate herbicide 1 L", 15000);
        Assert.Throws<DomainException>(() => shop.AddProduct("glyphosate  HERBICIDE 1 l", 1));
    }
}
