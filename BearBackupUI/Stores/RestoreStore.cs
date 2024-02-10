using BearBackup;
using BearBackup.BasicData;
using BearBackupUI.Core;
using BearBackupUI.Core.Actions;
using BearBackupUI.Core.DataTags;
using BearBackupUI.Helpers;
using BearBackupUI.Services;

namespace BearBackupUI.Stores;

public class RestoreStore : IStore
{
    public event EventHandler<DataArgs>? Changed;
    private readonly DispatchCenter _dispatchCenter;
    private readonly BackupService _backupService;
    private readonly TaskService _taskService;

    public RestoreStore(DispatchCenter dispatchCenter, BackupService backupService, TaskService taskService)
    {
        _dispatchCenter = dispatchCenter;
        _dispatchCenter.AddListener(typeof(RestoreAction), ActionReceived);

        _backupService = backupService;
        _taskService = taskService;
    }

    private void ActionReceived(object? sender, ActionArgs e)
    {
        if (e.Type is not RestoreAction.Restore) return;

        var backupItemRecord = (BackupItemRecord)(e.GetData(RestoreTag.BackupItemRecord) ?? throw new NullReferenceException());
        var recordInfo = (RecordInfo)(e.GetData(RestoreTag.RecordInfo) ?? throw new NullReferenceException());
        var restorePath = (string)(e.GetData(RestoreTag.RestorePath) ?? throw new NullReferenceException());

        var repo = _backupService.GetRepo(backupItemRecord.ID);

        BearBackup.BasicData.Index? index = null;
        if (repo is MirroringBackup mirror)
            index = mirror.GetIndex();
        else if (repo is VersioningBackup version)
            index = version.GetIndex(recordInfo);

        if (index is null)
            throw new NotImplementedException();

        try
        {
            if (e.TryGetData(RestoreTag.RestoreAll, out _))
            {
                var task = repo.GenerateRestoreTask(restorePath, index);
                _taskService.AddTask(backupItemRecord, task);
            }
            else if (e.TryGetData(RestoreTag.RestoreDir, out var dirPathObj))
            {
                var dirPath = (string)(dirPathObj ?? throw new NullReferenceException());
                var subIndex = index.GetSubIndex(dirPath) ?? throw new Exception("SubIndex not found.");
                var task = repo.GenerateRestoreTask(restorePath, subIndex);
                _taskService.AddTask(backupItemRecord, task);
            }
            else if (e.TryGetData(RestoreTag.RestoreFile, out var filePathObj))
            {
                (var parentName, var fileName) = ((string?, string))(filePathObj ?? throw new NullReferenceException());
                if (parentName is null)
                {
                    var fileInfo = index.FileInfoArr.First(info => info.Name == fileName);
                    var task = repo.GenerateRestoreTask(restorePath, (index, [fileInfo]));
                    _taskService.AddTask(backupItemRecord, task);
                }
                else
                {
                    var subIndex = index.GetSubIndex(parentName) ?? throw new Exception("SubIndex not found.");
                    var task = repo.GenerateRestoreTask(restorePath, subIndex);
                    _taskService.AddTask(backupItemRecord, task);
                }
            }
            else
            {
                return;
            }
        }
        catch (Exception ex)
        {
            var dataIn = new DataArgs();
            dataIn.AddData(RestoreTag.FailedReasons, ex.Message);
            Changed?.Invoke(this, dataIn);
            return;
        }

        var data = new DataArgs();
        data.AddEmptyData(RestoreTag.SuccessConfirm);
        Changed?.Invoke(this, data);
    }

    public DataArgs GetData()
    {
        return new DataArgs();
    }

    public DataArgs GetData(int id, RecordInfo recordInfo)
    {
        var repo = _backupService.GetRepo(id);

        BearBackup.BasicData.Index? index = null;
        if (repo is MirroringBackup mirror)
            index = mirror.GetIndex();
        else if (repo is VersioningBackup version)
            index = version.GetIndex(recordInfo);

        if (index is null)
            throw new NotImplementedException();

        var data = new DataArgs();
        data.AddData(RestoreTag.Index, index);
        return data;
    }

    public void Dispose()
    {
        _dispatchCenter.RemoveListener(ActionReceived);

        Changed.UnsubscribeAll();
        Changed = null;

        GC.SuppressFinalize(this);
    }
}
