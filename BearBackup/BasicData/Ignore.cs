namespace BearBackup.BasicData;

public class Ignore
{
    public string[] IgnoredDirs => [.. _ignoredDirs];
    public string[] IgnoredFiles => [.. _ignoredFiles];
    private readonly List<string> _ignoredDirs;
    private readonly List<string> _ignoredFiles;

    public Ignore()
    {
        _ignoredDirs = [];
        _ignoredFiles = [];
    }

    public Ignore(string[] dirPaths, string[] filePaths)
    {
        _ignoredDirs = [.. dirPaths];
        _ignoredFiles = [.. filePaths];
    }

    public void InsertFilePath(string filePath)
    {
        _ignoredFiles.Add(filePath);
    }

    public void InsertDirPath(string dirPath)
    {
        _ignoredDirs.Add(dirPath);
    }

    public bool RemoveFilePath(string filePath)
    {
        return _ignoredFiles.Remove(filePath);
    }

    public bool RemoveDirPath(string dirPath)
    {
        return _ignoredDirs.Remove(dirPath);
    }
}
