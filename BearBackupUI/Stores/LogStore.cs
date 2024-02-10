using BearBackupUI.Core;
using BearBackupUI.Core.Actions;
using BearBackupUI.Helpers;
using BearBackupUI.Services;

namespace BearBackupUI.Stores;

public class LogStore : IStore
{
    public event EventHandler<DataArgs>? Changed;
    private readonly DispatchCenter _dispatchCenter;
    private readonly LogService _logService;

    public LogStore(DispatchCenter dispatchCenter, LogService logService)
    {
        _dispatchCenter = dispatchCenter;
        _dispatchCenter.AddListener(typeof(LogAction), ActionReceived);

        _logService = logService;
        _logService.Changed += Log_Changed;
    }

    private void Log_Changed(object? sender, EventArgs e)
    {
        var logs = _logService.GetLogs();
        Changed?.Invoke(this, new DataArgs(logs));
    }

    private void ActionReceived(object? sender, ActionArgs e)
    {
        if (e.Type is LogAction.ClearLog)
        {
            _logService.ClearLogs();
            Changed?.Invoke(this, new DataArgs(""));
        }
    }

    public DataArgs GetData()
    {
        return new DataArgs(_logService.GetLogs());
    }

    public void Dispose()
    {
        _dispatchCenter.RemoveListener(ActionReceived);

        Changed.UnsubscribeAll();
        Changed = null;

        GC.SuppressFinalize(this);
    }
}
