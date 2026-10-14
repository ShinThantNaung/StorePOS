using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using StorePOS.Infrastructure;
using StorePOS.Models;
using StorePOS.ViewModels;

namespace StorePOS.Dialogs;

/// <summary>One product in the delivery being recorded. The quantity can be changed in the row.</summary>
public sealed class StockInEntry : ObservableObject
{
    private readonly Action _changed;
    private string _addingText;

    public StockInEntry(ProductRow product, decimal onHand, decimal qty, Action changed)
    {
        Product = product;
        OnHand = onHand;
        _changed = changed;
        _addingText = Fmt.Qty(qty);
    }

    public ProductRow Product { get; }
    public decimal OnHand { get; }
    public string Name => Product.FullName;
    public string Unit => Product.Unit;
    public string NowText => Fmt.Stock(OnHand);

    public string AddingText
    {
        get => _addingText;
        set
        {
            if (!Set(ref _addingText, value)) return;
            OnPropertyChanged(nameof(AfterText));
            _changed();
        }
    }

    public decimal? Qty => Fmt.TryParseQty(_addingText, Product.SoldByWeight, out var q) ? q : null;
    public string AfterText => Qty is { } q ? Fmt.Stock(OnHand + q) : "—";
    public string AddingLabel => Loc.F("StockIn_AddingFor", Name);
    public string RemoveLabel => Loc.F("StockIn_RemoveFor", Name);

    public void AddQuantity(decimal more) => AddingText = Fmt.Qty((Qty ?? 0) + more);
}

/// <summary>ST3: one delivery with a date, an optional supplier and note, and one or more products.</summary>
public partial class RecordStockInDialog : Window, INotifyPropertyChanged
{
    private readonly Dictionary<long, decimal> _onHand;
    private DateTime _date;
    private ProductRow? _picked;
    private string _qtyText = "";
    private string _supplierText = "";
    private string _noteText = "";
    private string _addProblem = "";

    public RecordStockInDialog()
    {
        var s = App.Services;
        _date = s.Today;
        var levels = s.Stock.Levels().Where(l => l.Product.Active).ToList();
        _onHand = levels.ToDictionary(l => l.Product.Id, l => l.OnHand);
        Products = levels.Select(l => new ProductRow(l.Product)).ToList();
        Suppliers = [.. s.Stock.Suppliers(), Loc.Get("StockIns_NotGiven")];
        AddCommand = new RelayCommand(Add);
        RemoveCommand = new RelayCommand(p =>
        {
            if (p is StockInEntry e) Lines.Remove(e);
            LinesChanged();
        });
        InitializeComponent();
        DataContext = this;
        Loaded += (_, _) => Picker.FocusBox();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));

    public List<ProductRow> Products { get; }
    public List<string> Suppliers { get; }
    public ObservableCollection<StockInEntry> Lines { get; } = [];
    public RelayCommand AddCommand { get; }
    public RelayCommand RemoveCommand { get; }
    public long SavedId { get; private set; }
    public DateTime Today => App.Services.Today;

    public DateTime Date
    {
        get => _date;
        set
        {
            _date = value.Date > Today ? Today : value.Date;
            Changed();
            Changed(nameof(DateText));
        }
    }

    public string DateText => Loc.Date(_date, "Date_Day");

    public ProductRow? PickedProduct
    {
        get => _picked;
        set
        {
            _picked = value;
            Changed();
            SetAddProblem("");
        }
    }

    public string QtyText
    {
        get => _qtyText;
        set
        {
            _qtyText = value;
            Changed();
            SetAddProblem("");
        }
    }

    public string SupplierText
    {
        get => _supplierText;
        set { _supplierText = value ?? ""; Changed(); }
    }

    public string NoteText
    {
        get => _noteText;
        set { _noteText = value ?? ""; Changed(); }
    }

    public string FooterText => Lines.Count == 1 ? Loc.Get("StockIn_FooterOne") : Loc.F("StockIn_Footer", Lines.Count);

    public bool CanSave => Lines.Count > 0 && Lines.All(l => l.Qty != null);

    /// <summary>A problem with the product being added, else the first row whose quantity is wrong.</summary>
    public string Problem
    {
        get
        {
            if (_addProblem.Length > 0) return _addProblem;
            var bad = Lines.FirstOrDefault(l => l.Qty == null);
            return bad == null ? "" : QtyProblem(bad.Product);
        }
    }

    private static string QtyProblem(ProductRow p) =>
        Loc.F(p.SoldByWeight ? "Sales_FixQtyDecimal" : "Sales_FixQtyWhole", p.FullName);

    private void SetAddProblem(string text)
    {
        _addProblem = text;
        Changed(nameof(Problem));
    }

    private void LinesChanged()
    {
        Changed(nameof(FooterText));
        Changed(nameof(CanSave));
        Changed(nameof(Problem));
    }

    private void Picker_Picked(object? sender, EventArgs e) => QtyBox.Focus();

    /// <summary>Adding a product that is already in the list adds to its quantity.</summary>
    private void Add()
    {
        if (_picked is not { } p)
        {
            SetAddProblem(Loc.Get("StockIn_PickFirst"));
            Picker.FocusBox();
            return;
        }
        if (!Fmt.TryParseQty(_qtyText, p.SoldByWeight, out var qty))
        {
            SetAddProblem(QtyProblem(p));
            QtyBox.Focus();
            return;
        }
        var existing = Lines.FirstOrDefault(l => l.Product.Id == p.Id);
        if (existing != null) existing.AddQuantity(qty);
        else Lines.Add(new StockInEntry(p, _onHand.GetValueOrDefault(p.Id), qty, LinesChanged));
        PickedProduct = null;
        Picker.Clear();
        QtyText = "";
        SetAddProblem("");
        LinesChanged();
        Picker.FocusBox();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!CanSave) return;
        var supplier = _supplierText.Trim();
        if (supplier == Loc.Get("StockIns_NotGiven")) supplier = "";
        try
        {
            SavedId = App.Services.Stock.RecordStockIn(_date, supplier, _noteText,
                Lines.Select(l => new StockInLineInput(l.Product.Id, l.Qty!.Value)).ToList());
            DialogResult = true;
        }
        catch (DomainException ex)
        {
            SetAddProblem(ex.Message);
        }
    }
}
