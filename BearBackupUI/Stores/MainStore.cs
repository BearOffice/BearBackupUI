using BearBackupUI.Core.Actions;
using BearBackupUI.Core;
using BearBackupUI.Services;
using BearBackupUI.Core.DataTags;
using BearBackupUI.Helpers;
using BearBackup;
using BearBackup.BasicData;

namespace BearBackupUI.Stores;

public class MainStore : IStore
{
    public event EventHandler<DataArgs>? Changed;
    private readonly DispatchCenter _dispatchCenter;
    private readonly TaskService _taskService;
    private readonly BackupService _backupService;
    private readonly ConfigService _configService;
    private readonly OnUsingToken _token;
    private bool _isRunning;

    public MainStore(DispatchCenter dispatchCenter, 
        TaskService taskService, BackupService backupService, ConfigService configService)
    {
        _dispatchCenter = dispatchCenter;
        _dispatchCenter.AddListener(typeof(MainAction), ActionReceived);

        _taskService = taskService;
        _backupService = backupService;
        _configService = configService;

        _taskService.Executing += TaskService_Executing;
        _taskService.FaultOccurred += TaskService_FaultOccurred;

        _token = new OnUsingToken();
        _backupService.AddOnUsingToken(_token);
        _backupService.BackupRepoChanged += BackupRepoChanged;
        _backupService.FailedToLoad += BackupService_FailedToLoad;
    }

    private void TaskService_FaultOccurred(object? sender, Exception e)
    {
        var data = new DataArgs();
        data.AddData(MainTag.TaskFaulted, e);
        Changed?.Invoke(this, data);
    }

    private void BackupService_FailedToLoad(object? sender, BackupItemRecord[] e)
    {
        var data = new DataArgs();
        data.AddData(MainTag.FailedToLoadRepos, e);
        Changed?.Invoke(this, data);
    }

    private void TaskService_Executing(object? sender, ProgressEventArgs e)
    {
        // Record will remove the changes inside the repo and will have no effect on BackupRepoChanged.
        // -> manually trigger the update event.
        if (!e.IsProgressing) BackupRepoChanged(null, new EventArgs());

        if (!e.IsProgressing && _taskService.TaskQueue.Length == 0)
        {
            _isRunning = false;
            var data = new DataArgs();
            data.AddData(MainTag.TaskStatus, _isRunning);
            Changed?.Invoke(this, data);

            return;
        }

        if (!_isRunning)
        {
            _isRunning = true;
            var data = new DataArgs();
            data.AddData(MainTag.TaskStatus, _isRunning);
            Changed?.Invoke(this, data);
        }
    }

    private void BackupRepoChanged(object? sender, EventArgs e)
    {
        var data = new DataArgs();
        data.AddData(MainTag.Repos, _backupService.BackupRepos.Keys.ToArray());
        Changed?.Invoke(this, data);
    }

    private void ActionReceived(object? sender, ActionArgs e)
    {
        if (e.Type is MainAction.RequestRecord)
        {
            var data = new DataArgs();

            var item = (BackupItemRecord)(e.GetAnonymousData() ?? throw new NullReferenceException());
            var repo = _backupService.GetRepo(item.ID);
            if (repo is MirroringBackup mirror)
            {
                var info = mirror.GetRecordInfo();
                RecordInfo[] records = info is null ? [] : [info];
                data.AddData(MainTag.Records, records);
            }
            else if (repo is VersioningBackup version)
            {
                var info = version.GetRecordInfo();
                var records = info is null ? [] : info;
                data.AddData(MainTag.Records, records);
            }
            else
            {
                throw new NotImplementedException();
            }

            Changed?.Invoke(this, data);
        }
        else if (e.Type is MainAction.RemoveRepo)
        {
            var data = new DataArgs();

            var item = (BackupItemRecord)(e.GetAnonymousData() ?? throw new NullReferenceException());
            if (_taskService.RunningTask?.Item1.ID == item.ID || _taskService.TaskQueue.Any(i => i.Item1.ID == item.ID))
            {
                data.AddData(MainTag.RemoveRepoConfirm, false);
            }
            else
            {
                _backupService.RemoveRepo(item.ID);
                data.AddData(MainTag.RemoveRepoConfirm, true);
            }

            Changed?.Invoke(this, data);
        }
        else if (e.Type is MainAction.RemoveRecord)
        {
            (var item, var records) = ((BackupItemRecord, RecordInfo[]))(e.GetAnonymousData() ?? throw new NullReferenceException());
            var repo = _backupService.GetRepo(item.ID);
            if (repo is MirroringBackup mirror)
            {
                var task = mirror.GenerateRemoveTask(records);
                _taskService.AddTask(item, task);
            }
            else if (repo is VersioningBackup version)
            {
                var task = version.GenerateRemoveTask(records);
                _taskService.AddTask(item, task);
            }
            else
            {
                throw new NotImplementedException();
            }

            var data = new DataArgs();
            data.AddData(MainTag.RemoveRecordConfirm, true);
            Changed?.Invoke(this, data);
        }
        else if (e.Type is MainAction.RemoveFailedRepos)
        {
            var item = (BackupItemRecord[])(e.GetAnonymousData() ?? throw new NullReferenceException());
            foreach (var rec in item)
            {
                _configService.RemoveBackupItemRecord(rec.ID);
            }
        }
    }

    public DataArgs GetData()
    {
        var data = new DataArgs();
        data.AddData(MainTag.TaskStatus, _taskService.IsRunning || _taskService.TaskQueue.Length > 0);
        data.AddData(MainTag.Repos, _backupService.BackupRepos.Keys.ToArray());

        return data;
    }

    public void Dispose()
    {
        _token.Dispose();
        _backupService.ClearCache();

        _dispatchCenter.RemoveListener(ActionReceived);

        Changed.UnsubscribeAll();
        Changed = null;

        GC.SuppressFinalize(this);
    }
}