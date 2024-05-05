using BearBackup.Tools;
using System.Diagnostics.CodeAnalysis;

namespace BearBackup.BasicData;

public class RecordInfo : IEquatable<RecordInfo>
{
    public string Name { get; }
    public DateTime Created { get; }
    public string? Comment { get; }

    public RecordInfo(string name, DateTime created, string? comment = null)
    {
        if (!name.IsValidFileName())
            throw new ArgumentException($"Name `{name}` is not valid. The record name must be a valid file name.");

        Name = name;
        Created = created;
        Comment = comment;
    }

    public RecordInfo(string name, string? comment = null)
    {
		if (!name.IsValidFileName())
			throw new ArgumentException($"Name `{name}` is not valid. The record name must be a valid file name.");

		Name = name;
        Created = DateTime.UtcNow;
        Comment = comment;
    }

    public override bool Equals([AllowNull] object right)
    {
        if (right is RecordInfo recordInfo)
        {
            if (right is not null)
                return Name.Equals(recordInfo.Name, StringComparison.CurrentCultureIgnoreCase);
        }

        return false;
    }

    public bool Equals([AllowNull] RecordInfo other)
    {
        if (other is null) return false;
        return Name.Equals(other.Name, StringComparison.CurrentCultureIgnoreCase);
    }

    public static bool operator ==(RecordInfo left, RecordInfo right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(RecordInfo left, RecordInfo right)
    {
        return !left.Equals(right);
    }

    public override int GetHashCode()
    {
        return Name.ToLower().GetHashCode();
    }
}
