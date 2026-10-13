using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace StorePOS.Controls;

/// <summary>Date box (shows the formatted date) plus a calendar icon button that opens a month calendar.</summary>
public partial class DateBox : UserControl
{
    public static readonly DependencyProperty SelectedDateProperty = DependencyProperty.Register(
        nameof(SelectedDate), typeof(DateTime), typeof(DateBox),
        new FrameworkPropertyMetadata(DateTime.Today, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty MaxDateProperty = DependencyProperty.Register(
        nameof(MaxDate), typeof(DateTime?), typeof(DateBox), new PropertyMetadata(null));

    public static readonly DependencyProperty DisplayTextProperty = DependencyProperty.Register(
        nameof(DisplayText), typeof(string), typeof(DateBox), new PropertyMetadata(""));

    public static readonly DependencyProperty BoxWidthProperty = DependencyProperty.Register(
        nameof(BoxWidth), typeof(double), typeof(DateBox), new PropertyMetadata(200.0));

    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label), typeof(string), typeof(DateBox), new PropertyMetadata(""));

    private bool _syncing;

    public DateBox()
    {
        InitializeComponent();
    }

    public DateTime SelectedDate
    {
        get => (DateTime)GetValue(SelectedDateProperty);
        set => SetValue(SelectedDateProperty, value);
    }

    public DateTime? MaxDate
    {
        get => (DateTime?)GetValue(MaxDateProperty);
        set => SetValue(MaxDateProperty, value);
    }

    public string DisplayText
    {
        get => (string)GetValue(DisplayTextProperty);
        set => SetValue(DisplayTextProperty, value);
    }

    public double BoxWidth
    {
        get => (double)GetValue(BoxWidthProperty);
        set => SetValue(BoxWidthProperty, value);
    }

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    private void Open(object sender, RoutedEventArgs e)
    {
        _syncing = true;
        Cal.DisplayDateEnd = MaxDate;
        Cal.SelectedDate = SelectedDate;
        Cal.DisplayDate = SelectedDate;
        _syncing = false;
        Pop.IsOpen = true;
        Cal.Focus();
    }

    private void Box_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Space or Key.F4)
        {
            Open(sender, e);
            e.Handled = true;
        }
    }

    private void Cal_SelectedDatesChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncing || Cal.SelectedDate is not { } d) return;
        // The calendar keeps the mouse captured after a click; release it so the next click works.
        Mouse.Capture(null);
        Pop.IsOpen = false;
        SelectedDate = d;
    }
}
