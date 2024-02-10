namespace BearBackup.Tools;

internal static class Text
{
    internal static bool IsValidFileName(this string text)
    {
        if (text.Length > 255) return false;

        if (string.IsNullOrWhiteSpace(text)) return false;
        if (text != text.TrimStartAndEnd()) return false;
        return text.IndexOfAny(Path.GetInvalidFileNameChars()) == -1;
    }

    internal static bool IsValidDirName(this string text)
    {
        if (text.Length > 255) return false;

        if (string.IsNullOrWhiteSpace(text)) return false;
        if (text != text.TrimStartAndEnd()) return false;
        return text.IndexOfAny(Path.GetInvalidFileNameChars()) == -1;
    }

    internal static (string, string) SplitAt(this string text, int index)
    {
        if (index < 0 || index >= text.Length) throw new ArgumentOutOfRangeException(nameof(index));
        return (text[..index], text[index..]);
    }

    internal static string TrimStartAndEnd(this string text)
    {
        return text.TrimStart().TrimEnd();
    }

    internal static string InsertPathSepAtStart(this string text)
    {
        if (!text.StartsWith(Path.DirectorySeparatorChar))
            text = Path.DirectorySeparatorChar.ToString() + text;

        return text;
    }

    internal static string InsertPathSepAtEnd(this string text)
    {
        if (!text.EndsWith(Path.DirectorySeparatorChar))
            text += Path.DirectorySeparatorChar.ToString();

        return text;
    }

    internal static string InsertPathSepAtStartAndEnd(this string text)
    {
        return text.InsertPathSepAtStart().InsertPathSepAtEnd();
    }

    internal static string RemovePathSepAtStart(this string text)
    {
        if (text.StartsWith(Path.DirectorySeparatorChar))
            text = text[1..];

        return text;
    }

    internal static string RemovePathSepAtEnd(this string text)
    {
        if (text.EndsWith(Path.DirectorySeparatorChar))
            text = text[..^1];

        return text;
    }

    internal static string RemovePathSepAtStartAndEnd(this string text)
    {
        return text.RemovePathSepAtStart().RemovePathSepAtEnd();
    }
}
