using System.ComponentModel;
using System.Windows.Controls;

namespace StorePOS.Views;

public partial class StockInsView : UserControl
{
    public StockInsView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            // Always newest first; the arrow says so.
            DateColumn.SortDirection = ListSortDirection.Descending;
            SearchBox.Focus();
        };
    }
}
