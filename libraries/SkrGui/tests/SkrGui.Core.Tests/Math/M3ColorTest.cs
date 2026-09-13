using SkrGui;
using static SkrGui.Tests.OriginalMathHelpers;
namespace SkrGui.Tests;
internal static class M3ColorTest
{
private readonly record struct PaletteCase(M3TonalPalette Palette,double Hue,double Chroma,uint Key,uint Tone3725);
private readonly record struct BoundaryCase(bool TertiaryAxis,uint Fixed,uint Below,uint Above,double BelowHue,double AboveHue,uint BelowKey,uint AboveKey,uint BelowError,uint AboveError);
private static double LinearizeSrgb8(byte encoded){
    double normalized = (double)(encoded) / 255.0;
    return normalized <= 0.040449936 ?
        normalized / 12.92 :
        Math.Pow((normalized + 0.055) / 1.055, 2.4);
}
private static HCTColor HctFromSrgb8(SRGBColor color){
    return HCTColor.FromLinear(
        LinearizeSrgb8(color.Red8()),
        LinearizeSrgb8(color.Green8()),
        LinearizeSrgb8(color.Blue8())
    );
}
private static SRGBColor[] RoleColors(M3ColorScheme scheme){
    return new SRGBColor[]{
        scheme.Background,
        scheme.OnBackground,
        scheme.Surface,
        scheme.SurfaceDim,
        scheme.SurfaceBright,
        scheme.SurfaceContainerLowest,
        scheme.SurfaceContainerLow,
        scheme.SurfaceContainer,
        scheme.SurfaceContainerHigh,
        scheme.SurfaceContainerHighest,
        scheme.OnSurface,
        scheme.SurfaceVariant,
        scheme.OnSurfaceVariant,
        scheme.Outline,
        scheme.OutlineVariant,
        scheme.InverseSurface,
        scheme.InverseOnSurface,
        scheme.Shadow,
        scheme.Scrim,
        scheme.SurfaceTint,
        scheme.Primary,
        scheme.PrimaryDim,
        scheme.OnPrimary,
        scheme.PrimaryContainer,
        scheme.OnPrimaryContainer,
        scheme.InversePrimary,
        scheme.PrimaryFixed,
        scheme.PrimaryFixedDim,
        scheme.OnPrimaryFixed,
        scheme.OnPrimaryFixedVariant,
        scheme.Secondary,
        scheme.SecondaryDim,
        scheme.OnSecondary,
        scheme.SecondaryContainer,
        scheme.OnSecondaryContainer,
        scheme.SecondaryFixed,
        scheme.SecondaryFixedDim,
        scheme.OnSecondaryFixed,
        scheme.OnSecondaryFixedVariant,
        scheme.Tertiary,
        scheme.TertiaryDim,
        scheme.OnTertiary,
        scheme.TertiaryContainer,
        scheme.OnTertiaryContainer,
        scheme.TertiaryFixed,
        scheme.TertiaryFixedDim,
        scheme.OnTertiaryFixed,
        scheme.OnTertiaryFixedVariant,
        scheme.Error,
        scheme.ErrorDim,
        scheme.OnError,
        scheme.ErrorContainer,
        scheme.OnErrorContainer,
    };
}
private static void CheckRoles(
    M3ColorScheme scheme,
    uint[] expected
){
    var actual = RoleColors(scheme);
    for (int index = 0; index < actual.Length; ++index)
    {
        EqualNumeric(actual[index].ToArgb32(), expected[index]);
    }
}
private static byte HexDigit(char value){
    return (byte)(
        value >= '0' && value <= '9' ? value - '0' : value - 'A' + 10
    );
}
private static void CheckSrgb8(SRGBColor color){
    EqualNumeric(color.A, 1.0f);
    EqualNumeric(color.R, (float)(color.Red8()) / 255.0f);
    EqualNumeric(color.G, (float)(color.Green8()) / 255.0f);
    EqualNumeric(color.B, (float)(color.Blue8()) / 255.0f);
}
private static void CheckEncodedRoles(M3ColorScheme scheme, string expected){
    var actual = RoleColors(scheme);
    for (int index = 0; index < actual.Length; ++index)
    {
        int offset = index * 6;
        uint packed = 0xff000000u |
            (uint)(HexDigit(expected[offset])) << 20 |
            (uint)(HexDigit(expected[offset + 1])) << 16 |
            (uint)(HexDigit(expected[offset + 2])) << 12 |
            (uint)(HexDigit(expected[offset + 3])) << 8 |
            (uint)(HexDigit(expected[offset + 4])) << 4 |
            (uint)(HexDigit(expected[offset + 5]));
        EqualNumeric(actual[index].ToArgb32(), packed);
        CheckSrgb8(actual[index]);
    }
}
private static void CheckSameRoles(M3ColorScheme lhs, M3ColorScheme rhs){
    var lhs_colors = RoleColors(lhs);
    var rhs_colors = RoleColors(rhs);
    for (int index = 0; index < lhs_colors.Length; ++index)
    {
        EqualNumeric(lhs_colors[index].ToArgb32(), rhs_colors[index].ToArgb32());
    }
}
[GuiTest("math/m3/m3_color_test.cpp::gui/math/m3/tonal-palette")]
public static void Case0(){

    HCTColor source = HCTColor.FromSRGB(SRGBColor.FromARGB32(0xff4285f4));
    M3TonalPalette from_source = M3TonalPalette.FromHCT(source);
    Check.That(Math.Abs(from_source.Hue() - source.Hue) <= 0.0002);
    Check.That(Math.Abs(from_source.Chroma() - source.Chroma) <= 0.0002);
    EqualNumeric(from_source.KeyColor().ToSrgb8().ToArgb32(), 0xff4285f4u);

    M3TonalPalette palette = M3TonalPalette.FromHueAndChroma(
        source.Hue,
        source.Chroma
    );
    EqualNumeric(palette.KeyColor().ToSrgb8().ToArgb32(), 0xff2b74e2u);
    EqualNumeric(palette.Srgb(0.0).ToArgb32(), 0xff000000u);
    EqualNumeric(palette.Srgb(37.25).ToArgb32(), 0xff0054b4u);
    EqualNumeric(palette.Srgb(100.0).ToArgb32(), 0xffffffffu);
    HCTColor actual_hct = palette.Hct(37.25);
    HCTColor expected_hct = HctFromSrgb8(palette.Srgb(37.25));
    EqualNumeric(actual_hct, expected_hct);

    M3TonalPalette yellow = M3TonalPalette.FromHueAndChroma(110.0, 80.0);
    EqualNumeric(yellow.Srgb(98.0).ToArgb32(), 0xfffffcc8u);
    EqualNumeric(yellow.Srgb(99.0).ToArgb32(), 0xfffffee4u);
    EqualNumeric(yellow.Srgb(100.0).ToArgb32(), 0xffffffffu);
    EqualNumeric(yellow.Hct(99.0), HctFromSrgb8(yellow.Srgb(99.0)));

}
[GuiTest("math/m3/m3_color_test.cpp::gui/math/m3/cmf-palette-parameters")]
public static void Case1(){

    M3ColorScheme scheme = M3ColorScheme.FromCMF(
        SRGBColor.FromARGB32(0xff4285f4u),
        SRGBColor.FromARGB32(0xffff6600u),
        false
    );
    HCTColor primary_source = scheme.PrimarySourceColor;
    HCTColor tertiary_source = scheme.TertiarySourceColor.Value;


    PaletteCase[] cases = new PaletteCase[]{
        new PaletteCase( scheme.PrimaryPalette, primary_source.Hue, primary_source.Chroma, 0xff2b74e2u, 0xff0054b4u ),
        new PaletteCase( scheme.SecondaryPalette, primary_source.Hue, primary_source.Chroma * 0.5, 0xff6277a3u, 0xff435782u ),
        new PaletteCase( scheme.TertiaryPalette, tertiary_source.Hue, tertiary_source.Chroma, 0xfffe6500u, 0xff983a00u ),
        new PaletteCase( scheme.NeutralPalette, primary_source.Hue, primary_source.Chroma * 0.2, 0xff727786u, 0xff525866u ),
        new PaletteCase( scheme.NeutralVariantPalette, primary_source.Hue, primary_source.Chroma * 0.2, 0xff727786u, 0xff525866u ),
        new PaletteCase( scheme.ErrorPalette, 24.0, primary_source.Chroma, 0xffca4d46u, 0xffa02f2au ),
    };
    foreach (PaletteCase palette_case in cases)
    {
        Check.That(Math.Abs(palette_case.Palette.Hue() - palette_case.Hue) <= 0.0002);
        Check.That(Math.Abs(palette_case.Palette.Chroma() - palette_case.Chroma) <= 0.0002);
        EqualNumeric(
            palette_case.Palette.KeyColor().ToSrgb8().ToArgb32(),
            palette_case.Key
        );
        EqualNumeric(palette_case.Palette.Srgb(37.25).ToArgb32(), palette_case.Tone3725);
    }

    EqualNumeric(scheme.PrimaryPalette.Srgb(-10.0).ToArgb32(), 0xff000000u);
    EqualNumeric(scheme.PrimaryPalette.Srgb(110.0).ToArgb32(), 0xffffffffu);

}
[GuiTest("math/m3/m3_color_test.cpp::gui/math/m3/cmf-dual-source-reference")]
public static void Case2(){

    M3ColorScheme scheme = M3ColorScheme.FromCMF(
        SRGBColor.FromARGB32(0xff4285f4),
        SRGBColor.FromARGB32(0xffff6600),
        false,
        0.0
    );
    uint[] kExpected = {
        0xfff9f9ffu,
        0xff27324bu,
        0xfff9f9ffu,
        0xffcedaf9u,
        0xfff9f9ffu,
        0xffffffffu,
        0xfff1f3ffu,
        0xffe9edffu,
        0xffe0e8ffu,
        0xffd8e2ffu,
        0xff27324bu,
        0xffd8e2ffu,
        0xff535f7au,
        0xff6f7b96u,
        0xffa6b2d0u,
        0xff020e24u,
        0xff989dacu,
        0xff000000u,
        0xff000000u,
        0xff005bc2u,
        0xff005bc2u,
        0xff005bc2u,
        0xfff9f8ffu,
        0xff5291ffu,
        0xff001232u,
        0xff4d8efeu,
        0xff5291ffu,
        0xff4084f2u,
        0xff000000u,
        0xff001d48u,
        0xff4a5f89u,
        0xff4a5f89u,
        0xfff9f8ffu,
        0xffb7ccfdu,
        0xff2e436cu,
        0xffb7ccfdu,
        0xffa9beeeu,
        0xff193057u,
        0xff384d76u,
        0xffa43f00u,
        0xffa43f00u,
        0xfffff7f5u,
        0xffff6600u,
        0xff300d00u,
        0xffff6600u,
        0xffea5d00u,
        0xff000000u,
        0xff411400u,
        0xffaa3630u,
        0xffaa3630u,
        0xfffff7f6u,
        0xfff36b61u,
        0xff380002u,
    };
    CheckRoles(scheme, kExpected);

    Check.That(scheme.TertiarySourceColor.HasValue);
    EqualNumeric(scheme.PrimarySourceColor.ToSrgb8().ToArgb32(), 0xff4285f4u);
    EqualNumeric(scheme.TertiarySourceColor.Value.ToSrgb8().ToArgb32(), 0xffff6600u);
    EqualNumeric(scheme.TertiaryPalette.KeyColor().ToSrgb8().ToArgb32(), 0xfffe6500u);

}
[GuiTest("math/m3/m3_color_test.cpp::gui/math/m3/cmf-2026-phone-role-golden")]
public static void Case3(){



    string[] kSingleLight = {
        "F9F9FF27324BF9F9FFCEDAF9F9F9FFFFFFFFF1F3FFE9EDFFE0E8FFD8E2FF27324BD8E2FF535F7A6F7B96A6B2D0020E24989DAC000000000000005BC2005BC2005BC2F9F8FF5291FF0012324D8EFE5291FF4084F2000000001D484A5F894A5F89F9F8FFB7CCFD2E436CB7CCFDA9BEEE193057384D76345EA6345EA6F9F8FF6C93DF0012326C93DF5F85D1000000001D48AA3630AA3630FFF7F6F36B61380002",
        "F9F9FF27324BF9F9FFCEDAF9F9F9FFFFFFFFF1F3FFE9EDFFE0E8FFD8E2FF27324BD8E2FF535F7A6F7B96A6B2D0020E24989DAC000000000000005BC2005BC2005BC2F9F8FF5291FF0012324D8EFE5291FF4084F2000000001D484A5F894A5F89F9F8FFB7CCFD2E436CB7CCFDA9BEEE193057384D76345EA6345EA6F9F8FF6C93DF0012326C93DF5F85D1000000001D48AA3630AA3630FFF7F6F36B61380002",
        "F9F9FF27324BF9F9FFCEDAF9F9F9FFFFFFFFF1F3FFE9EDFFE0E8FFD8E2FF27324BD8E2FF535F7A6F7B96A6B2D0020E24989DAC000000000000005BC2005BC2005BC2F9F8FF5291FF0012324D8EFE5291FF4084F2000000001D484A5F894A5F89F9F8FFB7CCFD2E436CB7CCFDA9BEEE193057384D76345EA6345EA6F9F8FF6C93DF0012326C93DF5F85D1000000001D48AA3630AA3630FFF7F6F36B61380002",
        "F9F9FF1F2B43F9F9FFCEDAF9F9F9FFFFFFFFF1F3FFE9EDFFE0E8FFD8E2FF1F2B43D8E2FF444F695F6B868490AD020E24ADB2C2000000000000004CA4004CA4004CA4DEE6FF689CFF00173C5A95FF689CFF4D8EFE0000000013343A4F793A4F79DEE6FF899ECC00173C899ECC7C91BE000000001334214D95214D95DEE6FF779DEA00173C779DEA6990DC000000001334942623942623FFDFDCFD73683C0002",
        "F9F9FF18243BF9F9FFCEDAF9F9F9FFFFFFFFF1F3FFE9EDFFE0E8FFD8E2FF18243BD8E2FF37435C535F7A6F7B96020E24C0C4D500000000000000408C00408C00408CCBDAFF2771DFFFFFFF669BFF2771DF0A64D2FFFFFFFFFFFF2D426B2D426BCBDAFF6074A0FFFFFF6074A0536893FFFFFFFFFFFF0C40880C4088CBDAFF4C73BDFFFFFF4C73BD3F67B0FFFFFFFFFFFF831918831918FFCEC9C74B43FFFFFF",
        "F9F9FF000000F9F9FFCEDAF9F9F9FFFFFFFFF1F3FFE9EDFFE0E8FFD8E2FF000000D8E2FF27324B444F6957637D020E24E6EBFC00000000000000306D00306D00306DCBDAFF005FCAFFFFFF8CB2FF005FCA0053B3FFFFFFFFFFFF1C325A1C325ACBDAFF4E638DFFFFFF4E638D425681FFFFFFFFFFFF00306D00306DCBDAFF3961AAFFFFFF3961AA2A559DFFFFFFFFFFFF6B04096B0409FFCEC9AF3A34FFFFFF",
        "F9F9FF000000F9F9FFCEDAF9F9F9FFFFFFFFF1F3FFE9EDFFE0E8FFD8E2FF000000D8E2FF18243B37435C46526C020E24FFFFFF000000000000002251002251002251CBDAFF004FAAFFFFFFAAC5FF004FAA004393FFFFFFFFFFFF0A234A0A234ACBDAFF3D527CFFFFFF3D527C31466FFFFFFFFFFFFF002251002251CBDAFF255098FFFFFF25509813448CFFFFFFFFFFFF510004510004FFCFCA982925FFFFFF",
    };
    string[] kSingleDark = {
        "090E19DCE5FF090E19090E19202C440000000B132210192B161F331A263DDCE5FF1A263D9FABC96A75913C4861F9F9FF505563000000000000689CFF689CFF689CFF001E4A5291FF001232005BC25291FF4084F2000000001D48899ECC899ECC051F461930579BB0E0B7CCFDA9BEEE193057384D76779DEA779DEA001E4A6C93DF0012326C93DF5F85D1000000001D48FD7368FD73684A00036B0409FF968C",
        "090E19DCE5FF090E19090E19202C440000000B132210192B161F331A263DDCE5FF1A263D9FABC96A75913C4861F9F9FF505563000000000000689CFF689CFF689CFF001E4A5291FF001232005BC25291FF4084F2000000001D48899ECC899ECC051F461930579BB0E0B7CCFDA9BEEE193057384D76779DEA779DEA001E4A6C93DF0012326C93DF5F85D1000000001D48FD7368FD73684A00036B0409FF968C",
        "090E19DCE5FF090E19090E19202C440000000B132210192B161F331A263DDCE5FF1A263D9FABC96A75913C4861F9F9FF5055630000000000006D9FFF6D9FFF6D9FFF00214F5291FF001232005BC25291FF4084F2000000001D48899ECC899ECC051F461930579BB0E0B7CCFDA9BEEE193057384D767AA0ED7AA0ED00214F6C93DF0012326C93DF5F85D1000000001D48FD7368FD73684A00036B0409FF968C",
        "090E19FFFFFF090E19090E19202C440000000B132210192B161F331A263DFFFFFF1A263DA6B2D07A85A256627DF9F9FF40455200000000000080AAFF80AAFF80AAFF002456689CFF00173C0056B8689CFF4D8EFE00000000133492A7D692A7D60822494D628DFFFFFFB7CCFDA9BEEE011C43273D6584ABF984ABF9002456779DEA00173C779DEA6990DC000000001334FF8378FF83784F0004AE3933FFFFFF",
        "090E19FFFFFF090E19090E19202C440000000B132210192B161F331A263DFFFFFF1A263DADB9D78893B06A7591F9F9FF33384500000000000097B8FF97B8FF97B8FF002A61689CFF000F2C0051AF689CFF4D8EFE000000000000A4B9E9A4B9E9142B536074A0FFFFFFB7CCFDA9BEEE00030E19305797B8FF97B8FF002A61779DEA000F2C779DEA6990DC000000000000FF9F96FF9F96600006C74B43FFFFFF",
        "090E19FFFFFF090E19090E19202C440000000B132210192B161F331A263DFFFFFF1A263DC5D0F09BA7C5838FABF9F9FF171C28000000000000BDD1FFBDD1FFBDD1FF002A61689CFF000000004291689CFF4D8EFE000000000000BDD1FFBDD1FF142B53899ECC000000B7CCFDA9BEEE000000011C43BDD1FFBDD1FF002A61779DEA000000779DEA6990DC000000000000FFC2BBFFC2BB600006FD7368000000",
        "090E19FFFFFF090E19090E19202C440000000B132210192B161F331A263DFFFFFF1A263DDCE5FFADB9D798A4C1F9F9FF000000000000000000DCE5FFDCE5FFDCE5FF002A6173A3FF00000000367873A3FF73A3FF000000000000DCE5FFDCE5FF142B538FA4D2000000B7CCFDA9BEEE00000000030EDCE5FFDCE5FF002A617DA3F00000007DA3F07DA3F0000000000000FFDEDAFFDEDA600006FF7C71000000",
    };
    string[] kDualLight = {
        "F9F9FF27324BF9F9FFCEDAF9F9F9FFFFFFFFF1F3FFE9EDFFE0E8FFD8E2FF27324BD8E2FF535F7A6F7B96A6B2D0020E24989DAC000000000000005BC2005BC2005BC2F9F8FF5291FF0012324D8EFE5291FF4084F2000000001D484A5F894A5F89F9F8FFB7CCFD2E436CB7CCFDA9BEEE193057384D76A43F00A43F00FFF7F5FF6600300D00FF6600EA5D00000000411400AA3630AA3630FFF7F6F36B61380002",
        "F9F9FF27324BF9F9FFCEDAF9F9F9FFFFFFFFF1F3FFE9EDFFE0E8FFD8E2FF27324BD8E2FF535F7A6F7B96A6B2D0020E24989DAC000000000000005BC2005BC2005BC2F9F8FF5291FF0012324D8EFE5291FF4084F2000000001D484A5F894A5F89F9F8FFB7CCFD2E436CB7CCFDA9BEEE193057384D76A43F00A43F00FFF7F5FF6600300D00FF6600EA5D00000000411400AA3630AA3630FFF7F6F36B61380002",
        "F9F9FF27324BF9F9FFCEDAF9F9F9FFFFFFFFF1F3FFE9EDFFE0E8FFD8E2FF27324BD8E2FF535F7A6F7B96A6B2D0020E24989DAC000000000000005BC2005BC2005BC2F9F8FF5291FF0012324D8EFE5291FF4084F2000000001D484A5F894A5F89F9F8FFB7CCFD2E436CB7CCFDA9BEEE193057384D76A43F00A43F00FFF7F5FF6600300D00FF6600EA5D00000000411400AA3630AA3630FFF7F6F36B61380002",
        "F9F9FF1F2B43F9F9FFCEDAF9F9F9FFFFFFFFF1F3FFE9EDFFE0E8FFD8E2FF1F2B43D8E2FF444F695F6B868490AD020E24ADB2C2000000000000004CA4004CA4004CA4DEE6FF689CFF00173C5A95FF689CFF4D8EFE0000000013343A4F793A4F79DEE6FF899ECC00173C899ECC7C91BE0000000013348A34008A3400FFE0D4FF742D310D00FF742DF562000000002A0A00942623942623FFDFDCFD73683C0002",
        "F9F9FF18243BF9F9FFCEDAF9F9F9FFFFFFFFF1F3FFE9EDFFE0E8FFD8E2FF18243BD8E2FF37435C535F7A6F7B96020E24C0C4D500000000000000408C00408C00408CCBDAFF2771DFFFFFFF669BFF2771DF0A64D2FFFFFFFFFFFF2D426B2D426BCBDAFF6074A0FFFFFF6074A0536893FFFFFFFFFFFF762B00762B00FFCFBCC74E00FFFFFFC74E00B34500FFFFFFFFFFFF831918831918FFCEC9C74B43FFFFFF",
        "F9F9FF000000F9F9FFCEDAF9F9F9FFFFFFFFF1F3FFE9EDFFE0E8FFD8E2FF000000D8E2FF27324B444F6957637D020E24E6EBFC00000000000000306D00306D00306DCBDAFF005FCAFFFFFF8CB2FF005FCA0053B3FFFFFFFFFFFF1C325A1C325ACBDAFF4E638DFFFFFF4E638D425681FFFFFFFFFFFF5B1F005B1F00FFD0BCAA4200FFFFFFAA4200963900FFFFFFFFFFFF6B04096B0409FFCEC9AF3A34FFFFFF",
        "F9F9FF000000F9F9FFCEDAF9F9F9FFFFFFFFF1F3FFE9EDFFE0E8FFD8E2FF000000D8E2FF18243B37435C46526C020E24FFFFFF000000000000002251002251002251CBDAFF004FAAFFFFFFAAC5FF004FAA004393FFFFFFFFFFFF0A234A0A234ACBDAFF3D527CFFFFFF3D527C31466FFFFFFFFFFFFF431500431500FFD0BD8F3600FFFFFF8F36007C2E00FFFFFFFFFFFF510004510004FFCFCA982925FFFFFF",
    };
    string[] kDualDark = {
        "090E19DCE5FF090E19090E19202C440000000B132210192B161F331A263DDCE5FF1A263D9FABC96A75913C4861F9F9FF505563000000000000689CFF689CFF689CFF001E4A5291FF001232005BC25291FF4084F2000000001D48899ECC899ECC051F461930579BB0E0B7CCFDA9BEEE193057384D76FF742DFF742D3D1300FF6600300D00FF6600EA5D00000000411400FD7368FD73684A00036B0409FF968C",
        "090E19DCE5FF090E19090E19202C440000000B132210192B161F331A263DDCE5FF1A263D9FABC96A75913C4861F9F9FF505563000000000000689CFF689CFF689CFF001E4A5291FF001232005BC25291FF4084F2000000001D48899ECC899ECC051F461930579BB0E0B7CCFDA9BEEE193057384D76FF742DFF742D3D1300FF6600300D00FF6600EA5D00000000411400FD7368FD73684A00036B0409FF968C",
        "090E19DCE5FF090E19090E19202C440000000B132210192B161F331A263DDCE5FF1A263D9FABC96A75913C4861F9F9FF5055630000000000006D9FFF6D9FFF6D9FFF00214F5291FF001232005BC25291FF4084F2000000001D48899ECC899ECC051F461930579BB0E0B7CCFDA9BEEE193057384D76FF8042FF8042471700FF6600300D00FF6600EA5D00000000411400FD7368FD73684A00036B0409FF968C",
        "090E19FFFFFF090E19090E19202C440000000B132210192B161F331A263DFFFFFF1A263DA6B2D07A85A256627DF9F9FF40455200000000000080AAFF80AAFF80AAFF002456689CFF00173C0056B8689CFF4D8EFE00000000133492A7D692A7D60822494D628DFFFFFFB7CCFDA9BEEE011C43273D65FF8C56FF8C56471700FF742D310D00FF742DF562000000002A0A00FF8378FF83784F0004AE3933FFFFFF",
        "090E19FFFFFF090E19090E19202C440000000B132210192B161F331A263DFFFFFF1A263DADB9D78893B06A7591F9F9FF33384500000000000097B8FF97B8FF97B8FF002A61689CFF000F2C0051AF689CFF4D8EFE000000000000A4B9E9A4B9E9142B536074A0FFFFFFB7CCFDA9BEEE00030E193057FFA177FFA177511B00FF742D240800FF742DF56200000000000000FF9F96FF9F96600006C74B43FFFFFF",
        "090E19FFFFFF090E19090E19202C440000000B132210192B161F331A263DFFFFFF1A263DC5D0F09BA7C5838FABF9F9FF171C28000000000000BDD1FFBDD1FFBDD1FF002A61689CFF000000004291689CFF4D8EFE000000000000BDD1FFBDD1FF142B53899ECC000000B7CCFDA9BEEE000000011C43FFC3AAFFC3AA511B00FF742D000000FF742DF56200000000000000FFC2BBFFC2BB600006FD7368000000",
        "090E19FFFFFF090E19090E19202C440000000B132210192B161F331A263DFFFFFF1A263DDCE5FFADB9D798A4C1F9F9FF000000000000000000DCE5FFDCE5FFDCE5FF002A6173A3FF00000000367873A3FF73A3FF000000000000DCE5FFDCE5FF142B538FA4D2000000B7CCFDA9BEEE00000000030EFFDFD2FFDFD2501B00FF7F41000000FF7F41FF7F41000000000000FFDEDAFFDEDA600006FF7C71000000",
    };
    double[] kContrastLevels = { -1.0, -0.5, 0.0, 0.25, 0.5, 0.75, 1.0 };

    SRGBColor primary = SRGBColor.FromARGB32(0xff4285f4u);
    SRGBColor tertiary = SRGBColor.FromARGB32(0xffff6600u);
    for (int index = 0; index < kContrastLevels.Length; ++index)
    {
        CheckEncodedRoles(
            M3ColorScheme.FromCMF(primary, false, kContrastLevels[index]),
            kSingleLight[index]
        );
        CheckEncodedRoles(
            M3ColorScheme.FromCMF(primary, true, kContrastLevels[index]),
            kSingleDark[index]
        );
        CheckEncodedRoles(
            M3ColorScheme.FromCMF(primary, tertiary, false, kContrastLevels[index]),
            kDualLight[index]
        );
        CheckEncodedRoles(
            M3ColorScheme.FromCMF(primary, tertiary, true, kContrastLevels[index]),
            kDualDark[index]
        );
    }

}
[GuiTest("math/m3/m3_color_test.cpp::gui/math/m3/cmf-canonicalization-aliases-and-grid")]
public static void Case4(){

    SRGBColor source = SRGBColor.FromARGB32(0xff4285f4u);
    HCTColor actual_hct = HCTColor.FromSRGB(source);
    HCTColor same_rgb_request = new HCTColor(
        actual_hct.Hue + 0.0001,
        actual_hct.Chroma,
        actual_hct.Tone
    );

    M3ColorScheme single = M3ColorScheme.FromCMF(source, false);
    M3ColorScheme hct_single = M3ColorScheme.FromCMF(actual_hct, false);
    M3ColorScheme same_rgb = M3ColorScheme.FromCMF(actual_hct, same_rgb_request, false);
    CheckSameRoles(single, hct_single);
    CheckSameRoles(single, same_rgb);
    Check.False(single.TertiarySourceColor.HasValue);
    Check.That(same_rgb.TertiarySourceColor.HasValue);
    Check.That(Math.Abs(same_rgb.TertiaryPalette.Chroma() - same_rgb.PrimarySourceColor.Chroma * 0.75) <= 0.0002);
    EqualNumeric(same_rgb.TertiaryPalette.KeyColor().ToSrgb8().ToArgb32(), 0xff4f76c0u);

    SRGBColor transparent = new SRGBColor(source.R, source.G, source.B, 0.1f);
    M3ColorScheme alpha_ignored = M3ColorScheme.FromCMF(transparent, false);
    CheckSameRoles(single, alpha_ignored);
    EqualNumeric(alpha_ignored.PrimarySourceColor.ToSrgb8().ToArgb32(), 0xff4285f4u);

    SRGBColor non_finite_alpha = new SRGBColor(
        source.R,
        source.G,
        source.B,
        float.NaN
    );
    CheckSameRoles(single, M3ColorScheme.FromCMF(non_finite_alpha, false));

    SRGBColor non_grid_source = new SRGBColor(66.25f / 255.0f, 132.75f / 255.0f, 244.2f / 255.0f, 0.3f);
    EqualNumeric(non_grid_source.ToArgb32(), 0x4d4285f4u);
    CheckSameRoles(single, M3ColorScheme.FromCMF(non_grid_source, false));

    SRGBColor tertiary = SRGBColor.FromARGB32(0xffff6600u);
    M3ColorScheme dual = M3ColorScheme.FromCMF(source, tertiary, false);
    SRGBColor transparent_tertiary = new SRGBColor(tertiary.R, tertiary.G, tertiary.B, 0.2f);
    CheckSameRoles(dual, M3ColorScheme.FromCMF(source, transparent_tertiary, false));

    Check.That(single.Background == single.Surface);
    Check.That(single.OnBackground == single.OnSurface);
    Check.That(single.SurfaceVariant == single.SurfaceContainerHighest);
    Check.That(single.SurfaceTint == single.Primary);
    Check.That(single.PrimaryDim == single.Primary);
    Check.That(single.SecondaryDim == single.Secondary);
    Check.That(single.TertiaryDim == single.Tertiary);
    Check.That(single.ErrorDim == single.Error);

    foreach (SRGBColor color in RoleColors(single))
    {
        EqualNumeric(color.Alpha8(), 255);
        Check.That(Math.Abs(color.R * 255.0f - HctMath.Round(color.R * 255.0f)) <= 0.0001f);
        Check.That(Math.Abs(color.G * 255.0f - HctMath.Round(color.G * 255.0f)) <= 0.0001f);
        Check.That(Math.Abs(color.B * 255.0f - HctMath.Round(color.B * 255.0f)) <= 0.0001f);
    }

}
[GuiTest("math/m3/m3_color_test.cpp::gui/math/m3/cmf-thresholds-and-contrast-saturation")]
public static void Case5(){

    M3ColorScheme low_chroma = M3ColorScheme.FromCMF(
        SRGBColor.FromARGB32(0xff777777u),
        false
    );
    LessEqual(low_chroma.PrimarySourceColor.Chroma, 12.0);
    EqualNumeric(low_chroma.ErrorPalette.Hue(), 24.0);
    EqualNumeric(low_chroma.ErrorPalette.Chroma(), 50.0);
    EqualNumeric(low_chroma.ErrorPalette.KeyColor().ToSrgb8().ToArgb32(), 0xffbe5850u);
    EqualNumeric(low_chroma.Error.ToArgb32(), 0xff9f413au);
    EqualNumeric(low_chroma.OnError.ToArgb32(), 0xfffff7f6u);
    EqualNumeric(low_chroma.ErrorContainer.ToArgb32(), 0xfffb877du);
    EqualNumeric(low_chroma.OnErrorContainer.ToArgb32(), 0xff570b0bu);
    EqualNumeric(low_chroma.Primary.ToArgb32(), 0xff5e5e5eu);
    EqualNumeric(low_chroma.PrimaryContainer.ToArgb32(), 0xffe3e2e2u);
    EqualNumeric(low_chroma.PrimaryFixedDim.ToArgb32(), 0xffd5d4d4u);
    M3ColorScheme low_chroma_dark = M3ColorScheme.FromCMF(
        SRGBColor.FromARGB32(0xff777777u),
        true
    );
    EqualNumeric(low_chroma_dark.Primary.ToArgb32(), 0xffc7c6c6u);
    EqualNumeric(low_chroma_dark.PrimaryContainer.ToArgb32(), 0xff747474u);

    M3ColorScheme below = M3ColorScheme.FromCMF(
        SRGBColor.FromARGB32(0xff5788b3u),
        false
    );
    M3ColorScheme above = M3ColorScheme.FromCMF(
        SRGBColor.FromARGB32(0xff5788b4u),
        false
    );
    M3ColorScheme second_above = M3ColorScheme.FromCMF(
        SRGBColor.FromARGB32(0xffb2764bu),
        false
    );
    Less(below.PrimarySourceColor.Tone, 55.0);
    Greater(above.PrimarySourceColor.Tone, 55.0);
    Greater(second_above.PrimarySourceColor.Tone, 55.0);
    EqualNumeric(below.PrimaryFixed.ToArgb32(), 0xff4779a3u);
    EqualNumeric(below.OnPrimaryFixed.ToArgb32(), 0xffffffffu);
    EqualNumeric(above.PrimaryFixed.ToArgb32(), 0xff6798c5u);
    EqualNumeric(above.OnPrimaryFixed.ToArgb32(), 0xff000000u);
    EqualNumeric(second_above.PrimaryFixed.ToArgb32(), 0xffc48559u);

    M3ColorScheme byte_exact = M3ColorScheme.FromCMF(
        SRGBColor.FromARGB32(0xfffeb314u),
        false
    );
    Check.That(Math.Abs(byte_exact.PrimarySourceColor.Hue - 78.53991642255667) <= 1.0e-9);
    Check.That(Math.Abs(byte_exact.PrimarySourceColor.Chroma - 58.41229976950718) <= 1.0e-9);
    Check.That(Math.Abs(byte_exact.PrimarySourceColor.Tone - 78.08796996651718) <= 1.0e-9);
    EqualNumeric(byte_exact.PrimaryPalette.KeyColor().ToSrgb8().ToArgb32(), 0xfff4ab00u);
    EqualNumeric(byte_exact.InversePrimary.ToArgb32(), 0xfffeb314u);

    SRGBColor source = SRGBColor.FromARGB32(0xff4285f4u);
    M3ColorScheme minimum = M3ColorScheme.FromCMF(source, false, -1.0);
    M3ColorScheme below_minimum = M3ColorScheme.FromCMF(source, false, -10.0);
    M3ColorScheme maximum = M3ColorScheme.FromCMF(source, false, 1.0);
    M3ColorScheme above_maximum = M3ColorScheme.FromCMF(source, false, 10.0);
    CheckSameRoles(minimum, below_minimum);
    CheckSameRoles(maximum, above_maximum);
    EqualNumeric(below_minimum.ContrastLevel, -1.0);
    EqualNumeric(above_maximum.ContrastLevel, 1.0);

}
[GuiTest("math/m3/m3_color_test.cpp::gui/math/m3/default-scheme-is-opaque-black")]
public static void Case6(){

    M3ColorScheme scheme = new();
    EqualNumeric(scheme.PrimarySourceColor, new HCTColor());
    Check.False(scheme.TertiarySourceColor.HasValue);
    Check.False(scheme.IsDark);
    EqualNumeric(scheme.ContrastLevel, 0.0);
    foreach (SRGBColor color in RoleColors(scheme))
    {
        EqualNumeric(color.ToArgb32(), 0xff000000u);
    }

}
[GuiTest("math/m3/m3_color_test.cpp::gui/math/m3/cmf-error-hue-boundaries")]
public static void Case7(){


    BoundaryCase[] kCases = new BoundaryCase[]{
        new BoundaryCase( false, 0xffda3c30u, 0xffda3566u, 0xffda3565u, 16, 20, 0xffdb384eu, 0xffdb3943u, 0xffb91c39u, 0xffb91e2eu ),
        new BoundaryCase( false, 0xffd93f23u, 0xffdb374fu, 0xffdb374eu, 20, 24, 0xffdb3943u, 0xffdb3b37u, 0xffb91e2eu, 0xffb82122u ),
        new BoundaryCase( false, 0xffda3c30u, 0xffdb3943u, 0xffdb3942u, 32, 16, 0xffd9401bu, 0xffdb374eu, 0xffb62700u, 0xffb91b39u ),
        new BoundaryCase( false, 0xffdb3a3du, 0xffda3d2au, 0xffda3e2au, 32, 16, 0xffd9401au, 0xffdb384eu, 0xffb62700u, 0xffb91c39u ),
        new BoundaryCase( false, 0xffd93f23u, 0xffd9401bu, 0xffd8401bu, 20, 24, 0xffdb3943u, 0xffda3c37u, 0xffb91e2eu, 0xffb82222u ),
        new BoundaryCase( false, 0xffda3c30u, 0xffcf4b00u, 0xffcf4c00u, 16, 20, 0xffd54152u, 0xffd44248u, 0xffb3273cu, 0xffb32a33u ),
        new BoundaryCase( false, 0xffda3c30u, 0xff008a3fu, 0xff008a40u, 20, 16, 0xffc75051u, 0xffc6505au, 0xffa7393bu, 0xffa73843u ),
        new BoundaryCase( false, 0xffdb3943u, 0xff266cffu, 0xff276cffu, 24, 32, 0xffd5433cu, 0xffd34625u, 0xffb32a28u, 0xffb12f0eu ),
        new BoundaryCase( true, 0xff8851ffu, 0xffdb365au, 0xffdb3659u, 16, 32, 0xffd93a4fu, 0xffd7421eu, 0xffb81f3au, 0xffb52a04u ),
        new BoundaryCase( true, 0xffd93f23u, 0xffdb3943u, 0xffdb3942u, 24, 16, 0xffda3c37u, 0xffdb384eu, 0xffb82122u, 0xffb91c39u ),
        new BoundaryCase( true, 0xffd93470u, 0xffdb3b37u, 0xffdb3b36u, 28, 16, 0xffda3e2au, 0xffdb384eu, 0xffb72414u, 0xffb91c39u ),
        new BoundaryCase( true, 0xffdb3849u, 0xffda3d2au, 0xffda3e2au, 32, 24, 0xffd9401bu, 0xffdb3b37u, 0xffb62700u, 0xffb82122u ),
        new BoundaryCase( true, 0xffd93470u, 0xffd9401bu, 0xffd8401bu, 16, 20, 0xffdb384eu, 0xffdb3943u, 0xffb91c39u, 0xffb91e2eu ),
        new BoundaryCase( true, 0xff897700u, 0xffd74305u, 0xffd74304u, 20, 32, 0xffbe5757u, 0xffbd5941u, 0xff9f4041u, 0xff9e422cu ),
        new BoundaryCase( true, 0xffdb365au, 0xffdb3b37u, 0xffdb3b36u, 32, 20, 0xffd9401bu, 0xffdb3943u, 0xffb62700u, 0xffb91e2eu ),
        new BoundaryCase( true, 0xffdb365au, 0xffd9401bu, 0xffd8401bu, 20, 24, 0xffdb3943u, 0xffdb3b37u, 0xffb91e2eu, 0xffb82122u ),
        new BoundaryCase( true, 0xffdb3849u, 0xffd9401bu, 0xffd8401bu, 24, 28, 0xffdb3b37u, 0xffda3d2au, 0xffb82122u, 0xffb72414u ),
        new BoundaryCase( true, 0xffdb3a3du, 0xffdb3b37u, 0xffdb3b36u, 32, 16, 0xffd9401bu, 0xffdb374eu, 0xffb62700u, 0xffb91b39u ),
        new BoundaryCase( true, 0xffd93f23u, 0xffda3d2au, 0xffda3e2au, 16, 20, 0xffdb384eu, 0xffdb3a43u, 0xffb91c39u, 0xffb91f2eu ),
        new BoundaryCase( true, 0xffcf4b00u, 0xffdb3943u, 0xffdb3942u, 24, 16, 0xffd4433du, 0xffd54152u, 0xffb32b28u, 0xffb3273cu ),
        new BoundaryCase( true, 0xffcf4b00u, 0xffda3d2au, 0xffda3e2au, 16, 24, 0xffd54152u, 0xffd4433du, 0xffb3273cu, 0xffb32b28u ),
        new BoundaryCase( true, 0xff897700u, 0xffdb3b37u, 0xffdb3b36u, 32, 20, 0xffbd5941u, 0xffbe5757u, 0xff9e422cu, 0xff9f4041u ),
        new BoundaryCase( true, 0xff00a0a0u, 0xffdb3943u, 0xffdb3942u, 24, 16, 0xffbe5850u, 0xffbd575eu, 0xff9f413au, 0xff9f3f47u ),
        new BoundaryCase( true, 0xff00a0a0u, 0xffda3d2au, 0xffda3e2au, 16, 24, 0xffbd575eu, 0xffbe5850u, 0xff9f3f47u, 0xff9f413au ),
        new BoundaryCase( true, 0xff8851ffu, 0xffda3d2au, 0xffda3e2au, 32, 16, 0xffd7421eu, 0xffd93a4fu, 0xffb52a04u, 0xffb81f3au ),
    };

    foreach (BoundaryCase boundary in kCases)
    {
        SRGBColor @fixed = SRGBColor.FromARGB32(boundary.Fixed);
        SRGBColor below = SRGBColor.FromARGB32(boundary.Below);
        SRGBColor above = SRGBColor.FromARGB32(boundary.Above);
        M3ColorScheme below_scheme = boundary.TertiaryAxis ?
            M3ColorScheme.FromCMF(@fixed, below, false) :
            M3ColorScheme.FromCMF(below, @fixed, false);
        M3ColorScheme above_scheme = boundary.TertiaryAxis ?
            M3ColorScheme.FromCMF(@fixed, above, false) :
            M3ColorScheme.FromCMF(above, @fixed, false);

        EqualNumeric(below_scheme.ErrorPalette.Hue(), boundary.BelowHue);
        EqualNumeric(above_scheme.ErrorPalette.Hue(), boundary.AboveHue);
        EqualNumeric(
            below_scheme.ErrorPalette.KeyColor().ToSrgb8().ToArgb32(),
            boundary.BelowKey
        );
        EqualNumeric(
            above_scheme.ErrorPalette.KeyColor().ToSrgb8().ToArgb32(),
            boundary.AboveKey
        );
        EqualNumeric(below_scheme.Error.ToArgb32(), boundary.BelowError);
        EqualNumeric(above_scheme.Error.ToArgb32(), boundary.AboveError);
    }

}
[GuiTest("math/m3/m3_color_test.cpp::gui/math/m3/invalid-input-defaults")]
public static void Case8(){

    double infinity = double.PositiveInfinity;
    float float_nan = float.NaN;
    HCTColor invalid_hct_source = new();
    invalid_hct_source.Hue = infinity;
    M3ColorScheme invalid_hct = M3ColorScheme.FromCMF(invalid_hct_source, false);
    M3ColorScheme invalid_rgb = M3ColorScheme.FromCMF(
        new SRGBColor(float_nan, 0.0f, 0.0f),
        false
    );
    M3ColorScheme invalid_contrast = M3ColorScheme.FromCMF(
        SRGBColor.FromARGB32(0xff4285f4u),
        false,
        infinity
    );
    foreach (M3ColorScheme scheme in new[]{ invalid_hct, invalid_rgb, invalid_contrast })
    {
        foreach (SRGBColor color in RoleColors(scheme))
        {
            EqualNumeric(color.ToArgb32(), 0xff000000u);
        }
    }

    M3TonalPalette palette = M3TonalPalette.FromHueAndChroma(20.0, 40.0);
    EqualNumeric(palette.Srgb(infinity).ToArgb32(), 0xff000000u);
    EqualNumeric(palette.Hct(infinity), new HCTColor());

    HCTColor invalid_palette_source = new();
    invalid_palette_source.Chroma = infinity;
    M3TonalPalette invalid_source_palette = M3TonalPalette.FromHCT(invalid_palette_source);
    M3TonalPalette invalid_parameters_palette =
        M3TonalPalette.FromHueAndChroma(infinity, 40.0);
    EqualNumeric(invalid_source_palette.Hue(), 0.0);
    EqualNumeric(invalid_source_palette.Chroma(), 0.0);
    EqualNumeric(invalid_source_palette.KeyColor(), new HCTColor());
    EqualNumeric(invalid_parameters_palette.Hue(), 0.0);
    EqualNumeric(invalid_parameters_palette.Chroma(), 0.0);
    EqualNumeric(invalid_parameters_palette.KeyColor(), new HCTColor());

}
}
