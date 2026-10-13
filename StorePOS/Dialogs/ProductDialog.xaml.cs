using System.Windows;
using StorePOS.Infrastructure;
using StorePOS.Models;

namespace StorePOS.Dialogs;

/// <summary>PR5: add, edit and deactivate a product.</summary>
public partial class ProductDialog : Window
{
    private readonly Product? _existing;

    public ProductDialog(Product? existing)
    {
        InitializeComponent();
        _existing = existing;
        TitleText.Text = Loc.Get(existing == null ? "Product_AddTitle" : "Product_EditTitle");
        CategoryBox.ItemsSource = Categories.All.Select(c => new { Label = Categories.Display(c), Value = c }).ToList();
        CategoryBox.SelectedValue = existing?.Category ?? Category.Pesticide;
        NameBox.Text = existing?.Name ?? "";
        UnitBox.Text = existing?.Unit ?? "";
        PriceBox.Text = existing == null ? "" : Fmt.Money(existing.Price);
        WeightBox.IsChecked = existing?.SoldByWeight ?? false;
        ActiveBox.IsChecked = existing?.Active ?? true;
        ActiveBox.Visibility = existing == null ? Visibility.Collapsed : Visibility.Visible;
        Loaded += (_, _) => NameBox.Focus();
    }

    public long SavedId { get; private set; }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!Fmt.TryParseMoney(PriceBox.Text, out var price))
        {
            ErrorText.Text = Loc.Get("Err_PriceInvalid");
            return;
        }
        var active = ActiveBox.IsChecked == true;

        // Deactivating asks first, like every other destructive action.
        if (_existing is { Active: true } && !active)
        {
            var ok = Dialogs.Confirm(
                Loc.Get("Deactivate_Title"),
                Loc.F("Deactivate_Text", _existing.Name),
                Loc.Get("Deactivate_Keep"),
                Loc.Get("Deactivate_Action"));
            if (!ok) return;
        }

        try
        {
            SavedId = App.Services.Products.Save(new Product
            {
                Id = _existing?.Id ?? 0,
                Name = NameBox.Text,
                Category = (Category)CategoryBox.SelectedValue,
                Unit = UnitBox.Text,
                Price = price,
                SoldByWeight = WeightBox.IsChecked == true,
                Active = active,
            });
            DialogResult = true;
        }
        catch (DomainException ex)
        {
            ErrorText.Text = ex.Message;
        }
    }
}
