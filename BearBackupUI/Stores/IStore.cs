using BearBackupUI.Core;

namespace BearBackupUI.Stores;

public interface IStore : IDisposable
{
    public event EventHandler<DataArgs>? Changed;
    public DataArgs GetData();
}
