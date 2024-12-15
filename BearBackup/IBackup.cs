using BearBackup.BasicData;
using BearBackup.Task;

namespace BearBackup;

public interface IBackup
{
    public IBackupTask GenerateBackupTask(string backupTarget, RecordInfo recordInfo);
    public IRemoveTask GenerateRemoveTask(RecordInfo recordInfo);
	public IRemoveTask GenerateRemoveTask(RecordInfo[] recordInfoArr);
	public IRestoreTask GenerateRestoreTask(string restorePath, Index index);
    public IRestoreTask GenerateRestoreTask(string restorePath, (Index index, FileInfo[]) files);
}
