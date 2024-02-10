using BearBackup.BasicData;
using BearBackup.Tools;
using System.Runtime;

namespace BearBackup.Task;

public class VersioningRemoveTask : IRemoveTask
{
    public bool IsCompleted { get; private set; }
    public event Action<ProgressEventArgs>? Progress;
    private readonly VersioningBackup _backup;
    private readonly RecordInfo _recordToRemove;

    internal VersioningRemoveTask(VersioningBackup backup, RecordInfo recordToRemove)
    {
        _backup = backup;
        _recordToRemove = recordToRemove;
    }

    public void Execute(out ExceptionInfo[] exceptions)
    {
        if (IsCompleted) throw new Exception("Task already completed.");
        AddIndeterminateEvent();

        var es = new List<ExceptionInfo>();

        var recordInfoArr = _backup.GetRecordInfo();
        if (recordInfoArr is null || !recordInfoArr.Contains(_recordToRemove))
            throw new BadBackupException("The record specified does not exist.");

        var mirrorIndex = _backup.GetIndex(_recordToRemove) ??
            throw new BadBackupException("No index is associated with the given record. Repository is broken.");

        var remainHashes = new HashSet<string>();
        foreach (var record in recordInfoArr)
        {
            if (record == _recordToRemove) continue;

            var index = _backup.GetIndex(record) ??
                throw new BadBackupException("No index is associated with the record. Repository is broken."); ;

            foreach ((_, var fileInfo) in index.GetAllFileInfo())
            {
                remainHashes.Add(fileInfo.SHA1 ?? throw new BadBackupException("Hash code not found. Index file is broken."));
            }
        }

        var hashToRemove = mirrorIndex.GetAllFileInfo()
                                      .Select(f => f.Item2.SHA1 ??
                                           throw new BadBackupException("Hash code not found. Index file is broken."))
                                      .ToHashSet()
                                      .Except(remainHashes)
                                      .ToArray();

        var totalNum = hashToRemove.Length;
        var count = 0;
        AddEvent(totalNum, count, true);

        foreach (var hash in hashToRemove)
        {
            (var prefix, var name) = hash.SplitAt(2);
            var deletePath = Path.Combine(_backup.BlobPath, prefix, name);

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

        // Remove empty dirs in blob dir.
        RemoveEmptyDirectories(_backup.BlobPath);

        var indexToRemove = Path.Combine(_backup.IndexPath, _recordToRemove.Name);
        File.Delete(indexToRemove);
        Writer.WriteRecordInfo(_backup.RecordPath, recordInfoArr.Where(i => i != _recordToRemove).ToArray());
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

    private static void RemoveEmptyDirectories(string path)
    {
        IEnumerable<string> subPaths;
        try
        {
            subPaths = Directory.EnumerateDirectories(path);
        }
        catch
        {
            return;
        }

        foreach (var subPath in subPaths)
        {
            try
            {
                var isEmpty = !(Directory.EnumerateFiles(subPath).Any() || Directory.EnumerateDirectories(subPath).Any());
                if (isEmpty) Directory.Delete(subPath);
            }
            catch { }
        }
    }
}
