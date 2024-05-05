namespace BearBackup.Tools;

internal static class DirectoryInfoExtensions
{
	internal static bool IsEmpty(this DirectoryInfo info)
	{
		if (!info.Exists) return true;
		return !info.EnumerateFiles().Any() && !info.EnumerateDirectories().Any();
	}
}
