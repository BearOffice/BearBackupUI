using System.IO;
using BearBackup;
using BearMarkupLanguage;
using BearMarkupLanguage.Serialization;
using Wpf.Ui.Appearance;

namespace BearBackupUI.Services;

public class ConfigService
{
    public ApplicationTheme ThemeType
    {
        get => _ml.GetValue<ApplicationTheme>("theme type");
        set => _ml.ChangeValue("theme type", value);
    }
    public bool LaunchMinimized
    {
        get => _ml.GetValue<bool>("launch minimized");
        set => _ml.ChangeValue("launch minimized", value);
    }
    public bool AutoStartup
    {
        get => _ml.GetValue<bool>("auto startup");
        set => _ml.ChangeValue("auto startup", value);
    }
    public bool CheckHash
    {
        get => _ml.GetValue<bool>("check hash");
        set => _ml.ChangeValue("check hash", value);
    }
    public BackupItemRecord[] BackupItemRecords
    {
        get
        {
            var keys = _ml.GetAllKeys("backup items");
            var list = new List<BackupItemRecord>();
            foreach (var key in keys)
            {
                var dic = _ml.GetValue<Dictionary<string, string?>>("backup items", key);
                list.Add(new BackupItemRecord
                {
                    ID = int.Parse(key),
                    Item = BackupItem.FromDictionary(dic),
                });
            }
            return [.. list];
        }
    }
    public string BasePath { get; }
    private readonly BearML _ml;

    public ConfigService(string basePath)
    {
        BasePath = basePath;

        var configPath = Path.Combine(BasePath, "config");
        if (!File.Exists(configPath))
        {
			_ml = new BearML(configPath);
            _ml.AddKeyValue("theme type", ApplicationTheme.Unknown);
            _ml.AddKeyValue("launch minimized", false);
            _ml.AddKeyValue("auto startup", false);
            _ml.AddKeyValue("check hash", false);
            _ml.AddEmptyBlock("backup items");
        }
        else
        {
            _ml = new BearML(configPath);
        }
    }

    public int AddBackupItemRecord(BackupItem item)
    {
        var id = 0;
        var allIds = _ml.GetAllKeys("backup items");
        if (allIds.Length > 0)
        {
            if (int.TryParse(allIds[^1], out var result))
                id = result + 1;
            else
                throw new Exception("Backup item's identifier is broken.");
        }

        _ml.AddKeyValue("backup items", id.ToString(), item.ToDictionary());

        return id;
    }

    public void ChangeBackupItemRecord(int id, BackupItem item)
    {
        _ml.ChangeValue("backup items", id.ToString(), item.ToDictionary());
    }

    public void RemoveBackupItemRecord(int id)
    {
        _ml.RemoveKey("backup items", id.ToString());
    }
}

public record BackupItemRecord
{
    public required int ID { get; init; }
    public required BackupItem Item { get; init; }
}

public record BackupItem
{
    public required string BackupPath { get; init; }
    public required BackupRepoType RepoType { get; init; }
    public required string BackupTarget { get; init; }
    public required int? ScheduledPeriod { get; init; }
    public required DateTime? LastBackupDateTime { get; init; }

    internal Dictionary<string, object?> ToDictionary()
    {
        return new Dictionary<string, object?>
        {
            { "backup path", BackupPath },
            { "repo type", RepoType },
            { "backup target", BackupTarget },
            { "scheduled period", ScheduledPeriod?.ToString() ?? null },
            { "last backup", LastBackupDateTime?.ToBinary().ToString() ?? null }
        };
    }

    internal static BackupItem FromDictionary(Dictionary<string, string?> dic)
    {
        return new BackupItem
        {
            BackupPath = dic["backup path"] ?? throw new BadBackupException("Backup item is broken."),
            RepoType = (BackupRepoType)Enum.Parse(typeof(BackupRepoType), 
                dic["repo type"] ?? throw new BadBackupException("Backup item is broken.")),
            BackupTarget = dic["backup target"] ?? throw new BadBackupException("Backup item is broken."),
#pragma warning disable CS8604
            ScheduledPeriod = string.IsNullOrEmpty(dic["scheduled period"]) ? null : int.Parse(dic["scheduled period"]),
            LastBackupDateTime =
                string.IsNullOrEmpty(dic["last backup"]) ? null : DateTime.FromBinary(long.Parse(dic["last backup"])),
#pragma warning restore CS8604
        };
    }
}

public enum BackupRepoType
{
    Mirroring,
    Versioning,
}