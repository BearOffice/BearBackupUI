using BearBackup.BasicData;
using BearBackup.Tools;

namespace BearBackup.Task;

public class VersioningBackupTask : IBackupTask
{
    public bool IsCompleted { get; private set; }
    public event Action<ProgressEventArgs>? Progress;
    private readonly VersioningBackup _backup;
    private readonly string _backupTarget;
    private readonly RecordInfo _recordInfo;

    internal VersioningBackupTask(VersioningBackup backup, string backupTarget, RecordInfo recordInfo)
    {
        _backup = backup;
        _backupTarget = backupTarget.InsertPathSepAtEnd();
        _recordInfo = recordInfo;
    }

    public void Execute(out ExceptionInfo[] exceptions)
    {
        if (IsCompleted) throw new Exception("Task already completed.");
        AddIndeterminateEvent();

        var recordInfoArr = _backup.GetRecordInfo();
        if (recordInfoArr is not null && recordInfoArr.Contains(_recordInfo))
            throw new BadBackupException("Record name must be unique.");

        var es = new List<ExceptionInfo>();

        // Index that saved always contains hash information.
        var mirrorIndex = recordInfoArr is null ? new Index() : _backup.GetIndex(recordInfoArr[^1]);
        var targetIndex = IndexBuilder.Build(_backupTarget, out var exArr, ignore: _backup.GetIgnore());
        es.AddRange(exArr);

        if (_backup.FileComparer.CompareHash)
        {
            IndexBuilder.CalculateAllFilesHash(_backupTarget, targetIndex, out exArr);
            DropFiles(targetIndex, exArr);
            es.AddRange(exArr);
        }

        // Diff without considering attr changes.
        (_, var targetFileUnique) = IndexComparison.DiffFileInfo(mirrorIndex, targetIndex,
            _backup.FileComparer, _backup.DirComparer, considerAttr: false);

        // Complete lacking hashes.
        if (!_backup.FileComparer.CompareHash)
        {
            var uni = targetFileUnique.Aggregate(new List<(string, FileInfo)>(), (acc, u) =>
            {
                acc.AddRange(u.Item2.Select(i => (u.Item1.GetFileFullName(i), i)));
                return acc;
            }).ToArray();

            IndexBuilder.CalculateAllFilesHash(_backupTarget, uni, out exArr);
            if (exArr.Length > 0)
            {
                DropFiles(targetIndex, exArr);
                // Recalculate targetFileUnique since DropFiles method only deal with index.
                (_, targetFileUnique) = IndexComparison.DiffFileInfo(mirrorIndex, targetIndex,
                    _backup.FileComparer, _backup.DirComparer, considerAttr: false);
                es.AddRange(exArr);
            }

            (var mirrorFileIntersect, var targetFileIntersect) = IndexComparison.IntersectFileInfo(mirrorIndex, targetIndex,
                _backup.FileComparer, _backup.DirComparer, considerAttr: false);

            foreach (((var mIndex, var mFiles), (var tIndex, var tFiles)) in mirrorFileIntersect.Zip(targetFileIntersect))
            {
                if (mIndex.DirInfo?.FullName != tIndex.DirInfo?.FullName)
                    throw new Exception("Inconsistency occurred. Index file is broken.");

                foreach ((var mFile, var tFile) in mFiles.Zip(tFiles))
                {
                    if (mFile.Name != tFile.Name)
                        throw new Exception("Inconsistency occurred. Index file is broken.");

                    tFile.SetHash(mFile.SHA1 ?? throw new BadBackupException("Hash code not found. Index file is broken."));
                }
            }
        }

        var totalNum = targetFileUnique.Length;
        var count = 0;
        AddEvent(totalNum, count, true);

        var blobHashes = _backup.GetBlobHashes().ToHashSet();
        var existPrefixes = _backup.GetBlobPrefixes().ToHashSet();

        foreach ((var subIndex, var fileInfoArr) in targetFileUnique)
        {
            foreach (var fileInfo in fileInfoArr)
            {
                var hash = fileInfo.SHA1 ?? throw new Exception("Unreachable.");
                if (blobHashes.Contains(hash)) continue;

                var fileFullName = subIndex.GetFileFullName(fileInfo);
                var sourcePath = Path.Combine(_backupTarget, fileFullName);

                (var prefix, var name) = hash.SplitAt(2);
                var createPath = Path.Combine(_backup.BlobPath, prefix, name);
                try
                {
                    if (!existPrefixes.Contains(prefix))
                    {
                        Directory.CreateDirectory(Path.Combine(_backup.BlobPath, prefix));
                        existPrefixes.Add(prefix);
                    }

                    File.Copy(sourcePath, createPath);
                    File.SetAttributes(createPath, FileAttributes.Normal);
                }
                catch (Exception e)
                {
                    subIndex.RemoveFileInfo(fileInfo);
                    es.Add(new ExceptionInfo(createPath, FileType.File, e));
                }

                blobHashes.Add(hash);
            }

            count++;
            AddEvent(totalNum, count, true);
        }

        Writer.WriteIndex(Path.Combine(_backup.IndexPath, _recordInfo.Name), targetIndex);
        if (recordInfoArr is null)
            Writer.WriteRecordInfo(_backup.RecordPath, _recordInfo);
        else
            Writer.WriteRecordInfo(_backup.RecordPath, [.. recordInfoArr, _recordInfo]);
        _backup.ClearCaches();

        exceptions = [.. es];
        IsCompleted = true;
        AddEvent(totalNum, count, false);
    }

    private void AddEvent(int totalNum, int completedNum, bool isProgressing)
    {
        Progress?.Invoke(new ProgressEventArgs
        {
            IsDeterminate = true,
            TotalNum = totalNum,
            CompletedNum = completedNum,
            IsProgressing = isProgressing,
        });
    }

    private void AddIndeterminateEvent()
    {
        Progress?.Invoke(new ProgressEventArgs
        {
            IsDeterminate = false,
            TotalNum = 0,
            CompletedNum = 0,
            IsProgressing = true,
        });
    }

    private void DropFiles(Index index, ExceptionInfo[] exceptions)
    {
        foreach (var ex in exceptions)
        {
            if (!ex.FileType.HasFlag(FileType.File)) continue;

            var relativePath = ex.Path[_backupTarget.Length..];
            var parIndex = index.GetSubIndex(Path.GetDirectoryName(relativePath));
            parIndex?.RemoveFileInfo(Path.GetFileName(relativePath));
        }
    }
}
