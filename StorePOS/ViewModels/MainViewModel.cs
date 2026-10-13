using System.Windows.Threading;
using StorePOS.Infrastructure;
using StorePOS.Services;

namespace StorePOS.ViewModels;

public enum Section { Sales, Prices, Debts, Reports, Backup }

public interface IPage
{
    void Activate();
}

public sealed class MainViewModel : ObservableObject
{
    private readonly AppServices _s;
    private readonly DispatcherTimer _timer;
    private Section _section;
    private object? _currentPage;
    private string _todayText = "";
    private string _statusNote = "";
    private bool _backupRunning;

    public MainViewModel(AppServices services)
    {
        _s = services;
        SalesPage = new SalesViewModel(services, this);
        PricesPage = new PricesViewModel(services);
        DebtsPage = new DebtsViewModel(services, this);
        ReportsPage = new ReportsViewModel(services);
        BackupPage = new BackupViewModel(services, this);
        NavigateCommand = new RelayCommand(p => Navigate((Section)p!));

        UpdateToday();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _timer.Tick += (_, _) =>
        {
            UpdateToday();
            // Left open overnight: still make today's copy.
            if (!_s.Backup.BackedUpToday) StartBackgroundBackup();
        };
        _timer.Start();
    }

    public SalesViewModel SalesPage { get; }
    public PricesViewModel PricesPage { get; }
    public DebtsViewModel DebtsPage { get; }
    public ReportsViewModel ReportsPage { get; }
    public BackupViewModel BackupPage { get; }

    public RelayCommand NavigateCommand { get; }

    public Section Section
    {
        get => _section;
        private set => Set(ref _section, value);
    }

    public object? CurrentPage
    {
        get => _currentPage;
        private set => Set(ref _currentPage, value);
    }

    public string TodayText
    {
        get => _todayText;
        private set => Set(ref _todayText, value);
    }

    public string StatusNote
    {
        get => _statusNote;
        set => Set(ref _statusNote, value);
    }

    public void Navigate(Section section)
    {
        Section = section;
        IPage page = section switch
        {
            Section.Sales => SalesPage,
            Section.Prices => PricesPage,
            Section.Debts => DebtsPage,
            Section.Reports => ReportsPage,
            _ => BackupPage,
        };
        page.Activate();
        CurrentPage = page;
    }

    /// <summary>Customer detail counts as part of Debts, so Debts stays marked.</summary>
    public void OpenCustomer(long customerId)
    {
        Section = Section.Debts;
        var page = new CustomerDetailViewModel(_s, this, customerId);
        page.Activate();
        CurrentPage = page;
    }

    /// <summary>After a restore every screen must re-read the database.</summary>
    public void DataReplaced()
    {
        SalesPage.ClearAfterRestore();
        UpdateBackupNote();
    }

    private void UpdateToday() => TodayText = Loc.Date(_s.Clock(), "Date_Long");

    public void UpdateBackupNote() =>
        StatusNote = _s.Backup.BackedUpToday ? "" : Loc.Get("Status_NoBackupToday");

    /// <summary>Once a day: copy the database to the backup folder without slowing the start.</summary>
    public void StartBackgroundBackup()
    {
        if (_backupRunning || _s.Backup.BackedUpToday) return;
        _backupRunning = true;
        var dispatcher = Dispatcher.CurrentDispatcher;
        Task.Run(() =>
        {
            string? error = null;
            try { _s.Backup.BackupNow(); }
            catch (Exception ex) { error = ex.Message; }
            dispatcher.Invoke(() =>
            {
                _backupRunning = false;
                BackupPage.AutoBackupFinished(error);
                UpdateBackupNote();
            });
        });
    }
}
