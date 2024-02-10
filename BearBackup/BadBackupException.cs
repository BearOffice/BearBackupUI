namespace BearBackup;

public class BadBackupException : Exception
{
    public BadBackupException() : base() { }
    public BadBackupException(string message) : base(message) { }
    public BadBackupException(string message, Exception innerException)
        : base(message, innerException) { }
}
