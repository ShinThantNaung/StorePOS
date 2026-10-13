using StorePOS.Data;
using StorePOS.Infrastructure;
using StorePOS.Models;

namespace StorePOS.Services;

/// <summary>
/// Reports are calculated from the sales and repayments tables on demand. Voided sales and
/// reversed repayments are excluded everywhere. Every figure is a plain sum over the period,
/// so a week always equals the sum of its seven days.
/// </summary>
public sealed class ReportService(Db db)
{
    public ReportTotals Totals(DateTime from, DateTime toExclusive)
    {
        using var s = db.Read();
        (string, object?)[] range = [("$f", Fmt.DbTime(from)), ("$t", Fmt.DbTime(toExclusive))];
        var sales = s.Query("""
            SELECT COUNT(*), COALESCE(SUM(total), 0), COALESCE(SUM(amount_paid), 0),
                   COALESCE(SUM(total - amount_paid), 0),
                   COALESCE(SUM(CASE WHEN total > amount_paid THEN 1 ELSE 0 END), 0)
            FROM sales WHERE status = 'saved' AND sold_at >= $f AND sold_at < $t
            """,
            r => (Count: r.GetInt32(0), Total: r.GetInt64(1), Paid: r.GetInt64(2), Credit: r.GetInt64(3),
                  CreditCount: r.GetInt32(4)),
            range).Single();
        var voided = s.Scalar<long>(
            "SELECT COUNT(*) FROM sales WHERE status = 'voided' AND sold_at >= $f AND sold_at < $t", range);
        var repaid = s.Scalar<long>("""
            SELECT COALESCE(SUM(amount), 0) FROM repayments
            WHERE reversed = 0 AND paid_on >= $fd AND paid_on < $td
            """, ("$fd", Fmt.DbDate(from)), ("$td", Fmt.DbDate(toExclusive)));

        return new ReportTotals
        {
            TotalSold = sales.Total,
            CashAtSale = sales.Paid,
            Repayments = repaid,
            CreditGiven = sales.Credit,
            CreditSales = sales.CreditCount,
            SalesCount = sales.Count,
            VoidedCount = (int)voided,
        };
    }

    /// <summary>Sales per product in the period: every active product (so ones with no sales
    /// are included) plus any inactive product that still sold something.</summary>
    public List<ProductSales> ProductSales(DateTime from, DateTime toExclusive)
    {
        using var s = db.Read();
        return s.Query("""
            SELECT p.id, p.name, p.unit, p.sold_by_weight, COALESCE(x.qty, 0), COALESCE(x.amt, 0)
            FROM products p
            LEFT JOIN (SELECT si.product_id, SUM(si.quantity_milli) AS qty, SUM(si.line_total) AS amt
                       FROM sale_items si JOIN sales s ON s.id = si.sale_id
                       WHERE s.status = 'saved' AND s.sold_at >= $f AND s.sold_at < $t
                       GROUP BY si.product_id) x ON x.product_id = p.id
            WHERE p.active = 1 OR x.qty IS NOT NULL
            """,
            r => new ProductSales
            {
                ProductId = r.GetInt64(0), Name = r.GetString(1), Unit = r.GetString(2),
                SoldByWeight = r.Bool(3), Quantity = Fmt.FromMilli(r.GetInt64(4)), Amount = r.GetInt64(5),
            },
            ("$f", Fmt.DbTime(from)), ("$t", Fmt.DbTime(toExclusive)));
    }

    public static List<ProductSales> BestSellers(IEnumerable<ProductSales> all, bool byQuantity, int take = 10) =>
        (byQuantity
            ? all.Where(p => p.Quantity > 0).OrderByDescending(p => p.Quantity).ThenByDescending(p => p.Amount)
            : all.Where(p => p.Quantity > 0).OrderByDescending(p => p.Amount).ThenByDescending(p => p.Quantity))
        .ThenBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
        .Take(take).ToList();

    public static List<ProductSales> SlowSellers(IEnumerable<ProductSales> all, int take = 10) =>
        all.OrderBy(p => p.Amount).ThenBy(p => p.Quantity)
            .ThenBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
            .Take(take).ToList();
}
