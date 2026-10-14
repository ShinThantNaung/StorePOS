using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using StorePOS.Infrastructure;
using StorePOS.Models;
using StorePOS.Services;

namespace StorePOS.ViewModels;

/// <summary>One line in the cart. Quantity and unit price are typed in the row.</summary>
public sealed class CartLine : ObservableObject
{
    private readonly Action _changed;
    private string _qtyText;
    private string _priceText;

    public CartLine(Product product, decimal qty, Action changed)
    {
        Product = product;
        _changed = changed;
        _qtyText = Fmt.Qty(qty);
        _priceText = Fmt.Money(product.Price);
        ListPrice = product.Price;
    }

    public Product Product { get; }
    public string Name => Product.FullName;
    public string Unit => Units.Display(Product.Unit);
    public long ListPrice { get; }

    public string QtyText
    {
        get => _qtyText;
        set { if (Set(ref _qtyText, value)) Changed(); }
    }

    public string PriceText
    {
        get => _priceText;
        set { if (Set(ref _priceText, value)) Changed(); }
    }

    public decimal? Qty => Fmt.TryParseQty(_qtyText, Product.SoldByWeight, out var q) ? q : null;
    public long? Price => Fmt.TryParseMoney(_priceText, out var p) ? p : null;
    public bool IsValid => Qty != null && Price != null;

    /// <summary>TX2: unit price × quantity, rounded to whole kyat.</summary>
    public long Amount => IsValid ? Fmt.LineTotal(Price!.Value, Qty!.Value) : 0;

    public string AmountText => IsValid ? Fmt.Money(Amount) : "—";

    /// <summary>TX3: a changed price is for this sale only; the list price stays.</summary>
    public bool IsPriceChanged => Price != null && Price != ListPrice;

    public string ChangedNote => Loc.F("Cart_Changed", Fmt.Money(ListPrice));
    public string QtyLabel => Loc.F("Cart_QtyFor", Name);
    public string PriceLabel => Loc.F("Cart_PriceFor", Name);

    public void AddQuantity(decimal more)
    {
        if (Qty is { } q) QtyText = Fmt.Qty(q + more);
    }

    public SaleLineInput ToInput() =>
        new(Product.Id, Product.FullName, Product.Unit, Product.SoldByWeight, Qty!.Value, Price!.Value, ListPrice);

    private void Changed()
    {
        OnPropertyChanged(nameof(AmountText));
        OnPropertyChanged(nameof(IsPriceChanged));
        _changed();
    }
}

public sealed class SalesViewModel : ObservableObject, IPage
{
    private readonly AppServices _s;
    private readonly BulkObservableCollection<ProductRow> _products = [];
    private string _searchText = "";
    private string _companyText = "";
    private string _nameKey = "";
    private string _companyKey = "";
    private List<SizeOption> _sizes = [];
    private SizeOption? _size;
    private ProductRow? _selectedProduct;
    private string _quantityText = "1";
    private string _paidText = "";
    private CustomerOption? _selectedCustomer;
    private string _message = "";

    public SalesViewModel(AppServices services, MainViewModel main)
    {
        _s = services;
        ProductsView = new ListCollectionView(_products) { Filter = MatchesSearch };
        Cart.CollectionChanged += (_, _) => Recalc();
        AddToCartCommand = new RelayCommand(AddSelected, () => SelectedProduct != null && QuantityValue != null);
        RemoveLineCommand = new RelayCommand(p => { if (p is CartLine line) Cart.Remove(line); });
        ClearCartCommand = new RelayCommand(ClearCart, () => Cart.Count > 0);
        SaveCommand = new RelayCommand(Save, () => CanSave);
        NewCustomerCommand = new RelayCommand(NewCustomer);
    }

    public ListCollectionView ProductsView { get; }
    public ObservableCollection<CartLine> Cart { get; } = [];
    public ObservableCollection<CustomerOption> Customers { get; } = [];

    public RelayCommand AddToCartCommand { get; }
    public RelayCommand RemoveLineCommand { get; }
    public RelayCommand ClearCartCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand NewCustomerCommand { get; }

    public void Activate()
    {
        var selectedId = SelectedProduct?.Id;
        _products.ReplaceAll(_s.Products.List(includeInactive: false).Select(p => new ProductRow(p)));
        Sizes = SizeOption.From(_products);
        SelectedProduct = _products.FirstOrDefault(p => p.Id == selectedId);
        ReloadCustomers(SelectedCustomer?.Id);
    }

    private void ReloadCustomers(long? selectId)
    {
        Customers.Clear();
        Customers.Add(new CustomerOption(null));
        foreach (var c in _s.Debts.Customers()) Customers.Add(new CustomerOption(c));
        SelectedCustomer = Customers.FirstOrDefault(c => c.Id == selectId) ?? Customers[0];
    }

    // ---- Products panel ----

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!Set(ref _searchText, value)) return;
            _nameKey = Fmt.SearchKey(value);
            ProductsView.Refresh();
        }
    }

    public string CompanyText
    {
        get => _companyText;
        set
        {
            if (!Set(ref _companyText, value)) return;
            _companyKey = Fmt.SearchKey(value);
            ProductsView.Refresh();
        }
    }

    /// <summary>"All" first, then each size on sale. Rebuilt every time the screen opens.</summary>
    public List<SizeOption> Sizes
    {
        get => _sizes;
        private set
        {
            var key = _size?.Key ?? "";
            Set(ref _sizes, value);
            SelectedSize = value.FirstOrDefault(o => o.Key == key) ?? value[0];
        }
    }

    public SizeOption? SelectedSize
    {
        get => _size;
        set
        {
            if (value == null || !Set(ref _size, value)) return;
            ProductsView.Refresh();
        }
    }

    private bool MatchesSearch(object o) => ((ProductRow)o).Matches(_nameKey, _companyKey, _size?.Key ?? "");

    public ProductRow? SelectedProduct
    {
        get => _selectedProduct;
        set
        {
            if (!Set(ref _selectedProduct, value)) return;
            QuantityText = "1";
            OnPropertyChanged(nameof(SelectedName));
            OnPropertyChanged(nameof(QuantityHint));
        }
    }

    public string SelectedName => SelectedProduct?.FullName ?? Loc.Get("Sales_NothingSelected");

    public string QuantityText
    {
        get => _quantityText;
        set => Set(ref _quantityText, value);
    }

    private decimal? QuantityValue =>
        SelectedProduct != null && Fmt.TryParseQty(QuantityText, SelectedProduct.SoldByWeight, out var q) ? q : null;

    public string QuantityHint => Loc.Get(SelectedProduct?.SoldByWeight == true ? "Sales_HintDecimals" : "Sales_HintWhole");

    private void AddSelected()
    {
        if (SelectedProduct == null || QuantityValue is not { } qty) return;
        Add(SelectedProduct.Product, qty);
        QuantityText = "1";
    }

    /// <summary>Double-click on a product row adds it with quantity 1.</summary>
    public void AddOne(ProductRow row) => Add(row.Product, 1);

    private void Add(Product product, decimal qty)
    {
        var existing = Cart.FirstOrDefault(l => l.Product.Id == product.Id && !l.IsPriceChanged && l.IsValid);
        if (existing != null) existing.AddQuantity(qty);
        else Cart.Add(new CartLine(product, qty, Recalc));
        Message = "";
    }

    // ---- Cart and payment ----

    public string ItemCountText => Cart.Count == 1 ? Loc.Get("Cart_OneItem") : Loc.F("Cart_Items", Cart.Count);

    public long Total => Cart.Sum(l => l.Amount);
    public string TotalText => Fmt.Ks(Total);

    public string PaidText
    {
        get => _paidText;
        set { if (Set(ref _paidText, value)) Recalc(); }
    }

    private long? Paid => Fmt.TryParseMoney(PaidText, out var p) ? p : null;
    private long Owed => Paid is { } p ? Math.Max(0, Total - p) : Total;

    public bool ShowResult => Paid != null && Cart.Count > 0;
    public string ResultLabel => Paid >= Total ? Loc.Get("Sales_Change") : Loc.Get("Sales_Owed");
    public string ResultText => Paid is { } p ? Fmt.Ks(Math.Abs(Total - p)) : "";

    public CustomerOption? SelectedCustomer
    {
        get => _selectedCustomer;
        set { if (Set(ref _selectedCustomer, value)) Recalc(); }
    }

    /// <summary>TX5: a customer is required only when part of the sale is unpaid.</summary>
    public bool CustomerRequired => Cart.Count > 0 && Paid != null && Owed > 0;

    public string CustomerHint => Loc.Get(CustomerRequired ? "Sales_CustomerRequired" : "Sales_CustomerOptional");

    public string Problem
    {
        get
        {
            if (Cart.Count == 0) return "";
            var bad = Cart.FirstOrDefault(l => !l.IsValid);
            if (bad != null)
            {
                if (bad.Qty == null)
                    return Loc.F(bad.Product.SoldByWeight ? "Sales_FixQtyDecimal" : "Sales_FixQtyWhole", bad.Name);
                return Loc.F("Sales_FixPrice", bad.Name);
            }
            if (string.IsNullOrWhiteSpace(PaidText)) return Loc.Get("Sales_TypePaid");
            if (Paid == null) return Loc.Get("Sales_PaidNotNumber");
            if (Owed > 0 && SelectedCustomer?.Id == null) return Loc.Get("Sales_ChooseCustomer");
            return "";
        }
    }

    public bool CanSave => Cart.Count > 0 && Problem.Length == 0;

    /// <summary>Shown next to Save sale: what is blocking, or that the last sale was saved.</summary>
    public string Message
    {
        get => _message;
        private set { if (Set(ref _message, value)) OnPropertyChanged(nameof(FooterText)); }
    }

    public string FooterText => Problem.Length > 0 ? Problem : Message;

    private void Recalc()
    {
        foreach (var name in new[]
                 {
                     nameof(ItemCountText), nameof(Total), nameof(TotalText), nameof(ShowResult), nameof(ResultLabel),
                     nameof(ResultText), nameof(CustomerRequired), nameof(CustomerHint), nameof(Problem),
                     nameof(CanSave), nameof(FooterText),
                 })
            OnPropertyChanged(name);
        if (Cart.Count > 0 && Message.Length > 0) Message = "";
    }

    private void Save()
    {
        if (!CanSave) return;
        var total = Total;
        var id = _s.Sales.Save(Cart.Select(l => l.ToInput()).ToList(), Paid!.Value, SelectedCustomer?.Id);
        Cart.Clear();
        _paidText = "";
        OnPropertyChanged(nameof(PaidText));
        SelectedCustomer = Customers.FirstOrDefault();
        Message = Loc.F("Sales_Saved", Fmt.SaleNo(id), Fmt.Ks(total));
        Recalc();
    }

    private void ClearCart()
    {
        var n = Cart.Count;
        if (n == 0) return;
        var ok = Dialogs.Dialogs.Confirm(
            Loc.Get("Confirm_ClearTitle"),
            n == 1 ? Loc.Get("Confirm_ClearOne") : Loc.F("Confirm_ClearMany", n),
            Loc.Get("Confirm_KeepItems"),
            Loc.Get("Sales_ClearCart"));
        if (!ok) return;
        Cart.Clear();
        PaidText = "";
        SelectedCustomer = Customers.FirstOrDefault();
    }

    public void ClearAfterRestore()
    {
        Cart.Clear();
        PaidText = "";
        Message = "";
    }

    private void NewCustomer()
    {
        var id = Dialogs.Dialogs.EditCustomer(null);
        if (id != null) ReloadCustomers(id);
    }
}
