using BearBackup.BasicData;
using BearBackup.Comparers;
using BearBackup.Task;
using BearBackup.Tools;
using System.Text.RegularExpressions;

namespace BearBackup;

public partial class VersioningBackup : IBackup
{
	public string Name { get; }
	public string Path { get; }
	public string BlobPath { get; }
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
	private OrderedDictionary<RecordInfo, Index?>? _recordsCache;
	private string[]? _blobHashesCache;
	private Ignore? _ignoreCache;
	private bool _cacheMode;

	private VersioningBackup(string path, bool cacheMode)
	{
		var dirInfo = new DirectoryInfo(path);

		if (!dirInfo.Exists) throw new ArgumentException("Path does not exist.");
		Path = path;
		Name = dirInfo.Name;

		BlobPath = System.IO.Path.Combine(path, Environment.Blob);
		if (!Directory.Exists(BlobPath)) throw new BadBackupException("Blobs not found. Repository is broken.");

		IndexPath = System.IO.Path.Combine(path, Environment.Index);
		if (!Directory.Exists(IndexPath)) throw new BadBackupException("Indexes not found. Repository is broken.");

		RecordPath = System.IO.Path.Combine(path, Environment.Record);
		if (!File.Exists(RecordPath)) throw new BadBackupException("Record not found. Repository is broken.");

		BackupIgnorePath = System.IO.Path.Combine(path, Environment.BackupIgnore);
		if (!File.Exists(BackupIgnorePath)) BackupIgnorePath = null;

		DirComparer = new GeneralDirComparer();
		FileComparer = new LooseFileComparer();
		_cacheMode = cacheMode;
	}

	public static VersioningBackup Create(string path, bool cacheMode = false)
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

		var blobPath = System.IO.Path.Combine(path, Environment.Blob);
		var indexPath = System.IO.Path.Combine(path, Environment.Index);
		var recordPath = System.IO.Path.Combine(path, Environment.Record);

		Directory.CreateDirectory(blobPath);
		Directory.CreateDirectory(indexPath);
		File.Create(recordPath).Close();

		return new VersioningBackup(path, cacheMode);
	}

	public static VersioningBackup Open(string path, bool cacheMode = false)
	{
		return new VersioningBackup(path, cacheMode);
	}

	public RecordInfo[]? GetRecordInfo()
	{
		if (_recordsCache is null)
		{
			var records = Writer.ReadRecordFile(RecordPath);
			if (records is null) return null;

			if (_cacheMode)
			{
				_recordsCache = [];
				foreach (var record in records)
				{
					_recordsCache.Add(record, null);
				}
			}
			return records;
		}

		return [.. _recordsCache.Keys];
	}

	public Index GetIndex(RecordInfo recordInfo, bool threadSafe = true)
	{
		var recs = GetRecordInfo();
		if (recs is null || !recs.Contains(recordInfo))
			throw new BadBackupException("The specified record not found.");

		if (_recordsCache is not null && _recordsCache[recordInfo] is not null)
		{
#pragma warning disable CS8603
			return _recordsCache[recordInfo];
#pragma warning restore CS8603
		}

		var indexPath = System.IO.Path.Combine(IndexPath, recordInfo.Name);
		if (!File.Exists(indexPath)) throw new BadBackupException("No index file associates with the specified record.");

		var index = Writer.ReadIndexFile(indexPath, threadSafe) ?? throw new BadBackupException("Index file is broken.");

		if (_recordsCache is not null) _recordsCache[recordInfo] = index;

		return index;
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

	public string[] GetBlobHashes()
	{
		if (_blobHashesCache is null)
		{
			var basePath = BlobPath.InsertPathSepAtEnd();

			var blobHashes = Directory.EnumerateFiles(BlobPath, "*.*", SearchOption.AllDirectories)
									  .Select(path =>
									  {
										  var code = path[BlobPath.Length..].Replace(
											  System.IO.Path.DirectorySeparatorChar.ToString(), string.Empty);
										  if (code.Length != 40 || !_hexRegex().IsMatch(code))
											  throw new BadBackupException($"Blob file is broken.");

										  return code;
									  })
									  .ToArray();

			if (_cacheMode) _blobHashesCache = blobHashes;
			return blobHashes;
		}
		else
		{
			return _blobHashesCache;
		}
	}

	internal string[] GetBlobPrefixes()
	{
		var basePath = BlobPath.InsertPathSepAtEnd();

		return Directory.EnumerateDirectories(BlobPath)
						.Select(p =>
						{
							var prefix = p[basePath.Length..].RemovePathSepAtEnd();
							if (prefix.Length != 2 || !_hexRegex().IsMatch(prefix))
								throw new BadBackupException("Blob structure is broken");
							return prefix;
						})
						.ToArray();
	}

	public void ClearCaches()
	{
		_recordsCache = null;
		_blobHashesCache = null;
		_ignoreCache = null;

		GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive);
	}

	public IBackupTask GenerateBackupTask(string backupTarget, RecordInfo recordInfo)
	{
		return new VersioningBackupTask(this, backupTarget, recordInfo);
	}

	public IRemoveTask GenerateRemoveTask(RecordInfo recordInfo)
	{
		return new VersioningRemoveTask(this, recordInfo);
	}

	public IRemoveTask GenerateRemoveTask(RecordInfo[] recordInfoArr)
	{
		return new VersioningRemoveTask(this, recordInfoArr);
	}

	public IRestoreTask GenerateRestoreTask(string restorePath, Index index)
	{
		return new VersioningRestoreTask(this, restorePath, index);
	}

	public IRestoreTask GenerateRestoreTask(string restorePath, (Index index, FileInfo[]) files)
	{
		return new VersioningRestoreTask(this, restorePath, files);
	}

	[GeneratedRegex(@"^[0-9a-f]+$")]
	private static partial Regex _hexRegex();
}
