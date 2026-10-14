using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using StorePOS.Dialogs;
using StorePOS.Infrastructure;
using StorePOS.Models;
using StorePOS.ViewModels;

namespace StorePOS;

/// <summary>
/// Developer aid: "StorePOS.exe --screenshots &lt;folder&gt; --data &lt;folder&gt;" renders every screen and
/// dialog at 1366 × 700 to PNG files (with demo data), then exits.
/// </summary>
internal static class Screenshots
{
    public static void Run(MainWindow window, MainViewModel main, string folder)
    {
        Directory.CreateDirectory(folder);
        window.WindowState = WindowState.Normal;
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = 0;
        window.Top = 0;
        window.Root.Width = 1366;
        window.Root.Height = 700;
        window.SizeToContent = SizeToContent.WidthAndHeight;
        window.Show();
        var s = App.Services;

        main.Navigate(Section.Sales);
        var sales = main.SalesPage;
        foreach (var (name, qty) in new[] { ("Glyphosate", "2"), ("Urea fertilizer (loose)", "2.5"), ("Paraquat", "1"), ("Rice seed", "10") })
        {
            sales.SelectedProduct = sales.ProductsView.Cast<ProductRow>().First(p => p.Name.StartsWith(name));
            sales.QuantityText = qty;
            sales.AddToCartCommand.Execute(null);
        }
        sales.Cart[2].PriceText = "8,000";
        sales.SelectedProduct = sales.ProductsView.Cast<ProductRow>().First(p => p.Name.StartsWith("Cypermethrin"));
        sales.PaidText = "50,000";
        sales.SelectedCustomer = sales.Customers.Skip(1).First();
        Shot(window.Root, folder, "01-sales");
        sales.Cart[0].QtyText = "500";
        Shot(window.Root, folder, "01b-sales-no-stock");

        main.Navigate(Section.Prices);
        main.PricesPage.Selected = null;
        Shot(window.Root, folder, "02-prices");

        main.Navigate(Section.Debts);
        Shot(window.Root, folder, "03-debts");

        var owing = s.Debts.Balances().OrderByDescending(b => b.Balance).First();
        main.OpenCustomer(owing.Customer.Id);
        Shot(window.Root, folder, "04-customer");

        main.Navigate(Section.Reports);
        var reports = main.ReportsPage;
        if (reports.DaySales.Count > 1) reports.DaySales[1].IsExpanded = true;
        Shot(window.Root, folder, "05-reports-daily");
        Services.ExportService.SavePdf(reports.BuildDocument(), Path.Combine(folder, "report-daily.pdf"));
        Services.ExportService.SaveExcel(reports.BuildDocument(), Path.Combine(folder, "report-daily.xlsx"));
        reports.Mode = "Weekly";
        Shot(window.Root, folder, "06-reports-weekly");
        reports.Mode = "Daily";

        main.Navigate(Section.Backup);
        Shot(window.Root, folder, "07-backup");

        DialogShot(folder, "08-confirm", new ConfirmDialog(Loc.Get("Confirm_ClearTitle"), Loc.F("Confirm_ClearMany", 4),
            Loc.Get("Confirm_KeepItems"), Loc.Get("Sales_ClearCart")));
        DialogShot(folder, "09-void", new ReasonDialog(Loc.Get("Void_Title"),
            [
                (Loc.Get("Void_Time"), Loc.Date(new DateTime(2026, 10, 13, 10, 5, 0), "Date_Day") + ", " + Loc.Date(new DateTime(2026, 10, 13, 10, 5, 0), "Time_Short")), (Loc.Get("Void_Customer"), Loc.Get("Report_WalkIn")),
                (Loc.Get("Void_Items"), "Paraquat herbicide 500 ml × 1"), (Loc.Get("Void_Total"), Fmt.Ks(8500)),
            ],
            Loc.Get("Void_Paragraph"), Loc.Get("Void_Placeholder"), Loc.Get("Void_Action")));
        var repay = new RepaymentDialog(owing);
        repay.AmountText = "20,000";
        DialogShot(folder, "10-repayment", repay);
        DialogShot(folder, "11-product", new ProductDialog(s.Products.List(true).First(p => p.SoldByWeight && p.Company.Length > 0)));
        DialogShot(folder, "12-customer", new CustomerDialog(null));

        var csv = Path.Combine(s.DataFolder, "sample-prices.csv");
        File.WriteAllText(csv, """
            name,category,unit,price,sold by weight,company,size
            Glyphosate herbicide,Herbicide,bottle,16000,no,Green Valley,1 L
            Paraquat herbicide,Herbicide,bottle,8500,no,Shwe Lal,500 ml
            Atrazine herbicide,Herbicide,pack,14000,no,Golden Harvest,1 kg
            Urea fertilizer (loose),Fertilizer,kg,2300,yes
            "Hybrid maize seed, 2 kg pack",Seed,pack,"28,000",no
            Cypermethrin insecticide 250 ml,Pesticide,bottle,12.5OO,no
            Sesame seed 1 kg pack,Insecticide,pack,7500,no
            ,Seed,pack,3000,no
            ပဲတီစိမ်း မျိုး ၁ ကီလို,Seed,ထုပ်,"၅,၅၀၀",no
            """, new System.Text.UTF8Encoding(true));
        var import = new ImportCsvDialog();
        import.Load(csv);
        DialogShot(folder, "13-import", import);

        main.Navigate(Section.Stock);
        var stock = main.StockPage;
        stock.Selected = stock.View.Cast<StockRow>()
            .First(r => r.Product.Name.StartsWith("Glyphosate") && r.Product.Company == "Green Valley" && r.Product.Size == "1 L");
        Shot(window.Root, folder, "14-stock");
        stock.Show = "Out";
        Shot(window.Root, folder, "15-stock-out");
        stock.Show = "All";

        main.OpenStockIns();
        if (main.StockInsPage.View.Count > 1) ((StockInRow)main.StockInsPage.View.GetItemAt(1)).IsExpanded = true;
        Shot(window.Root, folder, "16-stock-ins");

        var stockIn = new RecordStockInDialog();
        foreach (var (name, qty) in new[] { ("Glyphosate", "24"), ("Paraquat", "24"), ("Butachlor", "12") })
        {
            stockIn.PickedProduct = stockIn.Products.First(p => p.Name.StartsWith(name));
            stockIn.QtyText = qty;
            stockIn.AddCommand.Execute(null);
        }
        stockIn.SupplierText = "Shwe Pyi Trading";
        stockIn.NoteText = "Invoice SP-1204";
        DialogShot(folder, "17-stock-in", stockIn);

        var paraquat = s.Products.List(false).First(p => p.Name.StartsWith("Paraquat"));
        var adjust = new AdjustStockDialog(paraquat.Id)
        {
            CountText = Fmt.Qty(Math.Max(0, s.Stock.OnHand(paraquat.Id) - 2)),
            Reason = AdjustReason.Damaged,
            NoteText = "2 bottles cracked in the box",
        };
        DialogShot(folder, "18-adjust", adjust);

        var glyphosate = s.Products.List(false).First(p => p.Name.StartsWith("Glyphosate") && p.Company == "Green Valley" && p.Size == "1 L");
        DialogShot(folder, "19-price-history", new PriceHistoryDialog(glyphosate));

        Application.Current.Shutdown();
    }

    private static void Settle()
    {
        for (var i = 0; i < 4; i++)
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ContextIdle, new Action(() => { }));
    }

    private static void Shot(FrameworkElement element, string folder, string name)
    {
        Settle();
        element.UpdateLayout();
        Settle();
        var width = (int)Math.Ceiling(element.ActualWidth);
        var height = (int)Math.Ceiling(element.ActualHeight);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle((Brush)Application.Current.FindResource("Grey100"), null, new Rect(0, 0, width, height));
            dc.DrawRectangle(new VisualBrush(element), null, new Rect(0, 0, width, height));
        }
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(folder, name + ".png"));
        encoder.Save(stream);
    }

    private static void DialogShot(string folder, string name, Window dialog)
    {
        dialog.WindowStartupLocation = WindowStartupLocation.Manual;
        dialog.Left = 40;
        dialog.Top = 40;
        dialog.ShowInTaskbar = false;
        dialog.Show();
        Shot((FrameworkElement)dialog.Content, folder, name);
        dialog.Close();
    }
}
