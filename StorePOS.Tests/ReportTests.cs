using StorePOS.Services;

namespace StorePOS.Tests;

public class ReportTests
{
    /// <summary>PRD Feature 4 "done when": the weekly total equals the sum of the seven daily reports.</summary>
    [Fact]
    public void Weekly_figures_equal_the_sum_of_the_seven_days()
    {
        using var shop = new TestShop();
        var gly = shop.AddProduct("Glyphosate herbicide 1 L", 15000, stock: 1000);
        var urea = shop.AddProduct("Urea fertilizer (loose)", 2200, weight: true, stock: 1000);
        var c = shop.AddCustomer("Daw Khin Mya");
        var monday = new DateTime(2026, 10, 5);
        var rng = new Random(3);
        for (var d = 0; d < 7; d++)
        {
            for (var n = 0; n < 5; n++)
            {
                var at = monday.AddDays(d).AddHours(8 + n * 2).AddMinutes(rng.Next(60));
                var lines = new[] { TestShop.Line(gly, rng.Next(1, 4)), TestShop.Line(urea, rng.Next(1, 9) * 0.5m) };
                var total = lines.Sum(l => l.LineTotal);
                var credit = n % 3 == 0;
                var id = shop.Sell(at, credit ? total / 2 : total + 1000, credit ? c : null, lines);
                if (d == 2 && n == 1) shop.S.Sales.Void(id, "wrong");
            }
            shop.Now = monday.AddDays(d).AddHours(20);
            if (d % 2 == 1) shop.S.Debts.RecordRepayment(c, 5000, shop.Now.Date);
        }
        // A sale just past midnight on Sunday belongs to the next week.
        shop.Sell(monday.AddDays(7), 15000, null, TestShop.Line(gly, 1));

        var week = shop.S.Reports.Totals(monday, monday.AddDays(7));
        var days = Enumerable.Range(0, 7).Select(i => shop.S.Reports.Totals(monday.AddDays(i), monday.AddDays(i + 1))).ToList();

        Assert.Equal(days.Sum(x => x.TotalSold), week.TotalSold);
        Assert.Equal(days.Sum(x => x.CashReceived), week.CashReceived);
        Assert.Equal(days.Sum(x => x.CreditGiven), week.CreditGiven);
        Assert.Equal(days.Sum(x => x.SalesCount), week.SalesCount);
        Assert.Equal(34, week.SalesCount);
        Assert.Equal(1, week.VoidedCount);
        Assert.Equal(15000, week.Repayments);
    }

    [Fact]
    public void Daily_figures_follow_the_metric_definitions()
    {
        using var shop = new TestShop();
        var gly = shop.AddProduct("Glyphosate herbicide 1 L", 15000, stock: 100);
        var c = shop.AddCustomer("U Aung Myint");
        var day = new DateTime(2026, 10, 13);
        shop.Sell(day.AddHours(8), 50000, null, TestShop.Line(gly, 2));   // 30,000 cash, change given
        shop.Sell(day.AddHours(9), 20000, c, TestShop.Line(gly, 3));      // 45,000: 20,000 cash, 25,000 credit
        var voided = shop.Sell(day.AddHours(10), 15000, null, TestShop.Line(gly, 1));
        shop.S.Sales.Void(voided, "wrong");
        shop.Now = day.AddHours(11);
        shop.S.Debts.RecordRepayment(c, 5000, day);

        var t = shop.S.Reports.Totals(day, day.AddDays(1));
        Assert.Equal(75000, t.TotalSold);
        Assert.Equal(55000, t.CashReceived); // 30,000 + 20,000 + 5,000 repayment
        Assert.Equal(5000, t.Repayments);
        Assert.Equal(25000, t.CreditGiven);
        Assert.Equal(1, t.CreditSales);
        Assert.Equal(2, t.SalesCount);
        Assert.Equal(1, t.VoidedCount);
    }

    [Fact]
    public void Best_and_slow_sellers_include_products_with_no_sales()
    {
        using var shop = new TestShop();
        var a = shop.AddProduct("A cheap item", 1000, stock: 100);
        var b = shop.AddProduct("B expensive item", 50000, stock: 100);
        var urea = shop.AddProduct("Urea fertilizer (loose)", 2200, weight: true, stock: 100);
        var unsold = shop.AddProduct("Unsold item", 7000);
        var day = new DateTime(2026, 10, 13);
        shop.Sell(day.AddHours(8), 100000, null, TestShop.Line(a, 10), TestShop.Line(b, 1), TestShop.Line(urea, 12.5m));

        var all = shop.S.Reports.ProductSales(day, day.AddDays(1));
        var byKs = ReportService.BestSellers(all, byQuantity: false);
        var byQty = ReportService.BestSellers(all, byQuantity: true);
        var slow = ReportService.SlowSellers(all);

        Assert.Equal(["B expensive item", "Urea fertilizer (loose)", "A cheap item"], byKs.Select(p => p.Name));
        Assert.Equal(12.5m, byKs[1].Quantity);
        Assert.Equal("Urea fertilizer (loose)", byQty[0].Name);
        Assert.Equal(unsold.Id, slow[0].ProductId);
        Assert.DoesNotContain(byKs, p => p.ProductId == unsold.Id);
    }
}
