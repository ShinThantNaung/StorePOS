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

public sealed class Product
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public Category Category { get; set; }
    public string Unit { get; set; } = "";
    public long Price { get; set; }
    public bool SoldByWeight { get; set; }
    public bool Active { get; set; } = true;
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
    public string Describe() => $"{Name} × {Fmt.QtyWithUnit(Quantity, Unit, SoldByWeight)}";
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
