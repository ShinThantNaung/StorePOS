using System.Windows.Controls;

namespace StorePOS.Views;

public partial class StockView : UserControl
{
    public StockView()
    {
        InitializeComponent();
        Loaded += (_, _) => SearchBox.Focus();
    }
}
