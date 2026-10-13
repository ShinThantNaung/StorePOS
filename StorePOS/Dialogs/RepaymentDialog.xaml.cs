using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using StorePOS.Infrastructure;
using StorePOS.Models;

namespace StorePOS.Dialogs;

/// <summary>DB3: record a repayment. The money pays off the oldest sales first.</summary>
public partial class RepaymentDialog : Window, INotifyPropertyChanged
{
    private readonly CustomerBalance _customer;
    private DateTime _date;
    private string _amountText = "";

    public RepaymentDialog(CustomerBalance customer)
    {
        _customer = customer;
        _date = App.Services.Today;
        InitializeComponent();
        DataContext = this;
        Loaded += (_, _) => AmountBox.Focus();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));

    public string CustomerLine => _customer.Customer.Display;
    public string BalanceText => Fmt.Ks(_customer.Balance);
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

    public string AmountText
    {
        get => _amountText;
        set
        {
            _amountText = value;
            Changed();
            Changed(nameof(AfterText));
            Changed(nameof(Problem));
            Changed(nameof(CanSave));
        }
    }

    private long? Amount => Fmt.TryParseMoney(_amountText, out var a) && a > 0 ? a : null;

    public string AfterText => Amount switch
    {
        null => string.IsNullOrWhiteSpace(_amountText) ? "" : Loc.Get("Repay_CheckAmount"),
        var a when a > _customer.Balance => Loc.Get("Repay_CheckAmount"),
        var a when a == _customer.Balance => Loc.Get("Repay_PaidInFull"),
        var a => Loc.F("Repay_StillOwed", Fmt.Money(_customer.Balance - a!.Value)),
    };

    public string Problem => Amount > _customer.Balance ? Loc.Get("Repay_TooMuch") : "";

    public bool CanSave => Amount is { } a && a <= _customer.Balance;

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!CanSave) return;
        App.Services.Debts.RecordRepayment(_customer.Customer.Id, Amount!.Value, _date);
        DialogResult = true;
    }
}
