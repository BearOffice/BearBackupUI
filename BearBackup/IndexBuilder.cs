using BearBackup.BasicData;
using BearBackup.Tools;
using FileInfoIO = System.IO.FileInfo;
using DirInfoIO = System.IO.DirectoryInfo;

namespace BearBackup;

public static class IndexBuilder
{
    public static Index Build(string rootPath, out ExceptionInfo[] exceptions, Ignore? ignore = null)
    {
        if (!Directory.Exists(rootPath))
            throw new DirectoryNotFoundException($"Directory: \"{rootPath}\" does not exist.");

        rootPath = rootPath.InsertPathSepAtEnd();

        ignore ??= new Ignore();
        var abDirPaths = ignore.IgnoredDirs.Select(p => Path.Combine(rootPath, p.RemovePathSepAtStartAndEnd())).ToArray();
        var abFilePaths = ignore.IgnoredFiles.Select(p => Path.Combine(rootPath, p.RemovePathSepAtStartAndEnd())).ToArray();

        void BuildInner(string path, Index parIndex, List<ExceptionInfo> es)
        {
            DirInfoIO dirInfoIO;
            IEnumerable<FileInfoIO> fileInfoIOs;
            try
            {
                dirInfoIO = new DirInfoIO(path);
                fileInfoIOs = dirInfoIO
                    .EnumerateFiles()
                    .ExceptBy(abFilePaths, io => io.FullName)
                    .OrderBy(i => i.Name);
            }
            catch (Exception e)
            {
                es.Add(new ExceptionInfo(path, FileType.Dir, e));
                return;
            }

            foreach (var fileInfoIO in fileInfoIOs)
            {
                try
                {
                    var fileInfo = new FileInfo(fileInfoIO.Name, fileInfoIO.Attributes,
                        fileInfoIO.CreationTimeUtc, fileInfoIO.LastWriteTimeUtc, fileInfoIO.Length);
                    parIndex.InsertFileInfo(fileInfo);
                }
                catch (Exception e)
                {
                    es.Add(new ExceptionInfo(fileInfoIO.FullName, FileType.File, e));
                }
            }


            IEnumerable<DirInfoIO> dirInfoIOs;
            try
            {
                dirInfoIOs = dirInfoIO
                    .EnumerateDirectories()
                    .ExceptBy(abDirPaths, io => io.FullName)
                    .OrderBy(i => i.Name);
            }
            catch (Exception e)
            {
                es.Add(new ExceptionInfo(path, FileType.Dir, e));
                return;
            }

            foreach (var dirInfoIOIn in dirInfoIOs)
            {
                Index subIndex;
                try
                {
                    // dirInfoIn.FullName[rootPath.Length..] -> relative dir path
                    subIndex = new Index(
                        new DirInfo(dirInfoIOIn.FullName[rootPath.Length..], dirInfoIOIn.Attributes, dirInfoIOIn.CreationTimeUtc));
                    parIndex.InsertSubIndex(subIndex);
                }
                catch (Exception e)
                {
                    es.Add(new ExceptionInfo(dirInfoIOIn.FullName, FileType.Dir, e));
                    continue;
                }

                BuildInner(dirInfoIOIn.FullName, subIndex, es);
            }
        }

        var rootIndex = new Index();
        var es = new List<ExceptionInfo>();

        BuildInner(rootPath, rootIndex, es);

        exceptions = [.. es];
        return rootIndex;
    }

    public static void CalculateAllFilesHash(string rootPath, Index index, out ExceptionInfo[] exceptions,
        bool hashOverride = false, Action<ProgressEventArgs>? progress = null)
    {
        var infoArr = index.GetAllFileInfo().ToArray();
        CalculateAllFilesHash(rootPath, infoArr, out exceptions, hashOverride, progress);
    }

    public static void CalculateAllFilesHash(string rootPath, (string, FileInfo)[] files, out ExceptionInfo[] exceptions,
        bool hashOverride = false, Action<ProgressEventArgs>? progress = null)
    {
        var es = new List<ExceptionInfo>();
        var locker = new object();

        var infoArr = files;
        if (!hashOverride)
            infoArr = infoArr.Where(i => i.Item2.SHA1 is null).ToArray();

        var total = infoArr.Length;
        var current = 0;

        infoArr.AsParallel().AsOrdered().ForAll(t =>
        {
            var absolutePath = Path.Combine(rootPath, t.Item1);
            var hash = Hash.ComputeSHA1(absolutePath);

            if (hash is null)
            {
                lock (locker)
                {
                    es.Add(new ExceptionInfo(absolutePath, FileType.File, new Exception("Failed to compute SHA1.")));
                }
            }
            else
            {
                t.Item2.SetHash(hash);
            }

            Interlocked.Increment(ref current);
            progress?.Invoke(new ProgressEventArgs { TotalNum = total, CompletedNum = current, IsProgressing = true });
        });

        progress?.Invoke(new ProgressEventArgs { TotalNum = total, CompletedNum = current, IsProgressing = false });
        exceptions = [.. es];
    }
}
