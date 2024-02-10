namespace BearBackup;

public record ExceptionInfo
{
    public string Path { get; init; }
    public FileType FileType { get; init; }
    public Exception Exception { get; init; }

    public ExceptionInfo(string path, FileType fileType, Exception exception)
    {
        Path = path;
        FileType = fileType;
        Exception = exception;
    }
}

public enum FileType
{
    File,
    Dir,
    Unknown
}