using StorePOS.Data;
using StorePOS.Infrastructure;
using StorePOS.Models;

namespace StorePOS.Services;

public sealed class SaleService(Db db, Func<DateTime> clock)
{
    /// <summary>
    /// TX5/TX6: saves the sale, its items (prices copied) and, when part is unpaid, the debt,
    /// all in one transaction. Returns the new sale id.
    /// </summary>
    public long Save(IReadOnlyList<SaleLineInput> lines, long tendered, long? customerId)
    {
        if (lines.Count == 0) throw new DomainException(Loc.Get("Err_CartEmpty"));
        foreach (var l in lines)
        {
            if (l.Quantity <= 0 || decimal.Round(l.Quantity, 3) != l.Quantity)
                throw new DomainException(Loc.F("Err_QtyInvalid", l.Name));
            if (!l.SoldByWeight && decimal.Truncate(l.Quantity) != l.Quantity)
                throw new DomainException(Loc.F("Err_QtyWhole", l.Name));
            if (l.UnitPrice < 0) throw new DomainException(Loc.F("Err_LinePriceInvalid", l.Name));
        }
        if (tendered < 0) throw new DomainException(Loc.Get("Err_PaidInvalid"));

        var total = lines.Sum(l => l.LineTotal);
        var paid = Math.Min(tendered, total);
        var owed = total - paid;
        if (owed > 0 && customerId == null) throw new DomainException(Loc.Get("Err_CustomerRequired"));

        var now = clock();
        using var s = db.Write();
        var saleId = s.Insert("""
            INSERT INTO sales(sold_at, total, amount_paid, cash_tendered, customer_id, status)
            VALUES($t, $total, $paid, $tendered, $c, 'saved')
            """,
            ("$t", Fmt.DbTime(now)), ("$total", total), ("$paid", paid), ("$tendered", tendered),
            ("$c", customerId));

        foreach (var l in lines)
        {
            s.Exec("""
                INSERT INTO sale_items(sale_id, product_id, product_name, unit, sold_by_weight,
                    quantity_milli, unit_price, list_price, line_total)
                VALUES($s, $p, $n, $u, $w, $q, $up, $lp, $lt)
                """,
                ("$s", saleId), ("$p", l.ProductId), ("$n", l.Name), ("$u", l.Unit),
                ("$w", l.SoldByWeight ? 1 : 0), ("$q", Fmt.ToMilli(l.Quantity)),
                ("$up", l.UnitPrice), ("$lp", l.ListPrice), ("$lt", l.LineTotal));
        }

        if (owed > 0)
        {
            s.Exec("INSERT INTO debts(customer_id, sale_id, original_amount, created_at) VALUES($c, $s, $a, $t)",
                ("$c", customerId), ("$s", saleId), ("$a", owed), ("$t", Fmt.DbTime(now)));
            DebtService.Reallocate(s, customerId!.Value);
        }

        s.Commit();
        return saleId;
    }

    /// <summary>TX7: the sale stays, marked voided, and is left out of every report.</summary>
    public void Void(long saleId, string reason)
    {
        reason = (reason ?? "").Trim();
        if (reason.Length == 0) throw new DomainException(Loc.Get("Err_ReasonRequired"));

        using var s = db.Write();
        var status = s.Scalar<string?>("SELECT status FROM sales WHERE id = $id", ("$id", saleId));
        if (status == null) throw new DomainException(Loc.Get("Err_SaleNotFound"));
        if (status == "voided") throw new DomainException(Loc.Get("Err_AlreadyVoided"));

        s.Exec("UPDATE sales SET status='voided', void_reason=$r, voided_at=$t WHERE id=$id",
            ("$r", reason), ("$t", Fmt.DbTime(clock())), ("$id", saleId));

        var debtCustomer = s.Scalar<long?>("SELECT customer_id FROM debts WHERE sale_id = $id", ("$id", saleId));
        if (debtCustomer != null) DebtService.Reallocate(s, debtCustomer.Value);

        s.Commit();
    }

    public List<SaleRecord> SalesBetween(DateTime from, DateTime toExclusive)
    {
        using var s = db.Read();
        var sales = s.Query("""
            SELECT s.id, s.sold_at, s.total, s.amount_paid, s.customer_id, c.name, s.status, s.void_reason
            FROM sales s LEFT JOIN customers c ON c.id = s.customer_id
            WHERE s.sold_at >= $f AND s.sold_at < $t
            ORDER BY s.sold_at, s.id
            """,
            r => new SaleRecord
            {
                Id = r.GetInt64(0),
                SoldAt = Fmt.ParseDbTime(r.GetString(1)),
                Total = r.GetInt64(2),
                AmountPaid = r.GetInt64(3),
                CustomerId = r.LongOrNull(4),
                CustomerName = r.StrOrNull(5),
                Voided = r.GetString(6) == "voided",
                VoidReason = r.StrOrNull(7),
            },
            ("$f", Fmt.DbTime(from)), ("$t", Fmt.DbTime(toExclusive)));

        var byId = sales.ToDictionary(x => x.Id);
        var items = s.Query("""
            SELECT si.sale_id, si.product_id, si.product_name, si.unit, si.sold_by_weight,
                   si.quantity_milli, si.unit_price, si.line_total
            FROM sale_items si JOIN sales s ON s.id = si.sale_id
            WHERE s.sold_at >= $f AND s.sold_at < $t
            ORDER BY si.id
            """,
            r => (SaleId: r.GetInt64(0), Item: new SaleItemRow
            {
                ProductId = r.GetInt64(1),
                Name = r.GetString(2),
                Unit = r.GetString(3),
                SoldByWeight = r.Bool(4),
                Quantity = Fmt.FromMilli(r.GetInt64(5)),
                UnitPrice = r.GetInt64(6),
                LineTotal = r.GetInt64(7),
            }),
            ("$f", Fmt.DbTime(from)), ("$t", Fmt.DbTime(toExclusive)));
        foreach (var (saleId, item) in items)
            if (byId.TryGetValue(saleId, out var sale)) sale.Items.Add(item);
        return sales;
    }
}
