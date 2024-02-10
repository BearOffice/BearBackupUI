using BearBackup.BasicData;
using System.Diagnostics.CodeAnalysis;

namespace BearBackup.Comparers;

public class GeneralDirComparer : IDirComparer
{
    public ComparedResult Equals(DirInfo dirInfoL, DirInfo dirInfoR)
    {
        if (dirInfoL.FullName != dirInfoR.FullName)
            return ComparedResult.ContentDiff;

        if (dirInfoL.Attributes != dirInfoR.Attributes)
            return ComparedResult.AttrDiff;

        return ComparedResult.Same;
    }

    public int GetHashCode([DisallowNull] DirInfo dirInfo)
    {
        return dirInfo.FullName.GetHashCode() ^ dirInfo.Attributes.GetHashCode();
    }
}
