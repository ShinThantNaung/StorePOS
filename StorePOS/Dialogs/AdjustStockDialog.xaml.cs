using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using StorePOS.Infrastructure;
using StorePOS.Models;
using StorePOS.ViewModels;

namespace StorePOS.Dialogs;

/// <summary>
/// ST6/ST10: the shelf count replaces the app's number by recording the difference.
/// Also used once per product to enter the opening stock.
/// </summary>
public partial class AdjustStockDialog : Window, INotifyPropertyChanged
{
    private readonly Dictionary<long, decimal> _onHand;
    private ProductRow? _product;
    private string _countText = "";
    private AdjustReason _reason = AdjustReason.CountWrong;
    private string _noteText = "";
    private string _problem = "";

    public AdjustStockDialog(long? productId)
    {
        var levels = App.Services.Stock.Levels().Where(l => l.Product.Active || l.OnHand != 0).ToList();
        _onHand = levels.ToDictionary(l => l.Product.Id, l => l.OnHand);
        Products = levels.Select(l => new ProductRow(l.Product)).ToList();
        Reasons = AdjustReasons.All.Select(r => new { Label = AdjustReasons.Display(r), Value = r }).ToList<object>();
        _product = Products.FirstOrDefault(p => p.Id == productId);
        InitializeComponent();
        DataContext = this;
        Loaded += (_, _) =>
        {
            if (_product == null) Picker.FocusBox();
            else CountBox.Focus();
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));

    public List<ProductRow> Products { get; }
    public List<object> Reasons { get; }
    public long SavedProductId { get; private set; }

    public ProductRow? Product
    {
        get => _product;
        set
        {
            _product = value;
            Changed();
            Changed(nameof(AppSaysText));
            CountChanged();
        }
    }

    public string CountText
    {
        get => _countText;
        set
        {
            _countText = value;
            Changed();
            CountChanged();
        }
    }

    public AdjustReason Reason
    {
        get => _reason;
        set { _reason = value; Changed(); }
    }

    public string NoteText
    {
        get => _noteText;
        set { _noteText = value ?? ""; Changed(); }
    }

    public string Problem
    {
        get => _problem;
        private set { _problem = value; Changed(); }
    }

    private decimal OnHand => _product == null ? 0 : _onHand.GetValueOrDefault(_product.Id);
    private string Unit => _product?.Product.Unit ?? "";

    private decimal? Counted =>
        _product != null && Fmt.TryParseQty(_countText, _product.SoldByWeight, out var c, allowZero: true) ? c : null;

    public string AppSaysText => _product == null ? "—" : Units.Count(OnHand, Unit);

    /// <summary>"bottles" after the box; "bottle" once the count is 1.</summary>
    public string UnitWord => _product == null ? "" : Units.Word(Unit, Counted ?? 2);

    public string DifferenceText
    {
        get
        {
            if (_product == null) return "";
            if (Counted is { } c) return c == OnHand ? Loc.Get("Adjust_NoChange") : Units.Count(c - OnHand, Unit, showPlus: true);
            // A decimal typed for a product counted in whole numbers.
            if (!_product.SoldByWeight && Fmt.TryParseQty(_countText, true, out _, allowZero: true)) return Loc.Get("Sales_HintWhole");
            return Loc.Get("Adjust_TypeCount");
        }
    }

    public bool CanSave => _product != null && Counted is { } c && c != OnHand;

    private void CountChanged()
    {
        Changed(nameof(UnitWord));
        Changed(nameof(DifferenceText));
        Changed(nameof(CanSave));
        Problem = "";
    }

    private void Picker_Picked(object? sender, EventArgs e) => CountBox.Focus();

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!CanSave) return;
        try
        {
            App.Services.Stock.Adjust(_product!.Id, Counted!.Value, _reason, _noteText);
            SavedProductId = _product.Id;
            DialogResult = true;
        }
        catch (DomainException ex)
        {
            Problem = ex.Message;
        }
    }
}
