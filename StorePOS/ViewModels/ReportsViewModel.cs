using Microsoft.Win32;
using StorePOS.Infrastructure;
using StorePOS.Models;
using StorePOS.Services;

namespace StorePOS.ViewModels;

public sealed class BestRow
{
    public int Rank { get; init; }
    public required ProductSales Data { get; init; }
    public string Name => Data.Name;
    public string QtyText => Fmt.QtyWithUnit(Data.Quantity, Data.Unit, Data.SoldByWeight);
    public string AmountText => Fmt.Money(Data.Amount);
}

public sealed record SaleItemLine(string Text, string AmountText);

public sealed class DaySaleRow : ObservableObject
{
    private bool _isExpanded;

    public DaySaleRow(SaleRecord sale, Action<DaySaleRow> voidAction)
    {
        Sale = sale;
        ToggleCommand = new RelayCommand(() => IsExpanded = !IsExpanded);
        VoidCommand = new RelayCommand(() => voidAction(this));
        Items = sale.Items.Select(i => new SaleItemLine(i.Describe(), Fmt.Money(i.LineTotal))).ToList();
    }

    public SaleRecord Sale { get; }
    public RelayCommand ToggleCommand { get; }
    public RelayCommand VoidCommand { get; }
    public List<SaleItemLine> Items { get; }

    public string TimeText => Loc.Date(Sale.SoldAt, "Time_Short");
    public string CustomerText => Sale.CustomerName ?? Loc.Get("Report_WalkIn");
    public bool IsVoided => Sale.Voided;
    public string VoidReason => Sale.VoidReason ?? "";
    public bool HasOwes => !Sale.Voided && Sale.Owed > 0;
    public string OwesText => Loc.F("Report_Owes", Fmt.Money(Sale.Owed));
    public string PayText => Loc.F("Report_PaidOwes", Fmt.Money(Sale.AmountPaid), Fmt.Money(Sale.Owed));
    public string TotalText => Fmt.Money(Sale.Total);
    public bool CanVoid => !Sale.Voided;

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (!Set(ref _isExpanded, value)) return;
            OnPropertyChanged(nameof(ToggleLabel));
        }
    }

    public string ToggleLabel => Loc.F(IsExpanded ? "Report_HideItems" : "Report_ShowItems", TimeText);
}

public sealed class DayBar
{
    public string Label { get; init; } = "";
    /// <summary>This day's sales as a share of the busiest day (0 to 1); the view turns it into a bar width.</summary>
    public double Fraction { get; init; }
    public string AmountText { get; init; } = "";
}

public sealed class ReportsViewModel : ObservableObject, IPage
{
    private readonly AppServices _s;
    private string _mode = "Daily";
    private DateTime _day;
    private DateTime _weekStart;
    private bool _bestByQty;
    private ReportTotals _totals = new();
    private List<ProductSales> _productSales = [];

    public ReportsViewModel(AppServices services)
    {
        _s = services;
        _day = services.Today;
        _weekStart = Fmt.WeekStart(services.Today);
        PrevCommand = new RelayCommand(() => Step(-1));
        NextCommand = new RelayCommand(() => Step(1), CanStepForward);
        ExportExcelCommand = new RelayCommand(() => Export(excel: true), () => !_exporting);
        ExportPdfCommand = new RelayCommand(() => Export(excel: false), () => !_exporting);
    }

    public RelayCommand PrevCommand { get; }
    public RelayCommand NextCommand { get; }
    public RelayCommand ExportExcelCommand { get; }
    public RelayCommand ExportPdfCommand { get; }

    public BulkObservableCollection<BestRow> Best { get; } = [];
    public BulkObservableCollection<BestRow> Slow { get; } = [];
    public BulkObservableCollection<DaySaleRow> DaySales { get; } = [];
    public BulkObservableCollection<DayBar> WeekBars { get; } = [];

    public void Activate()
    {
        // The date may have rolled over while the app was open.
        if (_day > _s.Today) _day = _s.Today;
        Refresh();
    }

    /// <summary>"Daily" or "Weekly"</summary>
    public string Mode
    {
        get => _mode;
        set
        {
            if (!Set(ref _mode, value)) return;
            OnPropertyChanged(nameof(IsDaily));
            OnPropertyChanged(nameof(IsWeekly));
            Refresh();
        }
    }

    public bool IsDaily => _mode == "Daily";
    public bool IsWeekly => !IsDaily;
    public DateTime MaxDate => _s.Today;

    public DateTime Day
    {
        get => _day;
        set
        {
            var d = value.Date > _s.Today ? _s.Today : value.Date;
            if (!Set(ref _day, d)) return;
            Refresh();
        }
    }

    /// <summary>Picking any date selects its week (Monday to Sunday).</summary>
    public DateTime WeekStart
    {
        get => _weekStart;
        set
        {
            if (!Set(ref _weekStart, Fmt.WeekStart(value))) return;
            Refresh();
        }
    }

    public string DayText => Loc.Date(_day, "Date_Day");

    public string WeekText => Loc.F("Report_WeekRange",
        Loc.Date(_weekStart, "Date_WeekStart"), Loc.Date(_weekStart.AddDays(6), "Date_WeekEnd"));

    private bool CanStepForward() => IsDaily ? _day < _s.Today : _weekStart.AddDays(7) <= _s.Today;

    private void Step(int direction)
    {
        if (IsDaily) Day = _day.AddDays(direction);
        else WeekStart = _weekStart.AddDays(7 * direction);
    }

    private (DateTime From, DateTime To) Range => IsDaily ? (_day, _day.AddDays(1)) : (_weekStart, _weekStart.AddDays(7));

    // ---- Figures ----
    public string TotalSoldText => Fmt.Ks(_totals.TotalSold);
    public string CashText => Fmt.Ks(_totals.CashReceived);
    public string CashNote => Loc.F("Report_CashNote", Fmt.Money(_totals.Repayments));
    public string CreditText => Fmt.Ks(_totals.CreditGiven);

    public string CreditNote => _totals.CreditSales == 1
        ? Loc.Get("Report_CreditNoteOne")
        : Loc.F("Report_CreditNote", _totals.CreditSales);

    public string SalesCountText => Fmt.Money(_totals.SalesCount);

    public string SalesCountNote => _totals.VoidedCount switch
    {
        0 => Loc.Get("Report_VoidedNone"),
        1 => Loc.Get("Report_VoidedOne"),
        var n => Loc.F("Report_VoidedMany", n),
    };

    // ---- Lists ----
    public bool BestByQty => _bestByQty;

    public void SortBest(bool byQty)
    {
        _bestByQty = byQty;
        OnPropertyChanged(nameof(BestByQty));
        FillBest();
    }

    private void FillBest() =>
        Best.ReplaceAll(ReportService.BestSellers(_productSales, _bestByQty)
            .Select((p, i) => new BestRow { Rank = i + 1, Data = p }));

    public string DayTitle => Loc.F("Report_SalesOn", Loc.Date(_day, "Date_DayMonth"));

    public string DayCountText
    {
        get
        {
            var voided = DaySales.Count(r => r.IsVoided);
            return voided > 0
                ? Loc.F("Report_DayCountVoided", DaySales.Count, voided)
                : Loc.F("Report_DayCount", DaySales.Count);
        }
    }

    public bool NoDaySales => DaySales.Count == 0;

    public void Refresh()
    {
        var (from, to) = Range;
        _totals = _s.Reports.Totals(from, to);
        _productSales = _s.Reports.ProductSales(from, to);
        FillBest();
        Slow.ReplaceAll(ReportService.SlowSellers(_productSales).Select((p, i) => new BestRow { Rank = i + 1, Data = p }));

        if (IsDaily)
        {
            DaySales.ReplaceAll(_s.Sales.SalesBetween(from, to).Select(s => new DaySaleRow(s, VoidSale)));
        }
        else
        {
            var days = Enumerable.Range(0, 7).Select(i => from.AddDays(i))
                .Select(d => (Day: d, Total: _s.Reports.Totals(d, d.AddDays(1)).TotalSold)).ToList();
            var max = Math.Max(1, days.Max(d => d.Total));
            WeekBars.ReplaceAll(days.Select(d => new DayBar
            {
                Label = Loc.Date(d.Day, "Date_WeekdayShort"),
                Fraction = (double)d.Total / max,
                // Days still to come in the current week stay blank rather than showing 0.
                AmountText = d.Day > _s.Today ? "" : Fmt.Money(d.Total),
            }));
        }

        foreach (var p in new[]
                 {
                     nameof(DayText), nameof(WeekText), nameof(TotalSoldText), nameof(CashText), nameof(CashNote),
                     nameof(CreditText), nameof(CreditNote), nameof(SalesCountText), nameof(SalesCountNote),
                     nameof(DayTitle), nameof(DayCountText), nameof(NoDaySales), nameof(MaxDate),
                 })
            OnPropertyChanged(p);
    }

    private void VoidSale(DaySaleRow row)
    {
        var sale = row.Sale;
        var reason = Dialogs.Dialogs.AskReason(
            Loc.Get("Void_Title"),
            [
                (Loc.Get("Void_Time"), Loc.Date(sale.SoldAt, "Date_Day") + ", " + row.TimeText),
                (Loc.Get("Void_Customer"), row.CustomerText),
                (Loc.Get("Void_Items"), string.Join(", ", sale.Items.Select(i => i.Describe()))),
                (Loc.Get("Void_Total"), Fmt.Ks(sale.Total)),
            ],
            Loc.Get("Void_Paragraph"),
            Loc.Get("Void_Placeholder"),
            Loc.Get("Void_Action"));
        if (reason == null) return;
        _s.Sales.Void(sale.Id, reason);
        Refresh();
    }

    // ---- Export (RP7) ----
    public ReportDocument BuildDocument()
    {
        var doc = new ReportDocument
        {
            Title = Loc.Get(IsDaily ? "Report_DailyTitle" : "Report_WeeklyTitle"),
            Period = IsDaily ? DayText : WeekText,
        };
        doc.Figures.Add(new(Loc.Get("Report_TotalSold"), TotalSoldText, Loc.Get("Report_TotalSoldNote")));
        doc.Figures.Add(new(Loc.Get("Report_Cash"), CashText, CashNote));
        doc.Figures.Add(new(Loc.Get("Report_Credit"), CreditText, CreditNote));
        doc.Figures.Add(new(Loc.Get("Report_Count"), SalesCountText, SalesCountNote));

        var best = new ReportTable
        {
            Title = Loc.Get("Report_Best"),
            Headers = ["#", Loc.Get("Col_Product"), Loc.Get("Col_Qty"), Loc.Get("Col_Ks")],
            Numeric = [true, false, true, true],
        };
        foreach (var b in Best) best.Rows.Add([b.Rank, b.Name, b.QtyText, b.Data.Amount]);
        doc.Tables.Add(best);

        var slow = new ReportTable
        {
            Title = Loc.Get("Report_Slow"),
            Headers = [Loc.Get("Col_Product"), Loc.Get("Col_Qty"), Loc.Get("Col_Ks")],
            Numeric = [false, true, true],
            Footnote = Loc.Get("Report_SlowNote"),
        };
        foreach (var s in Slow) slow.Rows.Add([s.Name, s.QtyText, s.Data.Amount]);
        doc.Tables.Add(slow);

        if (IsDaily)
        {
            var sales = new ReportTable
            {
                Title = DayTitle,
                Headers = [Loc.Get("Col_Time"), Loc.Get("Col_Customer"), Loc.Get("Col_Items"), Loc.Get("Col_TotalKs")],
                Numeric = [false, false, false, true],
            };
            foreach (var r in DaySales)
            {
                var who = r.IsVoided ? $"{r.CustomerText} ({Loc.Get("Voided")}: {r.VoidReason})"
                    : r.HasOwes ? $"{r.CustomerText} ({r.OwesText})" : r.CustomerText;
                sales.Rows.Add([r.TimeText, who, string.Join(", ", r.Items.Select(i => i.Text)), r.Sale.Total]);
            }
            doc.Tables.Add(sales);
        }
        else
        {
            var perDay = new ReportTable
            {
                Title = Loc.Get("Report_PerDay"),
                Headers = [Loc.Get("Col_Day"), Loc.Get("Col_Ks")],
                Numeric = [false, true],
            };
            for (var i = 0; i < 7; i++)
            {
                var d = _weekStart.AddDays(i);
                perDay.Rows.Add([Loc.Date(d, "Date_Day"), _s.Reports.Totals(d, d.AddDays(1)).TotalSold]);
            }
            doc.Tables.Add(perDay);
        }
        return doc;
    }

    private bool _exporting;

    /// <summary>The file is written on a background thread so the window never freezes while a PDF is made.</summary>
    private async void Export(bool excel)
    {
        if (_exporting) return;
        var stamp = IsDaily ? _day.ToString("yyyy-MM-dd") : "week-" + _weekStart.ToString("yyyy-MM-dd");
        var dialog = new SaveFileDialog
        {
            FileName = $"StorePOS-report-{stamp}",
            DefaultExt = excel ? ".xlsx" : ".pdf",
            Filter = excel ? "Excel (*.xlsx)|*.xlsx" : "PDF (*.pdf)|*.pdf",
        };
        if (dialog.ShowDialog() != true) return;
        var doc = BuildDocument();
        var path = dialog.FileName;

        _exporting = true;
        System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
        try
        {
            await Task.Run(() =>
            {
                if (excel) ExportService.SaveExcel(doc, path);
                else ExportService.SavePdf(doc, path);
            });
        }
        catch (IOException ex)
        {
            throw new DomainException(Loc.F("Export_Failed", ex.Message));
        }
        finally
        {
            System.Windows.Input.Mouse.OverrideCursor = null;
            _exporting = false;
        }
        Dialogs.Dialogs.Info(Loc.Get("Export_DoneTitle"), Loc.F("Export_Done", path));
    }
}
