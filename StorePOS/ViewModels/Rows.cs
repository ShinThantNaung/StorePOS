using StorePOS.Infrastructure;
using StorePOS.Models;

namespace StorePOS.ViewModels;

/// <summary>A product as shown in the Sales and Prices tables.</summary>
public sealed class ProductRow(Product p)
{
    public Product Product { get; } = p;
    public long Id => Product.Id;
    public string Name => Product.Name;
    public string Unit => Product.Unit;
    public bool SoldByWeight => Product.SoldByWeight;
    public long Price => Product.Price;
    public string PriceText => Fmt.Money(Product.Price);
    public string CategoryName => Categories.Display(Product.Category);
    public Category Category => Product.Category;
    public string CategorySortKey => CategoryName + "\u0001" + Name;
    public string UnitDisplay => Product.SoldByWeight ? Loc.F("Prices_UnitWeight", Product.Unit) : Product.Unit;
    public bool Inactive => !Product.Active;
    public string SearchKey { get; } = Fmt.SearchKey(p.Name);
}

public sealed class CustomerOption(Customer? customer)
{
    public Customer? Customer { get; } = customer;
    public long? Id => Customer?.Id;
    public string Display => Customer?.Display ?? Loc.Get("Sales_NoCustomer");
    public override string ToString() => Display;
}
