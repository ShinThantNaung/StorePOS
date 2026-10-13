using StorePOS.Data;

namespace StorePOS.Services;

/// <summary>Everything the screens need, created once at startup.</summary>
public sealed class AppServices
{
    public AppServices(string dataFolder, AppSettings settings, Func<DateTime> clock)
    {
        DataFolder = dataFolder;
        Settings = settings;
        Clock = clock;
        Db = new Db(Path.Combine(dataFolder, "store.db"));
        Products = new ProductService(Db);
        Sales = new SaleService(Db, clock);
        Debts = new DebtService(Db, clock);
        Reports = new ReportService(Db);
        Import = new CsvImportService(Db, Products);
        Backup = new BackupService(Db, settings, SettingsPath(dataFolder), Path.Combine(dataFolder, "SafetyCopies"), clock);
    }

    public static string SettingsPath(string dataFolder) => Path.Combine(dataFolder, "settings.json");

    public string DataFolder { get; }
    public AppSettings Settings { get; }
    public Func<DateTime> Clock { get; }
    public Db Db { get; }
    public ProductService Products { get; }
    public SaleService Sales { get; }
    public DebtService Debts { get; }
    public ReportService Reports { get; }
    public CsvImportService Import { get; }
    public BackupService Backup { get; }

    public DateTime Today => Clock().Date;

    public void SaveSettings() => Settings.Save(SettingsPath(DataFolder));
}
