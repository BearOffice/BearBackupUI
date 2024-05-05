using BearBackup.BasicData;
using BearBackup.Comparers;
using BearBackup.Task;
using BearBackup.Tools;
using BearMarkupLanguage;
using System.IO;

namespace BearBackup;

public class MirroringBackup : IBackup
{
    public string Name { get; }
    public string Path { get; }
    public string MirrorPath { get; }
    public string IndexPath { get; }
    public string RecordPath { get; }
    public string? BackupIgnorePath { get; private set; }
    public IDirComparer DirComparer { get; set; }
    public IFileComparer FileComparer { get; set; }
    public bool CacheMode
    {
        get => _cacheMode;
        set
        {
            _cacheMode = value;
            if (!_cacheMode) ClearCaches();
        }
    }
    private RecordInfo? _recordCache;
    private Index? _indexCache;
    private Ignore? _ignoreCache;
    private bool _cacheMode;

    private MirroringBackup(string path, bool cacheMode)
    {
        var dirInfo = new DirectoryInfo(path);

        if (!dirInfo.Exists) throw new ArgumentException("Path does not exist.");
        Path = path;
        Name = dirInfo.Name;

        MirrorPath = System.IO.Path.Combine(path, Environment.Mirror);
        if (!Directory.Exists(MirrorPath)) throw new BadBackupException("Mirror not found. Repository is broken.");

        IndexPath = System.IO.Path.Combine(path, Environment.Index);
        if (!File.Exists(IndexPath)) throw new BadBackupException("Index not found. Repository is broken.");

        RecordPath = System.IO.Path.Combine(path, Environment.Record);
        if (!File.Exists(RecordPath)) throw new BadBackupException("Record not found. Repository is broken.");

        BackupIgnorePath = System.IO.Path.Combine(path, Environment.BackupIgnore);
        if (!File.Exists(BackupIgnorePath)) BackupIgnorePath = null;

        DirComparer = new GeneralDirComparer();
        FileComparer = new LooseFileComparer();
        _cacheMode = cacheMode;
    }

    public static MirroringBackup Create(string path, bool cacheMode = false)
    {
        var dirInfo = new DirectoryInfo(path);
        if (dirInfo.Exists)
        {
            if (!dirInfo.IsEmpty())
                throw new BadBackupException($"Directory `{path}` must be empty before initialization.");
        }
        else
        {
            dirInfo.Create();
        }

        var mirrorPath = System.IO.Path.Combine(path, Environment.Mirror);
        var indexPath = System.IO.Path.Combine(path, Environment.Index);
        var recordPath = System.IO.Path.Combine(path, Environment.Record);

        Directory.CreateDirectory(mirrorPath);
        File.Create(indexPath).Close();
        File.Create(recordPath).Close();

        return new MirroringBackup(path, cacheMode);
    }

    public static MirroringBackup Open(string path, bool cacheMode = false)
    {
        return new MirroringBackup(path, cacheMode);
    }

    public RecordInfo? GetRecordInfo()
    {
        if (_recordCache is null)
        {
            var records = Writer.ReadRecordFile(RecordPath);
            if (records is null) return null;

            if (records.Length != 1)
                throw new BadBackupException("Record file is broken.");

            if (_cacheMode) _recordCache = records[0];
            return records[0];
        }

        return _recordCache;
    }

    public Index? GetIndex()
    {
        // Return null if no record associates with index.
        if (GetRecordInfo() is null) return null;

        if (_indexCache is null)
        {
            var index = Writer.ReadIndexFile(IndexPath);
            if (index is null) return null;

            if (_cacheMode) _indexCache = index;
            return index;
        }

        return _indexCache;
    }

    public Ignore? GetIgnore()
    {
        if (_ignoreCache is null)
        {
            if (BackupIgnorePath is null) return null;

            var ignore = Writer.ReadIgnoreFile(BackupIgnorePath);
            if (ignore is null) return null;

            if (_cacheMode) _ignoreCache = ignore;
            return ignore;
        }

        return _ignoreCache;
    }

    public void SetIgnore(Ignore ignore)
    {
        BackupIgnorePath ??= System.IO.Path.Combine(Path, Environment.BackupIgnore);
        Writer.WriteIgnore(BackupIgnorePath, ignore);
        _ignoreCache = null;
    }

    public void RemoveIgnore()
    {
        if (BackupIgnorePath is null) return;
        
        File.Delete(BackupIgnorePath);
        BackupIgnorePath = null;
        _ignoreCache = null;
    }

    public void ClearCaches()
    {
        _recordCache = null;
        _indexCache = null;
        _ignoreCache = null;
    }

    public IBackupTask GenerateBackupTask(string backupTarget, RecordInfo recordInfo)
    {
        return new MirroringBackupTask(this, backupTarget, recordInfo);
    }

    public IRemoveTask GenerateRemoveTask(RecordInfo recordInfo)
    {
        return new MirroringRemoveTask(this, recordInfo);
    }

    public IRestoreTask GenerateRestoreTask(string restorePath, Index index)
    {
        return new MirroringRestoreTask(this, restorePath, index);
    }

    public IRestoreTask GenerateRestoreTask(string restorePath, (Index index, FileInfo[]) files)
    {
        return new MirroringRestoreTask(this, restorePath, files);
    }
}
