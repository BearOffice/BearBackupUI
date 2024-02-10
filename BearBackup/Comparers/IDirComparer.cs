using BearBackup.BasicData;
using System.Diagnostics.CodeAnalysis;

namespace BearBackup.Comparers;

public interface IDirComparer
{
    public ComparedResult Equals(DirInfo dirInfoL, DirInfo dirInfoR);
    public int GetHashCode([DisallowNull] DirInfo dirInfo);
}
