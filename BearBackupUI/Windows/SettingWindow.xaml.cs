using BearBackupUI.Core;
using BearBackupUI.Core.Actions;
using BearBackupUI.Core.DataTags;
using BearBackupUI.Services;
using BearBackupUI.Stores;
using Wpf.Ui.Appearance;

namespace BearBackupUI.Windows;

public partial class SettingWindow : UiWindow
{
    private readonly DispatchCenter _dispatchCenter;
    private readonly SettingStore _store;

    public SettingWindow(DispatchCenter dispatchCenter, SettingStore store)
    {
        InitializeComponent();
        _dispatchCenter = dispatchCenter;
        _store = store;

        var data = _store.GetData();
        if (data.TryGetData(SettingTag.Theme, out var theme))
        {
            var themeType = (ThemeType)(theme ?? throw new NullReferenceException());
            switch (themeType)
            {
                case ThemeType.Light:
                    LRadioButton.IsChecked = true;
                    break;
                case ThemeType.Dark:
                    DRadioButton.IsChecked = true;
                    break;
                case ThemeType.Unknown:
                    SRadioButton.IsChecked = true;
                    break;
                default:
                    LRadioButton.IsChecked = true;
                    break;
            }
        }

        if (data.TryGetData(SettingTag.Startup, out var startup))
        {
            var autoStartup = (bool)(startup ?? throw new NullReferenceException());
            if (autoStartup)
            {
                StartupToggleSwitch.IsChecked = true;
                StartupLabel.Content = "On";
            }
        }

        if (data.TryGetData(SettingTag.Launch, out var launch))
        {
            var minLaunch = (bool)(launch ?? throw new NullReferenceException());
            if (minLaunch)
            {
                LaunchToggleSwitch.IsChecked = true;
                LaunchLabel.Content = "On";
            }
        }

        if (data.TryGetData(SettingTag.CheckHash, out var check))
        {
            var checkHash = (bool)(check ?? throw new NullReferenceException());
            if (checkHash)
            {
                CheckHashToggleSwitch.IsChecked = true;
                CheckHashLabel.Content = "On";
            }
        }
    }

    private void RadioButton_Checked(object sender, RoutedEventArgs e)
    {
        ThemeType themeType;
        if (sender == LRadioButton)
            themeType = ThemeType.Light;
        else if (sender == DRadioButton)
            themeType = ThemeType.Dark;
        else if (sender == SRadioButton)
            themeType = ThemeType.Unknown;
        else
            throw new NotImplementedException();

        var action = new ActionArgs(SettingAction.ChangeTheme, themeType);
        _dispatchCenter.DispatchEvent(action);
    }

    private void StartupToggleSwitch_Click(object sender, RoutedEventArgs e)
    {
        if (StartupToggleSwitch.IsChecked == true)
        {
            StartupLabel.Content = "On";

            var action = new ActionArgs(SettingAction.ChangeStartup, true);
            _dispatchCenter.DispatchEvent(action);
        }
        else
        {
            StartupLabel.Content = "Off";

            var action = new ActionArgs(SettingAction.ChangeStartup, false);
            _dispatchCenter.DispatchEvent(action);
        }
    }

    private void LaunchToggleSwitch_Click(object sender, RoutedEventArgs e)
    {
        if (LaunchToggleSwitch.IsChecked == true)
        {
            LaunchLabel.Content = "On";

            var action = new ActionArgs(SettingAction.ChangeLaunch, true);
            _dispatchCenter.DispatchEvent(action);
        }
        else
        {
            LaunchLabel.Content = "Off";

            var action = new ActionArgs(SettingAction.ChangeLaunch, false);
            _dispatchCenter.DispatchEvent(action);
        }
    }

    private void CheckHashToggleSwitch_Click(object sender, RoutedEventArgs e)
    {
        if (CheckHashToggleSwitch.IsChecked == true)
        {
            CheckHashLabel.Content = "On";

            var action = new ActionArgs(SettingAction.ChangeCheckHash, true);
            _dispatchCenter.DispatchEvent(action);
        }
        else
        {
            CheckHashLabel.Content = "Off";

            var action = new ActionArgs(SettingAction.ChangeCheckHash, false);
            _dispatchCenter.DispatchEvent(action);
        }
    }

    private void OnClosed(object sender, EventArgs e)
    {
        _store.Dispose();
    }
}
