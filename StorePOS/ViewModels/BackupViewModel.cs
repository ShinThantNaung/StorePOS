using Microsoft.Win32;
using StorePOS.Infrastructure;
using StorePOS.Services;

namespace StorePOS.ViewModels;

public sealed class BackupCopyRow(BackupCopy copy)
{
    public BackupCopy Copy { get; } = copy;
    public string DateText => Loc.Date(Copy.CreatedAt, "Date_Day") + ", " + Loc.Date(Copy.CreatedAt, "Time_Short");
    public string SizeText => Loc.F("Backup_SizeKb", Fmt.Money(Math.Max(1, Copy.SizeBytes / 1024)));
}

public sealed class BackupViewModel : ObservableObject, IPage
{
    private readonly AppServices _s;
    private readonly MainViewModel _main;
    private readonly string _startLanguage;
    private string _status = "";
    private bool _statusIsProblem;

    public BackupViewModel(AppServices services, MainViewModel main)
    {
        _s = services;
        _main = main;
        _startLanguage = services.Settings.Language;
        ChooseFolderCommand = new RelayCommand(ChooseFolder);
        BackupNowCommand = new RelayCommand(BackupNow);
        RestoreCommand = new RelayCommand(p => { if (p is BackupCopyRow r) Restore(r.Copy.Path, r.DateText); });
        RestoreFromFileCommand = new RelayCommand(RestoreFromFile);
    }

    public RelayCommand ChooseFolderCommand { get; }
    public RelayCommand BackupNowCommand { get; }
    public RelayCommand RestoreCommand { get; }
    public RelayCommand RestoreFromFileCommand { get; }

    public BulkObservableCollection<BackupCopyRow> Copies { get; } = [];

    public string FolderText => _s.Backup.Folder ?? "";

    public string LastBackupText => _s.Backup.LastBackupAt is { } t
        ? Loc.F("Backup_Last", Loc.Date(t, "Date_Day") + ", " + Loc.Date(t, "Time_Short"))
        : Loc.Get("Backup_Never");

    public string Status
    {
        get => _status;
        private set => Set(ref _status, value);
    }

    public bool StatusIsProblem
    {
        get => _statusIsProblem;
        private set => Set(ref _statusIsProblem, value);
    }

    public bool NoCopies => Copies.Count == 0;

    /// <summary>"my" or "en". Takes effect the next time the app starts.</summary>
    public string Language
    {
        get => _s.Settings.Language;
        set
        {
            if (_s.Settings.Language == value) return;
            _s.Settings.Language = value;
            _s.SaveSettings();
            OnPropertyChanged();
            OnPropertyChanged(nameof(LanguageChanged));
        }
    }

    public bool LanguageChanged => _s.Settings.Language != _startLanguage;

    public void Activate() => Refresh();

    private void Refresh()
    {
        Copies.ReplaceAll(_s.Backup.Copies().Select(c => new BackupCopyRow(c)));
        OnPropertyChanged(nameof(FolderText));
        OnPropertyChanged(nameof(LastBackupText));
        OnPropertyChanged(nameof(NoCopies));
        _main.UpdateBackupNote();
    }

    private void Report(string text, bool problem)
    {
        Status = text;
        StatusIsProblem = problem;
    }

    public void AutoBackupFinished(string? error)
    {
        if (error != null) Report(Loc.F("Backup_AutoFailed", error), true);
        Refresh();
    }

    private void ChooseFolder()
    {
        var dialog = new OpenFolderDialog { Title = Loc.Get("Backup_ChooseFolder") };
        if (Directory.Exists(_s.Backup.Folder)) dialog.InitialDirectory = _s.Backup.Folder;
        if (dialog.ShowDialog() != true) return;
        _s.Backup.SetFolder(dialog.FolderName);
        Report("", false);
        Refresh();
    }

    private void BackupNow()
    {
        try
        {
            var path = _s.Backup.BackupNow();
            Report(Loc.F("Backup_Done", Path.GetFileName(path)), false);
        }
        catch (DomainException ex)
        {
            Report(ex.Message, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException)
        {
            Report(Loc.F("Backup_Failed", ex.Message), true);
        }
        Refresh();
    }

    private void RestoreFromFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = Loc.Get("Restore_ChooseFile"),
            Filter = "Store POS backup (*.db)|*.db",
        };
        if (Directory.Exists(_s.Backup.Folder)) dialog.InitialDirectory = _s.Backup.Folder;
        if (dialog.ShowDialog() != true) return;
        Restore(dialog.FileName, Path.GetFileName(dialog.FileName));
    }

    private void Restore(string path, string label)
    {
        var ok = Dialogs.Dialogs.Confirm(
            Loc.Get("Restore_ConfirmTitle"),
            Loc.F("Restore_ConfirmText", label),
            Loc.Get("Restore_Keep"),
            Loc.Get("Restore_Action"));
        if (!ok) return;
        try
        {
            var safety = _s.Backup.Restore(path);
            Report(Loc.F("Restore_Done", label, safety), false);
        }
        catch (DomainException ex)
        {
            Report(ex.Message, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException)
        {
            Report(Loc.F("Restore_Failed", ex.Message), true);
        }
        _main.DataReplaced();
        Refresh();
    }
}
