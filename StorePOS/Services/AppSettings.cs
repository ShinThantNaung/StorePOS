using System.Text.Json;

namespace StorePOS.Services;

/// <summary>Per-laptop settings, kept outside the database so restoring a copy never changes them.</summary>
public sealed class AppSettings
{
    public string Language { get; set; } = "my";
    public string? BackupFolder { get; set; }
    public DateTime? LastBackupAt { get; set; }

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static AppSettings Load(string path)
    {
        try
        {
            if (File.Exists(path))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path)) ?? new AppSettings();
        }
        catch (Exception)
        {
            // A damaged settings file must never stop the shop from opening; start with defaults.
        }
        return new AppSettings();
    }

    public void Save(string path)
    {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(this, Options));
        File.Move(tmp, path, overwrite: true);
    }
}
