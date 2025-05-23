﻿using BearBackup.BasicData;

namespace BearBackup.Task;

public class MirroringRemoveTask : IRemoveTask
{
    public bool IsCompleted { get; private set; }
    public event Action<ProgressEventArgs>? Progress;
    private readonly MirroringBackup _backup;
    private readonly RecordInfo[] _recordsToRemove;

    internal MirroringRemoveTask(MirroringBackup backup, RecordInfo recordToRemove)
    {
        _backup = backup;
        _recordsToRemove = [recordToRemove];
    }

    internal MirroringRemoveTask(MirroringBackup backup, RecordInfo[] recordsToRemove)
    {
        if (recordsToRemove.Length > 1)
            throw new ArgumentException("The maximum number of records that can be removed is one.");
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

        var record = _backup.GetRecordInfo();
        if (record is null || record != _recordsToRemove[0])
            throw new BadBackupException("The record specified does not exist.");
        var mirrorIndex = _backup.GetIndex() ??
            throw new BadBackupException("No index is associated with the given record. Repository is broken.");

        var totalNum = mirrorIndex.FileInfoArr.Length + 1;  // 1 -> Treat dirs as 1 batch
        var count = 0;
        AddEvent(totalNum, count, true);

        foreach (var fileInfo in mirrorIndex.FileInfoArr)
        {
            var deletePath = Path.Combine(_backup.MirrorPath, mirrorIndex.GetFileFullName(fileInfo));
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

        foreach (var subIndex in mirrorIndex.SubIndexArr)
        {
            var deletePath = Path.Combine(_backup.MirrorPath,
                (subIndex.DirInfo ?? throw new Exception("Unreachable.")).FullName);
            try
            {
                Directory.Delete(deletePath, true);
            }
            catch (Exception e)
            {
                es.Add(new ExceptionInfo(deletePath, FileType.Dir, e));
            }
        }
        count++;
        AddEvent(totalNum, count, true);

        Writer.WriteIndex(_backup.IndexPath, null);
        Writer.WriteRecordInfo(_backup.RecordPath, default(RecordInfo));
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
}
