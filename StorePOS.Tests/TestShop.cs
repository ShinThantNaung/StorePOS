using System.Globalization;
using Microsoft.Data.Sqlite;
using StorePOS.Infrastructure;
using StorePOS.Models;
using StorePOS.Services;

namespace StorePOS.Tests;

/// <summary>A throwaway shop database with a clock the test controls.</summary>
public sealed class TestShop : IDisposable
{
    public TestShop()
    {
        Loc.Culture = CultureInfo.GetCultureInfo("en-GB");
        Folder = Path.Combine(Path.GetTempPath(), "storepos-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Folder);
        Settings = new AppSettings { BackupFolder = Path.Combine(Folder, "Backups") };
        S = new AppServices(Folder, Settings, () => Now);
        S.Db.EnsureCreated();
    }

    public string Folder { get; }
    public AppSettings Settings { get; }
    public AppServices S { get; }
    public DateTime Now { get; set; } = new(2026, 10, 13, 9, 0, 0);

    public Product AddProduct(string name, long price, bool weight = false, Category cat = Category.Pesticide,
        string company = "", string size = "")
    {
        var p = new Product
        {
            Name = name, Company = company, Size = size, Category = cat, Unit = weight ? "kg" : "bottle", Price = price,
            SoldByWeight = weight,
        };
        p.Id = S.Products.Save(p);
        return p;
    }

    public long AddCustomer(string name, string address = "Hlaing village") =>
        S.Debts.SaveCustomer(new Customer { Name = name, Address = address });

    public static SaleLineInput Line(Product p, decimal qty, long? price = null) =>
        new(p.Id, p.Name, p.Unit, p.SoldByWeight, qty, price ?? p.Price, p.Price);

    public long Sell(DateTime at, long tendered, long? customer, params SaleLineInput[] lines)
    {
        Now = at;
        return S.Sales.Save(lines, tendered, customer);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(Folder, recursive: true); }
        catch (IOException) { /* a stray handle on Windows; the temp folder is cleaned later */ }
    }
}
