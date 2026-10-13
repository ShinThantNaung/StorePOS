using System.Windows.Controls;
using StorePOS.Infrastructure;

namespace StorePOS.Views;

public partial class DebtsView : UserControl
{
    public DebtsView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            GridSort.Sync(Table);
            SearchBox.Focus();
        };
    }
}
