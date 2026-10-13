using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using StorePOS.Infrastructure;
using StorePOS.ViewModels;

namespace StorePOS.Views;

public partial class PricesView : UserControl
{
    public PricesView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            GridSort.Sync(Table);
            SearchBox.Focus();
        };
    }

    private void Grid_Sorting(object sender, DataGridSortingEventArgs e) => GridSort.OnSorting(Table, e);

    private void Grid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        var d = e.OriginalSource as DependencyObject;
        while (d != null && d is not DataGridRow) d = d is Visual ? VisualTreeHelper.GetParent(d) : null;
        if (d is DataGridRow) ((PricesViewModel)DataContext).Edit();
    }
}
