using BearBackupUI.Services;
using Wpf.Ui.Tray.Controls;
using Microsoft.Extensions.DependencyInjection;
using System.Text;

namespace BearBackupUI.Windows;

public partial class NotifyIconWindow : Window
{
	private readonly IServiceProvider _serviceProvider;
	private readonly ConfigService _configService;
	private readonly TaskService _taskService;
	private MainWindow? _mainWindow;

	public NotifyIconWindow(IServiceProvider serviceProvider,
		ConfigService configService, BackupService backupService, TaskService taskService)
	{
		InitializeComponent();
		_serviceProvider = serviceProvider;
		_configService = configService;
		_taskService = taskService;

		backupService.FailedToLoad += BackupService_FailedToLoad;
		_taskService.FaultOccurred += TaskService_FaultOccurred;

		if (!_configService.LaunchMinimized) ShowMainWindow();
	}

	private void TaskService_FaultOccurred(object? sender, Exception e)
	{
		if (_mainWindow is not null) return;

		MessageBox.Show($"Task faulted.\n{e.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
	}

	private void BackupService_FailedToLoad(object? sender, BackupItemRecord[] e)
	{
		if (_mainWindow is not null) return;
		if (e.Length == 0) return;

		var result = MessageBox.Show($"Remove repositories failed to open?\n{BuildErrorMessage(e)}", "Question",
			MessageBoxButton.YesNo, MessageBoxImage.Question);

		if (result == MessageBoxResult.Yes)
		{
			foreach (var item in e)
			{
				_configService.RemoveBackupItemRecord(item.ID);
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

	private void ContextMenu_KeyUp(object sender, KeyEventArgs e)
	{
		if (e.Key == Key.O)
			ShowMainWindow();
		else if (e.Key == Key.Q)
			Application.Current.Shutdown();
	}

	private void OpenMenuItem_Click(object sender, RoutedEventArgs e)
	{
		ShowMainWindow();
	}

	private void QuitMenuItem_Click(object sender, RoutedEventArgs e)
	{
		if (_taskService.IsRunning || _taskService.TaskQueue.Length != 0)
		{
			var result = MessageBox.Show($"Tasks still running. " +
				$"Force quit may cause fatal damage to the backup repository.\nQuit this application?", "Warning",
				MessageBoxButton.OKCancel, MessageBoxImage.Warning, MessageBoxResult.Cancel);

			if (result == MessageBoxResult.OK) Application.Current.Shutdown();
		}
		else
		{
			Application.Current.Shutdown();
		}
	}

	private void NotifyIcon_LeftClick(NotifyIcon sender, RoutedEventArgs e)
	{
		ShowMainWindow();
	}

	private void ShowMainWindow()
	{
		if (_mainWindow is not null)
		{
			_mainWindow.Show();
			BringWindowToFront(_mainWindow);
			return;
		}

		_mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
		_mainWindow.Closed += (sender, args) =>
		{
			_mainWindow = null;

			GC.WaitForPendingFinalizers();
			GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive);
		};

		_mainWindow.Show();
		BringWindowToFront(_mainWindow);
	}

	private static void BringWindowToFront(Window window)
	{
		if (window.WindowState == WindowState.Minimized)
			window.WindowState = WindowState.Normal;
		window.Activate();
	}
}
