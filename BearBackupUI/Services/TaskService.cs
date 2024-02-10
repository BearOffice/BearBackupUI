using BearBackup.Task;
using BearBackup.BasicData;
using BearBackup;
using System.IO;
using System.Text;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System;

namespace BearBackupUI.Services;

public class TaskService
{
    public event EventHandler<ProgressEventArgs>? Executing;
    public event EventHandler? TasksChanged;
    public event EventHandler<Exception>? FaultOccurred;
    public (BackupItemRecord, ITask)? RunningTask { get; private set; }
    public (BackupItemRecord, ITask)[] TaskQueue { get => [.. _waitingTasks]; }
    public CompletedTaskInfo[] CompletedTasks { get => [.. _completedTasks]; }
    public bool IsRunning { get => RunningTask is not null; }
    private readonly BackupService _backupService;
    private readonly ObservableCollection<(BackupItemRecord, ITask)> _waitingTasks;
    private readonly List<CompletedTaskInfo> _completedTasks;
    private readonly Timer _timer;
    private readonly object _locker;
    private OnUsingToken? _token;

    public TaskService(BackupService backupService)
    {
        _backupService = backupService;
        _waitingTasks = [];
        _completedTasks = [];
        _timer = new Timer(ScheduledTasks, null, TimeSpan.FromSeconds(3), TimeSpan.FromMinutes(50));
        _locker = new object();

        _waitingTasks.CollectionChanged += (_, _) => TasksChanged?.Invoke(this, new EventArgs());
    }

    private void ScheduledTasks(object? state)
    {
        foreach ((var record, var repo) in _backupService.BackupRepos)
        {
            var period = record.Item.ScheduledPeriod;
            if (period == null) continue;

            var last = record.Item.LastBackupDateTime;
            if (last is null)
            {
                lock (_locker)
                {
                    _waitingTasks.Add((record,
                    repo.GenerateBackupTask(record.Item.BackupTarget, new RecordInfo(GenerateBackupRecordName()))));
                }
            }
            else
            {
                var nextBackup = ((DateTime)last).AddHours((int)period);
                if (nextBackup < DateTime.UtcNow)
                {
                    lock (_locker)
                    {
                        _waitingTasks.Add((record,
                            repo.GenerateBackupTask(record.Item.BackupTarget, new RecordInfo(GenerateBackupRecordName()))));
                    }
                }
            }
        }

        StartTasks();
    }

    public void AddTask(BackupItemRecord record, ITask task)
    {
        lock (_locker)
        {
            _waitingTasks.Add((record, task));
        }
        StartTasks();
    }

    public void MoveUpTaskAt(int index)
    {
        lock (_locker)
        {
            if (index < 0 || index >= _waitingTasks.Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            if (index == 0) return;

            var item = _waitingTasks[index];
            _waitingTasks.Move(index, index - 1);
        }
    }

    public void MoveUpTask(ITask task)
    {
        lock (_locker)
        {
            var result = _waitingTasks.ToList().FindIndex(item => item.Item2 == task);

            if (result == -1) throw new ArgumentException("Task specified not exists.", nameof(task));
            if (result == 0) return;

            var item = _waitingTasks[result];
            _waitingTasks.Move(result, result - 1);
        }
    }

    public void MoveDownTaskAt(int index)
    {
        lock (_locker)
        {
            if (index < 0 || index >= _waitingTasks.Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            if (index == _waitingTasks.Count - 1) return;

            var item = _waitingTasks[index];
            _waitingTasks.Move(index, index + 1);
        }
    }

    public void MoveDownTask(ITask task)
    {
        lock (_locker)
        {
            var result = _waitingTasks.ToList().FindIndex(item => item.Item2 == task);

            if (result == -1) throw new ArgumentException("Task specified not exists.", nameof(task));
            if (result == _waitingTasks.Count - 1) return;

            var item = _waitingTasks[result];
            _waitingTasks.Move(result, result + 1);
        }
    }

    public void RemoveTaskAt(int index)
    {
        lock (_locker)
        {
            if (index < 0 || index >= _waitingTasks.Count)
                throw new ArgumentOutOfRangeException(nameof(index));

            _waitingTasks.RemoveAt(index);
        }
    }

    public bool RemoveTask(ITask task)
    {
        lock (_locker)
        {
            var result = _waitingTasks.ToList().FindIndex(item => item.Item2 == task);

            if (result == -1) return false;
            _waitingTasks.RemoveAt(result);
        }

        return true;
    }

    public void ClearAllTasks()
    {
        lock (_locker)
        {
            _waitingTasks.Clear();
        }
    }

    private void StartTasks()
    {
        lock (_locker)
        {
            if (_token is not null || _waitingTasks.Count == 0) return;
        }
        _token = new OnUsingToken();
        _backupService.AddOnUsingToken(_token);

        Task.Run(() =>
        {
            ProgressEventArgs? finalArgs = null;

            var count = 0;
            lock (_locker) { count = _waitingTasks.Count; }
            while (count > 0)
            {
                lock (_locker)
                {
                    RunningTask = _waitingTasks[0];
                    _waitingTasks.RemoveAt(0);
                }

                var task = RunningTask.Value.Item2;
                task.Progress += args =>
                {
                    if (args.IsProgressing)
                        Executing?.Invoke(this, args);
                    else
                        finalArgs = args;
                };

                ExceptionInfo[]? exceptions = null;
                try
                {
                    task.Execute(out var ex);
                    exceptions = ex ?? [];
                }
                catch (Exception e)
                {
                    Logging.Critical($"Fatal failed on running task `{task}`. {e.Message}");
                    FaultOccurred?.Invoke(this, e);
                }

                var isFaulted = exceptions is null;  // `exceptions` will be assigned if task executed successfully.
                if (exceptions is not null)
                {
                    foreach (var exception in exceptions)
                    {
                        Logging.Error($"Failed on `{exception.Path}`. {exception.Exception.Message}");
                    }
                }

                var record = RunningTask.Value.Item1;

                if (!isFaulted && task is IBackupTask)
                    _backupService.ChangeRepoConfig(record.ID, record.Item with { LastBackupDateTime = DateTime.UtcNow });

                lock (_locker) { count = _waitingTasks.Count; }
                if (count != 0) Executing?.Invoke(this, finalArgs ?? 
                    new ProgressEventArgs { IsDeterminate = true, IsProgressing = false });

                _completedTasks.Add(GenerateCompletedTaskInfo(
                    record, task, exceptions is null ? 0 : exceptions.Length, isFaulted));

                if (!isFaulted && task is IBackupTask)
                    Logging.Info($"Backup task finished. Backup path: `{record.Item.BackupPath}`");
            }

            RunningTask = null;
            _token.Dispose();
            _token = null;
            _backupService.ClearCache();
            Executing?.Invoke(this, finalArgs ?? new ProgressEventArgs { IsDeterminate = true, IsProgressing = false });

            StartTasks();  // Make sure no tasks left.
        });
    }

    private static string GenerateBackupRecordName()
    {
        var dateTime = DateTime.Now;

        var sb = new StringBuilder("scheduled backup ");
        sb.Append(dateTime.Year.ToString("D4"))
          .Append(dateTime.Month.ToString("D2"))
          .Append(dateTime.Day.ToString("D2"))
          .Append('-')
          .Append(dateTime.Hour.ToString("D2"))
          .Append(dateTime.Minute.ToString("D2"))
          .Append(dateTime.Second.ToString("D2"));

        return sb.ToString();
    }

    private static CompletedTaskInfo GenerateCompletedTaskInfo(
        BackupItemRecord record, ITask task, int exceptionNum, bool isFaulted)
    {
        return new CompletedTaskInfo
        {
            TaskType = GetTaskType(task),
            BackupPath = record.Item.BackupPath,
            RepoType = record.Item.RepoType,
            BackupTarget = record.Item.BackupTarget,
            FailureCount = exceptionNum,
            IsFaulted = isFaulted,
            CompletedTime = DateTime.Now,
        };
    }

    public static TaskType GetTaskType(ITask task)
    {
        if (task is IBackupTask) return TaskType.BackupTask;
        else if (task is IRemoveTask) return TaskType.RemoveTask;
        else if (task is IRestoreTask) return TaskType.RestoreTask;

        return TaskType.UnknownTask;
    }
}

public class CompletedTaskInfo
{
    public required TaskType TaskType { get; init; }
    public required string BackupPath { get; init; }
    public required BackupRepoType RepoType { get; init; }
    public required string BackupTarget { get; init; }
    public required int FailureCount { get; init; }
    public required bool IsFaulted { get; init; }
    public required DateTime CompletedTime { get; init; }
}

public enum TaskType
{
    BackupTask,
    RemoveTask,
    RestoreTask,
    UnknownTask,
}