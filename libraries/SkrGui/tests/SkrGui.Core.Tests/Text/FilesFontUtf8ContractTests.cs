namespace SkrGui.Tests;

internal static class FilesFontUtf8ContractTests
{
    [GuiTest("audit/files-font/catalog-family-matching-preserves-source-bytes")]
    private static void CatalogFamilyMatchingPreservesSourceBytes()
    {
        var face=new FilesFontProviderFace();byte[] bytes={0x61,0,0xff};
        FilesFontProviderParseHelper.AddUniqueFamily(face.Families,new Utf8StringView(bytes));
        Check.That(face.HasFamily(new Utf8StringView(new byte[]{0x61,0,0xff})));
        Check.False(face.HasFamily(new Utf8StringView(new byte[]{0x61,0,0xfe})));
        bytes[0]=0x62;Check.That(face.HasFamily(new Utf8StringView(new byte[]{0x61,0,0xff})));
        FilesFontProviderParseHelper.AddUniqueFamily(face.Families,new Utf8StringView(new byte[]{0x61,0,0xff}));Check.Equal(1,face.Families.Count);
        FilesFontProviderParseHelper.AddUniqueFamily(face.Families,new Utf8StringView(new byte[]{0x61,0,0xfe}));Check.Equal(2,face.Families.Count);
    }
}
