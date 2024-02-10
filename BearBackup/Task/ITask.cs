namespace BearBackup.Task;

public interface ITask
{
    public bool IsCompleted { get; }
    public event Action<ProgressEventArgs>? Progress;

    public void Execute(out ExceptionInfo[] exceptions);
}
