using BearMarkupLanguage;
using BearBackup.Comparers;
using BearBackup.BasicData;

namespace BearBackup;

public class BackupRepo
{
    public BackupRepo()
    {
        //var dir = new DirInfo("abc", FileAttributes.ReadOnly, DateTime.Now);
        //var s = Enumerable.Repeat(dir, 20).ToArray();
        //var a = BearML.Serialize(s);
        //Console.WriteLine(a);

        var index1 = IndexBuilder.Build("C:\\Users\\Bear\\Desktop\\Test\\TestA", out _); 
        Console.WriteLine(BearML.Serialize(index1));

        Console.WriteLine("---------------------");
        var s = index1.GetSubIndex("B\\New folder");

        Console.WriteLine(BearML.Serialize(s));
    }

    public void Test1()
    {
        var index1 = IndexBuilder.Build("C:\\Users\\Bear\\Desktop\\Test\\TestA", out _);
        var index2 = IndexBuilder.Build("C:\\Users\\Bear\\Desktop\\Test\\TestB", out _);

        (var l, var r) = IndexComparison.DiffFileInfo(index1, index2, new LooseFileComparer(), new GeneralDirComparer());
        Console.WriteLine(BearML.Serialize(l.Select(t => (t.Item1.DirInfo?.FullName, t.Item2)).ToArray()));
        Console.WriteLine("-----------------");
        Console.WriteLine(BearML.Serialize(r.Select(t => (t.Item1.DirInfo?.FullName, t.Item2)).ToArray()));
        Console.WriteLine("-----------------");
        Console.WriteLine("-----------------");
        (var dL, var dR) = IndexComparison.DiffDirInfo(index1, index2, new GeneralDirComparer());
        Console.WriteLine(BearML.Serialize(dL.Select(t => t.Item2).ToArray()));
        Console.WriteLine("-----------------");
        Console.WriteLine(BearML.Serialize(dR.Select(t => t.Item2).ToArray()));
    }

    public void Test2()
    {
        var rootIndex = IndexBuilder.Build("D:\\[Ebook]", out var es);
        //Console.WriteLine(BearML.Serialize(es));

        IndexBuilder.CalculateAllFilesHash("D:\\[Ebook]", rootIndex, out es);
        // Console.WriteLine(BearML.Serialize(rootIndex.GetAllFileInfo().ToArray()));
        // Console.WriteLine(BearML.Serialize(es));

        var provider = new BearMarkupLanguage.Conversion.ConversionProvider(typeof(BasicData.FileInfo), literal =>
        {
            var dic = BearML.Deserialize<Dictionary<string, string?>>(literal);

            return new BasicData.FileInfo(
                dic["Name"] ?? throw new Exception("Null exception."),
                BearML.Deserialize<FileAttributes>(dic["Attributes"]),
                BearML.Deserialize<DateTime>(dic["Created"]),
                BearML.Deserialize<DateTime>(dic["Modified"]),
                BearML.Deserialize<long>(dic["Size"]),
                dic["SHA1"]);

        }, obj => BearML.Serialize(obj));

        var ser = BearML.Serialize(rootIndex.GetAllFileInfo().ToArray(), new[] { provider });

        // Console.WriteLine(ser);
        var x = BearML.Deserialize<BasicData.FileInfo[]>(ser, new[] { provider });
        Console.WriteLine();
        Console.WriteLine(BearML.Serialize(x));
    }
}
