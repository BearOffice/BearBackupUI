using BearBackup.BasicData;
using BearMarkupLanguage;
using BearMarkupLanguage.Conversion;

namespace BearBackup;

internal static class Writer
{
	private static readonly object _locker = new object();
	private static readonly IConversionProvider[] _providers = new IConversionProvider[] {
		new ConversionProvider(typeof(DateTime),
			literal => DateTime.FromBinary(long.Parse(literal)),
			obj => ((DateTime)obj).ToBinary().ToString())
	};

	internal static Index? ReadIndexFile(string path, bool threadSafe = true)
	{
		BearML ml;
		if (threadSafe)
		{
			lock (_locker)
			{
				ml = new BearML(path, providers: _providers);
			}
		}
		else
		{
			ml = new BearML(path, providers: _providers);
		}

		if (ml.GetAllKeys().Length == 0) return null;

		return new Index(null, ml.GetValue<Index[]>("SubIndexArr"), ml.GetValue<FileInfo[]>("FileInfoArr"));
	}

	internal static void WriteIndex(string path, Index? index)
	{
		lock (_locker)
		{
			var ml = new BearML(path, overwrites: true, providers: _providers)
			{
				DelayedSave = true
			};

			if (index is not null)
			{
				ml.AddKeyValue("SubIndexArr", index.SubIndexArr);
				ml.AddKeyValue("FileInfoArr", index.FileInfoArr);
			}

			ml.Save();
		}
	}

	internal static RecordInfo[]? ReadRecordFile(string path)
	{
		BearML ml;
		lock (_locker)
		{
			ml = new BearML(path, providers: _providers);
		}
		var keys = ml.GetAllKeys();
		if (keys.Length == 0) return null;

		var records = new List<RecordInfo>();
		foreach (var key in keys)
		{
			var dic = ml.GetValue<Dictionary<string, string?>>(key);
			records.Add(new RecordInfo(
				key,
				BearML.Deserialize<DateTime>(dic["Created"], providers: _providers),
				dic["Comment"]));
		}

		return [.. records];
	}

	internal static void WriteRecordInfo(string path, RecordInfo? recordInfo)
	{
		if (recordInfo is null)
			WriteRecordInfo(path, Array.Empty<RecordInfo>());
		else
			WriteRecordInfo(path, new[] { recordInfo });
	}

	internal static void WriteRecordInfo(string path, RecordInfo[] recordInfoArr)
	{
		lock (_locker)
		{
			var ml = new BearML(path, overwrites: true, providers: _providers)
			{
				DelayedSave = true
			};

			foreach (var record in recordInfoArr)
			{
				var dic = new Dictionary<string, object?>
				{
					{ "Created", record.Created },
					{ "Comment", record.Comment }
				};

				ml.AddKeyValue(record.Name, dic);
			}

			ml.Save();
		}
	}

	internal static Ignore? ReadIgnoreFile(string path)
	{
		BearML ml;
		lock (_locker)
		{
			ml = new BearML(path);
		}
		if (ml.GetAllKeys().Length == 0) return null;
		return new Ignore(ml.GetValue<string[]>(Environment.IgnoredDirs), ml.GetValue<string[]>(Environment.IgnoredFiles));
	}

	internal static void WriteIgnore(string path, Ignore? ignore)
	{
		lock (_locker)
		{
			var ml = new BearML(path, overwrites: true)
			{
				DelayedSave = true
			};

			if (ignore is not null)
			{
				ml.AddKeyValue(Environment.IgnoredDirs, ignore.IgnoredDirs);
				ml.AddKeyValue(Environment.IgnoredFiles, ignore.IgnoredFiles);
			}

			ml.Save();
		}
	}
}
