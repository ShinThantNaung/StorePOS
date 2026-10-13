using System.Globalization;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;
using StorePOS.Dialogs;
using StorePOS.Infrastructure;
using StorePOS.Services;
using StorePOS.ViewModels;

namespace StorePOS;

public partial class App : Application
{
    private static AppServices? _services;
    private Mutex? _singleInstance;

    public static AppServices Services => _services ?? throw new InvalidOperationException("Not started.");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnUnhandledException;

        string? dataArg = null, screenshotDir = null;
        var demo = false;
        for (var i = 0; i < e.Args.Length; i++)
        {
            switch (e.Args[i])
            {
                case "--data" when i + 1 < e.Args.Length: dataArg = e.Args[++i]; break;
                case "--screenshots" when i + 1 < e.Args.Length: screenshotDir = e.Args[++i]; break;
                case "--demo": demo = true; break;
            }
        }

        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StorePOS");
        var dataFolder = dataArg ?? (demo ? Path.Combine(root, "Demo") : root);
        Directory.CreateDirectory(dataFolder);

        // Only one copy of the app may write to the shop's database at a time.
        var folderHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(Path.GetFullPath(dataFolder).ToLowerInvariant())))[..16];
        _singleInstance = new Mutex(true, "StorePOS-" + folderHash, out var first);
        if (!first && screenshotDir == null)
        {
            MessageBox.Show("Store POS is already open.", "Store POS");
            Shutdown();
            return;
        }

        var settings = AppSettings.Load(AppServices.SettingsPath(dataFolder));
        settings.BackupFolder ??= dataArg != null || demo
            ? Path.Combine(dataFolder, "Backups")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "StorePOS Backups");

        ApplyLanguage(settings.Language);

        _services = new AppServices(dataFolder, settings, () => DateTime.Now);
        Services.Db.EnsureCreated();
        if ((demo || screenshotDir != null) && Services.Products.List(true).Count == 0) DemoData.Seed(Services);

        var main = new MainViewModel(Services);
        var window = new MainWindow { DataContext = main };
        MainWindow = window;

        if (screenshotDir != null)
        {
            Screenshots.Run(window, main, screenshotDir);
            return;
        }

        window.Show();
        main.Navigate(Section.Sales);
        main.StartBackgroundBackup();
    }

    private static void ApplyLanguage(string language)
    {
        var culture = CultureInfo.GetCultureInfo(language == "en" ? "en-GB" : "my-MM");
        Loc.Culture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
        // xml:lang on every element, so WPF picks the right shaping for Burmese text.
        FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(culture.IetfLanguageTag)));
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        if (e.Exception is DomainException d)
        {
            Dialogs.Dialogs.Error(d.Message);
            return;
        }
        try
        {
            var folder = _services?.DataFolder ?? Path.GetTempPath();
            File.AppendAllText(Path.Combine(folder, "errors.log"), $"{DateTime.Now:u}\n{e.Exception}\n\n");
        }
        catch (Exception)
        {
            // Logging must never hide the original problem from the user.
        }
        Dialogs.Dialogs.Error(Loc.F("Err_Unexpected", e.Exception.Message));
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
