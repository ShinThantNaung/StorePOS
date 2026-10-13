using System.Globalization;
using Microsoft.Data.Sqlite;
using StorePOS.Data;
using StorePOS.Infrastructure;

namespace StorePOS.Services;

public sealed record BackupCopy(string Path, DateTime CreatedAt, long SizeBytes);

/// <summary>
/// Copies the database with SQLite's online backup API, so a copy is always complete even if
/// a sale is being saved at the same moment.
/// </summary>
public sealed class BackupService(Db db, AppSettings settings, string settingsPath, string safetyFolder, Func<DateTime> clock)
{
    public const string FilePrefix = "StorePOS-backup-";
    private const string StampFormat = "yyyy-MM-dd-HHmmss";

    public string? Folder => settings.BackupFolder;
    public DateTime? LastBackupAt => settings.LastBackupAt;

    public void SetFolder(string folder)
    {
        settings.BackupFolder = folder;
        settings.Save(settingsPath);
    }

    public bool BackedUpToday => settings.LastBackupAt?.Date == clock().Date;

    public string BackupNow()
    {
        var folder = settings.BackupFolder;
        if (string.IsNullOrWhiteSpace(folder)) throw new DomainException(Loc.Get("Backup_NoFolder"));
        try { Directory.CreateDirectory(folder); }
        catch (Exception) { throw new DomainException(Loc.F("Backup_FolderMissing", folder)); }

        var now = clock();
        var target = Path.Combine(folder, FilePrefix + now.ToString(StampFormat, CultureInfo.InvariantCulture) + ".db");
        CopyDatabase(db.FilePath, target);

        settings.LastBackupAt = now;
        settings.Save(settingsPath);
        return target;
    }

    public List<BackupCopy> Copies()
    {
        var folder = settings.BackupFolder;
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return [];
        return Directory.EnumerateFiles(folder, FilePrefix + "*.db")
            .Select(path =>
            {
                var stamp = Path.GetFileNameWithoutExtension(path)[FilePrefix.Length..];
                var created = DateTime.TryParseExact(stamp, StampFormat, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var t) ? t : File.GetLastWriteTime(path);
                return new BackupCopy(path, created, new FileInfo(path).Length);
            })
            .OrderByDescending(c => c.CreatedAt)
            .ToList();
    }

    /// <summary>Replaces the live data with a copy. The current data is saved to a safety copy first.</summary>
    public string Restore(string sourcePath)
    {
        if (!IsStoreDatabase(sourcePath)) throw new DomainException(Loc.Get("Restore_NotACopy"));

        Directory.CreateDirectory(safetyFolder);
        var safety = Path.Combine(safetyFolder,
            "before-restore-" + clock().ToString(StampFormat, CultureInfo.InvariantCulture) + ".db");
        CopyDatabase(db.FilePath, safety);

        SqliteConnection.ClearAllPools();
        using (var source = Open(sourcePath, SqliteOpenMode.ReadOnly))
        using (var target = Open(db.FilePath, SqliteOpenMode.ReadWriteCreate))
        {
            source.BackupDatabase(target);
        }
        SqliteConnection.ClearAllPools();
        db.EnsureCreated();
        return safety;
    }

    private static SqliteConnection Open(string path, SqliteOpenMode mode)
    {
        var conn = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path, Mode = mode, Pooling = false,
        }.ToString());
        conn.Open();
        return conn;
    }

    private static void CopyDatabase(string sourcePath, string targetPath)
    {
        var tmp = targetPath + ".part";
        using (var source = Open(sourcePath, SqliteOpenMode.ReadOnly))
        using (var target = Open(tmp, SqliteOpenMode.ReadWriteCreate))
        {
            source.BackupDatabase(target);
        }
        File.Move(tmp, targetPath, overwrite: true);
    }

    private static bool IsStoreDatabase(string path)
    {
        try
        {
            using var conn = Open(path, SqliteOpenMode.ReadOnly);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT COUNT(*) FROM sqlite_master WHERE type = 'table'
                AND name IN ('products','customers','sales','sale_items','debts','repayments','repayment_allocations')
                """;
            return Convert.ToInt64(cmd.ExecuteScalar()) == 7;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
