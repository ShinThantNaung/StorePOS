using System.ComponentModel;
using System.Windows.Data;
using StorePOS.Infrastructure;
using StorePOS.Services;

namespace StorePOS.ViewModels;

public sealed class PricesViewModel : ObservableObject, IPage
{
    private readonly AppServices _s;
    private readonly BulkObservableCollection<ProductRow> _rows = [];
    private string _searchText = "";
    private string _companyText = "";
    private string _nameKey = "";
    private string _companyKey = "";
    private List<SizeOption> _sizes = [];
    private SizeOption? _size;
    private string _category = "All";
    private ProductRow? _selected;

    public PricesViewModel(AppServices services)
    {
        _s = services;
        View = new ListCollectionView(_rows) { Filter = Matches };
        View.SortDescriptions.Add(new SortDescription(nameof(ProductRow.Name), ListSortDirection.Ascending));
        AddCommand = new RelayCommand(Add);
        EditCommand = new RelayCommand(Edit, () => Selected != null);
        HistoryCommand = new RelayCommand(() => Dialogs.Dialogs.PriceHistory(Selected!.Product), () => Selected != null);
        ImportCommand = new RelayCommand(Import);
    }

    public ListCollectionView View { get; }
    public RelayCommand AddCommand { get; }
    public RelayCommand EditCommand { get; }
    public RelayCommand HistoryCommand { get; }
    public RelayCommand ImportCommand { get; }

    public void Activate() => Reload(Selected?.Id);

    private void Reload(long? selectId)
    {
        _rows.ReplaceAll(_s.Products.List(includeInactive: true).Select(p => new ProductRow(p)));
        Sizes = SizeOption.From(_rows);
        Selected = _rows.FirstOrDefault(r => r.Id == selectId);
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

    public string CompanyText
    {
        get => _companyText;
        set
        {
            if (!Set(ref _companyText, value)) return;
            _companyKey = Fmt.SearchKey(value);
            Refilter();
        }
    }

    /// <summary>"All" first, then each size in the price list. Rebuilt on every reload.</summary>
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
        set { if (value != null && Set(ref _size, value)) Refilter(); }
    }

    /// <summary>"All" or a Category name; bound to the toggle buttons.</summary>
    public string Category
    {
        get => _category;
        set { if (Set(ref _category, value)) Refilter(); }
    }

    public ProductRow? Selected
    {
        get => _selected;
        set => Set(ref _selected, value);
    }

    public string CountText => Loc.F("Prices_Showing", View.Count, _rows.Count);
    public bool IsEmpty => View.Count == 0;

    /// <summary>PR2/PR3: any part of the name or company, ignoring capitals, combined with the size and category.</summary>
    private bool Matches(object o)
    {
        var row = (ProductRow)o;
        if (_category != "All" && row.Category.ToString() != _category) return false;
        return row.Matches(_nameKey, _companyKey, _size?.Key ?? "");
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

    private void Add()
    {
        var id = Dialogs.Dialogs.EditProduct(null);
        if (id != null) Reload(id);
    }

    public void Edit()
    {
        if (Selected == null) return;
        var id = Dialogs.Dialogs.EditProduct(Selected.Product);
        if (id != null) Reload(id);
    }

    private void Import()
    {
        if (Dialogs.Dialogs.ImportCsv()) Reload(Selected?.Id);
    }
}
