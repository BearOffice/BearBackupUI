using BearBackup;
using BearBackup.Tools;
using BearBackup.Comparers;
using BearBackup.BasicData;

namespace BearBackupUI.Services;

public class BackupService
{
    public event EventHandler? BackupRepoChanged;
    public event EventHandler<BackupItemRecord[]>? FailedToLoad;
    public OrderedDictionary<BackupItemRecord, IBackup> BackupRepos
    {
        get
        {
            if (_backupRepos is null)
            {
                _backupRepos = [];
                var items = _configService.BackupItemRecords;

                IFileComparer fileComparer = _configService.CheckHash ? new TightFileComparer() : new LooseFileComparer();

                var failed = new List<BackupItemRecord>();
                foreach (var item in items)
                {
                    try
                    {
                        IBackup backup;
                        if (item.Item.RepoType == BackupRepoType.Mirroring)
                        {
                            var mirror = MirroringBackup.Open(item.Item.BackupPath, cacheMode: true);
                            mirror.FileComparer = fileComparer;
                            backup = mirror;
                        }
                        else if (item.Item.RepoType == BackupRepoType.Versioning)
                        {
                            var version = VersioningBackup.Open(item.Item.BackupPath, cacheMode: true);
                            version.FileComparer = fileComparer;
                            backup = version;
                        }
                        else
                        {
                            throw new NotImplementedException();
                        }

                        _backupRepos.Add(item, backup);
                    }
                    catch (Exception e)
                    {
                        Logging.Error($"Failed to open `{item.Item.BackupPath}`. {e.Message}");
                        failed.Add(item);
                    }
                }
                if (failed.Count > 0)
                    FailedToLoad?.Invoke(this, [.. failed]);

                return _backupRepos;
            }
            else
            {
                return _backupRepos;
            }
        }
    }

    private readonly ConfigService _configService;
    private readonly List<OnUsingToken> _onUsingTokens;
    private OrderedDictionary<BackupItemRecord, IBackup>? _backupRepos;

    public BackupService(ConfigService configService)
    {
        _configService = configService;
        _onUsingTokens = [];
    }

    public IBackup GetRepo(int id)
    {
        if (_backupRepos is null)
        {
            var record = _configService.BackupItemRecords.First(i => i.ID == id);

            IBackup backup;
            if (record.Item.RepoType == BackupRepoType.Mirroring)
                backup = MirroringBackup.Open(record.Item.BackupPath, cacheMode: false);
            else if (record.Item.RepoType == BackupRepoType.Versioning)
                backup = VersioningBackup.Open(record.Item.BackupPath, cacheMode: false);
            else
                throw new NotImplementedException();

            return backup;
        }
        else
        {
            var pair = _backupRepos.First(pair => pair.Key.ID == id);
            return pair.Value;
        }
    }

    public int AddExistsRepo(BackupItem backupItem)
    {
        IBackup backup;
        if (backupItem.RepoType == BackupRepoType.Mirroring)
            backup = MirroringBackup.Open(backupItem.BackupPath, cacheMode: true);
        else if (backupItem.RepoType == BackupRepoType.Versioning)
            backup = VersioningBackup.Open(backupItem.BackupPath, cacheMode: true);
        else
            throw new NotImplementedException();

        var id = _configService.AddBackupItemRecord(backupItem);
        _backupRepos?.Add(new BackupItemRecord { ID = id, Item = backupItem }, backup);
        BackupRepoChanged?.Invoke(this, new EventArgs());

        return id;
    }

    public int CreateRepo(BackupItem backupItem)
    {
        IBackup backup;
        if (backupItem.RepoType == BackupRepoType.Mirroring)
            backup = MirroringBackup.Create(backupItem.BackupPath, cacheMode: true);
        else if (backupItem.RepoType == BackupRepoType.Versioning)
            backup = VersioningBackup.Create(backupItem.BackupPath, cacheMode: true);
        else
            throw new NotImplementedException();

        var id = _configService.AddBackupItemRecord(backupItem);
        _backupRepos?.Add(new BackupItemRecord { ID = id, Item = backupItem }, backup);
        BackupRepoChanged?.Invoke(this, new EventArgs());

        return id;
    }

    public void ChangeRepoConfig(int id, BackupItem backupItem)
    {
        _configService.ChangeBackupItemRecord(id, backupItem);
        if (_backupRepos is not null)
        {
            var pair = _backupRepos.First(pair => pair.Key.ID == id);
            var index = _backupRepos.IndexOf(pair.Key);
            _backupRepos.RemoveAt(index);
            _backupRepos.Insert(index, new BackupItemRecord { ID = id, Item = backupItem }, pair.Value);
        }
        BackupRepoChanged?.Invoke(this, new EventArgs());
    }

    public void RemoveRepo(int id)
    {
        _configService.RemoveBackupItemRecord(id);
        if (_backupRepos is not null)
        {
            var pair = _backupRepos.First(pair => pair.Key.ID == id);
            var index = _backupRepos.IndexOf(pair.Key);
            _backupRepos.RemoveAt(index);
        }
        BackupRepoChanged?.Invoke(this, new EventArgs());
    }

    public void AddOnUsingToken(OnUsingToken token)
    {
        _onUsingTokens.Add(token);
    }

    public void ClearCache()
    {
        if (_onUsingTokens.All(t => t.IsDisposed))
        {
            if (_backupRepos is not null)
            {
                foreach ((_, var repo) in _backupRepos)
                {
                    if (repo is MirroringBackup mirror)
                        mirror.ClearCaches();
                    else if (repo is VersioningBackup version)
                        version.ClearCaches();
                    else
                        throw new NotImplementedException();
                }
            }

            _backupRepos = null;
            _onUsingTokens.Clear();
			GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive);
		}
    }
}

public class OnUsingToken
{
    public bool IsDisposed { get; private set; }

    public OnUsingToken() { }

    public void Dispose()
    {
        IsDisposed = true;
    }
}