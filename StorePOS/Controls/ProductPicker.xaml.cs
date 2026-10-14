using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using StorePOS.Infrastructure;
using StorePOS.ViewModels;

namespace StorePOS.Controls;

/// <summary>
/// Search box for one product: typing part of the name or company lists the matches under the box.
/// Up/Down move through the list, Enter or a click picks, Escape closes it.
/// </summary>
public partial class ProductPicker : UserControl
{
    private const int MaxShown = 50;
    private bool _settingText;

    public static readonly DependencyProperty ProductsProperty = DependencyProperty.Register(
        nameof(Products), typeof(IEnumerable<ProductRow>), typeof(ProductPicker), new PropertyMetadata(null));

    public static readonly DependencyProperty SelectedProductProperty = DependencyProperty.Register(
        nameof(SelectedProduct), typeof(ProductRow), typeof(ProductPicker),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedChanged));

    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label), typeof(string), typeof(ProductPicker), new PropertyMetadata(""));

    public ProductPicker()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            // The list is a separate window: it must not stay on screen when the dialog moves or loses focus.
            if (Window.GetWindow(this) is { } w)
            {
                w.Deactivated += (_, _) => Close();
                w.LocationChanged += (_, _) => Close();
            }
        };
    }

    public IEnumerable<ProductRow>? Products
    {
        get => (IEnumerable<ProductRow>?)GetValue(ProductsProperty);
        set => SetValue(ProductsProperty, value);
    }

    public ProductRow? SelectedProduct
    {
        get => (ProductRow?)GetValue(SelectedProductProperty);
        set => SetValue(SelectedProductProperty, value);
    }

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Raised after a product is picked from the list, so the next box can take focus.</summary>
    public event EventHandler? Picked;

    public void FocusBox() => Box.Focus();

    /// <summary>Empties the box for the next product.</summary>
    public void Clear()
    {
        SelectedProduct = null;
        SetText("");
        Close();
    }

    private static void OnSelectedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var picker = (ProductPicker)d;
        if (e.NewValue is ProductRow row && picker.Box.Text != row.FullName) picker.SetText(row.FullName);
    }

    private void SetText(string text)
    {
        _settingText = true;
        Box.Text = text;
        Box.CaretIndex = text.Length;
        _settingText = false;
    }

    private void Box_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_settingText) return;
        SelectedProduct = null;
        ShowMatches();
    }

    private void ShowMatches()
    {
        var key = Fmt.SearchKey(Box.Text);
        if (key.Length == 0 || Products == null)
        {
            Close();
            return;
        }
        var matches = Products
            .Where(p => p.SearchKey.Contains(key, StringComparison.Ordinal) || p.CompanyKey.Contains(key, StringComparison.Ordinal))
            .Take(MaxShown)
            .ToList();
        MatchList.ItemsSource = matches;
        MatchList.SelectedIndex = matches.Count > 0 ? 0 : -1;
        NoMatch.Visibility = matches.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        MatchPopup.IsOpen = true;
        if (matches.Count > 0) MatchList.ScrollIntoView(matches[0]);
    }

    private void Box_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var count = MatchList.Items.Count;
        switch (e.Key)
        {
            // After Escape, Down opens the list again.
            case Key.Down when !MatchPopup.IsOpen && SelectedProduct == null && Box.Text.Length > 0:
                ShowMatches();
                e.Handled = true;
                break;
            case Key.Down when MatchPopup.IsOpen && count > 0:
                Move(Math.Min(MatchList.SelectedIndex + 1, count - 1));
                e.Handled = true;
                break;
            case Key.Up when MatchPopup.IsOpen && count > 0:
                Move(Math.Max(MatchList.SelectedIndex - 1, 0));
                e.Handled = true;
                break;
            // Enter picks while the list is open; otherwise it reaches the dialog as usual.
            case Key.Enter when MatchPopup.IsOpen:
                if (MatchList.SelectedItem is ProductRow row) Pick(row);
                e.Handled = true;
                break;
            case Key.Escape when MatchPopup.IsOpen:
                Close();
                e.Handled = true;
                break;
            case Key.Tab when MatchPopup.IsOpen && MatchList.SelectedItem is ProductRow tabRow:
                Pick(tabRow, raisePicked: false);
                break;
        }
    }

    private void Move(int index)
    {
        MatchList.SelectedIndex = index;
        MatchList.ScrollIntoView(MatchList.SelectedItem);
    }

    private void List_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (ItemsControl.ContainerFromElement(MatchList, (DependencyObject)e.OriginalSource) is ListBoxItem item &&
            item.DataContext is ProductRow row)
        {
            Pick(row);
            e.Handled = true;
        }
    }

    private void Box_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => Close();

    private void Pick(ProductRow row, bool raisePicked = true)
    {
        SetText(row.FullName);
        SelectedProduct = row;
        Close();
        if (raisePicked) Picked?.Invoke(this, EventArgs.Empty);
    }

    private void Close() => MatchPopup.IsOpen = false;
}
