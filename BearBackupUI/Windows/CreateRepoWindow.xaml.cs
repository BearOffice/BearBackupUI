using BearBackupUI.Core.Actions;
using BearBackupUI.Core;
using BearBackupUI.Helpers;
using BearBackupUI.Services;
using BearBackupUI.Stores;
using Microsoft.Win32;
using BearBackupUI.Core.DataTags;
using System.IO;

namespace BearBackupUI.Windows;

public partial class CreateRepoWindow : FluentWindow
{
    private readonly DispatchCenter _dispatchCenter;
    private readonly CreateRepoStore _store;
    private bool _isWaiting;

    public CreateRepoWindow(DispatchCenter dispatchCenter, CreateRepoStore createRepoStore)
    {
        InitializeComponent();

        _dispatchCenter = dispatchCenter;
        _store = createRepoStore;

        _store.Changed += (sender, e) => this.InvokeIfNeeded(() => Store_Changed(sender, e));
    }

    private void Store_Changed(object? sender, DataArgs e)
    {
        if (e.TryGetData(CreateRepoTag.SuccessConfirm, out _))
        {
            _isWaiting = false;
            Close();
        }
        else if (e.TryGetData(CreateRepoTag.FailedReasons, out var data))
        {
            var reasons = (string)(data ?? string.Empty);
            MessageBox.Show($"Error occurred. \n{reasons}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);

            _isWaiting = false;
            MainGrid.IsEnabled = true;
        }
    }

    private void CreateButton_Click(object sender, RoutedEventArgs e)
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

        if (string.IsNullOrWhiteSpace(RepoNameTextBox.Text))
        {
            MessageBox.Show("Repository name is empty.", "Bad input", MessageBoxButton.OK, MessageBoxImage.Warning);
            RepoNameTextBox.Focus();
            return;
        }

        if (!IsValidDirName(RepoNameTextBox.Text))
        {
            MessageBox.Show("Repository name is invalid.", "Bad input", MessageBoxButton.OK, MessageBoxImage.Warning);
            RepoNameTextBox.Focus();
            return;
        }

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
            // To not allow relative paths.
            BackupPath = RepoPathTextBox.Text + Path.DirectorySeparatorChar + RepoNameTextBox.Text,
            BackupTarget = BackupTargetTextBox.Text,
            LastBackupDateTime = null,
            RepoType = (BackupRepoType)type,
            ScheduledPeriod = schedule,
        };


        var action = new ActionArgs(CreateRepoAction.Create);
        action.AddData(CreateRepoTag.BackupItem, backupItem);

        if (IgnoreCheckBox.IsChecked ?? false)
        {
            action.AddData(CreateRepoTag.IgnoreDir, DirTextBox.Text);
            action.AddData(CreateRepoTag.IgnoreFile, FileTextBox.Text);
        }

        _dispatchCenter.DispatchEvent(action, newThread: true);
        _isWaiting = true;
        MainGrid.IsEnabled = false;
    }

    private static bool IsValidDirName(string text)
    {
        if (text.Length > 255) return false;

        if (string.IsNullOrWhiteSpace(text)) return false;
        if (text != text.TrimStart().TrimEnd()) return false;
        return text.IndexOfAny(Path.GetInvalidFileNameChars()) == -1;
    }

    private void RepoPathButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Multiselect = false,
            Title = "Select directory of the repository to create"
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
