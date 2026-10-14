using StorePOS.Infrastructure;

namespace StorePOS.Models;

public enum Category { Pesticide, Herbicide, Fertilizer, Seed, Other }

public static class Categories
{
    public static readonly Category[] All =
        [Category.Pesticide, Category.Herbicide, Category.Fertilizer, Category.Seed, Category.Other];

    public static string Code(Category c) => c.ToString().ToLowerInvariant();

    public static Category FromCode(string code) => Enum.Parse<Category>(code, ignoreCase: true);

    public static string Display(Category c) => Loc.Get("Cat_" + c);

    /// <summary>Accepts the English name or the name shown in the current interface language.</summary>
    public static bool TryParse(string? text, out Category category)
    {
        var key = Fmt.SearchKey(text);
        foreach (var c in All)
        {
            if (key == Code(c) || key == Fmt.SearchKey(Display(c)))
            {
                category = c;
                return true;
            }
        }
        category = Category.Other;
        return false;
    }
}

/// <summary>A product is sold by the bottle or by the bag. The code is stored; the name follows the interface language.</summary>
public static class Units
{
    public const string Bottle = "bottle";
    public const string Bag = "bag";

    public static readonly string[] All = [Bottle, Bag];

    private static string Name(string code) => Loc.Get(code == Bottle ? "Unit_Bottle" : "Unit_Bag");

    /// <summary>Products saved before the unit became a fixed choice show the unit as it was typed.</summary>
    public static string Display(string unit) => TryParse(unit, out var code) ? Name(code) : unit;

    /// <summary>The unit after a number: "bottle" or "bottles" in English, one form in Burmese.</summary>
    public static string Word(string unit, decimal count) =>
        TryParse(unit, out var code)
            ? Loc.Get((code == Bottle ? "UnitWord_Bottle" : "UnitWord_Bag") + (Math.Abs(count) == 1 ? "_One" : "_Many"))
            : unit;

    /// <summary>"31 bottles", "−2 bags", "+36 bottles", "37.5 kg".</summary>
    public static string Count(decimal count, string unit, bool showPlus = false) =>
        $"{Fmt.Stock(count, showPlus)} {Word(unit, count)}";

    /// <summary>Accepts the code ("bag") or the name shown in the current interface language.</summary>
    public static bool TryParse(string? text, out string unit)
    {
        var key = Fmt.SearchKey(text);
        foreach (var u in All)
        {
            if (key == u || key == Fmt.SearchKey(Name(u)))
            {
                unit = u;
                return true;
            }
        }
        unit = "";
        return false;
    }
}

public sealed class Product
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    /// <summary>Who makes it, for example "Green Valley". Optional.</summary>
    public string Company { get; set; } = "";
    /// <summary>For example "1 L" or "500 cc". Optional.</summary>
    public string Size { get; set; } = "";
    public Category Category { get; set; }
    public string Unit { get; set; } = "";
    public long Price { get; set; }
    public bool SoldByWeight { get; set; }
    public bool Active { get; set; } = true;
    /// <summary>When the price last changed (PR1); null for a product with no recorded price yet.</summary>
    public DateTime? PriceChangedAt { get; set; }

    /// <summary>The name as copied into sales and shown in reports.</summary>
    public string FullName => FormatFullName(Name, Size, Company);

    /// <summary>"Glyphosate 1 L (Green Valley)"; just the name when size and company are empty.</summary>
    public static string FormatFullName(string name, string size, string company)
    {
        var text = size.Length == 0 ? name : $"{name} {size}";
        return company.Length == 0 ? text : $"{text} ({company})";
    }
}

public sealed class Customer
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public string? Phone { get; set; }

    public string Display => string.IsNullOrWhiteSpace(Address) ? Name : $"{Name}, {Address}";
}

public sealed class CustomerBalance
{
    public required Customer Customer { get; init; }
    public long Balance { get; init; }
}

public sealed record SaleLineInput(
    long ProductId, string Name, string Unit, bool SoldByWeight,
    decimal Quantity, long UnitPrice, long ListPrice)
{
    public long LineTotal => Fmt.LineTotal(UnitPrice, Quantity);
}

public sealed class SaleItemRow
{
    public long ProductId { get; init; }
    public string Name { get; init; } = "";
    public string Unit { get; init; } = "";
    public bool SoldByWeight { get; init; }
    public decimal Quantity { get; init; }
    public long UnitPrice { get; init; }
    public long LineTotal { get; init; }

    /// <summary>"Glyphosate herbicide 1 L × 2" or "Urea fertilizer (loose) × 2.5 kg"</summary>
    public string Describe() => $"{Name} × {Fmt.QtyWithUnit(Quantity, Units.Display(Unit), SoldByWeight)}";
}

public sealed class SaleRecord
{
    public long Id { get; init; }
    public DateTime SoldAt { get; init; }
    public long Total { get; init; }
    public long AmountPaid { get; init; }
    public long? CustomerId { get; init; }
    public string? CustomerName { get; init; }
    public bool Voided { get; init; }
    public string? VoidReason { get; init; }
    public List<SaleItemRow> Items { get; } = [];
    public long Owed => Total - AmountPaid;
}

public enum LedgerKind { CreditSale, Repayment }

public sealed class LedgerEntry
{
    public LedgerKind Kind { get; init; }
    public long Id { get; init; }
    public DateTime Date { get; init; }
    public DateTime SortKey { get; init; }
    public long SaleTotal { get; init; }
    public long SalePaid { get; init; }
    public long Amount { get; init; }
    public bool Cancelled { get; init; }
    public string? Reason { get; init; }
    public string ItemsText { get; init; } = "";
    public long BalanceAfter { get; set; }
}

public sealed class ProductSales
{
    public long ProductId { get; init; }
    public string Name { get; init; } = "";
    public string Unit { get; init; } = "";
    public bool SoldByWeight { get; init; }
    public decimal Quantity { get; init; }
    public long Amount { get; init; }
}

/// <summary>
/// PR8: where a price came from. Added = the Add product dialog; Edited = the Edit product dialog;
/// CsvImport = a CSV file, for a new product (no old price) or a changed price.
/// </summary>
public enum PriceSource { Added, Edited, CsvImport }

/// <summary>One price a product has had. OldPrice is null for its first price.</summary>
public sealed class PriceChange
{
    public long Id { get; init; }
    public DateTime At { get; init; }
    public long? OldPrice { get; init; }
    public long NewPrice { get; init; }
    public PriceSource Source { get; init; }
    public string FileName { get; init; } = "";
}

public enum StockMoveKind { Sale, Void, StockIn, Adjustment }

/// <summary>ST6: why a shelf count differed from the app.</summary>
public enum AdjustReason { Damaged, CountWrong, Expired, Other }

public static class AdjustReasons
{
    public static readonly AdjustReason[] All =
        [AdjustReason.Damaged, AdjustReason.CountWrong, AdjustReason.Expired, AdjustReason.Other];

    public static string Code(AdjustReason r) => r switch
    {
        AdjustReason.Damaged => "damaged",
        AdjustReason.CountWrong => "count_wrong",
        AdjustReason.Expired => "expired",
        _ => "other",
    };

    public static AdjustReason FromCode(string? code) =>
        All.FirstOrDefault(r => Code(r) == code, AdjustReason.Other);

    public static string Display(AdjustReason r) => Loc.Get("Adjust_Reason_" + r);
}

/// <summary>One product's stock: the sum of its movements, and when stock last came in.</summary>
public sealed class StockLevel
{
    public required Product Product { get; init; }
    public decimal OnHand { get; init; }
    public DateTime? LastStockIn { get; init; }
    public bool IsOut => OnHand <= 0;
}

/// <summary>ST7: one change to a product's stock, with the stock left after it.</summary>
public sealed class StockMovement
{
    public long Id { get; init; }
    public DateTime At { get; init; }
    public StockMoveKind Kind { get; init; }
    public decimal Change { get; init; }
    public decimal After { get; init; }
    public long? SaleId { get; init; }
    public string? CustomerName { get; init; }
    public string Supplier { get; init; } = "";
    public AdjustReason? Reason { get; init; }
    public string Note { get; init; } = "";
}

public sealed record StockInLineInput(long ProductId, decimal Quantity);

/// <summary>ST4: one delivery and the products in it.</summary>
public sealed class StockInRecord
{
    public long Id { get; init; }
    public DateTime ReceivedAt { get; init; }
    public string Supplier { get; init; } = "";
    public string Note { get; init; } = "";
    public List<StockInLine> Lines { get; } = [];
}

public sealed class StockInLine
{
    public long ProductId { get; init; }
    public string Name { get; init; } = "";
    public decimal Quantity { get; init; }
    public decimal After { get; init; }
    public decimal Before => After - Quantity;
}

public sealed class ReportTotals
{
    public long TotalSold { get; init; }
    public long CashAtSale { get; init; }
    public long Repayments { get; init; }
    public long CashReceived => CashAtSale + Repayments;
    public long CreditGiven { get; init; }
    public int CreditSales { get; init; }
    public int SalesCount { get; init; }
    public int VoidedCount { get; init; }
}
