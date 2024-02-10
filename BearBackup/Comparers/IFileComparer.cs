using System.Diagnostics.CodeAnalysis;

namespace BearBackup.Comparers;

public interface IFileComparer
{
    public bool CompareHash { get; }
    public ComparedResult Equals(FileInfo fileInfoL, FileInfo fileInfoR);
    public int GetHashCode([DisallowNull] FileInfo fileInfo);
}
