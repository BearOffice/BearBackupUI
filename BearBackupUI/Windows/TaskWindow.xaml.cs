using BearBackup;
using BearBackup.Task;
using BearBackupUI.Core;
using BearBackupUI.Core.Actions;
using BearBackupUI.Core.DataTags;
using BearBackupUI.Helpers;
using BearBackupUI.Services;
using BearBackupUI.Stores;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Media.Media3D;
using Stopwatch = BearBackupUI.Helpers.Stopwatch;

namespace BearBackupUI.Windows;

public partial class TaskWindow : FluentWindow
{
	private readonly DispatchCenter _dispatchCenter;
	private readonly TaskStore _store;
	private bool _isRunning;
	private int _prevMoveOp;  // 0 -> none, -1 -> up, 1 -> down in task queue.

	public TaskWindow(DispatchCenter dispatchCenter, TaskStore taskStore)
	{
		InitializeComponent();
		DataContext = this;

		_dispatchCenter = dispatchCenter;
		_store = taskStore;

		Store_Changed(null, _store.GetData());
		_store.Changed += (sender, e) => this.InvokeIfNeeded(() => Store_Changed(sender, e));
	}

	private void Store_Changed(object? sender, DataArgs e)
	{
		if (e.TryGetData(TaskTag.RunningTask, out var running))
		{
			var item = ((BackupItemRecord, ITask)?)running;
			if (item is not null)
			{
				if (!_isRunning)
				{
					_isRunning = true;
					SwitchRunningTaskPanel(_isRunning);
				}

				var obj = (TaskViewObject)TaskContentControl.Content;
				if (obj is null || obj.Record != item.Value.Item1)
				{
					ResetTaskContentControl(true);
					TaskContentControl.Content = new TaskViewObject { Record = item.Value.Item1, Task = item.Value.Item2 };
				}
			}
		}

		if (e.TryGetData(TaskTag.TaskQueue, out var tasks))
		{
			var item = ((BackupItemRecord, ITask)[])(tasks ?? throw new NullReferenceException());
			var selectedIndex = TaskQueueView.SelectedIndex + _prevMoveOp;
			TaskQueueView.ItemsSource = new ObservableCollection<TaskViewObject>(
				item.Select(i => new TaskViewObject { Record = i.Item1, Task = i.Item2 }));

			if (selectedIndex >= 0 || item.Length > 0)
			{
				if (item.Length > selectedIndex)
					TaskQueueView.SelectedIndex = selectedIndex;
				else
					TaskQueueView.SelectedIndex = item.Length - 1;

				TaskListView_SelectionChanged(null!, null!);  // Refresh the status of buttons
			}

			_prevMoveOp = 0;
		}

		if (e.TryGetData(TaskTag.CompletedTasks, out var completed))
		{
			var item = (CompletedTaskInfo[])(completed ?? throw new NullReferenceException());
			CompletedListView.ItemsSource = new ObservableCollection<CompletedTaskViewObject>(
				item.Select(i => new CompletedTaskViewObject { Info = i })
					.Reverse());

			if (item.Length > 0) CompletedListView.SelectedIndex = 0;
		}

		if (e.TryGetData(TaskTag.TaskProgress, out var args))
		{
			if (!_isRunning)
			{
				_isRunning = true;
				SwitchRunningTaskPanel(_isRunning);
			}

			var item = (TaskProgressArgs)(args ?? throw new NullReferenceException());

			TaskProgressBar.IsIndeterminate = !item.IsDeterminate;

			var pc = item.Percentage * 100;
			TaskProgressBar.Value = pc;
			ProgressLabel.Content = $"{pc:F2}";

			if (item.IsFinished)
			{
				_isRunning = false;
				if (item.IsLastTask) SwitchRunningTaskPanel(_isRunning);
			}
		}

		if (e.TryGetData(TaskTag.TimeElapsed, out var time))
		{
			if (_isRunning)
			{
				var item = (TimeSpan)(time ?? throw new NullReferenceException());
				UpdateTimeLabel(item);
			}
		}
	}

	private void UpdateTimeLabel(TimeSpan timeSpan)
	{
		this.InvokeIfNeeded(() =>
		{
			TimeLabel.Content = timeSpan.ToString(@"hh\:mm\:ss");
		});
	}

	private void SwitchRunningTaskPanel(bool visibility)
	{
		if (visibility)
		{
			TaskContentLabel.Visibility = Visibility.Hidden;
			TaskContentControl.Visibility = Visibility.Visible;
			ResetTaskContentControl(true);
		}
		else
		{
			TaskContentLabel.Visibility = Visibility.Visible;
			TaskContentControl.Visibility = Visibility.Hidden;
			TaskContentControl.Content = null;
			ResetTaskContentControl(false);
		}
	}

	private void ResetTaskContentControl(bool isIndeterminate)
	{
		TaskProgressBar.IsIndeterminate = isIndeterminate;
		TaskProgressBar.Value = 0;
		ProgressLabel.Content = "0";
		TimeLabel.Content = "00:00:00";
	}

	private void OnClosed(object sender, EventArgs e)
	{
		_store.Dispose();
	}

	private void RemoveButton_Click(object sender, RoutedEventArgs e)
	{
		if (TaskQueueView.SelectedItem is not null)
		{
			var item = (TaskViewObject)TaskQueueView.SelectedItem;
			_dispatchCenter.DispatchEvent(new ActionArgs(TaskAction.RemoveTask, item.Task));
		}
	}

	private void UpButton_Click(object sender, RoutedEventArgs e)
	{
		if (TaskQueueView.SelectedItem is not null)
		{
			var item = (TaskViewObject)TaskQueueView.SelectedItem;
			_prevMoveOp = -1;
			_dispatchCenter.DispatchEvent(new ActionArgs(TaskAction.TaskMoveUp, item.Task));
		}
	}

	private void DownButton_Click(object sender, RoutedEventArgs e)
	{
		if (TaskQueueView.SelectedItem is not null)
		{
			var item = (TaskViewObject)TaskQueueView.SelectedItem;
			_prevMoveOp = 1;
			_dispatchCenter.DispatchEvent(new ActionArgs(TaskAction.TaskMoveDown, item.Task));
		}
	}

	private void TaskListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		UpButton.IsEnabled = TaskQueueView.SelectedIndex > 0;
		DownButton.IsEnabled = TaskQueueView.SelectedIndex >= 0 && TaskQueueView.SelectedIndex < TaskQueueView.Items.Count - 1;
	}
}

public class TaskViewObject
{
	public string TaskTitle
	{
		get
		{
			var sb = new StringBuilder("[");
			sb.Append(TaskService.GetTaskType(Task).ToString());
			sb.Append("] ");
			sb.Append(Path.GetFileName(Record.Item.BackupPath));

			return sb.ToString();
		}
	}
	public string BackupTarget { get => "Backup target: " + Record.Item.BackupTarget; }
	public required BackupItemRecord Record { get; init; }
	public required ITask Task { get; init; }
}

public class CompletedTaskViewObject
{
	public Visibility FaultVisibility { get => Info.IsFaulted ? Visibility.Visible : Visibility.Collapsed; }
	public string TaskTitle
	{
		get
		{
			var sb = new StringBuilder("[");
			sb.Append(Info.TaskType.ToString());
			sb.Append("] ");
			sb.Append(Path.GetFileName(Info.BackupPath));

			return sb.ToString();
		}
	}
	public string BackupTarget { get => "Backup target: " + Info.BackupTarget; }
	public string ExceptionInfo
	{
		get
		{
			var sb = new StringBuilder(Info.FailureCount.ToString());
			if (Info.FailureCount <= 1)
				sb.Append(" exception occurred.");
			else
				sb.Append(" exceptions occurred.");
			return sb.ToString();
		}
	}
	public string CompletedTime { get => "Completed at: " + Info.CompletedTime.ToString(); }
	public required CompletedTaskInfo Info { get; init; }
}