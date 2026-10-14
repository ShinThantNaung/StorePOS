using System.Windows.Data;
using StorePOS.Infrastructure;
using StorePOS.Models;
using StorePOS.Services;

namespace StorePOS.ViewModels;

public sealed record StockInLineRow(string Name, string AddedText, string BeforeText, string AfterText);

/// <summary>One delivery in the stock-in history. Opening it lists each product with its stock before and after.</summary>
public sealed class StockInRow : ObservableObject
{
    private bool _isExpanded;

    public StockInRow(StockInRecord record)
    {
        Record = record;
        ToggleCommand = new RelayCommand(() => IsExpanded = !IsExpanded);
        Lines = record.Lines
            .Select(l => new StockInLineRow(l.Name, Fmt.Stock(l.Quantity, showPlus: true), Fmt.Stock(l.Before), Fmt.Stock(l.After)))
            .ToList();
        var items = string.Join(", ", record.Lines.Select(l => $"{l.Name} × {Fmt.Qty(l.Quantity)}"));
        SummaryText = record.Lines.Count == 1
            ? Loc.F("StockIns_OneProduct", items)
            : Loc.F("StockIns_Products", record.Lines.Count, items);
        SearchKey = Fmt.SearchKey(record.Supplier + "\n" + string.Join("\n", record.Lines.Select(l => l.Name)));
    }

    public StockInRecord Record { get; }
    public RelayCommand ToggleCommand { get; }
    public List<StockInLineRow> Lines { get; }
    public string SummaryText { get; }
    public string SearchKey { get; }

    public string DateText => Loc.Date(Record.ReceivedAt, "Date_Short");
    public string TimeText => Loc.Date(Record.ReceivedAt, "Time_Short");
    public bool HasSupplier => Record.Supplier.Length > 0;
    public bool IsNotGiven => !HasSupplier;
    public string FromText => HasSupplier ? Record.Supplier : Loc.Get("StockIns_NotGiven");
    public string Note => Record.Note;

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (!Set(ref _isExpanded, value)) return;
            OnPropertyChanged(nameof(ToggleLabel));
        }
    }

    public string ToggleLabel => Loc.F(IsExpanded ? "StockIns_Hide" : "StockIns_Show", DateText);
}

/// <summary>ST4: every delivery, newest first. Search matches the supplier or any product in it.</summary>
public sealed class StockInsViewModel : ObservableObject, IPage
{
    private readonly AppServices _s;
    private readonly BulkObservableCollection<StockInRow> _rows = [];
    private string _searchText = "";
    private string _key = "";

    public StockInsViewModel(AppServices services, MainViewModel main)
    {
        _s = services;
        View = new ListCollectionView(_rows) { Filter = o => _key.Length == 0 || ((StockInRow)o).SearchKey.Contains(_key, StringComparison.Ordinal) };
        ShowCurrentCommand = new RelayCommand(() => main.Navigate(Section.Stock));
        RecordInCommand = new RelayCommand(RecordIn);
    }

    public ListCollectionView View { get; }
    public RelayCommand ShowCurrentCommand { get; }
    public RelayCommand RecordInCommand { get; }

    public void Activate() => Reload(null);

    /// <summary>Keeps open the deliveries that were open, and opens a newly recorded one.</summary>
    private void Reload(long? openId)
    {
        var open = _rows.Where(r => r.IsExpanded).Select(r => r.Record.Id).ToHashSet();
        if (openId != null) open.Add(openId.Value);
        _rows.ReplaceAll(_s.Stock.StockIns().Select(r => new StockInRow(r) { IsExpanded = open.Contains(r.Id) }));
        Counts();
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!Set(ref _searchText, value)) return;
            _key = Fmt.SearchKey(value);
            View.Refresh();
            Counts();
        }
    }

    public string CountText => View.Count == 1 ? Loc.Get("StockIns_CountOne") : Loc.F("StockIns_Count", View.Count);
    public bool IsEmpty => View.Count == 0;
    public string EmptyText => Loc.Get(_rows.Count == 0 ? "StockIns_None" : "StockIns_Empty");

    private void Counts()
    {
        OnPropertyChanged(nameof(CountText));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(EmptyText));
    }

    private void RecordIn()
    {
        if (Dialogs.Dialogs.RecordStockIn() is { } id) Reload(id);
    }
}
