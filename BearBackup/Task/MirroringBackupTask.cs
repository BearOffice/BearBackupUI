using BearBackup.BasicData;
using BearBackup.Tools;
using System.Linq;
using System.Runtime;

namespace BearBackup.Task;

public class MirroringBackupTask : IBackupTask
{
    public bool IsCompleted { get; private set; }
    public event Action<ProgressEventArgs>? Progress;
    private readonly MirroringBackup _backup;
    private readonly string _backupTarget;
    private readonly RecordInfo _recordInfo;

    internal MirroringBackupTask(MirroringBackup backup, string backupTarget, RecordInfo recordInfo)
    {
        _backup = backup;
        _backupTarget = backupTarget.InsertPathSepAtEnd();
        _recordInfo = recordInfo;
    }

    public void Execute(out ExceptionInfo[] exceptions)
    {
        if (IsCompleted) throw new Exception("Task already completed.");
        AddIndeterminateEvent();

        var es = new List<ExceptionInfo>();

        var mirrorIndex = _backup.GetIndex() ?? new Index();
        var targetIndex = IndexBuilder.Build(_backupTarget, out var exArr, ignore: _backup.GetIgnore());
        es.AddRange(exArr);

        if (_backup.FileComparer.CompareHash)
        {
            IndexBuilder.CalculateAllFilesHash(_backup.MirrorPath, mirrorIndex, out exArr);
            DropFiles(targetIndex, exArr);
            es.AddRange(exArr);

            IndexBuilder.CalculateAllFilesHash(_backupTarget, targetIndex, out exArr);
            DropFiles(targetIndex, exArr);
            es.AddRange(exArr);
        }

        // Diff without considering attr changes.
        (var mirrorDirUnique, var targetDirUnique) = IndexComparison.DiffDirInfo(mirrorIndex, targetIndex,
            _backup.DirComparer, considerAttr: false);
        (var mirrorFileUnique, var targetFileUnique) = IndexComparison.DiffFileInfo(mirrorIndex, targetIndex,
            _backup.FileComparer, _backup.DirComparer, considerAttr: false);


        var totalNum = mirrorFileUnique.Concat(targetFileUnique)
                                       .Select(i => i.Item2.Length)
                                       .Aggregate(0, (acc, i) => acc += i)
                       + 2;  // 2 -> Treat mirror dirs + target dirs as 2 batches
        var count = 0;
        AddEvent(totalNum, count, true);

        foreach ((var subIndex, var fileInfoArr) in mirrorFileUnique)
        {
            foreach (var fileInfo in fileInfoArr)
            {
                var deletePath = Path.Combine(_backup.MirrorPath, subIndex.GetFileFullName(fileInfo));
                try
                {
                    File.Delete(deletePath);
                }
                catch (Exception e)
                {
                    es.Add(new ExceptionInfo(deletePath, FileType.File, e));
                }

                count++;
                AddEvent(totalNum, count, true);
            }
        }

        // reverse -> reverse [a/b, a/b/c] to [a/b/c, a/b] to ensure the deleted dir is empty (does not contain sub dir).
        foreach ((var _, var dirInfo) in mirrorDirUnique.Reverse())
        {
            var deletePath = Path.Combine(_backup.MirrorPath, dirInfo.FullName);
            try
            {
                Directory.Delete(deletePath);
            }
            catch (Exception e)
            {
                es.Add(new ExceptionInfo(deletePath, FileType.Dir, e));
            }
        }
        count++;
        AddEvent(totalNum, count, true);

        // The files or dirs failed to create cannot be left in the new index.
        foreach ((var _, var dirInfo) in targetDirUnique)
        {
            var createPath = Path.Combine(_backup.MirrorPath, dirInfo.FullName);
            try
            {
                Directory.CreateDirectory(createPath);
            }
            catch (Exception e)
            {
                var parIndex = targetIndex.GetSubIndex(Path.GetDirectoryName(dirInfo.FullName));
                parIndex?.RemoveSubIndex(dirInfo);

                es.Add(new ExceptionInfo(createPath, FileType.Dir, e));
            }
        }
        count++;
        AddEvent(totalNum, count, true);

        foreach ((var subIndex, var fileInfoArr) in targetFileUnique)
        {
            foreach (var fileInfo in fileInfoArr)
            {
                var fileFullName = subIndex.GetFileFullName(fileInfo);
                var sourcePath = Path.Combine(_backupTarget, fileFullName);
                var createPath = Path.Combine(_backup.MirrorPath, fileFullName);
                try
                {
                    File.Copy(sourcePath, createPath);
                    File.SetAttributes(createPath, FileAttributes.Normal);
                }
                catch (Exception e)
                {
                    subIndex.RemoveFileInfo(fileInfo);
                    es.Add(new ExceptionInfo(createPath, FileType.File, e));
                }

                count++;
                AddEvent(totalNum, count, true);
            }
        }

        Writer.WriteIndex(_backup.IndexPath, targetIndex);
        Writer.WriteRecordInfo(_backup.RecordPath, _recordInfo);
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
