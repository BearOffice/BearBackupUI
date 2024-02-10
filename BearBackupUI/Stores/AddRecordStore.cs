using BearBackup;
using BearBackup.BasicData;
using BearBackupUI.Core;
using BearBackupUI.Core.Actions;
using BearBackupUI.Core.DataTags;
using BearBackupUI.Helpers;
using BearBackupUI.Services;

namespace BearBackupUI.Stores;

public class AddRecordStore : IStore
{
    public event EventHandler<DataArgs>? Changed;
    private readonly DispatchCenter _dispatchCenter;
    private readonly BackupService _backupService;
    private readonly TaskService _taskService;

    public AddRecordStore(DispatchCenter dispatchCenter, BackupService backupService, TaskService taskService)
    {
        _dispatchCenter = dispatchCenter;
        _dispatchCenter.AddListener(typeof(AddRecordAction), ActionReceived);

        _backupService = backupService;
        _taskService = taskService;
    }

    private void ActionReceived(object? sender, ActionArgs e)
    {
        if (e.Type is not AddRecordAction.Add) return;

        var backupItemRecord = (BackupItemRecord)(e.GetData(AddRecordTag.BackupItemRecord) ?? throw new NullReferenceException());
        var recordName = (string)(e.GetData(AddRecordTag.RecordName) ?? throw new NullReferenceException());
        e.TryGetData(AddRecordTag.Comment, out var comment);

        try
        {
            var backup = _backupService.GetRepo(backupItemRecord.ID);

            var task = backup.GenerateBackupTask(backupItemRecord.Item.BackupTarget,
                new RecordInfo(recordName, comment is null ? null : (string)comment));
            _taskService.AddTask(backupItemRecord, task);
        }
        catch (Exception ex)
        {
            var dataIn = new DataArgs();
            dataIn.AddData(AddRecordTag.FailedReasons, ex.Message);
            Changed?.Invoke(this, dataIn);
            return;
        }

        var data = new DataArgs();
        data.AddEmptyData(AddRecordTag.SuccessConfirm);
        Changed?.Invoke(this, data);
    }

    public DataArgs GetData()
    {
        return new DataArgs();
    }

    public void Dispose()
    {
        _dispatchCenter.RemoveListener(ActionReceived);

        Changed.UnsubscribeAll();
        Changed = null;

        GC.SuppressFinalize(this);
    }
}
