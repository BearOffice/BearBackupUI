using BearBackup;
using BearBackup.BasicData;
using BearBackupUI.Core;
using BearBackupUI.Core.Actions;
using BearBackupUI.Core.DataTags;
using BearBackupUI.Helpers;
using BearBackupUI.Services;

namespace BearBackupUI.Stores;

public class RepoInfoStore : IStore
{
    public event EventHandler<DataArgs>? Changed;
    private readonly DispatchCenter _dispatchCenter;
    private readonly BackupService _backupService;

    public RepoInfoStore(DispatchCenter dispatchCenter, BackupService backupService)
    {
        _dispatchCenter = dispatchCenter;
        _dispatchCenter.AddListener(typeof(RepoInfoAction), ActionReceived);

        _backupService = backupService;
    }

    private void ActionReceived(object? sender, ActionArgs e)
    {
        if (e.Type is not RepoInfoAction.Modify) return;

        var id = (int)(e.GetData(RepoInfoTag.BackupID) ?? throw new NullReferenceException());
        var backupItem = (BackupItem)(e.GetData(RepoInfoTag.BackupItem) ?? throw new NullReferenceException());

        e.TryGetData(RepoInfoTag.IgnoreDir, out var ignoreDirObj);
        e.TryGetData(RepoInfoTag.IgnoreFile, out var ignoreFileObj);

        try
        {
            _backupService.ChangeRepoConfig(id, backupItem);
            
            if (ignoreDirObj is not null || ignoreFileObj is not null)
            {
                var ignoreDir = GetPathsFromStr((string)(ignoreDirObj ?? string.Empty));
                var ignoreFile = GetPathsFromStr((string)(ignoreFileObj ?? string.Empty));

                var ignore = new Ignore(ignoreDir, ignoreFile);
                var repo = _backupService.GetRepo(id);
                if (repo is MirroringBackup mirror)
                    mirror.SetIgnore(ignore);
                else if (repo is VersioningBackup version)
                    version.SetIgnore(ignore);
                else
                    throw new NotImplementedException();
            }
            else
            {
                var repo = _backupService.GetRepo(id);
                if (repo is MirroringBackup mirror)
                    mirror.RemoveIgnore();
                else if (repo is VersioningBackup version)
                    version.RemoveIgnore();
                else
                    throw new NotImplementedException();
            }
        }
        catch (Exception ex)
        {
            var dataIn = new DataArgs();
            dataIn.AddData(RepoInfoTag.FailedReasons, ex.Message);
            Changed?.Invoke(this, dataIn);
            return;
        }

        var data = new DataArgs();
        data.AddEmptyData(RepoInfoTag.SuccessConfirm);
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

    public DataArgs GetData(int id)
    {
        var data = new DataArgs();
        data.AddData(RepoInfoTag.BackupItem, _backupService.BackupRepos.Keys.First(item => item.ID == id).Item);

        Ignore? ignore;
        var repo = _backupService.GetRepo(id);
        if (repo is MirroringBackup mirror)
            ignore= mirror.GetIgnore();
        else if (repo is VersioningBackup version)
            ignore = version.GetIgnore();
        else
            throw new NotImplementedException();

        if (ignore is not null)
        {
            data.AddData(RepoInfoTag.IgnoreDir, string.Join('\n', ignore.IgnoredDirs));
            data.AddData(RepoInfoTag.IgnoreFile, string.Join('\n', ignore.IgnoredFiles));
        }
          
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
