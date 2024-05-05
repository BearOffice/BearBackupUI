using BearBackup;
using BearBackup.BasicData;
using BearBackup.Task;
using BearBackup.Tools;
using BearBackupUI.Core;
using BearBackupUI.Core.Actions;
using BearBackupUI.Core.DataTags;
using BearBackupUI.Helpers;
using BearBackupUI.Services;
using BearBackupUI.Stores;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using Wpf.Ui.Appearance;

namespace BearBackupUI.Windows;

public partial class MainWindow : FluentWindow
{
    private readonly IServiceProvider _serviceProvider;
    private readonly DispatchCenter _dispatchCenter;
    private readonly MainStore _store;
    private TaskWindow? _taskWindow;
    private LogWindow? _logWindow;
    private SettingWindow? _settingWindow;
    private bool _isWaiting;

    public MainWindow(IServiceProvider serviceProvider, DispatchCenter dispatchCenter, MainStore store)
    {
        InitializeComponent();
        DataContext = this;

		_serviceProvider = serviceProvider;
        _dispatchCenter = dispatchCenter;
        _store = store;

        Store_Changed(null, _store.GetData());
        _store.Changed += (sender, e) => this.InvokeIfNeeded(() => Store_Changed(sender, e));
    }

    private void Store_Changed(object? sender, DataArgs e)
    {
        if (e.TryGetData(MainTag.TaskStatus, out var status))
        {
            var item = (bool)(status ?? throw new NullReferenceException());
            SwitchTaskIndicator(item);
        }

        if (e.TryGetData(MainTag.RemoveRepoConfirm, out var removeRepoConfirm))
        {
            var item = (bool)(removeRepoConfirm ?? throw new NullReferenceException());
            if (!item) MessageBox.Show("Cannot remove the selected repository. The repository is on using.", "Exclamation",
                MessageBoxButton.OK, MessageBoxImage.Exclamation);
            
            MainGrid.IsEnabled = true;
            _isWaiting = false;
        }

        if (e.TryGetData(MainTag.RemoveRecordConfirm, out var removeRecordConfirm))
        {
            var item = (bool)(removeRecordConfirm ?? throw new NullReferenceException());
            if (!item) MessageBox.Show("Cannot create record remove task.", "Exclamation",
                MessageBoxButton.OK, MessageBoxImage.Exclamation);

            MainGrid.IsEnabled = true;
            _isWaiting = false;
        }

        if (e.TryGetData(MainTag.TaskFaulted, out var taskFaulted))
        {
            var item = (Exception)(taskFaulted ?? throw new NullReferenceException());
            MessageBox.Show($"Task faulted.\n{item.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        if (e.TryGetData(MainTag.FailedToLoadRepos, out var failed))
        {
            var item = (BackupItemRecord[])(failed ?? throw new NullReferenceException());

            if (item.Length > 0)
            {
                var result = MessageBox.Show($"Remove repositories failed to open?\n{BuildErrorMessage(item)}", "Question",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                    _dispatchCenter.DispatchEvent(new ActionArgs(MainAction.RemoveFailedRepos, item), newThread: true);
            }
        }

        if (e.TryGetData(MainTag.Repos, out var repos))
        {
            var item = (BackupItemRecord[])(repos ?? throw new NullReferenceException());
            var selectedIndex = RepoListView.SelectedIndex;
            var selectedItem = (RepoViewObject)RepoListView.SelectedItem;

            var selectedRecordIndex = RecordListView.SelectedIndex;

            RepoListView.ItemsSource = new ObservableCollection<RepoViewObject>(
                item.Select(i => new RepoViewObject { Record = i }).ToArray());

            if (selectedIndex >= 0 || item.Length > 0)
            {
                if (item.Length > selectedIndex)
                    RepoListView.SelectedIndex = selectedIndex;
                else
                    RepoListView.SelectedIndex = item.Length - 1;

                // Reselect the record item if possible.
                if (selectedRecordIndex != -1 &&
                    selectedItem.Record.ID == ((RepoViewObject)RepoListView.SelectedItem).Record.ID)
                {
                    var recordCount = RecordListView.Items.Count;
                    if (recordCount > 0)
                    {
                        if (recordCount > selectedRecordIndex)
                            RecordListView.SelectedIndex = selectedRecordIndex;
                        else
                            RecordListView.SelectedIndex = recordCount - 1;
                    }
                }
            }
        }

        if (e.TryGetData(MainTag.Records, out var records))
        {
            var item = (RecordInfo[])(records ?? throw new NullReferenceException());
            var selectedIndex = RecordListView.SelectedIndex;
            RecordListView.ItemsSource = new ObservableCollection<RecordViewObject>(
                item.Select(i => new RecordViewObject { RecordInfo = i }));

            if (selectedIndex >= 0 || item.Length > 0)
            {
                if (item.Length > selectedIndex)
                    RecordListView.SelectedIndex = selectedIndex;
                else
                    RecordListView.SelectedIndex = item.Length - 1;
            }
        }
    }

    private static string BuildErrorMessage(BackupItemRecord[] records)
    {
        if (records.Length == 0) return string.Empty;

        var sb = records.Select(r => r.Item.BackupPath)
                        .Aggregate(new StringBuilder(), (acc, p) => acc.AppendLine(p));
        sb.Remove(sb.Length - 1, 1);
        return sb.ToString();
    }

    private void SwitchTaskIndicator(bool visibility)
    {
        if (visibility)
        {
            TaskProgressRing.IsIndeterminate = true;
            TaskStackPanel.Visibility = Visibility.Visible;
        }
        else
        {
            TaskStackPanel.Visibility = Visibility.Collapsed;
            TaskProgressRing.IsIndeterminate = false;
        }
    }

    private void RepoListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RepoListView.SelectedItem is not null)
        {
            var selected = (RepoViewObject)RepoListView.SelectedItem;
            _dispatchCenter.DispatchEvent(new ActionArgs(MainAction.RequestRecord, selected.Record));
        }
        else
        {
            RecordListView.ItemsSource = null;
        }
    }

    private void RemoveRepo_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show($"Remove the selected repository from the list?", "Question",
            MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            _isWaiting = true;
            MainGrid.IsEnabled = false;
            var selected = (RepoViewObject)RepoListView.SelectedItem;
            _dispatchCenter.DispatchEvent(new ActionArgs(MainAction.RemoveRepo, selected.Record), newThread: true);
        }
    }

    private void RemoveRecordButton_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show($"Remove the selected record? This operation cannot be reverted.", "Warning",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            _isWaiting = true;
            MainGrid.IsEnabled = false;
            var selectedRepo = (RepoViewObject)RepoListView.SelectedItem;
            var selectedRecord = (RecordViewObject)RecordListView.SelectedItem;
            _dispatchCenter.DispatchEvent(
                new ActionArgs(MainAction.RemoveRecord, (selectedRepo.Record, selectedRecord.RecordInfo)),
                newThread: true);
        }
    }

    private void RepoListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        var selected = (RepoViewObject)RepoListView.SelectedItem;
        if (selected is null) return;

        var repoInfoWindow = _serviceProvider.GetRequiredService<RepoInfoWindow>();
        repoInfoWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        repoInfoWindow.BackupItemRecord = selected.Record;
        repoInfoWindow.ShowDialog();
    }

    private void AddRecordButton_Click(object sender, RoutedEventArgs e)
    {
        var addRecordWindow = _serviceProvider.GetRequiredService<AddRecordWindow>();
        addRecordWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        addRecordWindow.BackupItemRecord = ((RepoViewObject)RepoListView.SelectedItem).Record;
        addRecordWindow.ShowDialog();
    }

    private void RestoreButton_Click(object sender, RoutedEventArgs e)
    {
        var restoreWindow = _serviceProvider.GetRequiredService<RestoreWindow>();
        restoreWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        restoreWindow.BackupItemRecord =((RepoViewObject)RepoListView.SelectedItem).Record;
        restoreWindow.RecordInfo = ((RecordViewObject)RecordListView.SelectedItem).RecordInfo;
        restoreWindow.ShowDialog();
    }

    private void CreateRepo_Click(object sender, RoutedEventArgs e)
    {
        var createWindow = _serviceProvider.GetRequiredService<CreateRepoWindow>();
        createWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        createWindow.ShowDialog();
    }

    private void AddRepo_Click(object sender, RoutedEventArgs e)
    {
        var addWindow = _serviceProvider.GetRequiredService<AddRepoWindow>();
        addWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        addWindow.ShowDialog();
    }

    private void Task_Click(object sender, RoutedEventArgs e)
    {
        ShowTaskWindow();
    }

    private void Log_Click(object sender, RoutedEventArgs e)
    {
        ShowLogWindow();
    }

    private void Setting_Click(object sender, RoutedEventArgs e)
    {
        ShowSettingWindow();
    }

    private void ShowTaskWindow()
    {
        if (_taskWindow is not null)
        {
            BringWindowToFront(_taskWindow);
            return;
        }

        _taskWindow = _serviceProvider.GetRequiredService<TaskWindow>();
        _taskWindow.Closed += (sender, args) =>
        {
            _taskWindow = null;
			GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive);
        };

        _taskWindow.Show();
        _taskWindow.Activate();
    }

    private void ShowLogWindow()
    {
        if (_logWindow is not null)
        {
            BringWindowToFront(_logWindow);
            return;
        }

        _logWindow = _serviceProvider.GetRequiredService<LogWindow>();
        _logWindow.Closed += (sender, args) =>
        {
            _logWindow = null;
			GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive);
        };

        _logWindow.Show();
        _logWindow.Activate();
    }

    private void ShowSettingWindow()
    {
        if (_settingWindow is not null)
        {
            BringWindowToFront(_settingWindow);
            return;
        }

        _settingWindow = _serviceProvider.GetRequiredService<SettingWindow>();
        _settingWindow.Closed += (sender, args) =>
        {
            _settingWindow = null;
			GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive);
		};

        _settingWindow.Show();
        _settingWindow.Activate();
    }

    private static void BringWindowToFront(Window window)
    {
        if (window.WindowState == WindowState.Minimized)
            window.WindowState = WindowState.Normal;
        window.Activate();
    }

    private void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_isWaiting) e.Cancel = true;
    }

    private void OnClosed(object sender, EventArgs e)
    {
        _taskWindow?.Close();
        _logWindow?.Close();
        _settingWindow?.Close();
        _store.Dispose();
    }
}

public class RepoViewObject
{
    public string RepoTitle { get => $"[{Record.Item.RepoType}] {Path.GetFileName(Record.Item.BackupPath)}"; }
    public string BackupPath { get => "Repo path: " + Record.Item.BackupPath; }
    public string BackupTarget { get => "Backup target: " + Record.Item.BackupTarget; }
    public string ScheduledPeriod
    {
        get
        {
            var sb = new StringBuilder("Backup period: ");
            var period = Record.Item.ScheduledPeriod;
            if (period is null) return sb.Append("None").ToString();

            var days = period / 24;
            int hours = period.Value % 24;

            var dayStr = days <= 1 ? " day" : " days";
            var hourStr = hours <= 1 ? " hour" : " hours";

            return sb.Append(days).Append(dayStr).Append(" and ")
                     .Append(hours).Append(hourStr).ToString();
        }
    }
    public string LastBackup
    {
        get
        {
            if (Record.Item.LastBackupDateTime is null) return "Last backup: None";
            return "Last backup: " + Record.Item.LastBackupDateTime.Value.ToLocalTime().ToString();
        }
    }
    public required BackupItemRecord Record { get; init; }
}

public class RecordViewObject
{
    public string Name { get => RecordInfo.Name; }
    public string Created { get => RecordInfo.Created.ToLocalTime().ToString(); }
    public string Comment
    {
        get
        {
            if (RecordInfo.Comment is null) return "Comment: None";
            return "Comment: " + RecordInfo.Comment;
        }
    }
    public required RecordInfo RecordInfo { get; init; }
}