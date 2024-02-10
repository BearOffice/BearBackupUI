using System.Diagnostics.CodeAnalysis;

namespace BearBackup.BasicData;

[Serializable]
public record FileInfo
{
    public required string Name { get; init; }
    public required FileAttributes Attributes { get; init; }
    public required DateTime Created { get; init; }
    public required DateTime Modified { get; init; }
    public required long Size { get; init; }
    public string? SHA1 { get; set; }

    public FileInfo() { }

    [SetsRequiredMembers]
    public FileInfo(string name, FileAttributes attributes, DateTime created, DateTime modified, long size)
    {
        Name = name;
        Attributes = attributes;
        Created = created;
        Modified = modified;
        Size = size;
        SHA1 = null;
    }

    [SetsRequiredMembers]
    public FileInfo(string name, FileAttributes attributes, DateTime created, DateTime modified, long size, string? sha1)
    {
        Name = name;
        Attributes = attributes;
        Created = created;
        Modified = modified;
        Size = size;
        SHA1 = sha1;
    }

    public void SetHash(string sha1)
    {
        SHA1 = sha1;
    }
}
