namespace BearBackup;

public readonly struct ProgressEventArgs
{
    public bool IsDeterminate { get; init; }
    public int TotalNum { get; init; }
    public int CompletedNum { get; init; }
    public bool IsProgressing { get; init; }
}
