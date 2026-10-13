using StorePOS.Data;
using StorePOS.Infrastructure;
using StorePOS.Models;

namespace StorePOS.Services;

/// <summary>
/// Customers, debts and repayments. A debt's remaining balance is always its original amount
/// minus its allocations; nothing is ever typed in.
/// </summary>
public sealed class DebtService(Db db, Func<DateTime> clock)
{
    private const string BalanceSql = """
        COALESCE((SELECT SUM(d.original_amount) FROM debts d JOIN sales s ON s.id = d.sale_id
                  WHERE d.customer_id = c.id AND s.status = 'saved'), 0)
        - COALESCE((SELECT SUM(a.amount) FROM repayment_allocations a
                    JOIN repayments r ON r.id = a.repayment_id
                    JOIN debts d ON d.id = a.debt_id JOIN sales s ON s.id = d.sale_id
                    WHERE r.customer_id = c.id AND r.reversed = 0 AND s.status = 'saved'), 0)
        """;

    private static Customer MapCustomer(Microsoft.Data.Sqlite.SqliteDataReader r) => new()
    {
        Id = r.GetInt64(0),
        Name = r.GetString(1),
        Address = r.GetString(2),
        Phone = r.StrOrNull(3),
    };

    public List<Customer> Customers()
    {
        using var s = db.Read();
        return s.Query("SELECT id, name, address, phone FROM customers ORDER BY name, address", MapCustomer);
    }

    public List<CustomerBalance> Balances()
    {
        using var s = db.Read();
        return s.Query($"SELECT c.id, c.name, c.address, c.phone, {BalanceSql} FROM customers c",
            r => new CustomerBalance { Customer = MapCustomer(r), Balance = r.GetInt64(4) });
    }

    public CustomerBalance? Balance(long customerId)
    {
        using var s = db.Read();
        return s.Query($"SELECT c.id, c.name, c.address, c.phone, {BalanceSql} FROM customers c WHERE c.id = $id",
            r => new CustomerBalance { Customer = MapCustomer(r), Balance = r.GetInt64(4) },
            ("$id", customerId)).FirstOrDefault();
    }

    public long SaveCustomer(Customer c)
    {
        var name = (c.Name ?? "").Trim();
        if (name.Length == 0) throw new DomainException(Loc.Get("Err_NameRequired"));
        var address = (c.Address ?? "").Trim();
        var phone = string.IsNullOrWhiteSpace(c.Phone) ? null : c.Phone.Trim();

        using var s = db.Write();
        var clash = s.Scalar<long?>("""
            SELECT id FROM customers WHERE lower(name) = lower($n) AND lower(address) = lower($a) AND id <> $id
            """, ("$n", name), ("$a", address), ("$id", c.Id));
        if (clash != null) throw new DomainException(Loc.Get("Err_CustomerExists"));

        long id;
        if (c.Id == 0)
        {
            id = s.Insert("INSERT INTO customers(name, address, phone, created_at) VALUES($n, $a, $p, $t)",
                ("$n", name), ("$a", address), ("$p", phone), ("$t", Fmt.DbTime(clock())));
        }
        else
        {
            s.Exec("UPDATE customers SET name=$n, address=$a, phone=$p WHERE id=$id",
                ("$n", name), ("$a", address), ("$p", phone), ("$id", c.Id));
            id = c.Id;
        }
        s.Commit();
        return id;
    }

    /// <summary>DB3: one repayment can clear several debts, oldest first.</summary>
    public long RecordRepayment(long customerId, long amount, DateTime date)
    {
        if (amount <= 0) throw new DomainException(Loc.Get("Err_AmountInvalid"));
        if (date.Date > clock().Date) throw new DomainException(Loc.Get("Err_DateFuture"));

        using var s = db.Write();
        var balance = s.Scalar<long>($"SELECT {BalanceSql} FROM customers c WHERE c.id = $id", ("$id", customerId));
        if (amount > balance) throw new DomainException(Loc.Get("Repay_TooMuch"));

        var id = s.Insert("INSERT INTO repayments(customer_id, amount, paid_on, created_at) VALUES($c, $a, $d, $t)",
            ("$c", customerId), ("$a", amount), ("$d", Fmt.DbDate(date)), ("$t", Fmt.DbTime(clock())));
        Reallocate(s, customerId);
        s.Commit();
        return id;
    }

    /// <summary>DB7: repayments are never deleted, only reversed with a reason.</summary>
    public void ReverseRepayment(long repaymentId, string reason)
    {
        reason = (reason ?? "").Trim();
        if (reason.Length == 0) throw new DomainException(Loc.Get("Err_ReasonRequired"));

        using var s = db.Write();
        var row = s.Query("SELECT customer_id, reversed FROM repayments WHERE id = $id",
            r => (Customer: r.GetInt64(0), Reversed: r.Bool(1)), ("$id", repaymentId)).FirstOrDefault();
        if (row == default) throw new DomainException(Loc.Get("Err_RepaymentNotFound"));
        if (row.Reversed) throw new DomainException(Loc.Get("Err_AlreadyReversed"));

        s.Exec("UPDATE repayments SET reversed=1, reverse_reason=$r, reversed_at=$t WHERE id=$id",
            ("$r", reason), ("$t", Fmt.DbTime(clock())), ("$id", repaymentId));
        Reallocate(s, row.Customer);
        s.Commit();
    }

    /// <summary>
    /// Rebuilds the customer's allocations: active repayments (by date) pay off open debts
    /// (oldest sale first). Throws when the repayments add up to more than the debts,
    /// for example when voiding a sale that was already repaid.
    /// </summary>
    internal static void Reallocate(Session s, long customerId)
    {
        s.Exec("DELETE FROM repayment_allocations WHERE repayment_id IN (SELECT id FROM repayments WHERE customer_id = $c)",
            ("$c", customerId));

        var debts = s.Query("""
            SELECT d.id, d.original_amount FROM debts d JOIN sales s ON s.id = d.sale_id
            WHERE d.customer_id = $c AND s.status = 'saved'
            ORDER BY s.sold_at, d.id
            """, r => new[] { r.GetInt64(0), r.GetInt64(1) }, ("$c", customerId));
        var repayments = s.Query("""
            SELECT id, amount FROM repayments WHERE customer_id = $c AND reversed = 0 ORDER BY paid_on, id
            """, r => (Id: r.GetInt64(0), Amount: r.GetInt64(1)), ("$c", customerId));

        var di = 0;
        foreach (var rep in repayments)
        {
            var left = rep.Amount;
            while (left > 0)
            {
                if (di >= debts.Count) throw new DomainException(Loc.Get("Err_RepaidMoreThanOwed"));
                var take = Math.Min(left, debts[di][1]);
                s.Exec("INSERT INTO repayment_allocations(repayment_id, debt_id, amount) VALUES($r, $d, $a)",
                    ("$r", rep.Id), ("$d", debts[di][0]), ("$a", take));
                debts[di][1] -= take;
                left -= take;
                if (debts[di][1] == 0) di++;
            }
        }
    }

    /// <summary>DB6: every credit sale and repayment with the running balance, newest first.</summary>
    public List<LedgerEntry> Ledger(long customerId)
    {
        using var s = db.Read();
        var credit = s.Query("""
            SELECT s.id, s.sold_at, s.total, s.amount_paid, d.original_amount, s.status, s.void_reason
            FROM debts d JOIN sales s ON s.id = d.sale_id WHERE d.customer_id = $c
            """,
            r => (Id: r.GetInt64(0), At: Fmt.ParseDbTime(r.GetString(1)), Total: r.GetInt64(2),
                  Paid: r.GetInt64(3), Owed: r.GetInt64(4), Voided: r.GetString(5) == "voided",
                  Reason: r.StrOrNull(6)),
            ("$c", customerId));

        var items = s.Query("""
            SELECT si.sale_id, si.product_name, si.unit, si.sold_by_weight, si.quantity_milli
            FROM sale_items si JOIN debts d ON d.sale_id = si.sale_id
            WHERE d.customer_id = $c ORDER BY si.id
            """,
            r => (SaleId: r.GetInt64(0), Text: new SaleItemRow
            {
                Name = r.GetString(1), Unit = r.GetString(2), SoldByWeight = r.Bool(3),
                Quantity = Fmt.FromMilli(r.GetInt64(4)),
            }.Describe()),
            ("$c", customerId))
            .GroupBy(x => x.SaleId)
            .ToDictionary(g => g.Key, g => string.Join(", ", g.Select(x => x.Text)));

        var entries = credit.Select(c => new LedgerEntry
        {
            Kind = LedgerKind.CreditSale, Id = c.Id, Date = c.At, SortKey = c.At,
            SaleTotal = c.Total, SalePaid = c.Paid, Amount = c.Owed, Cancelled = c.Voided,
            Reason = c.Reason, ItemsText = items.GetValueOrDefault(c.Id, ""),
        }).ToList();

        entries.AddRange(s.Query("""
            SELECT id, amount, paid_on, created_at, reversed, reverse_reason
            FROM repayments WHERE customer_id = $c
            """,
            r =>
            {
                var date = Fmt.ParseDbDate(r.GetString(2));
                var created = Fmt.ParseDbTime(r.GetString(3));
                return new LedgerEntry
                {
                    Kind = LedgerKind.Repayment, Id = r.GetInt64(0), Amount = r.GetInt64(1), Date = date,
                    SortKey = created.Date == date ? created : date.AddHours(12),
                    Cancelled = r.Bool(4), Reason = r.StrOrNull(5),
                };
            },
            ("$c", customerId)));

        entries.Sort((a, b) => a.SortKey != b.SortKey ? a.SortKey.CompareTo(b.SortKey) : a.Id.CompareTo(b.Id));
        long balance = 0;
        foreach (var e in entries)
        {
            if (!e.Cancelled) balance += e.Kind == LedgerKind.CreditSale ? e.Amount : -e.Amount;
            e.BalanceAfter = balance;
        }
        entries.Reverse();
        return entries;
    }
}
