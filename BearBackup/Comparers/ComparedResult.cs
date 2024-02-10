namespace BearBackup.Comparers;

public enum ComparedResult
{
    Same = 0b1,
    AttrDiff = 0b10,
    ContentDiff = 0b100,
}
