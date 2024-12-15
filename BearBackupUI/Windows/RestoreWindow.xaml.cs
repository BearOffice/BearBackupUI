using BearBackup.BasicData;
using BearBackupUI.Core;
using BearBackupUI.Core.Actions;
using BearBackupUI.Core.DataTags;
using BearBackupUI.Helpers;
using BearBackupUI.Services;
using BearBackupUI.Stores;
using Microsoft.Win32;
using System.IO;
using Index = BearBackup.BasicData.Index;
using TreeViewItem = System.Windows.Controls.TreeViewItem;

namespace BearBackupUI.Windows;

public partial class RestoreWindow : FluentWindow
{
    public BackupItemRecord? BackupItemRecord { get; set; }
    public RecordInfo? RecordInfo { get; set; }
    private readonly DispatchCenter _dispatchCenter;
    private readonly RestoreStore _store;
    private bool _isWaiting;
    private readonly DataTemplate _dirTemplate;
    private readonly DataTemplate _fileTemplate;

    public RestoreWindow(DispatchCenter dispatchCenter, RestoreStore restoreStore)
    {
        InitializeComponent();
        DataContext = this;

        _dispatchCenter = dispatchCenter;
        _store = restoreStore;

        _store.Changed += (sender, e) => this.InvokeIfNeeded(() => Store_Changed(sender, e));

        _dirTemplate = FindResource("DirItemTemplate") as DataTemplate ?? throw new Exception("Resource not found.");
        _fileTemplate = FindResource("FileItemTemplate") as DataTemplate ?? throw new Exception("Resource not found.");
    }

    private void Store_Changed(object? sender, DataArgs e)
    {
        if (e.TryGetData(RestoreTag.SuccessConfirm, out _))
        {
            MessageBox.Show("Restore task created.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
            _isWaiting = false;
            MainGrid.IsEnabled = true;
        }
        else if (e.TryGetData(RestoreTag.FailedReasons, out var data))
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
        if (RecordInfo is null)
            throw new Exception("Record info must be set before loading the window.");

        var data = _store.GetData(BackupItemRecord.ID, RecordInfo);
        var index = (Index)(data.GetData(RestoreTag.Index) ?? throw new NullReferenceException());

        BuildTreeView(index);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Multiselect = false,
            Title = "Select the directory to restore files"
        };
        var result = dialog.ShowDialog();
        if (result == false) return;

        var action = new ActionArgs(RestoreAction.Restore);
        action.AddData(RestoreTag.RestorePath, dialog.FolderName);
#pragma warning disable CS8602, CS8604
		action.AddData(RestoreTag.BackupItemRecord, BackupItemRecord);
		action.AddData(RestoreTag.RecordInfo, RecordInfo);
#pragma warning restore CS8602, CS8604
		action.AddEmptyData(RestoreTag.RestoreAll);
        _dispatchCenter.DispatchEvent(action, newThread: true);

        _isWaiting = true;
        MainGrid.IsEnabled = false;
    }

    private void SaveSelected_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Multiselect = false,
            Title = "Select the directory to restore files"
        };
        var result = dialog.ShowDialog();
        if (result == false) return;

        var selected = (TreeViewItem)(IndexTreeView.SelectedItem ?? throw new NullReferenceException());

        var action = new ActionArgs(RestoreAction.Restore);
        action.AddData(RestoreTag.RestorePath, dialog.FolderName);
#pragma warning disable CS8602, CS8604
        action.AddData(RestoreTag.BackupItemRecord, BackupItemRecord);
        action.AddData(RestoreTag.RecordInfo, RecordInfo);
#pragma warning restore CS8602, CS8604

        if (selected.Header is DirViewObject dir)
        {
            action.AddData(RestoreTag.RestoreDir, dir.FullName);
        }
        else if (selected.Header is FileViewObject file)
        {
            action.AddData(RestoreTag.RestoreFile, (file.ParentName, file.Name));
        }
        else
        {
            throw new NotImplementedException();
        }

        _dispatchCenter.DispatchEvent(action, newThread: true);

        _isWaiting = true;
        MainGrid.IsEnabled = false;
    }

    private void BuildTreeView(Index index)
    {
        foreach (var inIndex in index.SubIndexArr)
        {
            var subTreeViewItem = BuildDirItem(inIndex);
            IndexTreeView.Items.Add(subTreeViewItem);
        }

        foreach (var fileInfo in index.FileInfoArr)
        {
            IndexTreeView.Items.Add(new TreeViewItem
            {
                Header = new FileViewObject { Name = fileInfo.Name, ParentName = null },
                HeaderTemplate = _fileTemplate,
            });
        }

        TreeViewItem BuildDirItem(Index subIndex)
        {
            var treeViewItem = new TreeViewItem
            {
                Header = new DirViewObject 
                { 
                    FullName = subIndex.DirInfo?.FullName ?? string.Empty,
                    Index = subIndex,
                },
                HeaderTemplate = _dirTemplate,
            };
            if (subIndex.SubIndexArr.Length + subIndex.FileInfoArr.Length > 0) 
            {
                treeViewItem.Items.Add(null);  // Placeholder
            }

            treeViewItem.Expanded += DirItem_Expanded;

            return treeViewItem;
        }

        void DirItem_Expanded(object sender, RoutedEventArgs e)
        {
            var dirItem = (TreeViewItem)sender;
            if (dirItem.Items.Count != 1 || dirItem.Items[0] != null)
                return;

            dirItem.Items.Clear();

            var dirViewObj = (DirViewObject)dirItem.Header ?? throw new NullReferenceException();

            foreach (var inIndex in dirViewObj.Index.SubIndexArr)
            {
                var subTreeViewItem = BuildDirItem(inIndex);
                dirItem.Items.Add(subTreeViewItem);
            }

            foreach (var fileInfo in dirViewObj.Index.FileInfoArr)
            {
                dirItem.Items.Add(new TreeViewItem
                {
                    Header = new FileViewObject { Name = fileInfo.Name, ParentName = dirViewObj.FullName },
                    HeaderTemplate = _fileTemplate,
                });
            }
        }
    }

    private void Expand_Click(object sender, RoutedEventArgs e)
    {
        foreach (TreeViewItem item in IndexTreeView.Items)
        {
            ExpandOrCollapseTreeView(item, true);
        }
    }

    private void Collapse_Click(object sender, RoutedEventArgs e)
    {
        foreach (TreeViewItem item in IndexTreeView.Items)
        {
            ExpandOrCollapseTreeView(item, false);
        }
    }

    private static void ExpandOrCollapseTreeView(TreeViewItem item, bool value)
    {
        foreach (TreeViewItem subItem in item.Items)
        {
            if (subItem is null) continue;
            ExpandOrCollapseTreeView(subItem, value);
        }

        item.IsExpanded = value;
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

public class DirViewObject
{
    public required string FullName { get; init; }
    public required Index Index { get; init; }
}

public class FileViewObject
{
    public required string? ParentName { get; init; }
    public required string Name { get; init; }
}
