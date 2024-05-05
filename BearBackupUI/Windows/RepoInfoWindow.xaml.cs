using BearBackupUI.Core;
using BearBackupUI.Core.Actions;
using BearBackupUI.Core.DataTags;
using BearBackupUI.Helpers;
using BearBackupUI.Services;
using BearBackupUI.Stores;
using Microsoft.Win32;

namespace BearBackupUI.Windows;

public partial class RepoInfoWindow : FluentWindow
{
    public BackupItemRecord? BackupItemRecord { get; set; }
    private readonly DispatchCenter _dispatchCenter;
    private readonly RepoInfoStore _store;
    private bool _isWaiting;

    public RepoInfoWindow(DispatchCenter dispatchCenter, RepoInfoStore repoInfoStore)
    {
        InitializeComponent();

        _dispatchCenter = dispatchCenter;
        _store = repoInfoStore;

        _store.Changed += (sender, e) => this.InvokeIfNeeded(() => Store_Changed(sender, e));
    }

    private void Store_Changed(object? sender, DataArgs e)
    {
        if (e.TryGetData(RepoInfoTag.SuccessConfirm, out _))
        {
            _isWaiting = false;
            Close();
        }
        else if (e.TryGetData(RepoInfoTag.FailedReasons, out var data))
        {
            var reasons = (string)(data ?? string.Empty);
            MessageBox.Show($"Error occurred. \n{reasons}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);

            _isWaiting = false;
            MainGrid.IsEnabled = true;
        }
    }

    private void Window_ContentRendered(object sender, EventArgs e)
    {
        if (BackupItemRecord is null)
            throw new Exception("Backup item records must be set before loading the window.");

        var data = _store.GetData(BackupItemRecord.ID);
        var backupItem = (BackupItem)(data.GetData(RepoInfoTag.BackupItem) ??  throw new NullReferenceException());
        RepoPathTextBox.Text = backupItem.BackupPath;
        BackupTargetTextBox.Text = backupItem.BackupTarget;

        if (backupItem.ScheduledPeriod is not null)
        {
            ScheduleCheckBox.IsChecked = true;
            DaysNumberBox.Value = backupItem.ScheduledPeriod / 24;
            HoursNumberBox.Value = backupItem.ScheduledPeriod % 24;
        }

        data.TryGetData(RepoInfoTag.IgnoreDir, out var ignoreDirObj);
        data.TryGetData(RepoInfoTag.IgnoreFile, out var ignoreFileObj);

        if (ignoreDirObj is not null || ignoreFileObj is not null)
        {
            IgnoreCheckBox.IsChecked = true;
            DirTextBox.Text = (string)(ignoreDirObj ?? string.Empty);
            FileTextBox.Text = (string)(ignoreFileObj ?? string.Empty);
        }
    }

    private void EditButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(BackupTargetTextBox.Text))
        {
            MessageBox.Show("Backup target path is empty.", "Bad input", MessageBoxButton.OK, MessageBoxImage.Warning);
            BackupTargetTextBox.Focus();
            return;
        }

        int? schedule = null;
        if (ScheduleCheckBox.IsChecked ?? false)
        {
            if (!int.TryParse(DaysNumberBox.Text, out var days) || days < 0)
            {
                if (string.IsNullOrWhiteSpace(DaysNumberBox.Text))
                {
                    days = 0;
                }
                else
                {
                    MessageBox.Show("Invalid number specified.", "Bad input", MessageBoxButton.OK, MessageBoxImage.Warning);
                    DaysNumberBox.Focus();
                    return;
                }
            }
            schedule = days * 24;

            if (!int.TryParse(HoursNumberBox.Text, out var hours) || hours < 0)
            {
                if (string.IsNullOrWhiteSpace(HoursNumberBox.Text))
                {
                    hours = 0;
                }
                else
                {
                    MessageBox.Show("Invalid number specified.", "Bad input", MessageBoxButton.OK, MessageBoxImage.Warning);
                    HoursNumberBox.Focus();
                    return;
                }
            }
            schedule += hours;

            if (schedule <= 0)
            {
                MessageBox.Show("Schedule period must greater than 0.", "Bad input", MessageBoxButton.OK, MessageBoxImage.Warning);
                DaysNumberBox.Focus();
                return;
            }
        }

#pragma warning disable CS8602
        var backupItem = BackupItemRecord.Item with { BackupTarget = BackupTargetTextBox.Text, ScheduledPeriod = schedule };
#pragma warning restore CS8602

        var action = new ActionArgs(RepoInfoAction.Modify);
        action.AddData(RepoInfoTag.BackupID, BackupItemRecord.ID);
        action.AddData(RepoInfoTag.BackupItem, backupItem);

        if (IgnoreCheckBox.IsChecked ?? false)
        {
            action.AddData(RepoInfoTag.IgnoreDir, DirTextBox.Text);
            action.AddData(RepoInfoTag.IgnoreFile, FileTextBox.Text);
        }

        _dispatchCenter.DispatchEvent(action, newThread: true);
        _isWaiting = true;
        MainGrid.IsEnabled = false;
    }

    private void BackupTargetButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Multiselect = false,
            Title = "Select directory of the target to backup"
        };

        var result = dialog.ShowDialog();
        if (result == true)
            BackupTargetTextBox.Text = dialog.FolderName;
    }

    private void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_isWaiting) e.Cancel = true;
    }

    private void OnClosed(object sender, EventArgs e)
    {
        _store.Dispose();
    }
}
