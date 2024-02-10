using BearBackup;
using BearBackup.BasicData;
using BearBackupUI.Core;
using BearBackupUI.Core.Actions;
using BearBackupUI.Core.DataTags;
using BearBackupUI.Helpers;
using BearBackupUI.Services;

namespace BearBackupUI.Stores;

public class CreateRepoStore : IStore
{
    public event EventHandler<DataArgs>? Changed;
    private readonly DispatchCenter _dispatchCenter;
    private readonly BackupService _backupService;

    public CreateRepoStore(DispatchCenter dispatchCenter, BackupService backupService)
    {
        _dispatchCenter = dispatchCenter;
        _dispatchCenter.AddListener(typeof(CreateRepoAction), ActionReceived);

        _backupService = backupService;
    }

    private void ActionReceived(object? sender, ActionArgs e)
    {
        if (e.Type is not CreateRepoAction.Create) return;

        var backupItem = (BackupItem)(e.GetData(CreateRepoTag.BackupItem) ?? throw new NullReferenceException());
        e.TryGetData(CreateRepoTag.IgnoreDir, out var ignoreDir);
         e.TryGetData(CreateRepoTag.IgnoreFile, out var ignoreFile);

        Ignore? ignore = null;
        if (ignoreDir is not null || ignoreFile is not null)
        {
            var dirs = GetPathsFromStr((string)(ignoreDir ?? string.Empty));
            var files = GetPathsFromStr((string)(ignoreFile ?? string.Empty));

            ignore = new Ignore(dirs, files);
        }

        try
        {
            var id = _backupService.CreateRepo(backupItem);

            if (ignore is not null)
            {
                var repo = _backupService.GetRepo(id);

                if (repo is MirroringBackup mirror)
                    mirror.SetIgnore(ignore);
                else if (repo is VersioningBackup version)
                    version.SetIgnore(ignore);
                else
                    throw new NotImplementedException();
            }
        }
        catch (Exception ex)
        {
            var dataIn = new DataArgs();
            dataIn.AddData(CreateRepoTag.FailedReasons, ex.Message);
            Changed?.Invoke(this, dataIn);
            return;
        }

        var data = new DataArgs();
        data.AddEmptyData(CreateRepoTag.SuccessConfirm);
        Changed?.Invoke(this, data);
    }

    private static string[] GetPathsFromStr(string? str)
    {
        if (string.IsNullOrEmpty(str)) return [];

        return str.Split(["\r\n", "\r", "\n"], StringSplitOptions.None)
                  .Where(p => !string.IsNullOrWhiteSpace(p))
                  .ToArray();
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
