using BearBackup;
using BearBackup.Task;
using BearBackupUI.Core;
using BearBackupUI.Core.Actions;
using BearBackupUI.Core.DataTags;
using BearBackupUI.Helpers;
using BearBackupUI.Services;

namespace BearBackupUI.Stores;

public class TaskStore : IStore
{
    public event EventHandler<DataArgs>? Changed;

    private readonly DispatchCenter _dispatchCenter;
    private readonly TaskService _taskService;

    public TaskStore(DispatchCenter dispatchCenter, TaskService taskService)
    {
        _dispatchCenter = dispatchCenter;
        _dispatchCenter.AddListener(typeof(TaskAction), ActionReceived);

        _taskService = taskService;
        _taskService.Executing += TaskService_Executing;
        _taskService.TasksChanged += TaskService_TasksChanged;
		_taskService.TimeElapsed += TaskService_TimeElapsed;
    }

	private void TaskService_TimeElapsed(object? sender, TimeSpan e)
	{
		var data = new DataArgs();
		data.AddData(TaskTag.TimeElapsed, e);

		Changed?.Invoke(this, data);
	}

	private void TaskService_Executing(object? sender, ProgressEventArgs e)
    {
        DataArgs? data = null;
        if (!e.IsProgressing && _taskService.TaskQueue.Length == 0)
            data = GetData();
        data ??= new DataArgs();

        data.AddData(TaskTag.TaskProgress, new TaskProgressArgs
        {
            IsDeterminate = e.IsDeterminate,
            Percentage = e.TotalNum == 0 ? 0 : (double)e.CompletedNum / e.TotalNum,
            IsFinished = !e.IsProgressing,
            IsLastTask = _taskService.TaskQueue.Length == 0,
        });

        Changed?.Invoke(this, data);
    }

    private void TaskService_TasksChanged(object? sender, EventArgs e)
    {
        Changed?.Invoke(this, GetData());
    }

    private void ActionReceived(object? sender, ActionArgs e)
    {
        var task = (ITask)(e.GetAnonymousData() ?? throw new NullReferenceException());

        if (e.Type is TaskAction.TaskMoveUp)
            _taskService.MoveUpTask(task);
        else if (e.Type is TaskAction.TaskMoveDown)
            _taskService.MoveDownTask(task);
        else if (e.Type is TaskAction.RemoveTask)
            _taskService.RemoveTask(task);
    }

    public DataArgs GetData()
    {
        var data = new DataArgs();
#pragma warning disable CS8604
        data.AddData(TaskTag.RunningTask, _taskService.RunningTask);
#pragma warning restore CS8604
        data.AddData(TaskTag.TaskQueue, _taskService.TaskQueue);
        data.AddData(TaskTag.CompletedTasks, _taskService.CompletedTasks);

        return data;
    }

    public void Dispose()
    {
        _dispatchCenter.RemoveListener(ActionReceived);

        Changed.UnsubscribeAll();
        Changed = null;

        GC.SuppressFinalize(this);
    }
}

public struct TaskProgressArgs
{
    public bool IsDeterminate;
    public double Percentage;
    public bool IsFinished;
    public bool IsLastTask;
}
