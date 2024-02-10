using System.Diagnostics.CodeAnalysis;

namespace BearBackup.BasicData;

[Serializable]
public record DirInfo
{
    public required string FullName { get; init; } 
    public required FileAttributes Attributes { get; init; }
    public required DateTime Created { get; init; }

    public DirInfo() { }

    [SetsRequiredMembers]
    public DirInfo(string fullName, FileAttributes attributes, DateTime created)
    {
        FullName = fullName;
        Attributes = attributes;
        Created = created;
    }
}
