using System.Globalization;
using System.Text;
using StorePOS.Data;
using StorePOS.Infrastructure;
using StorePOS.Models;

namespace StorePOS.Services;

public enum ImportKind { New, PriceChange, NoChange }

public sealed class ImportRow
{
    public int Row { get; init; }
    public string Name { get; init; } = "";
    public string Company { get; init; } = "";
    public string Size { get; init; } = "";
    public Category Category { get; init; }
    public string Unit { get; init; } = "";
    public long Price { get; init; }
    public bool SoldByWeight { get; init; }
    public ImportKind Kind { get; init; }
    public long OldPrice { get; init; }
}

public sealed class ImportError
{
    public int Row { get; init; }
    public string Message { get; init; } = "";
}

public sealed class ImportPreview
{
    public List<ImportRow> Rows { get; } = [];
    public List<ImportError> Errors { get; } = [];
    public int RowsRead { get; set; }
    /// <summary>Recorded with every price the import adds or changes (PR8).</summary>
    public string FileName { get; set; } = "";
    public int NewCount => Rows.Count(r => r.Kind == ImportKind.New);
    public int ChangeCount => Rows.Count(r => r.Kind == ImportKind.PriceChange);
    public int SameCount => Rows.Count(r => r.Kind == ImportKind.NoChange);
}

/// <summary>
/// PR6: columns name, category, unit, price, sold by weight (yes/no), then company and size
/// (both optional). UTF-8 only. A row matches an existing product on name, company and size.
/// </summary>
public sealed class CsvImportService(Db db, ProductService products, Func<DateTime> clock)
{
    public ImportPreview Preview(string path)
    {
        byte[] bytes;
        try { bytes = File.ReadAllBytes(path); }
        catch (Exception ex) { throw new DomainException(Loc.F("Err_Csv_Unreadable", ex.Message)); }
        var preview = Preview(bytes);
        preview.FileName = Path.GetFileName(path);
        return preview;
    }

    public ImportPreview Preview(byte[] bytes)
    {
        string text;
        try { text = new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes); }
        catch (DecoderFallbackException) { throw new DomainException(Loc.Get("Err_Csv_NotUtf8")); }
        text = text.TrimStart('﻿');

        var existing = products.List(includeInactive: true)
            .GroupBy(p => ProductService.IdentityKey(p.Name, p.Company, p.Size)).ToDictionary(g => g.Key, g => g.First());
        var preview = new ImportPreview();
        var seen = new Dictionary<string, int>();

        foreach (var (rowNo, cells) in ParseCsv(text))
        {
            if (cells.All(string.IsNullOrWhiteSpace)) continue;
            if (rowNo == 1 && LooksLikeHeader(cells)) continue;
            preview.RowsRead++;

            var error = Validate(cells, out var row);
            if (error == null)
            {
                var key = ProductService.IdentityKey(row!.Name, row.Company, row.Size);
                if (seen.TryGetValue(key, out var firstRow)) error = Loc.F("Err_Csv_Duplicate", firstRow);
                else seen[key] = rowNo;
            }
            if (error != null)
            {
                preview.Errors.Add(new ImportError { Row = rowNo, Message = error });
                continue;
            }

            var match = existing.GetValueOrDefault(ProductService.IdentityKey(row!.Name, row.Company, row.Size));
            preview.Rows.Add(new ImportRow
            {
                Row = rowNo, Name = row.Name, Company = row.Company, Size = row.Size, Category = row.Category,
                Unit = row.Unit, Price = row.Price, SoldByWeight = row.SoldByWeight,
                Kind = match == null ? ImportKind.New : match.Price == row.Price ? ImportKind.NoChange : ImportKind.PriceChange,
                OldPrice = match?.Price ?? 0,
            });
        }
        if (preview.RowsRead == 0) throw new DomainException(Loc.Get("Err_Csv_Empty"));
        return preview;
    }

    /// <summary>
    /// Adds new products and updates the price of existing ones, in one transaction, writing each
    /// new or changed price to the price history with the file name.
    /// </summary>
    public int Apply(ImportPreview preview)
    {
        var count = 0;
        var now = clock();
        using var s = db.Write();
        foreach (var r in preview.Rows.Where(r => r.Kind != ImportKind.NoChange))
        {
            // Looked up again here: the product list may have changed since the preview.
            var key = ProductService.IdentityKey(r.Name, r.Company, r.Size);
            var current = s.Query("SELECT id, price FROM products WHERE name_key = $k",
                x => (Id: x.GetInt64(0), Price: x.GetInt64(1)), ("$k", key)).FirstOrDefault();
            if (current.Id == 0)
            {
                var id = s.Insert("""
                    INSERT INTO products(name, name_key, category, unit, price, sold_by_weight, active, company, size)
                    VALUES($n, $k, $c, $u, $p, $w, 1, $co, $size)
                    """,
                    ("$n", r.Name), ("$k", key), ("$c", Categories.Code(r.Category)), ("$u", r.Unit), ("$p", r.Price),
                    ("$w", r.SoldByWeight ? 1 : 0), ("$co", r.Company), ("$size", r.Size));
                ProductService.RecordPriceChange(s, id, null, r.Price, PriceSource.CsvImport, preview.FileName, now);
            }
            else if (current.Price != r.Price)
            {
                s.Exec("UPDATE products SET price = $p WHERE id = $id", ("$p", r.Price), ("$id", current.Id));
                ProductService.RecordPriceChange(s, current.Id, current.Price, r.Price, PriceSource.CsvImport, preview.FileName, now);
            }
            count++;
        }
        s.Commit();
        return count;
    }

    private static bool LooksLikeHeader(List<string> cells) =>
        cells.Count >= 4 && !Fmt.TryParseMoney(cells[3], out _) &&
        (Fmt.SearchKey(cells[0]) is "name" or "product" || Fmt.SearchKey(cells[3]) is "price");

    private static string? Validate(List<string> raw, out ImportRow? row)
    {
        row = null;
        var cells = raw.Select(c => c.Trim()).ToList();
        while (cells.Count > 7 && cells[^1].Length == 0) cells.RemoveAt(cells.Count - 1);
        if (cells.Count < 4 || cells.Count > 7) return Loc.F("Err_Csv_Columns", cells.Count);

        var name = cells[0];
        if (name.Length == 0) return Loc.Get("Err_Csv_NameEmpty");
        if (!Categories.TryParse(cells[1], out var category)) return Loc.F("Err_Csv_Category", cells[1]);
        var unit = cells[2];
        if (unit.Length == 0) return Loc.Get("Err_Csv_UnitEmpty");

        var priceText = cells[3];
        if (priceText.Length == 0) return Loc.Get("Err_Csv_PriceEmpty");
        if (!Fmt.TryParseMoney(priceText, out var price))
        {
            var cleaned = Fmt.NormalizeDigits(priceText).Replace(",", "");
            static bool IsNumber(string s) =>
                decimal.TryParse(s, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out _);
            if (cleaned.Any(ch => ch is 'O' or 'o') && IsNumber(cleaned.Replace('O', '0').Replace('o', '0')))
                return Loc.F("Err_Csv_PriceLetterO", priceText);
            if (IsNumber(cleaned)) return Loc.F("Err_Csv_PriceDecimal", priceText);
            return Loc.F("Err_Csv_PriceNotNumber", priceText);
        }

        var weightText = cells.Count > 4 ? Fmt.SearchKey(cells[4]) : "";
        bool weight;
        if (weightText is "" or "no" or "n" or "false" or "0" || weightText == Fmt.SearchKey(Loc.Get("Csv_No"))) weight = false;
        else if (weightText is "yes" or "y" or "true" or "1" || weightText == Fmt.SearchKey(Loc.Get("Csv_Yes"))) weight = true;
        else return Loc.F("Err_Csv_Weight", cells[4]);

        var company = cells.Count > 5 ? cells[5] : "";
        var size = cells.Count > 6 ? cells[6] : "";
        row = new ImportRow
        {
            Name = name, Company = company, Size = size, Category = category, Unit = unit, Price = price, SoldByWeight = weight,
        };
        return null;
    }

    /// <summary>RFC 4180: quoted fields, doubled quotes, commas and line breaks inside quotes.</summary>
    internal static IEnumerable<(int Row, List<string> Cells)> ParseCsv(string text)
    {
        var cells = new List<string>();
        var cell = new StringBuilder();
        var inQuotes = false;
        var line = 1;
        var rowStart = 1;
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                    else inQuotes = false;
                }
                else
                {
                    if (ch == '\n') line++;
                    cell.Append(ch);
                }
                continue;
            }
            switch (ch)
            {
                case '"': inQuotes = true; break;
                case ',': cells.Add(cell.ToString()); cell.Clear(); break;
                case '\r': break;
                case '\n':
                    cells.Add(cell.ToString()); cell.Clear();
                    yield return (rowStart, cells);
                    cells = [];
                    line++;
                    rowStart = line;
                    break;
                default: cell.Append(ch); break;
            }
        }
        if (cell.Length > 0 || cells.Count > 0)
        {
            cells.Add(cell.ToString());
            yield return (rowStart, cells);
        }
    }
}
