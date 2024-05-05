using BearBackupUI.Core;
using BearBackupUI.Core.Actions;
using BearBackupUI.Core.DataTags;
using BearBackupUI.Services;
using BearBackupUI.Stores;
using Wpf.Ui.Appearance;

namespace BearBackupUI.Windows;

public partial class SettingWindow : FluentWindow
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
            var themeType = (ApplicationTheme)(theme ?? throw new NullReferenceException());
            switch (themeType)
            {
                case ApplicationTheme.Light:
                    LRadioButton.IsChecked = true;
                    break;
                case ApplicationTheme.Dark:
                    DRadioButton.IsChecked = true;
                    break;
				case ApplicationTheme.HighContrast:
					HRadioButton.IsChecked = true;
					break;
				case ApplicationTheme.Unknown:
                    URadioButton.IsChecked = true;
                    break;
                default:
                    URadioButton.IsChecked = true;
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
		ApplicationTheme themeType;
        if (sender == LRadioButton)
            themeType = ApplicationTheme.Light;
        else if (sender == DRadioButton)
            themeType = ApplicationTheme.Dark;
		else if (sender == HRadioButton)
			themeType = ApplicationTheme.HighContrast;
		else if (sender == URadioButton)
            themeType = ApplicationTheme.Unknown;
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
