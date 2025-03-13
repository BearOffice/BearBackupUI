using BearBackup.BasicData;
using BearBackup.Tools;

namespace BearBackup.Task;

public class VersioningRemoveTask : IRemoveTask
{
    public bool IsCompleted { get; private set; }
    public event Action<ProgressEventArgs>? Progress;
    private readonly VersioningBackup _backup;
    private readonly RecordInfo[] _recordsToRemove;

    internal VersioningRemoveTask(VersioningBackup backup, RecordInfo recordToRemove)
    {
        _backup = backup;
        _recordsToRemove = [recordToRemove];
    }

    internal VersioningRemoveTask(VersioningBackup backup, RecordInfo[] recordsToRemove)
    {
        _backup = backup;
        _recordsToRemove = recordsToRemove;
    }

    public void Execute(out ExceptionInfo[] exceptions)
    {
        if (IsCompleted) throw new Exception("Task already completed.");
        if (_recordsToRemove.Length == 0)
        {
            IsCompleted = true;
            AddEvent(0, 0, false);
            exceptions = [];
            return;
        }

        AddIndeterminateEvent();

        var es = new List<ExceptionInfo>();

        var recordInfoArr = _backup.GetRecordInfo();
        if (recordInfoArr is null || _recordsToRemove.Any(i => !recordInfoArr.Contains(i)))
            throw new BadBackupException("The record specified does not exist.");

        var remainHashes = UnionHashes(recordInfoArr.Except(_recordsToRemove));
        var hashToRemove = UnionHashes(_recordsToRemove).Except(remainHashes)
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

        foreach (var r in _recordsToRemove)
        {
            var indexToRemove = Path.Combine(_backup.IndexPath, r.Name);
            File.Delete(indexToRemove);
        }
        Writer.WriteRecordInfo(_backup.RecordPath, recordInfoArr.Except(_recordsToRemove).ToArray());
        _backup.ClearCaches();

        exceptions = [.. es];
        IsCompleted = true;
        AddEvent(totalNum, count, false);
    }

    private string[] UnionHashes(IEnumerable<RecordInfo> recordInfoEnum)
    {
        var hashes = new HashSet<string>();
        var locker = new object();
        var gcCount = 0;
        var degree = System.Environment.ProcessorCount / 2 + 1;
        var options = new ParallelOptions { MaxDegreeOfParallelism = degree };

        Parallel.ForEach(recordInfoEnum, options, record =>
        {
            var index = _backup.GetIndex(record, threadSafe: false) ??
                throw new BadBackupException("No index is associated with the record. Repository is broken."); ;

            lock (locker)
            {
                foreach ((_, var fileInfo) in index.GetAllFileInfo())
                {
                    hashes.Add(
                        fileInfo.SHA1 ?? throw new BadBackupException("Hash code not found. Index file is broken."));
                }
            }

            index = null;
            if (Volatile.Read(ref gcCount) % degree == 0) GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive);
            Interlocked.Increment(ref gcCount);
        });

        return [.. hashes];
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
