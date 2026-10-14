using System.Windows.Data;
using StorePOS.Infrastructure;
using StorePOS.Models;
using StorePOS.Services;

namespace StorePOS.ViewModels;

/// <summary>A product in the stock table.</summary>
public sealed class StockRow(StockLevel level)
{
    public StockLevel Level { get; } = level;
    public Product Product => Level.Product;
    public long Id => Product.Id;
    public string NameAndSize => Product.FormatFullName(Product.Name, Product.Size, "");
    public string Company => Product.Company;
    public string Unit => Units.Display(Product.Unit);
    public string OnHandText => Fmt.Stock(Level.OnHand);
    public bool IsOut => Level.IsOut;
    public bool Inactive => !Product.Active;
    public string LastInText => Level.LastStockIn is { } d ? Loc.Date(d, "Date_Short") : "";
    public Category Category => Product.Category;
    public string SearchKey { get; } = Fmt.SearchKey(level.Product.Name);
    public string CompanyKey { get; } = Fmt.SearchKey(level.Product.Company);
}

/// <summary>One line of the selected product's stock movements.</summary>
public sealed class MovementRow(StockMovement m)
{
    public string DateText => Loc.Date(m.At, "Date_DayMonth") + ", " + Loc.Date(m.At, "Time_Short");
    public bool IsStockIn => m.Kind == StockMoveKind.StockIn;
    public string ChangeText => Fmt.Stock(m.Change, showPlus: true);
    public string AfterText => Fmt.Stock(m.After);

    public string WhatText => m.Kind switch
    {
        StockMoveKind.Sale => m.CustomerName == null
            ? Loc.F("Move_Sale", Fmt.SaleNo(m.SaleId ?? 0))
            : Loc.F("Move_SaleTo", Fmt.SaleNo(m.SaleId ?? 0), m.CustomerName),
        StockMoveKind.Void => Loc.F("Move_Void", Fmt.SaleNo(m.SaleId ?? 0)),
        StockMoveKind.StockIn => m.Supplier.Length == 0 ? Loc.Get("Move_StockIn") : Loc.F("Move_StockInFrom", m.Supplier),
        _ => m.Note.Length == 0
            ? Loc.F("Move_Adjust", AdjustReasons.Display(m.Reason ?? AdjustReason.Other))
            : Loc.F("Move_AdjustNote", AdjustReasons.Display(m.Reason ?? AdjustReason.Other), m.Note),
    };
}

public sealed record FilterOption(string Value, string Label);

/// <summary>ST2/ST7: current stock of every product, and the selected product's movements.</summary>
public sealed class StockViewModel : ObservableObject, IPage
{
    private readonly AppServices _s;
    private readonly MainViewModel _main;
    private readonly BulkObservableCollection<StockRow> _rows = [];
    private string _searchText = "";
    private string _nameKey = "";
    private string _category = "All";
    private string _show = "All";
    private StockRow? _selected;
    private List<MovementRow> _movements = [];
    private string _nowText = "";
    private string _lastInDate = "";
    private string _lastInDetail = "";

    public StockViewModel(AppServices services, MainViewModel main)
    {
        _s = services;
        _main = main;
        View = new ListCollectionView(_rows) { Filter = Matches };
        CategoryOptions =
        [
            new("All", Loc.Get("Cat_All")),
            .. Categories.All.Select(c => new FilterOption(c.ToString(), Categories.Display(c))),
        ];
        ShowHistoryCommand = new RelayCommand(main.OpenStockIns);
        AdjustCommand = new RelayCommand(Adjust);
        RecordInCommand = new RelayCommand(RecordIn);
    }

    public ListCollectionView View { get; }
    public List<FilterOption> CategoryOptions { get; }
    public RelayCommand ShowHistoryCommand { get; }
    public RelayCommand AdjustCommand { get; }
    public RelayCommand RecordInCommand { get; }

    public void Activate() => Reload(Selected?.Id);

    /// <summary>Products not on sale are listed only while some of them are still in the shop.</summary>
    private void Reload(long? selectId)
    {
        _rows.ReplaceAll(_s.Stock.Levels()
            .Where(l => l.Product.Active || l.OnHand != 0)
            .OrderBy(l => l.Product.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(l => l.Product.Size, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(l => l.Product.Company, StringComparer.CurrentCultureIgnoreCase)
            .Select(l => new StockRow(l)));
        SelectRow(_rows.FirstOrDefault(r => r.Id == selectId));
        Counts();
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!Set(ref _searchText, value)) return;
            _nameKey = Fmt.SearchKey(value);
            Refilter();
        }
    }

    /// <summary>"All" or a Category name; bound to the Category dropdown.</summary>
    public string Category
    {
        get => _category;
        set { if (value != null && Set(ref _category, value)) Refilter(); }
    }

    /// <summary>"All" or "Out"; bound to the Show toggle buttons.</summary>
    public string Show
    {
        get => _show;
        set { if (Set(ref _show, value)) Refilter(); }
    }

    public StockRow? Selected
    {
        get => _selected;
        set { if (value != _selected) SelectRow(value); }
    }

    private void SelectRow(StockRow? row)
    {
        _selected = row;
        OnPropertyChanged(nameof(Selected));
        OnPropertyChanged(nameof(HasSelection));
        LoadMovements();
    }

    public bool HasSelection => _selected != null;
    public List<MovementRow> Movements { get => _movements; private set => Set(ref _movements, value); }
    public bool NoMovements => HasSelection && _movements.Count == 0;
    public string NowText { get => _nowText; private set => Set(ref _nowText, value); }
    public string LastInDate { get => _lastInDate; private set => Set(ref _lastInDate, value); }
    public string LastInDetail { get => _lastInDetail; private set => Set(ref _lastInDetail, value); }

    public string CountText => Loc.F("Stock_Showing", View.Count, _rows.Count, _rows.Count(r => r.IsOut));
    public bool IsEmpty => View.Count == 0;

    private bool Matches(object o)
    {
        var row = (StockRow)o;
        if (_category != "All" && row.Category.ToString() != _category) return false;
        if (_show == "Out" && !row.IsOut) return false;
        return _nameKey.Length == 0 ||
               row.SearchKey.Contains(_nameKey, StringComparison.Ordinal) ||
               row.CompanyKey.Contains(_nameKey, StringComparison.Ordinal);
    }

    private void Refilter()
    {
        View.Refresh();
        Counts();
    }

    private void Counts()
    {
        OnPropertyChanged(nameof(CountText));
        OnPropertyChanged(nameof(IsEmpty));
    }

    private void LoadMovements()
    {
        if (_selected == null)
        {
            Movements = [];
            NowText = LastInDate = LastInDetail = "";
        }
        else
        {
            var moves = _s.Stock.Movements(_selected.Id);
            Movements = moves.Select(m => new MovementRow(m)).ToList();
            NowText = Units.Count(_selected.Level.OnHand, _selected.Product.Unit);
            var lastIn = moves.FirstOrDefault(m => m.Kind == StockMoveKind.StockIn);
            if (lastIn == null)
            {
                LastInDate = Loc.Get("Stock_LastInNone");
                LastInDetail = "";
            }
            else
            {
                LastInDate = Loc.Date(lastIn.At, "Date_Short") + ", " + Loc.Date(lastIn.At, "Time_Short");
                var added = Fmt.Stock(lastIn.Change, showPlus: true);
                LastInDetail = lastIn.Supplier.Length == 0 ? added : Loc.F("Stock_LastInFrom", added, lastIn.Supplier);
            }
        }
        OnPropertyChanged(nameof(NoMovements));
    }

    private void Adjust()
    {
        var id = Dialogs.Dialogs.AdjustStock(Selected?.Id);
        if (id != null) Reload(id);
    }

    private void RecordIn()
    {
        if (Dialogs.Dialogs.RecordStockIn() != null) Reload(Selected?.Id);
    }
}
