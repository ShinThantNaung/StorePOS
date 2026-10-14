using StorePOS.Data;
using StorePOS.Infrastructure;
using StorePOS.Models;

namespace StorePOS.Services;

public sealed class ProductService(Db db)
{
    private const string Columns = "id, name, category, unit, price, sold_by_weight, active, company, size";

    internal static Product Map(Microsoft.Data.Sqlite.SqliteDataReader r) => new()
    {
        Id = r.GetInt64(0),
        Name = r.GetString(1),
        Category = Categories.FromCode(r.GetString(2)),
        Unit = r.GetString(3),
        Price = r.GetInt64(4),
        SoldByWeight = r.Bool(5),
        Active = r.Bool(6),
        Company = r.GetString(7),
        Size = r.GetString(8),
    };

    /// <summary>
    /// What makes a product unique, stored in products.name_key: the name, company and size,
    /// ignoring capitals and spacing. Without company and size it is the plain name key.
    /// </summary>
    internal static string IdentityKey(string name, string company, string size)
    {
        var key = Fmt.SearchKey(name);
        company = Fmt.SearchKey(company);
        size = Fmt.SizeKey(size);
        return company.Length == 0 && size.Length == 0 ? key : $"{key}\u001f{company}\u001f{size}";
    }

    public List<Product> List(bool includeInactive)
    {
        using var s = db.Read();
        var where = includeInactive ? "" : "WHERE active = 1";
        return s.Query($"SELECT {Columns} FROM products {where} ORDER BY name", Map);
    }

    public Product? Get(long id)
    {
        using var s = db.Read();
        return s.Query($"SELECT {Columns} FROM products WHERE id = $id", Map, ("$id", id)).FirstOrDefault();
    }

    public long Save(Product p)
    {
        var name = (p.Name ?? "").Trim();
        var company = (p.Company ?? "").Trim();
        var size = (p.Size ?? "").Trim();
        var unit = (p.Unit ?? "").Trim();
        if (name.Length == 0) throw new DomainException(Loc.Get("Err_NameRequired"));
        if (unit.Length == 0) throw new DomainException(Loc.Get("Err_UnitRequired"));
        if (p.Price < 0) throw new DomainException(Loc.Get("Err_PriceInvalid"));

        var key = IdentityKey(name, company, size);
        using var s = db.Write();
        var clash = s.Scalar<long?>("SELECT id FROM products WHERE name_key = $k AND id <> $id",
            ("$k", key), ("$id", p.Id));
        if (clash != null) throw new DomainException(Loc.Get("Err_NameExists"));

        (string, object?)[] ps =
        [
            ("$id", p.Id), ("$name", name), ("$key", key), ("$cat", Categories.Code(p.Category)),
            ("$unit", unit), ("$price", p.Price), ("$w", p.SoldByWeight ? 1 : 0), ("$a", p.Active ? 1 : 0),
            ("$co", company), ("$size", size),
        ];
        long id;
        if (p.Id == 0)
        {
            id = s.Insert("""
                INSERT INTO products(name, name_key, category, unit, price, sold_by_weight, active, company, size)
                VALUES($name, $key, $cat, $unit, $price, $w, $a, $co, $size)
                """, ps);
        }
        else
        {
            s.Exec("""
                UPDATE products SET name=$name, name_key=$key, category=$cat, unit=$unit,
                    price=$price, sold_by_weight=$w, active=$a, company=$co, size=$size
                WHERE id=$id
                """, ps);
            id = p.Id;
        }
        s.Commit();
        return id;
    }
}
