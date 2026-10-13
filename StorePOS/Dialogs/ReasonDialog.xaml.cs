using System.Windows;
using System.Windows.Controls;
using StorePOS.Infrastructure;

namespace StorePOS.Dialogs;

public partial class ReasonDialog : Window
{
    public ReasonDialog(string title, IReadOnlyList<(string Label, string Value)> summary,
        string paragraph, string placeholder, string actionText)
    {
        InitializeComponent();
        TitleText.Text = title;
        Summary.ItemsSource = summary.Select(s => new { s.Label, s.Value }).ToList();
        ParagraphText.Text = paragraph;
        Ui.SetPlaceholder(ReasonBox, placeholder);
        ActionButton.Content = actionText;
        Loaded += (_, _) => ReasonBox.Focus();
    }

    public string Reason => ReasonBox.Text.Trim();

    /// <summary>The action stays disabled until a reason is typed.</summary>
    private void ReasonBox_TextChanged(object sender, TextChangedEventArgs e) =>
        ActionButton.IsEnabled = Reason.Length > 0;

    private void Action_Click(object sender, RoutedEventArgs e)
    {
        if (Reason.Length > 0) DialogResult = true;
    }
}
