using System.ComponentModel;
using System.Windows.Controls;
using StorePOS.ViewModels;

namespace StorePOS.Views;

public partial class ReportsView : UserControl
{
    public ReportsView()
    {
        InitializeComponent();
        Loaded += (_, _) => ShowBestSort();
    }

    private ReportsViewModel Vm => (ReportsViewModel)DataContext;

    /// <summary>Best sellers re-rank the top 10 by quantity or by kyat (default), highest first.</summary>
    private void BestGrid_Sorting(object sender, DataGridSortingEventArgs e)
    {
        e.Handled = true;
        Vm.SortBest(byQty: e.Column == QtyColumn);
        ShowBestSort();
    }

    private void ShowBestSort()
    {
        QtyColumn.SortDirection = Vm.BestByQty ? ListSortDirection.Descending : null;
        KsColumn.SortDirection = Vm.BestByQty ? null : ListSortDirection.Descending;
    }
}
