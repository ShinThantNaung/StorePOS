using System.Windows;

namespace StorePOS.Dialogs;

public partial class MessageDialog : Window
{
    public MessageDialog(string title, string message)
    {
        InitializeComponent();
        TitleText.Text = title;
        // A read-only text box, so a file path or error can be selected and copied.
        MessageText.Text = message;
        Loaded += (_, _) => CloseButton.Focus();
    }
}
