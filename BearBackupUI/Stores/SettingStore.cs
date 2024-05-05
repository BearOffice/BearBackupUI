using BearBackupUI.Core;
using BearBackupUI.Core.Actions;
using BearBackupUI.Core.DataTags;
using BearBackupUI.Helpers;
using BearBackupUI.Services;
using System.Data.Common;
using System.Threading.Channels;
using Wpf.Ui.Appearance;

namespace BearBackupUI.Stores;

public class SettingStore : IStore
{
    public event EventHandler<DataArgs>? Changed;
    private readonly DispatchCenter _dispatchCenter;
    private readonly ConfigService _configService;

    public SettingStore(DispatchCenter dispatchCenter, ConfigService configService)
    {
        _configService = configService;
        _dispatchCenter = dispatchCenter;
        _dispatchCenter.AddListener(typeof(SettingAction), ActionReceived);
    }

    private void ActionReceived(object? sender, ActionArgs e)
    {
        if (e.Type is SettingAction.ChangeTheme)
        {
            _configService.ThemeType = (ApplicationTheme)(e.GetAnonymousData() ?? throw new NullReferenceException());
        }
        else if (e.Type is SettingAction.ChangeStartup)
        {
            var autoStartup = (bool)(e.GetAnonymousData() ?? throw new NullReferenceException());
            _configService.AutoStartup = autoStartup;

            if (autoStartup)
                StartupRegister.AddStartup();
            else
                StartupRegister.RemoveStartup();
        }
        else if (e.Type is SettingAction.ChangeLaunch)
        {
            _configService.LaunchMinimized = (bool)(e.GetAnonymousData() ?? throw new NullReferenceException());
        }
        else if (e.Type is SettingAction.ChangeCheckHash)
        {
            _configService.CheckHash = (bool)(e.GetAnonymousData() ?? throw new NullReferenceException());
        }
    }

    public DataArgs GetData()
    {
        var data = new DataArgs();
        data.AddData(SettingTag.Theme, _configService.ThemeType);
        data.AddData(SettingTag.Startup, _configService.AutoStartup);
        data.AddData(SettingTag.Launch, _configService.LaunchMinimized);
        data.AddData(SettingTag.CheckHash, _configService.CheckHash);

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
