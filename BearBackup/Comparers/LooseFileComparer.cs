using System.Diagnostics.CodeAnalysis;

namespace BearBackup.Comparers;

public class LooseFileComparer : IFileComparer
{
    public bool CompareHash => false;

    public ComparedResult Equals(FileInfo fileInfoL, FileInfo fileInfoR)
    {
        if (fileInfoL.Name != fileInfoR.Name ||
            fileInfoL.Modified != fileInfoR.Modified ||
            fileInfoL.Size != fileInfoR.Size)
            return ComparedResult.ContentDiff;

        if (fileInfoL.Attributes != fileInfoR.Attributes)
            return ComparedResult.AttrDiff;

        return ComparedResult.Same;
    }

    public int GetHashCode([DisallowNull] FileInfo fileInfo)
    {
        return fileInfo.Name.GetHashCode() ^ fileInfo.Modified.GetHashCode()
            ^ fileInfo.Size.GetHashCode() ^ fileInfo.Attributes.GetHashCode();
    }
}
