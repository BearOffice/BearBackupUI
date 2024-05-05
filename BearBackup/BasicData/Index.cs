using BearBackup.Tools;
using BearMarkupLanguage;
using BearMarkupLanguage.Conversion;

namespace BearBackup.BasicData;

[Serializable]
public class Index
{
	public DirInfo? DirInfo { get; init; }
	public Index[] SubIndexArr
	{
		get => _subIndexList.ToArray();
		init => _subIndexList = [.. value];
	}
	public FileInfo[] FileInfoArr
	{
		get => _fileInfoList.ToArray();
		init => _fileInfoList = [.. value];
	}
	private List<Index> _subIndexList;
	private List<FileInfo> _fileInfoList;

	public Index()
	{
		_subIndexList = [];
		_fileInfoList = [];
	}

	public Index(DirInfo? dirInfo)
	{
		DirInfo = dirInfo;
		_subIndexList = [];
		_fileInfoList = [];
	}

	public Index(DirInfo? dirInfo, Index[] subIndexList, FileInfo[] fileInfoList)
	{
		DirInfo = dirInfo;
		_subIndexList = [.. subIndexList];
		_fileInfoList = [.. fileInfoList];
	}

	public void InsertSubIndex(Index subIndex)
	{
		if (subIndex.DirInfo is null)
			throw new Exception("DirInfo in SubIndex cannot be null.");

		_subIndexList.Add(subIndex);
	}

	public void InsertSubIndex(DirInfo dirInfo)
	{
		InsertSubIndex(new Index(dirInfo));
	}

	public void InsertFileInfo(FileInfo fileInfo)
	{
		_fileInfoList.Add(fileInfo);
	}

	public bool RemoveSubIndex(Index subIndex)
	{
		return _subIndexList.Remove(subIndex);
	}

	public bool RemoveSubIndex(DirInfo dirInfo)
	{
		var result = _subIndexList.Find(index => index.DirInfo == dirInfo);
		if (result is not null)
			return _subIndexList.Remove(result);
		else
			return false;
	}

	public bool RemoveFileInfo(FileInfo fileInfo)
	{
		return _fileInfoList.Remove(fileInfo);
	}

	public bool RemoveFileInfo(string fileName)
	{
		var result = _fileInfoList.Find(file => file.Name == fileName);
		if (result is not null)
			return _fileInfoList.Remove(result);
		else
			return false;
	}

	public Index? GetSubIndex(string? dirPath)
	{
		if (string.IsNullOrWhiteSpace(dirPath)) return null;

		var fullName = DirInfo?.FullName.InsertPathSepAtEnd() ?? string.Empty;
		if (!dirPath.StartsWith(fullName)) return null;

		var current = this;
		var parts = dirPath[fullName.Length..].Split(Path.DirectorySeparatorChar);
		for (var i = 1; i < parts.Length; i++)
		{
			var path = fullName + string.Join(Path.DirectorySeparatorChar, parts[..i]);
			var subIndex = current._subIndexList.Find(index => index.DirInfo?.FullName == path);

			if (subIndex is null) return null;
			current = subIndex;
		}

		return current._subIndexList.Find(index => index.DirInfo?.FullName == dirPath);
	}

	public IEnumerable<Index> GetAllIndexes()
	{
		yield return this;

		foreach (var subIndex in _subIndexList)
		{
			foreach (var result in subIndex.GetAllIndexes())
			{
				yield return result;
			}
		}
	}

	public IEnumerable<(Index, DirInfo)> GetAllDirInfoGrouped()
	{
		if (DirInfo is not null)
			yield return (this, DirInfo);

		foreach (var subIndex in _subIndexList)
		{
			foreach (var result in subIndex.GetAllDirInfoGrouped())
			{
				yield return result;
			}
		}
	}

	public IEnumerable<(Index, FileInfo[])> GetAllFileInfoGrouped()
	{
		yield return (this, _fileInfoList.ToArray());

		foreach (var subIndex in _subIndexList)
		{
			foreach (var result in subIndex.GetAllFileInfoGrouped())
			{
				yield return result;
			}
		}
	}

	public IEnumerable<DirInfo> GetAllDirInfo()
	{
		if (DirInfo is not null) yield return DirInfo;

		foreach (var subIndex in _subIndexList)
		{
			foreach (var dirInfo in subIndex.GetAllDirInfo())
			{
				yield return dirInfo;
			}
		}
	}

	public IEnumerable<(string, FileInfo)> GetAllFileInfo()
	{
		foreach (var fileInfo in _fileInfoList)
		{
			yield return (GetFileFullName(fileInfo), fileInfo);
		}

		foreach (var subIndex in _subIndexList)
		{
			foreach (var fileInfo in subIndex.GetAllFileInfo())
			{
				yield return fileInfo;
			}
		}
	}

	public string GetFileFullName(FileInfo fileInfo)
	{
		if (DirInfo is null)
			return fileInfo.Name;
		else
			return Path.Combine(DirInfo.FullName, fileInfo.Name);
	}
}

internal class IndexConversionProvider : IConversionProvider
{
	public Type Type => typeof(Index);
	private static readonly IConversionProvider[] _providers = [new IndexConversionProvider()];

	public object ConvertFromLiteral(string literal)
	{
		var dic = BearML.Deserialize<Dictionary<string, string?>>(literal, _providers);

		return new Index(
			BearML.Deserialize<DirInfo>(dic["DirInfo"]),
			BearML.Deserialize<Index[]>(dic["SubIndexArr"]),
			BearML.Deserialize<FileInfo[]>(dic["FileInfoArr"]));
	}

	public string ConvertToLiteral(object source)
	{
		var src = (Index)source;
		var dic = new Dictionary<string, object?>
		{
			{ "DirInfo", src.DirInfo },
			{ "SubIndexArr" , src.SubIndexArr },
			{ "FileInfoArr", src.FileInfoArr },
		};

		return BearML.Serialize(dic, _providers);
	}
}