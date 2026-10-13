using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using StorePOS.ViewModels;

namespace StorePOS.Views;

public partial class SalesView : UserControl
{
    public SalesView()
    {
        InitializeComponent();
        Loaded += (_, _) => SearchBox.Focus();
    }

    private SalesViewModel Vm => (SalesViewModel)DataContext;

    private void ProductGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (FindRow(e.OriginalSource as DependencyObject)?.Item is ProductRow row) Vm.AddOne(row);
    }

    /// <summary>Enter in the search box picks the first match and moves to the quantity box.</summary>
    private void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Down)) return;
        if (Vm.SelectedProduct == null || !Vm.ProductsView.Contains(Vm.SelectedProduct))
            Vm.SelectedProduct = Vm.ProductsView.Cast<ProductRow>().FirstOrDefault();
        if (Vm.SelectedProduct == null) return;
        ProductGrid.ScrollIntoView(Vm.SelectedProduct);
        QtyBox.Focus();
        e.Handled = true;
    }

    private void SelectAll(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is TextBox box) box.SelectAll();
    }

    private static DataGridRow? FindRow(DependencyObject? d)
    {
        while (d != null && d is not DataGridRow) d = d is Visual ? VisualTreeHelper.GetParent(d) : null;
        return d as DataGridRow;
    }
}
