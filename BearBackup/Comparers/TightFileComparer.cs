using System.Diagnostics.CodeAnalysis;

namespace BearBackup.Comparers;

public class TightFileComparer : IFileComparer
{
    public bool CompareHash => true;

    public ComparedResult Equals(FileInfo fileInfoL, FileInfo fileInfoR)
    {
        if (fileInfoL.Name != fileInfoR.Name ||
            fileInfoL.Modified != fileInfoR.Modified ||
            fileInfoL.Size != fileInfoR.Size ||
            fileInfoL.SHA1 != fileInfoR.SHA1)
            return ComparedResult.ContentDiff;

        if (fileInfoL.Attributes != fileInfoR.Attributes)
            return ComparedResult.AttrDiff;

        return ComparedResult.Same;
    }

    public int GetHashCode([DisallowNull] FileInfo fileInfo)
    {
        return fileInfo.Name.GetHashCode() ^ fileInfo.Modified.GetHashCode()
            ^ fileInfo.Size.GetHashCode() ^ fileInfo.Attributes.GetHashCode()
            ^ (fileInfo.SHA1 is null ? 0 : fileInfo.SHA1.GetHashCode());
    }
}
