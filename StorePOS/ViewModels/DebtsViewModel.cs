using System.ComponentModel;
using System.Windows.Data;
using StorePOS.Infrastructure;
using StorePOS.Services;

namespace StorePOS.ViewModels;

public sealed class DebtRow
{
    public long Id { get; init; }
    public string Name { get; init; } = "";
    public string Address { get; init; } = "";
    public long Balance { get; init; }
    public string BalanceText => Fmt.Money(Balance);
    public string SearchKey { get; init; } = "";
    public string OpenLabel => Loc.F("Debts_OpenFor", Name);
}

public sealed class DebtsViewModel : ObservableObject, IPage
{
    private readonly AppServices _s;
    private readonly MainViewModel _main;
    private readonly BulkObservableCollection<DebtRow> _rows = [];
    private string _searchText = "";
    private string _mode = "Owes";
    private long _totalOwed;
    private int _owingCount;

    public DebtsViewModel(AppServices services, MainViewModel main)
    {
        _s = services;
        _main = main;
        View = new ListCollectionView(_rows) { Filter = Matches };
        View.SortDescriptions.Add(new SortDescription(nameof(DebtRow.Balance), ListSortDirection.Descending));
        View.SortDescriptions.Add(new SortDescription(nameof(DebtRow.Name), ListSortDirection.Ascending));
        OpenCommand = new RelayCommand(p => { if (p is DebtRow r) _main.OpenCustomer(r.Id); });
    }

    public ListCollectionView View { get; }
    public RelayCommand OpenCommand { get; }

    public void Activate()
    {
        var balances = _s.Debts.Balances();
        _rows.ReplaceAll(balances.Select(b => new DebtRow
        {
            Id = b.Customer.Id,
            Name = b.Customer.Name,
            Address = b.Customer.Address,
            Balance = b.Balance,
            SearchKey = Fmt.SearchKey(b.Customer.Name + " " + b.Customer.Address),
        }));
        _totalOwed = balances.Sum(b => b.Balance);
        _owingCount = balances.Count(b => b.Balance > 0);
        OnPropertyChanged(nameof(TotalOwedText));
        OnPropertyChanged(nameof(OwingCountText));
        Counts();
    }

    public string TotalOwedText => Fmt.Ks(_totalOwed);
    public string OwingCountText => Fmt.Money(_owingCount);

    public string SearchText
    {
        get => _searchText;
        set { if (Set(ref _searchText, value)) Refilter(); }
    }

    /// <summary>"Owes" or "Paid"</summary>
    public string Mode
    {
        get => _mode;
        set { if (Set(ref _mode, value)) Refilter(); }
    }

    public string CountText => View.Count == 1 ? Loc.Get("Debts_OneCustomer") : Loc.F("Debts_Customers", View.Count);
    public bool IsEmpty => View.Count == 0;

    private bool Matches(object o)
    {
        var row = (DebtRow)o;
        if ((_mode == "Owes") != (row.Balance > 0)) return false;
        var key = Fmt.SearchKey(_searchText);
        return key.Length == 0 || row.SearchKey.Contains(key, StringComparison.Ordinal);
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
}
