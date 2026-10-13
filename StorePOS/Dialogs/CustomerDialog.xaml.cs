using System.Windows;
using StorePOS.Infrastructure;
using StorePOS.Models;

namespace StorePOS.Dialogs;

/// <summary>DB1: name and address; phone is optional.</summary>
public partial class CustomerDialog : Window
{
    private readonly Customer? _existing;

    public CustomerDialog(Customer? existing)
    {
        InitializeComponent();
        _existing = existing;
        TitleText.Text = Loc.Get(existing == null ? "Customer_NewTitle" : "Customer_EditTitle");
        NameBox.Text = existing?.Name ?? "";
        AddressBox.Text = existing?.Address ?? "";
        PhoneBox.Text = existing?.Phone ?? "";
        Loaded += (_, _) => NameBox.Focus();
    }

    public long SavedId { get; private set; }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SavedId = App.Services.Debts.SaveCustomer(new Customer
            {
                Id = _existing?.Id ?? 0,
                Name = NameBox.Text,
                Address = AddressBox.Text,
                Phone = PhoneBox.Text,
            });
            DialogResult = true;
        }
        catch (DomainException ex)
        {
            ErrorText.Text = ex.Message;
        }
    }
}
