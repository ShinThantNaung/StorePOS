using StorePOS.Data;
using StorePOS.Infrastructure;
using StorePOS.Models;

namespace StorePOS.Services;

/// <summary>
/// Feature 5. A product's stock is the sum of its stock_movements rows, never a typed-in figure.
/// Movements are only ever added: a mistake is fixed with an adjustment (ST9).
/// Sales and voids add their movements in SaleService, inside the sale's own transaction.
/// </summary>
public sealed class StockService(Db db, ProductService products, Func<DateTime> clock)
{
    /// <summary>ST2: every product with its stock and the date stock last came in.</summary>
    public List<StockLevel> Levels()
    {
        Dictionary<long, (long Milli, string? LastIn)> totals;
        using (var s = db.Read())
        {
            totals = s.Query("""
                SELECT product_id, SUM(change_milli), MAX(CASE WHEN kind = 'stock_in' THEN moved_at END)
                FROM stock_movements GROUP BY product_id
                """,
                r => (Id: r.GetInt64(0), Milli: r.GetInt64(1), LastIn: r.StrOrNull(2)))
                .ToDictionary(x => x.Id, x => (x.Milli, x.LastIn));
        }
        return products.List(includeInactive: true).Select(p =>
        {
            var t = totals.GetValueOrDefault(p.Id);
            return new StockLevel
            {
                Product = p,
                OnHand = Fmt.FromMilli(t.Milli),
                LastStockIn = t.LastIn == null ? null : Fmt.ParseDbTime(t.LastIn),
            };
        }).ToList();
    }

    public decimal OnHand(long productId)
    {
        using var s = db.Read();
        return Fmt.FromMilli(OnHandMilli(s, productId));
    }

    private static long OnHandMilli(Session s, long productId) =>
        s.Scalar<long>("SELECT COALESCE(SUM(change_milli), 0) FROM stock_movements WHERE product_id = $p",
            ("$p", productId));

    /// <summary>ST7: one product's movements, newest first, each with the stock left after it.</summary>
    public List<StockMovement> Movements(long productId)
    {
        using var s = db.Read();
        return s.Query("""
            SELECT m.id, m.moved_at, m.kind, m.change_milli, m.after_milli, m.sale_id, c.name, si.supplier, m.reason, m.note
            FROM (SELECT *, SUM(change_milli) OVER (ORDER BY moved_at, id ROWS UNBOUNDED PRECEDING) AS after_milli
                  FROM stock_movements WHERE product_id = $p) m
            LEFT JOIN sales s ON s.id = m.sale_id
            LEFT JOIN customers c ON c.id = s.customer_id
            LEFT JOIN stock_ins si ON si.id = m.stock_in_id
            ORDER BY m.moved_at DESC, m.id DESC
            """,
            r => new StockMovement
            {
                Id = r.GetInt64(0),
                At = Fmt.ParseDbTime(r.GetString(1)),
                Kind = KindFromCode(r.GetString(2)),
                Change = Fmt.FromMilli(r.GetInt64(3)),
                After = Fmt.FromMilli(r.GetInt64(4)),
                SaleId = r.LongOrNull(5),
                CustomerName = r.StrOrNull(6),
                Supplier = r.StrOrNull(7) ?? "",
                Reason = r.IsDBNull(8) ? null : AdjustReasons.FromCode(r.GetString(8)),
                Note = r.GetString(9),
            },
            ("$p", productId));
    }

    /// <summary>
    /// ST3: one delivery with one or more products, saved in one transaction. A delivery recorded
    /// for an earlier day is stamped with that day and the current time of day.
    /// </summary>
    public long RecordStockIn(DateTime day, string? supplier, string? note, IReadOnlyList<StockInLineInput> lines)
    {
        var now = clock();
        if (day.Date > now.Date) throw new DomainException(Loc.Get("Err_DateFuture"));
        var merged = lines.GroupBy(l => l.ProductId).Select(g => new StockInLineInput(g.Key, g.Sum(l => l.Quantity))).ToList();
        if (merged.Count == 0) throw new DomainException(Loc.Get("Err_StockIn_Empty"));
        var receivedAt = day.Date == now.Date ? now : day.Date + now.TimeOfDay;

        using var s = db.Write();
        foreach (var l in merged) CheckQuantity(s, l.ProductId, l.Quantity, allowZero: false);

        var id = s.Insert("""
            INSERT INTO stock_ins(received_at, supplier, note, created_at) VALUES($at, $sup, $note, $now)
            """,
            ("$at", Fmt.DbTime(receivedAt)), ("$sup", (supplier ?? "").Trim()), ("$note", (note ?? "").Trim()),
            ("$now", Fmt.DbTime(now)));
        foreach (var l in merged)
        {
            var milli = Fmt.ToMilli(l.Quantity);
            s.Exec("INSERT INTO stock_in_items(stock_in_id, product_id, quantity_milli) VALUES($s, $p, $q)",
                ("$s", id), ("$p", l.ProductId), ("$q", milli));
            s.Exec("""
                INSERT INTO stock_movements(product_id, moved_at, change_milli, kind, stock_in_id)
                VALUES($p, $at, $q, 'stock_in', $s)
                """,
                ("$p", l.ProductId), ("$at", Fmt.DbTime(receivedAt)), ("$q", milli), ("$s", id));
        }
        s.Commit();
        return id;
    }

    /// <summary>ST6/ST10: records the difference between the shelf count and the app. Returns the difference.</summary>
    public decimal Adjust(long productId, decimal counted, AdjustReason reason, string? note)
    {
        using var s = db.Write();
        CheckQuantity(s, productId, counted, allowZero: true);
        var diffMilli = Fmt.ToMilli(counted) - OnHandMilli(s, productId);
        if (diffMilli == 0) throw new DomainException(Loc.Get("Err_Adjust_NoChange"));
        s.Exec("""
            INSERT INTO stock_movements(product_id, moved_at, change_milli, kind, reason, note)
            VALUES($p, $at, $q, 'adjustment', $r, $n)
            """,
            ("$p", productId), ("$at", Fmt.DbTime(clock())), ("$q", diffMilli),
            ("$r", AdjustReasons.Code(reason)), ("$n", (note ?? "").Trim()));
        s.Commit();
        return Fmt.FromMilli(diffMilli);
    }

    /// <summary>ST1: whole numbers, except products sold by weight (at most 3 decimals).</summary>
    private static void CheckQuantity(Session s, long productId, decimal qty, bool allowZero)
    {
        var row = s.Query("SELECT name, sold_by_weight FROM products WHERE id = $id",
            r => (Name: r.GetString(0), Weight: r.Bool(1)), ("$id", productId)).FirstOrDefault();
        if (row.Name == null) throw new DomainException(Loc.Get("Err_ProductNotFound"));
        if ((allowZero ? qty < 0 : qty <= 0) || decimal.Round(qty, 3) != qty)
            throw new DomainException(Loc.F("Err_QtyInvalid", row.Name));
        if (!row.Weight && decimal.Truncate(qty) != qty)
            throw new DomainException(Loc.F("Err_StockQtyWhole", row.Name));
    }

    /// <summary>ST4: every delivery, newest first, with each product's stock before and after it.</summary>
    public List<StockInRecord> StockIns()
    {
        using var s = db.Read();
        var ins = s.Query("SELECT id, received_at, supplier, note FROM stock_ins ORDER BY received_at DESC, id DESC",
            r => new StockInRecord
            {
                Id = r.GetInt64(0),
                ReceivedAt = Fmt.ParseDbTime(r.GetString(1)),
                Supplier = r.GetString(2),
                Note = r.GetString(3),
            });
        var byId = ins.ToDictionary(x => x.Id);
        // The stock after each delivery line is the sum of that product's movements up to it.
        var lines = s.Query("""
            SELECT m.stock_in_id, m.product_id, p.name, p.size, p.company, m.change_milli,
                   (SELECT SUM(x.change_milli) FROM stock_movements x
                    WHERE x.product_id = m.product_id AND (x.moved_at, x.id) <= (m.moved_at, m.id))
            FROM stock_movements m
            JOIN products p ON p.id = m.product_id
            WHERE m.stock_in_id IS NOT NULL
            ORDER BY m.id
            """,
            r => (StockInId: r.GetInt64(0), Line: new StockInLine
            {
                ProductId = r.GetInt64(1),
                Name = Product.FormatFullName(r.GetString(2), r.GetString(3), r.GetString(4)),
                Quantity = Fmt.FromMilli(r.GetInt64(5)),
                After = Fmt.FromMilli(r.GetInt64(6)),
            }));
        foreach (var (stockInId, line) in lines)
            if (byId.TryGetValue(stockInId, out var record)) record.Lines.Add(line);
        return ins;
    }

    /// <summary>Earlier suppliers for the From box, most recently used first.</summary>
    public List<string> Suppliers()
    {
        using var s = db.Read();
        return s.Query("""
            SELECT supplier FROM stock_ins WHERE supplier <> ''
            GROUP BY supplier ORDER BY MAX(received_at) DESC
            """, r => r.GetString(0));
    }

    private static StockMoveKind KindFromCode(string code) => code switch
    {
        "sale" => StockMoveKind.Sale,
        "void" => StockMoveKind.Void,
        "stock_in" => StockMoveKind.StockIn,
        _ => StockMoveKind.Adjustment,
    };
}
