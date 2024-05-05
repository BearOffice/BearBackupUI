using BearBackupUI.Core;
using BearBackupUI.Core.Actions;
using BearBackupUI.Core.DataTags;
using BearBackupUI.Helpers;
using BearBackupUI.Services;
using BearBackupUI.Stores;
using Microsoft.Win32;
using System.Windows.Markup.Primitives;

namespace BearBackupUI.Windows;

public partial class AddRecordWindow : FluentWindow
{
    public BackupItemRecord? BackupItemRecord { get; set; }
    private readonly DispatchCenter _dispatchCenter;
    private readonly AddRecordStore _store;
    private bool _isWaiting;

    public AddRecordWindow(DispatchCenter dispatchCenter, AddRecordStore addRecordStore)
    {
        InitializeComponent();

        _dispatchCenter = dispatchCenter;
        _store = addRecordStore;

        _store.Changed += (sender, e) => this.InvokeIfNeeded(() => Store_Changed(sender, e));
    }

    private void Store_Changed(object? sender, DataArgs e)
    {
        if (e.TryGetData(AddRecordTag.SuccessConfirm, out _))
        {
            _isWaiting = false;
            Close();
        }
        else if (e.TryGetData(AddRecordTag.FailedReasons, out var data))
        {
            var reasons = (string)(data ?? string.Empty);
            MessageBox.Show($"Error occurred. \n{reasons}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);

            _isWaiting = false;
            MainGrid.IsEnabled = true;
        }
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(RecordNameTextBox.Text))
        {
            MessageBox.Show("Record name is empty.", "Bad input", MessageBoxButton.OK, MessageBoxImage.Warning);
            RecordNameTextBox.Focus();
            return;
        }

        var backupItemRecord = BackupItemRecord ?? throw new Exception("backup item record is null.");

        var action = new ActionArgs(AddRecordAction.Add);
        action.AddData(AddRecordTag.BackupItemRecord, backupItemRecord);
        action.AddData(AddRecordTag.RecordName, RecordNameTextBox.Text);

        if (!string.IsNullOrEmpty(CommentTextBox.Text))
            action.AddData(AddRecordTag.Comment, CommentTextBox.Text);

        _dispatchCenter.DispatchEvent(action, newThread: true);
        _isWaiting = true;
        MainGrid.IsEnabled = false;
    }

    private void Window_ContentRendered(object sender, EventArgs e)
    {
        if (BackupItemRecord is null)
            throw new Exception("Backup item records must be set before loading the window.");
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
