using System.ComponentModel;
using System.Windows;
using Microsoft.Win32;
using StorePOS.Infrastructure;
using StorePOS.Models;
using StorePOS.Services;

namespace StorePOS.Dialogs;

public sealed class ImportRowView(ImportRow r)
{
    public int Row => r.Row;
    public string Name => Product.FormatFullName(r.Name, r.Size, r.Company);
    public string CategoryName => Categories.Display(r.Category);
    public string UnitText => r.SoldByWeight ? Loc.F("Import_UnitWeight", r.Unit) : r.Unit;
    public string PriceText => Fmt.Money(r.Price);
    public bool IsChange => r.Kind != ImportKind.NoChange;

    public string ChangeText => r.Kind switch
    {
        ImportKind.New => Loc.Get("Import_New"),
        ImportKind.PriceChange => Loc.F("Import_PriceChange", Fmt.Money(r.OldPrice), Fmt.Money(r.Price)),
        _ => Loc.Get("Import_NoChange"),
    };
}

/// <summary>PR6: preview first; rows that cannot be read are listed, never skipped silently.</summary>
public partial class ImportCsvDialog : Window, INotifyPropertyChanged
{
    private ImportPreview? _preview;
    private string _filePath = "";
    private string _fileError = "";

    public ImportCsvDialog()
    {
        InitializeComponent();
        DataContext = this;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string FilePath => _filePath;
    public string FileError => _fileError;
    public List<ImportRowView> Rows { get; private set; } = [];
    public List<ImportError> Errors => _preview?.Errors ?? [];

    public string SummaryText => _preview == null ? "" : Loc.F("Import_Summary",
        _preview.RowsRead, _preview.NewCount, _preview.ChangeCount, _preview.SameCount, _preview.Errors.Count);

    public string RowsTitle => Loc.F("Import_RowsTitle", Rows.Count);
    public string ErrorsTitle => Loc.F("Import_ErrorsTitle", Errors.Count);

    /// <summary>Only new products and price changes are written; "No change" rows need nothing.</summary>
    private int ToApply => _preview == null ? 0 : _preview.NewCount + _preview.ChangeCount;

    public string ImportButtonText => Loc.F("Import_Button", Rows.Count);
    public bool CanImport => ToApply > 0;

    private void Choose_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = Loc.Get("Import_ChooseFile"), Filter = "CSV (*.csv)|*.csv|All files (*.*)|*.*" };
        if (dialog.ShowDialog(this) != true) return;
        Load(dialog.FileName);
    }

    public void Load(string path)
    {
        _filePath = path;
        try
        {
            _preview = App.Services.Import.Preview(path);
            _fileError = "";
        }
        catch (DomainException ex)
        {
            _preview = null;
            _fileError = ex.Message;
        }
        Rows = _preview?.Rows.Select(r => new ImportRowView(r)).ToList() ?? [];
        foreach (var p in new[]
                 {
                     nameof(FilePath), nameof(FileError), nameof(Rows), nameof(Errors), nameof(SummaryText),
                     nameof(RowsTitle), nameof(ErrorsTitle), nameof(ImportButtonText), nameof(CanImport),
                 })
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        if (_preview == null || !CanImport) return;
        App.Services.Import.Apply(_preview);
        DialogResult = true;
    }
}
