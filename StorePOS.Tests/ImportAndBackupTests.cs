using System.Text;
using StorePOS.Infrastructure;
using StorePOS.Models;
using StorePOS.Services;

namespace StorePOS.Tests;

public class ImportAndBackupTests
{
    private static byte[] Utf8(string s) => new UTF8Encoding(true).GetBytes(s);

    [Fact]
    public void Csv_preview_classifies_rows_and_lists_every_error()
    {
        using var shop = new TestShop();
        shop.AddProduct("Glyphosate herbicide 1 L", 15000, cat: Category.Herbicide);
        shop.AddProduct("Paraquat herbicide 500 ml", 8500, cat: Category.Herbicide);

        var preview = shop.S.Import.Preview(Utf8("""
            name,category,unit,price,sold by weight
            Glyphosate herbicide 1 L,Herbicide,bottle,"16,000",no
            paraquat HERBICIDE 500 ml,herbicide,bottle,8500,no
            "Hybrid maize seed, 2 kg pack",Seed,pack,28000,no
            Urea fertilizer (loose),Fertilizer,kg,2300,yes
            ပဲတီစိမ်း မျိုး ၁ ကီလို,Seed,ထုပ်,"၅,၅၀၀",no
            Cypermethrin,Pesticide,bottle,"12,5OO",no
            Sesame seed,Insecticide,pack,7500,no
            ,Seed,pack,3000,no
            Mancozeb,Pesticide,pack,11000,maybe
            Urea fertilizer (loose),Fertilizer,kg,2400,yes

            """));

        Assert.Equal(10, preview.RowsRead);
        Assert.Equal(3, preview.NewCount);
        Assert.Equal(1, preview.ChangeCount);
        Assert.Equal(1, preview.SameCount);
        Assert.Equal([7, 8, 9, 10, 11], preview.Errors.Select(e => e.Row));
        Assert.Contains("letter O", preview.Errors[0].Message);
        Assert.Contains("Insecticide", preview.Errors[1].Message);
        Assert.Contains("row 5", preview.Errors[4].Message);
        Assert.Equal("Hybrid maize seed, 2 kg pack", preview.Rows[2].Name);
        Assert.Equal(5500, preview.Rows.Single(r => r.Unit == "ထုပ်").Price);
        Assert.True(preview.Rows.Single(r => r.Name.StartsWith("Urea")).SoldByWeight);
    }

    [Fact]
    public void Csv_apply_adds_new_products_and_updates_prices_only()
    {
        using var shop = new TestShop();
        var gly = shop.AddProduct("Glyphosate herbicide 1 L", 15000, cat: Category.Herbicide);
        var preview = shop.S.Import.Preview(Utf8("""
            Glyphosate herbicide 1 L,Seed,bag,16000,no
            Atrazine herbicide 1 kg,Herbicide,pack,14000,no
            """));
        Assert.Equal(2, shop.S.Import.Apply(preview));

        var updated = shop.S.Products.Get(gly.Id)!;
        Assert.Equal(16000, updated.Price);
        Assert.Equal(Category.Herbicide, updated.Category); // only the price changes
        Assert.Equal(2, shop.S.Products.List(true).Count);
    }

    [Fact]
    public void Csv_that_is_not_UTF8_is_refused_with_advice()
    {
        using var shop = new TestShop();
        var ansi = new byte[] { (byte)'T', (byte)'e', 0xE9, (byte)',', (byte)'S', (byte)'e', (byte)'e', (byte)'d' };
        var ex = Assert.Throws<DomainException>(() => shop.S.Import.Preview(ansi));
        Assert.Contains("CSV UTF-8", ex.Message);
    }

    [Fact]
    public void Backup_and_restore_round_trip_keeps_a_safety_copy()
    {
        using var shop = new TestShop();
        var gly = shop.AddProduct("Glyphosate herbicide 1 L", 15000, stock: 100);
        shop.Sell(shop.Now, 15000, null, TestShop.Line(gly, 1));
        var copy = shop.S.Backup.BackupNow();
        Assert.True(File.Exists(copy));
        Assert.True(shop.S.Backup.BackedUpToday);
        Assert.Single(shop.S.Backup.Copies());

        shop.Sell(shop.Now.AddMinutes(5), 30000, null, TestShop.Line(gly, 2));
        Assert.Equal(2, shop.S.Sales.SalesBetween(shop.Now.Date, shop.Now.Date.AddDays(1)).Count);

        var safety = shop.S.Backup.Restore(copy);
        Assert.True(File.Exists(safety));
        Assert.Single(shop.S.Sales.SalesBetween(shop.Now.Date, shop.Now.Date.AddDays(1)));
    }

    [Fact]
    public void Restore_refuses_a_file_that_is_not_a_shop_copy()
    {
        using var shop = new TestShop();
        var bogus = Path.Combine(shop.Folder, "notes.db");
        File.WriteAllText(bogus, "hello");
        Assert.Throws<DomainException>(() => shop.S.Backup.Restore(bogus));
    }

    [Fact]
    public void Excel_and_pdf_exports_produce_files()
    {
        using var shop = new TestShop();
        var doc = new ReportDocument { Title = "Daily report", Period = "Tue, 13 Oct 2026" };
        doc.Figures.Add(new("Total sold", "78,500 Ks", "All sales, paid or not"));
        var table = new ReportTable { Title = "Best sellers", Headers = ["Product", "Ks"], Numeric = [false, true] };
        table.Rows.Add(["ယူရီးယား မြေဩဇာ", 104000L]);
        doc.Tables.Add(table);

        var xlsx = Path.Combine(shop.Folder, "r.xlsx");
        var pdf = Path.Combine(shop.Folder, "r.pdf");
        ExportService.SaveExcel(doc, xlsx);
        ExportService.SavePdf(doc, pdf);

        using (var zip = System.IO.Compression.ZipFile.OpenRead(xlsx))
        {
            var sheet = new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open()).ReadToEnd();
            Assert.Contains("ယူရီးယား မြေဩဇာ", sheet);
            Assert.Contains("<v>104000</v>", sheet);
        }
        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(File.ReadAllBytes(pdf), 0, 4));
    }
}
