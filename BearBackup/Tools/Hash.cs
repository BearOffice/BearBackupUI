using System.Security.Cryptography;

namespace BearBackup.Tools;

public static class Hash
{
    public static string? ComputeSHA1(string path)
    {
        try
        {
            var sha1 = SHA1.Create();

            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);

            var bs = sha1.ComputeHash(fs);
            var result = Convert.ToHexString(bs).ToLower();
            return result;
        }
        catch { }

        return null;
    }

    public static string?[] ComputeSHA1(string[] paths)
    {
        return [.. paths.AsParallel().Select(ComputeSHA1)];
    }
}
