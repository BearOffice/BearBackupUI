namespace BearBackup;

public static class Environment
{
    // versioning backup repo
    public const string Blob = "blob";

    // mirroring backup repo
    public const string Mirror = "mirror";

    // dir name in versioning backup, file name in mirroring backup
    public const string Index = "index";

    public const string Record = "record";
    public const string BackupIgnore = "ignore";

    public const string IgnoredDirs = "Dirs";
    public const string IgnoredFiles = "Files";
}
