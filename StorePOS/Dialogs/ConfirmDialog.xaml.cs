using System.Windows;

namespace StorePOS.Dialogs;

public partial class ConfirmDialog : Window
{
    public ConfirmDialog(string title, string message, string keepText, string actionText)
    {
        InitializeComponent();
        TitleText.Text = title;
        MessageText.Text = message;
        KeepButton.Content = keepText;
        ActionButton.Content = actionText;
        Loaded += (_, _) => KeepButton.Focus();
    }

    private void Action_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
