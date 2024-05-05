using BearBackupUI.Core;
using BearBackupUI.Core.Actions;
using BearBackupUI.Core.DataTags;
using BearBackupUI.Helpers;
using BearBackupUI.Services;
using BearBackupUI.Stores;
using Microsoft.Win32;

namespace BearBackupUI.Windows;

public partial class AddRepoWindow : FluentWindow
{
    private readonly DispatchCenter _dispatchCenter;
    private readonly AddRepoStore _store;
    private bool _isWaiting;

    public AddRepoWindow(DispatchCenter dispatchCenter, AddRepoStore addRepoStore)
    {
        InitializeComponent();

        _dispatchCenter = dispatchCenter;
        _store = addRepoStore;

        _store.Changed += (sender, e) => this.InvokeIfNeeded(() => Store_Changed(sender, e));
    }

    private void Store_Changed(object? sender, DataArgs e)
    {
        if (e.TryGetData(AddRepoTag.SuccessConfirm, out _))
        {
            _isWaiting = false;
            Close();
        }
        else if (e.TryGetData(AddRepoTag.FailedReasons, out var data))
        {
            var reasons = (string)(data ?? string.Empty);
            MessageBox.Show($"Error occurred. \n{reasons}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);

            _isWaiting = false;
            MainGrid.IsEnabled = true;
        }
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        BackupRepoType? type = null;
        if (TypeComboBox.SelectedItem is null)
        {
            MessageBox.Show("Repository type not selected.", "Bad input", MessageBoxButton.OK, MessageBoxImage.Warning);
            TypeComboBox.Focus();
            return;
        }
        else if (TypeComboBox.SelectedItem == MirroringItem)
        {
            type = BackupRepoType.Mirroring;
        }
        else if (TypeComboBox.SelectedItem == VersioningItem)
        {
            type = BackupRepoType.Versioning;
        }

        if (type is null) throw new Exception("Unreachable.");


        if (string.IsNullOrWhiteSpace(RepoPathTextBox.Text))
        {
            MessageBox.Show("Repository path is empty.", "Bad input", MessageBoxButton.OK, MessageBoxImage.Warning);
            RepoPathTextBox.Focus();
            return;
        }

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

        var backupItem = new BackupItem
        {
            BackupPath = RepoPathTextBox.Text,
            BackupTarget = BackupTargetTextBox.Text,
            LastBackupDateTime = null,
            RepoType = (BackupRepoType)type,
            ScheduledPeriod = schedule,
        };

        var action = new ActionArgs(AddRepoAction.AddExists, backupItem);
        _dispatchCenter.DispatchEvent(action, newThread: true);
        _isWaiting = true;
        MainGrid.IsEnabled = false;
    }

    private void RepoPathButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Multiselect = false,
            Title = "Select directory of the repository"
        };

        var result = dialog.ShowDialog();
        if (result == true)
            RepoPathTextBox.Text = dialog.FolderName;
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
