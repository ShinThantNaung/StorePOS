using StorePOS.Infrastructure;
using StorePOS.Models;
using StorePOS.Services;

namespace StorePOS.ViewModels;

public sealed class LedgerRow(LedgerEntry e)
{
    public LedgerEntry Entry { get; } = e;
    public string DateText => Loc.Date(Entry.Date, "Date_Short");
    public bool IsSale => Entry.Kind == LedgerKind.CreditSale;

    public string Title => IsSale
        ? Loc.F("Ledger_CreditSale", Fmt.SaleNo(Entry.Id), Fmt.Money(Entry.SaleTotal), Fmt.Money(Entry.SalePaid))
        : Entry.Cancelled ? Loc.F("Ledger_RepaymentReversed", Entry.Reason) : Loc.Get("Ledger_Repayment");

    public bool TitleSemiBold => IsSale;
    public string ItemsText => Entry.ItemsText;
    public bool HasItems => IsSale && Entry.ItemsText.Length > 0;

    /// <summary>A voided credit sale keeps its row, with the VOIDED tag and the reason.</summary>
    public bool ShowVoidedTag => IsSale && Entry.Cancelled;
    public string VoidReason => Entry.Reason ?? "";

    public string CreditText => IsSale ? Fmt.Money(Entry.Amount) : "";
    public string RepaidText => IsSale ? "" : Fmt.Money(Entry.Amount);
    public string BalanceText => Fmt.Money(Entry.BalanceAfter);
    public bool IsCancelled => Entry.Cancelled;
    public bool CanReverse => !IsSale && !Entry.Cancelled;
}

public sealed class CustomerDetailViewModel : ObservableObject, IPage
{
    private readonly AppServices _s;
    private readonly MainViewModel _main;
    private readonly long _customerId;
    private CustomerBalance? _data;

    public CustomerDetailViewModel(AppServices services, MainViewModel main, long customerId)
    {
        _s = services;
        _main = main;
        _customerId = customerId;
        BackCommand = new RelayCommand(() => _main.Navigate(Section.Debts));
        EditCommand = new RelayCommand(Edit);
        RecordRepaymentCommand = new RelayCommand(RecordRepayment, () => (_data?.Balance ?? 0) > 0);
        ReverseCommand = new RelayCommand(Reverse);
    }

    public RelayCommand BackCommand { get; }
    public RelayCommand EditCommand { get; }
    public RelayCommand RecordRepaymentCommand { get; }
    public RelayCommand ReverseCommand { get; }

    public BulkObservableCollection<LedgerRow> Rows { get; } = [];

    public string Name => _data?.Customer.Name ?? "";

    public string AddressLine
    {
        get
        {
            if (_data == null) return "";
            var c = _data.Customer;
            return string.IsNullOrWhiteSpace(c.Phone) ? c.Address : $"{c.Address}  ·  {c.Phone}";
        }
    }

    public string BalanceText => Fmt.Ks(_data?.Balance ?? 0);

    public string CountText
    {
        get
        {
            var sales = Rows.Count(r => r.IsSale);
            var reps = Rows.Count(r => !r.IsSale);
            var reversed = Rows.Count(r => !r.IsSale && r.IsCancelled);
            return reversed > 0
                ? Loc.F("Customer_CountReversed", sales, reps, reversed)
                : Loc.F("Customer_Count", sales, reps);
        }
    }

    public void Activate()
    {
        _data = _s.Debts.Balance(_customerId);
        Rows.ReplaceAll(_s.Debts.Ledger(_customerId).Select(e => new LedgerRow(e)));
        foreach (var p in new[] { nameof(Name), nameof(AddressLine), nameof(BalanceText), nameof(CountText) })
            OnPropertyChanged(p);
    }

    private void Edit()
    {
        if (_data == null) return;
        if (Dialogs.Dialogs.EditCustomer(_data.Customer) != null) Activate();
    }

    private void RecordRepayment()
    {
        if (_data == null) return;
        if (Dialogs.Dialogs.RecordRepayment(_data)) Activate();
    }

    private void Reverse(object? p)
    {
        if (p is not LedgerRow { CanReverse: true } row) return;
        var reason = Dialogs.Dialogs.AskReason(
            Loc.Get("Reverse_Title"),
            [
                (Loc.Get("Reverse_Date"), row.DateText),
                (Loc.Get("Reverse_Customer"), _data?.Customer.Display ?? ""),
                (Loc.Get("Reverse_Amount"), Fmt.Ks(row.Entry.Amount)),
            ],
            Loc.Get("Reverse_Paragraph"),
            Loc.Get("Reverse_Placeholder"),
            Loc.Get("Reverse_Action"));
        if (reason == null) return;
        _s.Debts.ReverseRepayment(row.Entry.Id, reason);
        Activate();
    }
}
