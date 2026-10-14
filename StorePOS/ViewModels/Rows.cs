using System.Globalization;
using StorePOS.Infrastructure;
using StorePOS.Models;

namespace StorePOS.ViewModels;

/// <summary>A product as shown in the Sales and Prices tables.</summary>
public sealed class ProductRow(Product p)
{
    public Product Product { get; } = p;
    public long Id => Product.Id;
    public string Name => Product.Name;
    public string FullName => Product.FullName;
    public string NameAndSize => Product.FormatFullName(Name, Size, "");
    public string Company => Product.Company;
    public string CompanySortKey => Company + "\u0001" + Name;
    public string Size => Product.Size;
    public string Unit => Units.Display(Product.Unit);
    public bool SoldByWeight => Product.SoldByWeight;
    public long Price => Product.Price;
    public string PriceText => Fmt.Money(Product.Price);
    public DateTime? PriceChangedAt => Product.PriceChangedAt;
    public string PriceChangedText => Product.PriceChangedAt is { } d ? Loc.Date(d, "Date_Short") : "";
    public string CategoryName => Categories.Display(Product.Category);
    public Category Category => Product.Category;
    public string CategorySortKey => CategoryName + "\u0001" + Name;
    public string UnitDisplay => Product.SoldByWeight ? Loc.F("Prices_UnitWeight", Unit) : Unit;
    public bool Inactive => !Product.Active;
    public string SearchKey { get; } = Fmt.SearchKey(p.Name);
    public string CompanyKey { get; } = Fmt.SearchKey(p.Company);
    public string SizeKey { get; } = Fmt.SizeKey(p.Size);

    /// <summary>Part of the name, part of the company, and the exact size. An empty key matches all.</summary>
    public bool Matches(string nameKey, string companyKey, string sizeKey) =>
        (nameKey.Length == 0 || SearchKey.Contains(nameKey, StringComparison.Ordinal)) &&
        (companyKey.Length == 0 || CompanyKey.Contains(companyKey, StringComparison.Ordinal)) &&
        (sizeKey.Length == 0 || SizeKey == sizeKey);
}

/// <summary>An entry in the Size filter on Sales and Prices: "All", or one size as first typed.</summary>
public sealed record SizeOption(string Key, string Display)
{
    private static readonly StringComparer Order =
        StringComparer.Create(CultureInfo.InvariantCulture, CompareOptions.NumericOrdering | CompareOptions.IgnoreCase);

    public static List<SizeOption> From(IEnumerable<ProductRow> rows) =>
    [
        new("", Loc.Get("Cat_All")),
        .. rows.Where(r => r.SizeKey.Length > 0)
            .GroupBy(r => r.SizeKey)
            .Select(g => new SizeOption(g.Key, g.First().Size))
            .OrderBy(o => o.Display, Order),
    ];
}

public sealed class CustomerOption(Customer? customer)
{
    public Customer? Customer { get; } = customer;
    public long? Id => Customer?.Id;
    public string Display => Customer?.Display ?? Loc.Get("Sales_NoCustomer");
    public override string ToString() => Display;
}
