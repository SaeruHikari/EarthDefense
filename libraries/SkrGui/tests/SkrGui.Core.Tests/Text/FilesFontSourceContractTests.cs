namespace SkrGui.Tests;

// Additional C++ -> C# boundary checks; not counted as original source cases.
internal static class FilesFontSourceContractTests
{
    [GuiTest("audit/files-font/failed-scalar-reads-preserve-output")]
    private static void FailedScalarReadsPreserveOutput()
    {
        byte[] data=[0x12,0x34,0x56,0x78];ushort u16=0xabba;uint u32=0xabba1234;
        Check.False(FilesFontProviderParseHelper.ReadU16(data,3,ref u16));Check.Equal((ushort)0xabba,u16);
        Check.False(FilesFontProviderParseHelper.ReadU16(data,ulong.MaxValue,ref u16));Check.Equal((ushort)0xabba,u16);
        Check.False(FilesFontProviderParseHelper.ReadU32(data,1,ref u32));Check.Equal(0xabba1234u,u32);
        Check.That(FilesFontProviderParseHelper.ReadU16(data,1,ref u16));Check.Equal((ushort)0x3456,u16);
        Check.That(FilesFontProviderParseHelper.ReadU32(data,0,ref u32));Check.Equal(0x12345678u,u32);
    }

    [GuiTest("audit/files-font/failed-table-lookup-preserves-output")]
    private static void FailedTableLookupPreservesOutput()
    {
        var sentinel=new FilesFontProviderTable(123,456);var table=sentinel;
        uint tag=FilesFontProviderParseHelper.MakeTag('t','e','s','t');
        Check.False(FilesFontProviderParseHelper.FindTable([],0,tag,ref table));Check.Equal(sentinel,table);
        byte[] directory=new byte[32];directory[5]=1; // One complete directory entry.
        Check.False(FilesFontProviderParseHelper.FindTable(directory,0,tag,ref table));Check.Equal(sentinel,table);
        directory[12]=(byte)'t';directory[13]=(byte)'e';directory[14]=(byte)'s';directory[15]=(byte)'t';
        directory[23]=31;directory[27]=2; // Requested payload outside source.
        Check.False(FilesFontProviderParseHelper.FindTable(directory,0,tag,ref table));Check.Equal(sentinel,table);
        directory[27]=1;
        Check.That(FilesFontProviderParseHelper.FindTable(directory,0,tag,ref table));Check.Equal(new FilesFontProviderTable(31,1),table);
    }

    [GuiTest("audit/files-font/name-and-face-early-failure-preserves-output")]
    private static void NameAndFaceEarlyFailurePreservesOutput()
    {
        Utf8StringView value="sentinel";
        Check.False(FilesFontProviderParseHelper.DecodeNameRecord([],0,0,0,ref value));Check.Equal((Utf8StringView)"sentinel",value);
        byte[] record=new byte[12];record[1]=2;
        Check.False(FilesFontProviderParseHelper.DecodeNameRecord(record,0,0,0,ref value));Check.Equal((Utf8StringView)"sentinel",value);
        record[1]=3;record[9]=1;
        Check.False(FilesFontProviderParseHelper.DecodeNameRecord(record,0,0,0,ref value));Check.Equal((Utf8StringView)"",value);
        FilesFontProviderFace face=new(){Weight=EFontWeight.Bold};var before=face;
        Check.False(FilesFontProviderParseHelper.ParseFace("bad",[],0,0,ref face));Check.Same(before,face);Check.Equal(EFontWeight.Bold,face.Weight);
    }

    [GuiTest("audit/files-font/catalog-is-a-borrowed-readonly-view")]
    private static void CatalogIsBorrowedReadOnlyView()
    {
        string path=Path.Combine(Path.GetTempPath(),"skrgui-font-catalog-contract-"+Guid.NewGuid()+".ttf");
        File.WriteAllBytes(path,TestFontAssets.Get("Latin"));
        try
        {
            var provider=new FilesFontProvider();var catalog=provider.FontFaces();Check.Equal(0,catalog.Count);
            Check.That(provider.AddFontFile(path));Check.Equal(1,catalog.Count);
            var borrowed=catalog[0];Check.That(borrowed.IsValid());var families=borrowed.Families;
            Check.False(catalog is System.Collections.IList);Check.False(families is IList<Utf8StringView>);
            Check.False(typeof(ReadOnlyFilesFontProviderFace).GetProperties().Any(p=>p.SetMethod!=null));
            var copy=borrowed.Copy();copy.Families.Clear();copy.Weight=EFontWeight.Bold;
            var source=borrowed.Source;source.SourceKey="changed";
            Check.That(families.Count>0);Check.Equal((Utf8StringView)path,borrowed.Source.SourceKey);
            Check.That(provider.QueryFace(families[0],borrowed.Weight,borrowed.Style,borrowed.Stretch,out var found));Check.That(found.IsValid());
            provider.ClearFontFiles();Check.Equal(0,catalog.Count);
        }
        finally {File.Delete(path);}
    }
}
