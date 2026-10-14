using System.ComponentModel;
using System.Windows;
using StorePOS.Infrastructure;
using StorePOS.Models;

namespace StorePOS.Dialogs;

public sealed class PriceHistoryRow(PriceChange c)
{
    public string DateText => Loc.Date(c.At, "Date_Short") + ", " + Loc.Date(c.At, "Time_Short");
    public string PriceText => Fmt.Money(c.NewPrice);
    public string ChangeText => c.OldPrice is { } old ? Fmt.SignedMoney(c.NewPrice - old) : Loc.Get("PriceHistory_FirstPrice");

    public string HowText => (c.Source, c.OldPrice, c.FileName.Length > 0) switch
    {
        (PriceSource.Added, _, _) => Loc.Get("PriceHistory_Added"),
        (PriceSource.Edited, _, _) => Loc.Get("PriceHistory_Edited"),
        (_, null, true) => Loc.F("PriceHistory_CsvAdded", c.FileName),
        (_, null, false) => Loc.Get("PriceHistory_CsvAddedNoFile"),
        (_, _, true) => Loc.F("PriceHistory_Csv", c.FileName),
        _ => Loc.Get("PriceHistory_CsvNoFile"),
    };
}

/// <summary>PR9: one product's price now, since when, and every earlier price, newest first.</summary>
public partial class PriceHistoryDialog : Window
{
    public PriceHistoryDialog(Product product)
    {
        var s = App.Services;
        product = s.Products.Get(product.Id) ?? product;
        var history = s.Products.PriceHistory(product.Id);

        ProductName = product.FullName;
        var category = Categories.Display(product.Category);
        SubtitleText = product.SoldByWeight
            ? Loc.F("PriceHistory_SoldByWeight", category, Units.Display(product.Unit))
            : Loc.F("PriceHistory_SoldBy", category, Units.Word(product.Unit, 1));
        PriceNowText = Fmt.Ks(product.Price);
        SinceText = history.Count == 0
            ? ""
            : Loc.F("PriceHistory_Since", Loc.Date(history[0].At, "Date_Short") + ", " + Loc.Date(history[0].At, "Time_Short"));
        Rows = history.Select(c => new PriceHistoryRow(c)).ToList();

        InitializeComponent();
        DataContext = this;
        // Always newest first; the arrow says so.
        Loaded += (_, _) => DateColumn.SortDirection = ListSortDirection.Descending;
    }

    public string ProductName { get; }
    public string SubtitleText { get; }
    public string PriceNowText { get; }
    public string SinceText { get; }
    public List<PriceHistoryRow> Rows { get; }
    public bool IsEmpty => Rows.Count == 0;
}
