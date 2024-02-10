using BearBackup.Tools;

namespace BearBackup.Task;

internal class MirroringRestoreTask : IRestoreTask
{
    public bool IsCompleted { get; private set; }
    public event Action<ProgressEventArgs>? Progress;
    private readonly MirroringBackup _backup;
    private readonly string _restorePath;
    private readonly Index? _indexToRestore;
    private readonly (Index, FileInfo[])? _filesToRestore;

    internal MirroringRestoreTask(MirroringBackup backup, string restorePath, Index indexToRestore)
    {
        _backup = backup;
        _restorePath = restorePath.InsertPathSepAtEnd();
        _indexToRestore = indexToRestore;
    }

    internal MirroringRestoreTask(MirroringBackup backup, string restorePath, (Index, FileInfo[]) filesToRestore)
    {
        _backup = backup;
        _restorePath = restorePath.InsertPathSepAtEnd();
        _filesToRestore = filesToRestore;
    }

    public void Execute(out ExceptionInfo[] exceptions)
    {
        if (IsCompleted) throw new Exception("Task already completed.");

        var dirInfo = new DirectoryInfo(_restorePath);
        if (!dirInfo.Exists) dirInfo.Create();

        if (_indexToRestore is not null) RestoreIndex(out exceptions);
        else if (_filesToRestore is not null) RestoreFiles(out exceptions);
        else throw new BadBackupException("The specified restore target is null.");

        IsCompleted = true;
    }

    private void RestoreIndex(out ExceptionInfo[] exceptions)
    {
        if (_indexToRestore is null) throw new BadBackupException("The specified restore target is null.");
        AddIndeterminateEvent();

        var es = new List<ExceptionInfo>();

        var basePath = _indexToRestore.DirInfo?.FullName.InsertPathSepAtEnd() ?? string.Empty;
        var files = _indexToRestore.GetAllFileInfo().ToArray();

        var totalNum = files.Length + 1;  // 1 -> Treat dirs as 1 batch
        var count = 0;
        AddEvent(totalNum, count, true);

        foreach (var dirInfo in _indexToRestore.GetAllDirInfo())
        {
            // Skip base path.
            if (dirInfo.FullName.InsertPathSepAtEnd().Length == basePath.Length)
                continue;

            // dirInfo.FullName[basePath.Length..] -> relative dir path
            var createPath = Path.Combine(_restorePath, dirInfo.FullName[basePath.Length..]);
            try
            {
                var dirInfoIO = new DirectoryInfo(createPath);
                dirInfoIO.Create();
                dirInfoIO.Attributes = dirInfo.Attributes;
            }
            catch (Exception e)
            {
                es.Add(new ExceptionInfo(createPath, FileType.Dir, e));
            }
        }
        count++;
        AddEvent(totalNum, count, true);

        foreach (var (filePath, fileInfo) in _indexToRestore.GetAllFileInfo())
        {
            var sourcePath = Path.Combine(_backup.MirrorPath, filePath);
            // filePath[basePath.Length..] -> relative file path
            var createPath = Path.Combine(_restorePath, filePath[basePath.Length..]);
            try
            {
                File.Copy(sourcePath, createPath);
                File.SetAttributes(createPath, fileInfo.Attributes);
            }
            catch (Exception e)
            {
                es.Add(new ExceptionInfo(createPath, FileType.File, e));
            }

            count++;
            AddEvent(totalNum, count, true);
        }

        exceptions = [.. es];
        AddEvent(totalNum, count, false);
    }

    private void RestoreFiles(out ExceptionInfo[] exceptions)
    {
        if (_filesToRestore is null) throw new BadBackupException("The specified restore target is null.");
        AddIndeterminateEvent();

        var es = new List<ExceptionInfo>();

        var subIndex = _filesToRestore.Value.Item1;
        var fileInfoArr = _filesToRestore.Value.Item2;

        var totalNum = fileInfoArr.Length;
        var count = 0;

        foreach (var fileInfo in fileInfoArr)
        {
            var fileFullName = subIndex.GetFileFullName(fileInfo);
            var sourcePath = Path.Combine(_backup.MirrorPath, fileFullName);
            var createPath = Path.Combine(_restorePath, fileInfo.Name);
            try
            {
                File.Copy(sourcePath, createPath);
                File.SetAttributes(createPath, fileInfo.Attributes);
            }
            catch (Exception e)
            {
                es.Add(new ExceptionInfo(createPath, FileType.File, e));
            }

            count++;
            AddEvent(totalNum, count, true);
        }

        exceptions = [.. es];
        AddEvent(totalNum, count, false);
    }

    private void AddEvent(int totalNum, int completedNum, bool isProgressing)
    {
        Progress?.Invoke(new ProgressEventArgs
        {
            IsDeterminate = true,
            TotalNum = totalNum,
            CompletedNum = completedNum,
            IsProgressing = isProgressing,
        });
    }

    private void AddIndeterminateEvent()
    {
        Progress?.Invoke(new ProgressEventArgs
        {
            IsDeterminate = false,
            TotalNum = 0,
            CompletedNum = 0,
            IsProgressing = true,
        });
    }
}
