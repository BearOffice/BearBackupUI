using System.Diagnostics.CodeAnalysis;
using BearBackup.BasicData;
using BearBackup.Comparers;

namespace BearBackup;

public static class IndexComparison
{
    public static ((Index, FileInfo[])[], (Index, FileInfo[])[]) DiffFileInfo(Index indexL, Index indexR,
        IFileComparer fileComparer, IDirComparer dirComparer, bool considerAttr = true)
    {
        var flatL = indexL.GetAllFileInfoGrouped()
            .SelectMany(tuple => tuple.Item2.Select(item => (tuple.Item1, item)));
        var flatR = indexR.GetAllFileInfoGrouped()
            .SelectMany(tuple => tuple.Item2.Select(item => (tuple.Item1, item)));

        var eqComparer = new FileEqualityComparer(fileComparer, dirComparer, considerAttr);
        var uniqueL = flatL.Except(flatR, eqComparer);
        var uniqueR = flatR.Except(flatL, eqComparer);

        var groupedL = uniqueL.GroupBy(tuple => tuple.Item1)
                              .Select(group => (group.Key, group.Select(t => t.ToTuple().Item2).ToArray()))
                              .ToArray();
        var groupedR = uniqueR.GroupBy(tuple => tuple.Item1)
                              .Select(group => (group.Key, group.Select(t => t.ToTuple().Item2).ToArray()))
                              .ToArray();

        return (groupedL, groupedR);
    }

    public static ((Index, FileInfo[])[], (Index, FileInfo[])[]) IntersectFileInfo(Index indexL, Index indexR,
        IFileComparer fileComparer, IDirComparer dirComparer, bool considerAttr = true)
    {
        var flatL = indexL.GetAllFileInfoGrouped()
            .SelectMany(tuple => tuple.Item2.Select(item => (tuple.Item1, item))).ToArray();
        var flatR = indexR.GetAllFileInfoGrouped()
            .SelectMany(tuple => tuple.Item2.Select(item => (tuple.Item1, item))).ToArray();

        var eqComparer = new FileEqualityComparer(fileComparer, dirComparer, considerAttr);
        var intersectL = flatL.Intersect(flatR, eqComparer).ToArray();
        var intersectR = flatR.Intersect(flatL, eqComparer).ToArray();

        var groupedL = intersectL.GroupBy(tuple => tuple.Item1)
                                 .Select(group => (group.Key, group.Select(t => t.ToTuple().Item2).ToArray()))
                                 .ToArray();
        var groupedR = intersectR.GroupBy(tuple => tuple.Item1)
                                 .Select(group => (group.Key, group.Select(t => t.ToTuple().Item2).ToArray()))
                                 .ToArray();

        return (groupedL, groupedR);
    }

    public static ((Index, DirInfo)[], (Index, DirInfo)[]) DiffDirInfo(Index indexL, Index indexR,
        IDirComparer comparer, bool considerAttr = true)
    {
        var enumL = indexL.GetAllDirInfoGrouped();
        var enumR = indexR.GetAllDirInfoGrouped();

        var eqComparer = new DirEqualityComparer(comparer, considerAttr);
        var uniqueL = enumL.Except(enumR, eqComparer);
        var uniqueR = enumR.Except(enumL, eqComparer);

        return (uniqueL.ToArray(), uniqueR.ToArray());
    }

    public static ((Index, DirInfo)[], (Index, DirInfo)[]) UnionDirInfo(Index indexL, Index indexR,
        IDirComparer comparer, bool considerAttr = true)
    {
        var enumL = indexL.GetAllDirInfoGrouped();
        var enumR = indexR.GetAllDirInfoGrouped();

        var eqComparer = new DirEqualityComparer(comparer, considerAttr);
        var uniqueL = enumL.Union(enumR, eqComparer);
        var uniqueR = enumR.Union(enumL, eqComparer);

        return (uniqueL.ToArray(), uniqueR.ToArray());
    }

    private class FileEqualityComparer : IEqualityComparer<(Index, FileInfo)>
    {
        private readonly IFileComparer _fileComparer;
        private readonly IDirComparer _dirComparer;
        private readonly bool _attrDiff;

        internal FileEqualityComparer(IFileComparer fileComparer, IDirComparer dirComparer, bool attrDiff)
        {
            _fileComparer = fileComparer;
            _dirComparer = dirComparer;
            _attrDiff = attrDiff;
        }

        public bool Equals((Index, FileInfo) x, (Index, FileInfo) y)
        {
            if (x.Item1.DirInfo is null && y.Item1.DirInfo is null)
            {
                var result = _fileComparer.Equals(x.Item2, y.Item2);
                if (result.HasFlag(ComparedResult.Same) || (!_attrDiff && result.HasFlag(ComparedResult.AttrDiff)))
                    return true;
            }
            else if (x.Item1.DirInfo is not null && y.Item1.DirInfo is not null)
            {
                if (!_dirComparer.Equals(x.Item1.DirInfo, y.Item1.DirInfo).HasFlag(ComparedResult.ContentDiff))
                {
                    var result = _fileComparer.Equals(x.Item2, y.Item2);
                    if (result.HasFlag(ComparedResult.Same) || (!_attrDiff && result.HasFlag(ComparedResult.AttrDiff)))
                        return true;
                }
            }

            return false;
        }

        public int GetHashCode([DisallowNull] (Index, FileInfo) obj)
        {
            // ^ attr hash -> ignore attr diff 
            var dirHashCode = obj.Item1.DirInfo is null ?
                0 : (_dirComparer.GetHashCode(obj.Item1.DirInfo) ^ obj.Item1.DirInfo.Attributes.GetHashCode());

            var fileHashCode = _fileComparer.GetHashCode(obj.Item2);
            if (!_attrDiff)
                fileHashCode ^= obj.Item2.Attributes.GetHashCode();

            return dirHashCode ^ fileHashCode;
        }
    }

    private class DirEqualityComparer : IEqualityComparer<(Index, DirInfo)>
    {
        private readonly IDirComparer _comparer;
        private readonly bool _attrDiff;

        internal DirEqualityComparer(IDirComparer comparer, bool attrDiff)
        {
            _comparer = comparer;
            _attrDiff = attrDiff;
        }

        public bool Equals((Index, DirInfo) x, (Index, DirInfo) y)
        {
            if (x.Item2 is null && y.Item2 is null)
            {
                return true;
            }
            else if (x.Item2 is not null && y.Item2 is not null)
            {
                var result = _comparer.Equals(x.Item2, y.Item2);
                if (result.HasFlag(ComparedResult.Same) || (!_attrDiff && result.HasFlag(ComparedResult.AttrDiff)))
                    return true;
            }

            return false;
        }

        public int GetHashCode([DisallowNull] (Index, DirInfo) obj)
        {
            var hash = _comparer.GetHashCode(obj.Item2);
            if (!_attrDiff) hash ^= obj.Item2.Attributes.GetHashCode();

            return hash;
        }
    }
}