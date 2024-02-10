using BearBackupUI.Core;
using BearBackupUI.Core.Actions;
using BearBackupUI.Core.DataTags;
using BearBackupUI.Helpers;
using BearBackupUI.Services;

namespace BearBackupUI.Stores;

public class AddRepoStore : IStore
{
    public event EventHandler<DataArgs>? Changed;
    private readonly DispatchCenter _dispatchCenter;
    private readonly BackupService _backupService;

    public AddRepoStore(DispatchCenter dispatchCenter, BackupService backupService)
    {
        _dispatchCenter = dispatchCenter;
        _dispatchCenter.AddListener(typeof(AddRepoAction), ActionReceived);

        _backupService = backupService;
    }

    private void ActionReceived(object? sender, ActionArgs e)
    {
        if (e.Type is not AddRepoAction.AddExists) return;

        var backupItem = (BackupItem)(e.GetAnonymousData() ?? throw new NullReferenceException());

        try
        {
            _backupService.AddExistsRepo(backupItem);
        }
        catch (Exception ex)
        {
            var dataIn = new DataArgs();
            dataIn.AddData(AddRepoTag.FailedReasons, ex.Message);
            Changed?.Invoke(this, dataIn);
            return;
        }

        var data = new DataArgs();
        data.AddEmptyData(AddRepoTag.SuccessConfirm);
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
