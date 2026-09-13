using static SkrGui.Tests.TextSourceTestHelpers;
using static SkrGui.Tests.TextContractFixture;
namespace SkrGui.Tests;
// All cases/loops/inputs from tests/text/text_raster_contract_tests.cpp @ 611561f8.
public static partial class TextRasterContractTests {
private static readonly bool[] kBackends = [false,true];
private static void Eq<T>(T a,T b)=>Check.Equal(a,b);
private static void Eq(ulong a,uint b)=>Check.Equal(a,(ulong)b);
private static void Eq(long a,int b)=>Check.Equal(a,(long)b);
private static void Eq(Utf8StringView a,string b)=>Check.Equal(a.ToString(),b);
private static void Eq(uint a,char b)=>Check.Equal(a,(uint)b);
private static void Ne<T>(T a,T b)=>Check.That(!EqualityComparer<T>.Default.Equals(a,b), $"Expected unequal: {a} and {b}");
private static void Near(float a,float b)=>Check.Near(b,a,(1.1920928955078125e-7 * 100) * (1 + Math.Max(Math.Abs(a),Math.Abs(b))));
private static bool FlagAny(ETextGraphemeFlag a,ETextGraphemeFlag b)=>(a&b)!=0;
private static bool finite_rect(Rectf r)=>r.IsFinite();
private static bool finite_size(Sizef s)=>s.IsFinite();

[GuiTest("gui/text-contract/raster/service-and-face-configuration")]
public static void SourceCase01()
{
    using TextContractFixture fixture = Make(true);
    FontFace? latin = PreloadFamily(fixture.Services, "Skr Test Latin");
    FontFace? color = PreloadFamily(fixture.Services, "Skr Test Color");
    Check.That(latin != null);
    Check.That(color != null);

    TextFontRasterConfig initial =
        fixture.Services.DefaultFontRasterConfig();
    Eq(initial.Mode,ETextRasterMode.Sdf);
    Check.False(initial.LcdMode);
    Eq(initial.Hinting,ETextHinting.Light);
    Check.That(initial.DisableEmbeddedBitmaps);
    Eq(initial.SdfPpem,TextFontRasterConfig.KLowPpem);
    Eq(initial.SdfSpread,4u);
    Eq(initial.SubpixelPositioning,ETextSubpixelPositioning.Auto);
    Check.That(initial.KeepRoundingRemainders);

    TextFontRasterConfig service_config = initial;
    service_config.Mode = ETextRasterMode.Bitmap;
    service_config.LcdMode = true;
    service_config.Hinting = (ETextHinting)(0xffu);
    service_config.DisableEmbeddedBitmaps = false;
    service_config.SdfPpem = uint.MaxValue;
    service_config.SdfSpread = 8u;
    service_config.SubpixelPositioning =
        (ETextSubpixelPositioning)(0xffu);
    service_config.KeepRoundingRemainders = false;
    fixture.Services.SetDefaultFontRasterConfig(service_config);
    Eq(fixture.Services.DefaultFontRasterConfig().SdfPpem,256u);
    Eq(fixture.Services.DefaultFontRasterConfig()
            .SubpixelPositioning,ETextSubpixelPositioning.Auto);
    Eq(fixture.Services.DefaultFontRasterConfig().Hinting,ETextHinting.Light);
    Check.False(fixture.Services.DefaultFontRasterConfig()
            .DisableEmbeddedBitmaps);
    Check.False(fixture.Services.DefaultFontRasterConfig()
            .KeepRoundingRemainders);

    service_config.SdfPpem = 47u;
    fixture.Services.SetDefaultFontRasterConfig(service_config);
    service_config = fixture.Services.DefaultFontRasterConfig();
    Eq(service_config.Mode,ETextRasterMode.Bitmap);
    Check.That(service_config.LcdMode);
    Eq(service_config.SdfPpem,TextFontRasterConfig.KMediumPpem);
    Eq(service_config.SdfSpread,8u);
    Eq(fixture.Services.FontRasterConfig(color.Id()),service_config);

    TextFontRasterConfig face_config = service_config;
    face_config.Mode = ETextRasterMode.Sdf;
    face_config.LcdMode = false;
    face_config.SdfPpem = TextFontRasterConfig.KHighPpem;
    Check.That(fixture.Services.SetFontRasterConfig(
        latin.Id(),
        face_config
    ));
    Eq(fixture.Services.FontRasterConfig(latin.Id()),face_config);
    Eq(fixture.Services.FontRasterConfig(color.Id()),service_config);

    TextLine line = fixture.Services.CreateLine();
    TextStyle style = MakeStyle("");
    style.FontFaces.Add(latin.Id());
    line.AddString("A", style);
    Check.That(line.Shape());
    TextRenderResult face_result = new();
    Check.That(line.Paint(Offsetf.Zero(), face_result));
    Eq((ulong)face_result.Commands.Count,1u);
    Eq(face_result.Commands[(int)(0)].PixelMode,ETextPixelMode.Sdf);

    Check.That(fixture.Services.ClearFontRasterConfig(latin.Id()));
    Eq(fixture.Services.FontRasterConfig(latin.Id()),service_config);
    TextRenderResult inherited_result = new();
    Check.That(line.Paint(Offsetf.Zero(), inherited_result));
    Eq((ulong)inherited_result.Commands.Count,1u);
    Eq(inherited_result.Commands[(int)(0)].PixelMode,ETextPixelMode.GrayLcd);
}

[GuiTest("gui/text-contract/raster/bitmap-light-hinting-keeps-narrow-glyphs-separated")]
public static void SourceCase02()
{
    var run_mode = (bool lcd) => {
        using TextContractFixture fixture = Make(true);
        FontFace? face = PreloadFamily(fixture.Services,
            "Skr Test Latin"
        );
        Check.That(face != null);

        TextFontRasterConfig config =
            fixture.Services.DefaultFontRasterConfig();
        config.Mode = ETextRasterMode.Bitmap;
        config.LcdMode = lcd;
        config.Hinting = ETextHinting.Light;
        config.SubpixelPositioning = ETextSubpixelPositioning.Auto;
        fixture.Services.SetDefaultFontRasterConfig(config);

        TextLine line = fixture.Services.CreateLine();
        TextStyle style = MakeStyle("", 9.0f);
        style.FontFaces.Add(face.Id());
        line.AddString("iiii", style);
        Check.That(line.Shape());

        TextRenderResult result = new();
        Check.That(line.Paint(Offsetf.Zero(), result));
        Eq((ulong)result.Commands.Count,1u);
        Eq((ulong)result.Rects.Count,4u);
        TextAtlasView atlas = fixture.Services.Atlas(
            result.Commands[(int)(0)].AtlasIndex
        );

        HorizontalCoverage previous = new();
        Check.That(horizontal_coverage(
            atlas,
            result.Rects[(int)(0)],
            previous
        ));
        for (ulong index = 1u; index < (ulong)result.Rects.Count; ++index)
        {
            HorizontalCoverage current = new();
            Check.That(horizontal_coverage(
                atlas,
                result.Rects[(int)(index)],
                current
            ));
            if (lcd)
            {
                Check.That((previous.Left + previous.Right) < (current.Left + current.Right));
            }
            else
            {
                Check.That((previous.Right) <= (current.Left));
            }
            previous = current;
        }
    };

    run_mode(false);
    run_mode(true);
}

[GuiTest("gui/text-contract/raster/bitmap-embolden-changes-coverage")]
public static void SourceCase03()
{
    using TextContractFixture fixture = Make(true);
    FontFace? face = PreloadFamily(fixture.Services, "Skr Test Latin");
    Check.That(face != null);

    TextFontRasterConfig config =
        fixture.Services.DefaultFontRasterConfig();
    config.Mode = ETextRasterMode.Bitmap;
    config.LcdMode = false;
    config.SubpixelPositioning = ETextSubpixelPositioning.Quarter;
    fixture.Services.SetDefaultFontRasterConfig(config);

    var raster_signature = () => {
        TextLine line = fixture.Services.CreateLine();
        TextStyle style = MakeStyle("", 16.0f);
        style.FontFaces.Add(face.Id());
        line.AddString("A", style);
        Check.That(line.Shape());
        TextRenderResult result = paint_glyph(line,
            Offsetf.Zero()
        );
        TextAtlasView atlas = fixture.Services.Atlas(
            result.Commands[(int)(0)].AtlasIndex
        );
        ulong signature = 0u;
        Check.That(atlas_rect_signature(
            atlas,
            result.Rects[(int)(0)], ref signature));
        return signature;
    };

    face.SetEmbolden(0.0f);
    ulong regular = raster_signature();
    face.SetEmbolden(1.0f);
    ulong emboldened = raster_signature();
    Ne(regular,emboldened);
}

[GuiTest("gui/text-contract/raster/bitmap-horizontal-positioning")]
public static void SourceCase04()
{
    RasterSignaturesDelegate raster_signatures = (
                                       bool lcd,
                                       ETextSubpixelPositioning positioning,
                                       ReadOnlySpan<float> positions
                                   ) => {
        using TextContractFixture fixture = Make(true);
        FontFace? face = PreloadFamily(fixture.Services,
            "Skr Test Latin"
        );
        Check.That(face != null);

        TextFontRasterConfig config =
            fixture.Services.DefaultFontRasterConfig();
        config.Mode = ETextRasterMode.Bitmap;
        config.LcdMode = lcd;
        config.SubpixelPositioning = positioning;
        fixture.Services.SetDefaultFontRasterConfig(config);

        TextLine line = fixture.Services.CreateLine();
        TextStyle style = MakeStyle("", 16.0f);
        style.FontFaces.Add(face.Id());
        line.AddString("A", style);
        Check.That(line.Shape());

        List<ulong> signatures = new();
        foreach (float position in positions)
        {
            TextRenderResult result = paint_glyph(line,
                new Offsetf(position, 0.0f)
            );
            TextAtlasView atlas = fixture.Services.Atlas(
                result.Commands[(int)(0)].AtlasIndex
            );
            ulong signature = 0u;
            Check.That(atlas_rect_signature(
                atlas,
                result.Rects[(int)(0)], ref signature));
            signatures.Add(signature);
        }
        return signatures;
    };

    float[] quarter_positions = {
        0.0f,
        0.13f,
        0.38f,
        0.63f,
        0.88f,
    };
    float[] half_positions = {
        0.0f,
        0.26f,
        0.74f,
        0.76f,
    };

    foreach (bool lcd in new bool[]{false,true})
    {
        List<ulong> quarter = raster_signatures(
            lcd,
            ETextSubpixelPositioning.Quarter,
            quarter_positions
        );
        Eq((ulong)quarter.Count,(ulong)quarter_positions.Length);
        Ne(quarter[(int)(0)],quarter[(int)(1)]);
        Ne(quarter[(int)(1)],quarter[(int)(2)]);
        Ne(quarter[(int)(2)],quarter[(int)(3)]);
        Eq(quarter[(int)(0)],quarter[(int)(4)]);

        List<ulong> half = raster_signatures(
            lcd,
            ETextSubpixelPositioning.Half,
            half_positions
        );
        Eq((ulong)half.Count,(ulong)half_positions.Length);
        Ne(half[(int)(0)],half[(int)(1)]);
        Eq(half[(int)(1)],half[(int)(2)]);
        Eq(half[(int)(0)],half[(int)(3)]);
    }
}

[GuiTest("gui/text-contract/raster/device-mapping-controls-bitmap-placement")]
public static void SourceCase05()
{
    using TextContractFixture fixture = Make(true);
    FontFace? face = PreloadFamily(fixture.Services, "Skr Test Latin");
    Check.That(face != null);

    TextFontRasterConfig config =
        fixture.Services.DefaultFontRasterConfig();
    config.Mode = ETextRasterMode.Bitmap;
    config.SubpixelPositioning = ETextSubpixelPositioning.Quarter;
    fixture.Services.SetDefaultFontRasterConfig(config);

    TextLine line = fixture.Services.CreateLine();
    TextStyle style = MakeStyle("", 16.0f);
    style.FontFaces.Add(face.Id());
    line.AddString("A", style);
    Check.That(line.Shape());
    var signature = (TextRenderResult result) => {
        ulong value = 0u;
        Check.That(atlas_rect_signature(
            fixture.Services.Atlas(result.Commands[(int)(0)].AtlasIndex),
            result.Rects[(int)(0)], ref value));
        return value;
    };

    TextRenderResult translated_origin = paint_glyph(line,
        new Offsetf(0.13f, 0.0f)
    );
    TextRenderResult translated_mapping = paint_glyph(line,
        Offsetf.Zero(),
        1.0f,
        new Offsetf(0.13f, 0.0f)
    );
    Eq(signature(translated_origin),signature(translated_mapping));
    Check.Near(
        translated_origin.Rects[(int)(0)].Rect.Left,
        translated_mapping.Rects[(int)(0)].Rect.Left + 0.13f,
        0.0001f
    );
    Check.Near(
        translated_origin.Rects[(int)(0)].Rect.Top,
        translated_mapping.Rects[(int)(0)].Rect.Top,
        0.0001f
    );

    TextRenderResult no_mapping = paint_glyph(line,
        new Offsetf(0.13f, 0.0f),
        1.0f,
        null
    );
    Ne(signature(no_mapping),signature(translated_origin));
    TextRenderResult no_mapping_shifted = paint_glyph(line,
        new Offsetf(0.38f, 0.0f),
        1.0f,
        null
    );
    Eq(signature(no_mapping),signature(no_mapping_shifted));
    Check.Near(
        no_mapping_shifted.Rects[(int)(0)].Rect.Left -
            no_mapping.Rects[(int)(0)].Rect.Left,
        0.25f,
        0.0001f
    );
}

[GuiTest("gui/text-contract/raster/bitmap-device-texel-mapping-is-pixel-perfect")]
public static void SourceCase06()
{
    foreach (bool advanced in new bool[]{false,true})
    {
        foreach (bool lcd in new bool[]{false,true})
        {
            using TextContractFixture fixture = Make(advanced);
            FontFace? face = PreloadFamily(fixture.Services,
                "Skr Test Latin"
            );
            Check.That(face != null);
            if (face is null)
            {
                continue;
            }

            fixture.Services.SetAtlasGlyphPadding(0u);
            TextFontRasterConfig config =
                fixture.Services.DefaultFontRasterConfig();
            config.Mode = ETextRasterMode.Bitmap;
            config.LcdMode = lcd;
            config.SubpixelPositioning =
                ETextSubpixelPositioning.Quarter;
            fixture.Services.SetDefaultFontRasterConfig(config);

            TextLine line = fixture.Services.CreateLine();
            TextStyle style = MakeStyle("", 16.0f);
            style.FontFaces.Add(face.Id());
            line.AddString("A", style);
            Check.That(line.Shape());

            foreach (float pixel_ratio in new float[] { 1.25f, 2.0f })
            {
                Offsetf kOrigin = new(0.23f, 0.19f);
                Offsetf kDeviceOffset = new(0.37f, 0.41f);
                TextRenderResult result = new();
                Check.That(line.Paint(
                    kOrigin,
                    result,
                    new TextPaintDesc{ PixelRatio = pixel_ratio, DeviceOffset = kDeviceOffset,
                    }
                ));
                Eq((ulong)result.Commands.Count,1u);
                Eq((ulong)result.Rects.Count,1u);
                if ((ulong)result.Commands.Count != 1u ||
                    (ulong)result.Rects.Count != 1u)
                {
                    continue;
                }

                TextRenderRect rect = result.Rects[(int)(0u)];
                TextAtlasView atlas = fixture.Services.Atlas(
                    result.Commands[(int)(0u)].AtlasIndex
                );
                Rectf device_rect = new(
                    rect.Rect.Left * pixel_ratio + kDeviceOffset.X,
                    rect.Rect.Top * pixel_ratio + kDeviceOffset.Y,
                    rect.Rect.Right * pixel_ratio + kDeviceOffset.X,
                    rect.Rect.Bottom * pixel_ratio + kDeviceOffset.Y
                );
                Check.Near(
                    device_rect.Left,
                    Round(device_rect.Left),
                    0.0001f
                );
                Check.Near(
                    device_rect.Top,
                    Round(device_rect.Top),
                    0.0001f
                );
                Check.Near(
                    device_rect.Right,
                    Round(device_rect.Right),
                    0.0001f
                );
                Check.Near(
                    device_rect.Bottom,
                    Round(device_rect.Bottom),
                    0.0001f
                );
                Check.Near(
                    device_rect.Width(),
                    rect.Uv.Width() * (float)(atlas.Width),
                    0.0001f
                );
                Check.Near(
                    device_rect.Height(),
                    rect.Uv.Height() * (float)(atlas.Height),
                    0.0001f
                );
            }
        }
    }
}

[GuiTest("gui/text-contract/raster/bitmap-device-y-snap-and-unmapped-continuity")]
public static void SourceCase07()
{
    foreach (bool advanced in new bool[]{false,true})
    {
        using TextContractFixture fixture = Make(advanced);
        FontFace? face = PreloadFamily(fixture.Services,
            "Skr Test Latin"
        );
        Check.That(face != null);
        if (face is null)
        {
            continue;
        }

        TextFontRasterConfig config =
            fixture.Services.DefaultFontRasterConfig();
        config.Mode = ETextRasterMode.Bitmap;
        config.SubpixelPositioning = ETextSubpixelPositioning.Quarter;
        fixture.Services.SetDefaultFontRasterConfig(config);

        TextLine line = fixture.Services.CreateLine();
        TextStyle style = MakeStyle("", 16.0f);
        style.FontFaces.Add(face.Id());
        line.AddString("A", style);
        Check.That(line.Shape());
        Check.False(line.Glyphs().IsEmpty);

        foreach (float pixel_ratio in new float[] { 1.25f, 2.0f })
        {
            float baseline = line.LineAscent() +
                line.Glyphs()[(int)(0u)].Offset.Y;
            Offsetf device_offset = new(
                0.37f,
                0.25f - baseline * pixel_ratio
            );
            var mapped_top = (float origin_y) => {
                return paint_glyph(line,
                           new Offsetf(0.0f, origin_y),
                           pixel_ratio,
                           device_offset
                       )
                           .Rects[(int)(0u)]
                           .Rect.Top *
                    pixel_ratio +
                    device_offset.Y;
            };

            float first = mapped_top(0.0f);
            float same_pixel = mapped_top(0.5f / pixel_ratio);
            float next_pixel = mapped_top(1.0f / pixel_ratio);
            Check.Near(first, same_pixel, 0.0001f);
            Check.Near(next_pixel - first, 1.0f, 0.0001f);

            TextRenderResult continuous_first = paint_glyph(line,
                new Offsetf(0.0f, 0.13f),
                pixel_ratio,
                null
            );
            TextRenderResult continuous_second = paint_glyph(line,
                new Offsetf(0.0f, 0.38f),
                pixel_ratio,
                null
            );
            Check.Near(
                continuous_second.Rects[(int)(0u)].Rect.Top -
                    continuous_first.Rects[(int)(0u)].Rect.Top,
                0.25f,
                0.0001f
            );
        }
    }
}

[GuiTest("gui/text-contract/raster/empty-bitmap-keeps-zero-raster-rect")]
public static void SourceCase08()
{
    foreach (bool advanced in new bool[]{false,true})
    {
        foreach (bool lcd in new bool[]{false,true})
        {
            using TextContractFixture fixture = Make(advanced);
            FontFace? face = PreloadFamily(fixture.Services,
                "Skr Test Latin"
            );
            Check.That(face != null);
            if (face is null)
            {
                continue;
            }

            fixture.Services.SetAtlasGlyphPadding(4u);
            TextFontRasterConfig config =
                fixture.Services.DefaultFontRasterConfig();
            config.Mode = ETextRasterMode.Bitmap;
            config.LcdMode = lcd;
            config.SubpixelPositioning =
                ETextSubpixelPositioning.Quarter;
            fixture.Services.SetDefaultFontRasterConfig(config);

            float kFontSize = 16.0f;
            uint glyph = face.GlyphIndex((uint)' ');
            Ne(glyph,0u);
            Check.That((face.GlyphAdvance(glyph, kFontSize).X) > (0.0f));
            Eq(face.GlyphOffset(glyph, kFontSize),Offsetf.Zero());
            Eq(face.GlyphSize(glyph, kFontSize),Sizef.Zero());
            Eq(fixture.Services.AtlasCount(),0u);
        }
    }
}

[GuiTest("gui/text-contract/raster/automatic-bitmap-positioning-follows-device-size")]
public static void SourceCase09()
{
    var variant_count = (float font_size, float pixel_ratio) => {
        using TextContractFixture fixture = Make(true);
        FontFace? face = PreloadFamily(fixture.Services,
            "Skr Test Latin"
        );
        Check.That(face != null);
        TextFontRasterConfig config =
            fixture.Services.DefaultFontRasterConfig();
        config.Mode = ETextRasterMode.Bitmap;
        config.SubpixelPositioning = ETextSubpixelPositioning.Auto;
        fixture.Services.SetDefaultFontRasterConfig(config);

        TextLine line = fixture.Services.CreateLine();
        TextStyle style = MakeStyle("", font_size);
        style.FontFaces.Add(face.Id());
        line.AddString("A", style);
        Check.That(line.Shape());

        float[] positions = {
            0.0f,
            0.13f,
            0.38f,
            0.76f,
        };
        List<ulong> variants = new();
        foreach (float position in positions)
        {
            TextRenderResult result = paint_glyph(line,
                new Offsetf(position / pixel_ratio, 0.0f),
                pixel_ratio
            );
            ulong signature = 0u;
            Check.That(atlas_rect_signature(
                fixture.Services.Atlas(result.Commands[(int)(0)].AtlasIndex),
                result.Rects[(int)(0)], ref signature));
            bool found = false;
            foreach (ulong candidate in variants)
            {
                found |= candidate == signature;
            }
            if (!found)
            {
                variants.Add(signature);
            }
        }
        return (ulong)variants.Count;
    };

    Eq(variant_count(16.0f, 1.0f),4u);
    Eq(variant_count(16.0f, 1.02f),2u);
    Eq(variant_count(20.0f, 1.0f),2u);
    Eq(variant_count(20.0f, 1.02f),1u);
    Eq(variant_count(12.0f, 1.25f),4u);
    Eq(variant_count(16.0f, 1.25f),2u);
    Eq(variant_count(16.0f, 2.0f),1u);
}

[GuiTest("gui/text-contract/raster/sdf-placement-remains-continuous")]
public static void SourceCase10()
{
    using TextContractFixture fixture = Make(true);
    FontFace? face = PreloadFamily(fixture.Services, "Skr Test Latin");
    Check.That(face != null);
    TextFontRasterConfig config =
        fixture.Services.DefaultFontRasterConfig();
    config.Mode = ETextRasterMode.Sdf;
    config.SubpixelPositioning = ETextSubpixelPositioning.Quarter;
    fixture.Services.SetDefaultFontRasterConfig(config);

    TextLine line = fixture.Services.CreateLine();
    TextStyle style = MakeStyle("", 16.0f);
    style.FontFaces.Add(face.Id());
    line.AddString("A", style);
    Check.That(line.Shape());

    TextRenderResult first = paint_glyph(line,
        new Offsetf(0.13f, 0.0f)
    );
    TextRenderResult second = paint_glyph(line,
        new Offsetf(0.38f, 0.0f),
        1.0f,
        new Offsetf(0.41f, 0.0f)
    );
    Check.Near(
        second.Rects[(int)(0)].Rect.Left - first.Rects[(int)(0)].Rect.Left,
        0.25f,
        0.0001f
    );
}

[GuiTest("gui/text-contract/raster/descriptor-default-and-service-isolation")]
public static void SourceCase11()
{
    TextFontRasterConfig bitmap_config = new();
    bitmap_config.Mode = ETextRasterMode.Bitmap;
    bitmap_config.LcdMode = true;
    bitmap_config.SdfPpem = TextFontRasterConfig.KHighPpem;
    bitmap_config.SdfSpread = 8u;

    TextServices bitmap_service = TextServices.CreateAdvanced(new TextServicesDesc { AddSystemFontProvider = false, DefaultFontRasterConfig = bitmap_config, IcuData = TextContractFixture.IcuData(),
    });
    TextServices sdf_service = TextServices.CreateAdvanced(new TextServicesDesc { AddSystemFontProvider = false, IcuData = TextContractFixture.IcuData(),
    });
    Check.That(bitmap_service != null);
    Check.That(sdf_service != null);
    bitmap_service.AddFontProvider(MakeProvider());
    sdf_service.AddFontProvider(MakeProvider());

    FontFace? bitmap_face = PreloadFamily(bitmap_service,
        "Skr Test Latin"
    );
    FontFace? sdf_face = PreloadFamily(sdf_service, "Skr Test Latin");
    Check.That(bitmap_face != null);
    Check.That(sdf_face != null);
    Eq(bitmap_service.DefaultFontRasterConfig(),bitmap_config);

    var paint = (TextServices services, FontFace face) => {
        TextLine line = services.CreateLine();
        TextStyle style = MakeStyle("");
        style.FontFaces.Add(face.Id());
        line.AddString("A", style);
        Check.That(line.Shape());
        TextRenderResult result = new();
        Check.That(line.Paint(Offsetf.Zero(), result));
        Eq((ulong)result.Commands.Count,1u);
        return result;
    };

    TextRenderResult bitmap_result = paint(bitmap_service,bitmap_face
    );
    TextRenderResult sdf_result = paint(sdf_service,sdf_face);
    Eq(bitmap_result.Commands[(int)(0)].PixelMode,ETextPixelMode.GrayLcd);
    Eq(sdf_result.Commands[(int)(0)].PixelMode,ETextPixelMode.Sdf);
    Check.That((bitmap_service.AtlasCount()) > (0u));
    Check.That((sdf_service.AtlasCount()) > (0u));

    sdf_service.ClearAtlases();
    Eq(sdf_service.AtlasCount(),0u);
    Check.That((bitmap_service.AtlasCount()) > (0u));
}

[GuiTest("gui/text-contract/raster/colored-glyph-through-line")]
public static void SourceCase12()
{
    foreach (bool advanced in new bool[]{false,true})
    {
        using TextContractFixture fixture = Make(advanced);
        FontFace? face = PreloadFamily(fixture.Services,
            "Skr Test Color"
        );
        Check.That(face != null);
        if (face is null)
        {
            continue;
        }

        TextFontRasterConfig config =
            fixture.Services.DefaultFontRasterConfig();
        config.Mode = ETextRasterMode.Bitmap;
        config.LcdMode = false;
        config.Hinting = ETextHinting.None;
        fixture.Services.SetDefaultFontRasterConfig(config);

        TextLine line = fixture.Services.CreateLine();
        TextStyle style = MakeStyle("");
        style.FontFaces.Add(face.Id());
        line.AddString("★", style);
        Check.That(line.Shape());

        TextRenderResult result = new();
        Check.That(line.Paint(Offsetf.Zero(), result));
        Check.False(result.IsEmpty());
        Eq(result.Commands[0].PixelMode,ETextPixelMode.Colored);
        Check.That((fixture.Services.AtlasCount()) > (0u));
        TextAtlasView atlas = fixture.Services.Atlas(
            result.Commands[0].AtlasIndex
        );
        Eq(atlas.Format,ETextAtlasFormat.RGBA8);
        Check.That(atlas_has_coverage(atlas));
    }
}

[GuiTest("gui/text-contract/raster/color-layer-transform-changes-geometry")]
public static void SourceCase13()
{

    RasterRoute[] routes = {
        new RasterRoute(true,false,true),
        new RasterRoute(false,false,false),
        new RasterRoute(true,true,false),
    };

    foreach (RasterRoute route in routes)
    {
        using TextContractFixture fixture = Make(route.Advanced);
        FontFace? face = PreloadFamily(fixture.Services,
            "Skr Test Color"
        );
        Check.That(face != null);
        if (face is null)
        {
            continue;
        }

        fixture.Services.SetAtlasGlyphPadding(0u);
        TextFontRasterConfig config =
            fixture.Services.DefaultFontRasterConfig();
        config.Mode = ETextRasterMode.Bitmap;
        config.LcdMode = route.Lcd;
        config.Hinting = ETextHinting.None;
        config.SubpixelPositioning =
            ETextSubpixelPositioning.Disabled;
        fixture.Services.SetDefaultFontRasterConfig(config);

        float kFontSize = 48.0f;
        uint glyph = face.GlyphIndex((uint)'★');
        Ne(glyph,0u);
        if (glyph == 0u)
        {
            continue;
        }
        Offsetf natural_advance =
            face.GlyphAdvance(glyph, kFontSize);

        TextLine line = fixture.Services.CreateLine();
        TextStyle style = MakeStyle("", kFontSize);
        style.FontFaces.Add(face.Id());
        line.AddString("★", style);
        Check.That(line.Shape());

        var paint = () => {
            TextRenderResult result = new();
            Check.That(line.Paint(
                Offsetf.Zero(),
                result,
                new TextPaintDesc{ DeviceOffset = null }
            ));
            Eq((ulong)result.Commands.Count,1u);
            Eq((ulong)result.Rects.Count,1u);
            return result;
        };

        TextRenderResult before = paint();
        TextAtlasView before_atlas = fixture.Services.Atlas(
            before.Commands[(int)(0u)].AtlasIndex
        );
        HorizontalCoverage before_coverage = new();
        ulong before_signature = 0u;
        Eq(before.Commands[(int)(0u)].PixelMode,ETextPixelMode.Colored);
        Eq(before_atlas.Format,ETextAtlasFormat.RGBA8);
        Check.That(horizontal_coverage(
            before_atlas,
            before.Rects[(int)(0u)],
            before_coverage
        ));
        Check.That(atlas_rect_signature(
            before_atlas,
            before.Rects[(int)(0u)], ref before_signature));

        Float3x3 transform = Float3x3.Identity();
        transform = transform with { M00 = 1.5f };
        face.SetTransform(transform);
        Check.False(line.IsReady());
        Check.That(line.Shape());

        TextRenderResult after = paint();
        TextAtlasView after_atlas = fixture.Services.Atlas(
            after.Commands[(int)(0u)].AtlasIndex
        );
        HorizontalCoverage after_coverage = new();
        ulong after_signature = 0u;
        Eq(after.Commands[(int)(0u)].PixelMode,ETextPixelMode.Colored);
        Eq(after_atlas.Format,ETextAtlasFormat.RGBA8);
        Check.That(horizontal_coverage(
            after_atlas,
            after.Rects[(int)(0u)],
            after_coverage
        ));
        Check.That(atlas_rect_signature(
            after_atlas,
            after.Rects[(int)(0u)], ref after_signature));

        float before_coverage_width =
            before_coverage.Right - before_coverage.Left;
        float after_coverage_width =
            after_coverage.Right - after_coverage.Left;
        if (route.TransformsColorLayers)
        {
            Check.That((after.Rects[(int)(0u)].Rect.Width()) > (before.Rects[(int)(0u)].Rect.Width() * 1.35f));
            Check.That((after_coverage_width) > (before_coverage_width * 1.35f));
            Ne(after_signature,before_signature);
        }
        else
        {
            Check.Near(
                after.Rects[(int)(0u)].Rect.Width(),
                before.Rects[(int)(0u)].Rect.Width(),
                0.0001f
            );
            Check.Near(
                after_coverage_width,
                before_coverage_width,
                0.0001f
            );
            Eq(after_signature,before_signature);
        }
        Eq(face.GlyphAdvance(glyph, kFontSize),natural_advance);
    }
}

[GuiTest("gui/text-contract/raster/color-palette-affects-rendering")]
public static void SourceCase14()
{
    foreach (bool advanced in new bool[]{false,true})
    {
        foreach (bool lcd in new bool[]{false,true})
        {
            using TextContractFixture fixture = Make(advanced);
            FontFace? face = PreloadFamily(fixture.Services,
                "Skr Test Color"
            );
            Check.That(face != null);
            if (face is null)
            {
                continue;
            }

            fixture.Services.SetAtlasGlyphPadding(0u);
            TextFontRasterConfig config =
                fixture.Services.DefaultFontRasterConfig();
            config.Mode = ETextRasterMode.Bitmap;
            config.LcdMode = lcd;
            config.Hinting = ETextHinting.None;
            config.SubpixelPositioning =
                ETextSubpixelPositioning.Disabled;
            fixture.Services.SetDefaultFontRasterConfig(config);

            float kFontSize = 48.0f;
            uint glyph = face.GlyphIndex((uint)'★');
            Offsetf advance = face.GlyphAdvance(glyph, kFontSize);
            Ne(glyph,0u);
            Eq(face.PaletteCount(),2u);

            TextLine line = fixture.Services.CreateLine();
            TextStyle style = MakeStyle("", kFontSize);
            style.FontFaces.Add(face.Id());
            line.AddString("★", style);
            Check.That(line.Shape());

            var paint = () => {
                TextRenderResult result = paint_glyph(line,
                    Offsetf.Zero(),
                    1.0f,
                    null
                );
                Eq(result.Commands[(int)(0u)].PixelMode,ETextPixelMode.Colored);
                TextAtlasView atlas = fixture.Services.Atlas(
                    result.Commands[(int)(0u)].AtlasIndex
                );
                Eq(atlas.Format,ETextAtlasFormat.RGBA8);
                ulong signature = 0u;
                Check.That(atlas_rect_signature(
                    atlas,
                    result.Rects[(int)(0u)], ref signature));
                return ( result, signature );
            };

            var (palette0, signature0) = paint();
            Check.That(face.SetUsedPalette(1u));
            Check.That(line.IsReady());
            var (palette1, signature1) = paint();
            Ne(signature1,signature0);

            SRGBColor[] custom = {
                new SRGBColor(1.0f, 0.0f, 0.0f, 1.0f),
                new SRGBColor(0.0f, 1.0f, 0.0f, 1.0f),
            };
            face.SetCustomPaletteColors(custom);
            Check.That(line.IsReady());
            var (custom_result, custom_signature) = paint();
            Check.Near(
                palette1.Rects[(int)(0u)].Rect.Width(),
                palette0.Rects[(int)(0u)].Rect.Width(),
                0.0001f
            );
            Check.Near(
                custom_result.Rects[(int)(0u)].Rect.Width(),
                palette0.Rects[(int)(0u)].Rect.Width(),
                0.0001f
            );
            Eq(face.GlyphAdvance(glyph, kFontSize),advance);

            face.SetCustomPaletteColors(Array.Empty<SRGBColor>());
            var (restored_result, restored_signature) = paint();
            Eq(restored_signature,signature1);
            Eq(restored_result.Rects[(int)(0u)].Rect,palette1.Rects[(int)(0u)].Rect);
            Ne(custom_signature,0u);
        }
    }
}

[GuiTest("gui/text-contract/raster/simple-slant-applies-once")]
public static void SourceCase15()
{
    float[] width_deltas = new float[2];
    uint route_index = 0u;
    foreach (bool advanced in new bool[]{false,true})
    {
        using TextContractFixture fixture = Make(advanced);
        FontFace? face = PreloadFamily(fixture.Services,
            "Skr Test Latin"
        );
        Check.That(face != null);
        if (face is null)
        {
            continue;
        }

        fixture.Services.SetAtlasGlyphPadding(0u);
        TextFontRasterConfig config =
            fixture.Services.DefaultFontRasterConfig();
        config.Mode = ETextRasterMode.Bitmap;
        config.LcdMode = false;
        config.Hinting = ETextHinting.None;
        config.SubpixelPositioning =
            ETextSubpixelPositioning.Disabled;
        fixture.Services.SetDefaultFontRasterConfig(config);

        float kFontSize = 48.0f;
        uint glyph = face.GlyphIndex((uint)'I');
        Offsetf advance = face.GlyphAdvance(glyph, kFontSize);
        TextLine line = fixture.Services.CreateLine();
        TextStyle style = MakeStyle("", kFontSize);
        style.FontFaces.Add(face.Id());
        line.AddString("I", style);
        Check.That(line.Shape());

        var paint = () => {
            TextRenderResult result = paint_glyph(line,
                Offsetf.Zero(),
                1.0f,
                null
            );
            ulong signature = 0u;
            Check.That(atlas_rect_signature(
                fixture.Services.Atlas(
                    result.Commands[(int)(0u)].AtlasIndex
                ),
                result.Rects[(int)(0u)], ref signature));
            return ( result.Rects[(int)(0u)].Rect, signature );
        };

        var (before, before_signature) = paint();
        Float3x3 slant = Float3x3.Identity();
        slant = slant with { M10 = 0.25f };
        face.SetTransform(slant);
        Check.False(line.IsReady());
        Check.That(line.Shape());
        var (after, after_signature) = paint();

        Ne(after_signature,before_signature);
        Check.That((after.Width()) > (before.Width() + 4.0f));
        Eq(face.GlyphAdvance(glyph, kFontSize),advance);
        width_deltas[route_index++] = after.Width() - before.Width();
    }

    Eq(route_index,(ulong)width_deltas.Length);
    if (route_index != (ulong)width_deltas.Length)
    {
        return;
    }
    Check.That((MathF.Abs(width_deltas[(int)(0u)] - width_deltas[(int)(1u)])) < (2.0f));
}

[GuiTest("gui/text-contract/raster/color-glyph-positioning-and-embolden")]
public static void SourceCase16()
{
    using TextContractFixture fixture = Make(true);
    FontFace? face = PreloadFamily(fixture.Services,
        "Skr Test Color"
    );
    Check.That(face != null);
    if (face is null)
    {
        return;
    }

    fixture.Services.SetAtlasGlyphPadding(0u);
    TextFontRasterConfig config =
        fixture.Services.DefaultFontRasterConfig();
    config.Mode = ETextRasterMode.Bitmap;
    config.LcdMode = false;
    config.Hinting = ETextHinting.None;
    config.SubpixelPositioning = ETextSubpixelPositioning.Quarter;
    fixture.Services.SetDefaultFontRasterConfig(config);

    TextLine line = fixture.Services.CreateLine();
    TextStyle style = MakeStyle("", 48.0f);
    style.FontFaces.Add(face.Id());
    line.AddString("★", style);
    Check.That(line.Shape());

    var paint = (float origin_x, Offsetf? device_offset) => {
        TextRenderResult result = paint_glyph(line,
            new Offsetf(origin_x, 0.0f),
            1.0f,
            device_offset
        );
        Eq(result.Commands[(int)(0u)].PixelMode,ETextPixelMode.Colored);
        ulong signature = 0u;
        Check.That(atlas_rect_signature(
            fixture.Services.Atlas(result.Commands[(int)(0u)].AtlasIndex),
            result.Rects[(int)(0u)], ref signature));
        return ( result, signature );
    };

    var (position0, position0_signature) = paint(
        0.13f,
        Offsetf.Zero()
    );
    var (position1, position1_signature) = paint(
        0.38f,
        Offsetf.Zero()
    );
    Ne(position0_signature,position1_signature);

    config.SubpixelPositioning = ETextSubpixelPositioning.Disabled;
    fixture.Services.SetDefaultFontRasterConfig(config);
    Check.That(line.Shape());
    var (regular, regular_signature) = paint(0.0f, null);
    face.SetEmbolden(1.0f);
    Check.False(line.IsReady());
    Check.That(line.Shape());
    var (emboldened, emboldened_signature) = paint(0.0f, null);
    Ne(emboldened_signature,regular_signature);
    Check.That((emboldened.Rects[(int)(0u)].Rect.Width()) > (regular.Rects[(int)(0u)].Rect.Width()));
}

[GuiTest("gui/text-contract/raster/color-bitmap-size-and-format")]
public static void SourceCase17()
{

    BitmapRasterRoute[] routes = {
        new BitmapRasterRoute(true,false),
        new BitmapRasterRoute(false,false),
        new BitmapRasterRoute(true,true),
        new BitmapRasterRoute(false,true),
    };

    foreach (BitmapRasterRoute route in routes)
    {
        using TextContractFixture fixture = Make(route.Advanced);
        FontFace? face = PreloadFamily(fixture.Services,
            "Skr Test Color Bitmap"
        );
        Check.That(face != null);
        if (face is null)
        {
            continue;
        }
        Check.That(face.HasColorGlyphs());

        fixture.Services.SetAtlasGlyphPadding(0u);
        TextFontRasterConfig config =
            fixture.Services.DefaultFontRasterConfig();
        config.Mode = ETextRasterMode.Bitmap;
        config.LcdMode = route.Lcd;
        config.Hinting = ETextHinting.None;
        config.SubpixelPositioning =
            ETextSubpixelPositioning.Disabled;
        fixture.Services.SetDefaultFontRasterConfig(config);

        float kFontSize = 16.0f;
        TextLine line = fixture.Services.CreateLine();
        TextStyle style = MakeStyle("", kFontSize);
        style.FontFaces.Add(face.Id());
        line.AddString("★", style);
        Check.That(line.Shape());

        uint glyph = face.GlyphIndex((uint)'★');
        Offsetf glyph_offset = face.GlyphOffset(
            glyph,
            kFontSize
        );
        Sizef glyph_size = face.GlyphSize(glyph, kFontSize);
        float kStrikeScale = 16.0f / 11.0f;
        Check.Near(glyph_offset.X, 0.0f, 0.0001f);
        Check.Near(glyph_offset.Y, -2.0f * kStrikeScale, 0.0001f);
        Check.Near(glyph_size.Width, 9.0f * kStrikeScale, 0.0001f);
        Check.Near(glyph_size.Height, 3.0f * kStrikeScale, 0.0001f);

        Offsetf kOrigin = new(0.13f, 0.27f);
        foreach (float pixel_ratio in new float[] { 1.0f, 1.25f, 2.0f })
        {
            TextRenderResult result = paint_glyph(line,
                kOrigin,
                pixel_ratio,
                null
            );
            Eq(result.Commands[(int)(0u)].PixelMode,ETextPixelMode.Colored);
            TextAtlasView atlas = fixture.Services.Atlas(
                result.Commands[(int)(0u)].AtlasIndex
            );
            Eq(atlas.Format,ETextAtlasFormat.RGBA8);
            Check.That(atlas_has_coverage(atlas));

            float atlas_width = result.Rects[(int)(0u)].Uv.Width() *
                (float)(atlas.Width);
            float atlas_height = result.Rects[(int)(0u)].Uv.Height() *
                (float)(atlas.Height);
            Check.Near(atlas_width, 9.0f, 0.001f);
            Check.Near(atlas_height, 3.0f, 0.001f);
            Check.Near(
                result.Rects[(int)(0u)].Rect.Left,
                kOrigin.X + glyph_offset.X,
                0.0001f
            );
            Check.Near(
                result.Rects[(int)(0u)].Rect.Top,
                kOrigin.Y + line.LineAscent() + glyph_offset.Y,
                0.0001f
            );
            Check.Near(
                result.Rects[(int)(0u)].Rect.Width(),
                glyph_size.Width,
                0.0001f
            );
            Check.Near(
                result.Rects[(int)(0u)].Rect.Height(),
                glyph_size.Height,
                0.0001f
            );
            Check.Near(
                result.Rects[(int)(0u)].Rect.Width() * pixel_ratio,
                glyph_size.Width * pixel_ratio,
                0.0001f
            );
            Check.Near(
                result.Rects[(int)(0u)].Rect.Height() * pixel_ratio,
                glyph_size.Height * pixel_ratio,
                0.0001f
            );
            Check.Near(
                result.Rects[(int)(0u)].Rect.Width() /
                    result.Rects[(int)(0u)].Rect.Height(),
                3.0f,
                0.001f
            );

            byte[] transparent_left = new byte[4];
            byte[] top_left = new byte[4];
            byte[] top_right = new byte[4];
            byte[] bottom_left = new byte[4];
            Check.That(atlas_rect_rgba_pixel(
                atlas,
                result.Rects[(int)(0u)],
                0u,
                0u,
                transparent_left
            ));
            Check.That(atlas_rect_rgba_pixel(
                atlas,
                result.Rects[(int)(0u)],
                2u,
                0u,
                top_left
            ));
            Check.That(atlas_rect_rgba_pixel(
                atlas,
                result.Rects[(int)(0u)],
                8u,
                0u,
                top_right
            ));
            Check.That(atlas_rect_rgba_pixel(
                atlas,
                result.Rects[(int)(0u)],
                2u,
                2u,
                bottom_left
            ));
            Eq(transparent_left,(new byte[]{0,0, 0, 0 }));
            Eq(top_left,(new byte[]{255,0, 0, 255 }));
            Eq(top_right,(new byte[]{255,255, 255, 255 }));
            Eq(bottom_left,(new byte[]{64,128, 192, 255 }));
        }
    }
}

[GuiTest("gui/text-contract/raster/color-bitmap-selects-nearest-size")]
public static void SourceCase18()
{

    StrikeCase[] cases = {
        new StrikeCase(10.0f,9.0f,3.0f,11.0f),
        new StrikeCase(7.0f,1.0f,1.0f,6.0f),
        new StrikeCase(8.5f,9.0f,3.0f,11.0f),
    };

    foreach (bool advanced in new bool[]{false,true})
    {
        using TextContractFixture fixture = Make(advanced);
        FontFace? face = PreloadFamily(fixture.Services,
            "Skr Test Color Bitmap"
        );
        Check.That(face != null);
        if (face is null)
        {
            continue;
        }

        fixture.Services.SetAtlasGlyphPadding(0u);
        TextFontRasterConfig config =
            fixture.Services.DefaultFontRasterConfig();
        config.Mode = ETextRasterMode.Bitmap;
        config.LcdMode = false;
        config.Hinting = ETextHinting.None;
        config.SubpixelPositioning =
            ETextSubpixelPositioning.Disabled;
        fixture.Services.SetDefaultFontRasterConfig(config);

        foreach (StrikeCase value in cases)
        {
            TextLine line = fixture.Services.CreateLine();
            TextStyle style = MakeStyle("", value.FontSize);
            style.FontFaces.Add(face.Id());
            line.AddString("★", style);
            Check.That(line.Shape());

            TextRenderResult result = paint_glyph(line,
                Offsetf.Zero(),
                1.0f,
                null
            );
            TextRenderRect rect = result.Rects[(int)(0u)];
            TextAtlasView atlas = fixture.Services.Atlas(
                result.Commands[(int)(0u)].AtlasIndex
            );
            float scale = value.FontSize / value.SelectedWidth;
            Check.Near(
                rect.Uv.Width() * (float)(atlas.Width),
                value.NativeWidth,
                0.001f
            );
            Check.Near(
                rect.Uv.Height() * (float)(atlas.Height),
                value.NativeHeight,
                0.001f
            );
            Check.Near(
                rect.Rect.Width(),
                value.NativeWidth * scale,
                0.0001f
            );
            Check.Near(
                rect.Rect.Height(),
                value.NativeHeight * scale,
                0.0001f
            );
        }
    }
}

[GuiTest("gui/text-contract/raster/glyph-removal-refreshes-dependent-output")]
public static void SourceCase19()
{
    foreach (bool clear_all in new bool[]{false,true})
    {
        using TextContractFixture fixture = Make(true);
        FontFace? face = PreloadFamily(fixture.Services,
            "Skr Test Latin"
        );
        Check.That(face != null);
        if (face is null)
        {
            continue;
        }

        TextFontRasterConfig config =
            fixture.Services.DefaultFontRasterConfig();
        config.Mode = ETextRasterMode.Bitmap;
        config.LcdMode = false;
        config.SubpixelPositioning =
            ETextSubpixelPositioning.Quarter;
        fixture.Services.SetDefaultFontRasterConfig(config);

        float kFontSize = 18.0f;
        uint glyph = face.GlyphIndex((uint)'A');
        Offsetf natural_offset =
            face.GlyphOffset(glyph, kFontSize);
        Offsetf custom_offset =
            natural_offset + new Offsetf(7.0f, 5.0f);
        face.SetGlyphOffset(glyph, kFontSize, custom_offset);

        TextLine line = fixture.Services.CreateLine();
        TextStyle style = MakeStyle("", kFontSize);
        style.FontFaces.Add(face.Id());
        line.AddString("A", style);
        Check.That(line.Shape());

        TextRenderResult result = new();
        Check.That(line.Paint(Offsetf.Zero(), result));
        Check.False(result.IsEmpty());

        if (clear_all)
        {
            face.ClearGlyphs(kFontSize);
        }
        else
        {
            face.RemoveGlyph(glyph, kFontSize);
        }
        Check.False(line.IsReady());
        Ne(face.GlyphOffset(glyph, kFontSize),custom_offset);
        Check.That(line.Shape());
        result.Clear();
        Check.That(line.Paint(Offsetf.Zero(), result));
        Check.False(result.IsEmpty());
    }
}

[GuiTest("gui/text-contract/raster/color-face-keeps-independent-size-overrides")]
public static void SourceCase20()
{
    using TextContractFixture fixture = Make(true);
    FontFace? face = PreloadFamily(fixture.Services, "Skr Test Color");
    Check.That(face != null);

    TextFontRasterConfig config =
        fixture.Services.DefaultFontRasterConfig();
    config.Mode = ETextRasterMode.Sdf;
    config.SdfPpem = TextFontRasterConfig.KLowPpem;
    fixture.Services.SetDefaultFontRasterConfig(config);

    uint glyph = face.GlyphIndex((uint)'A');
    Ne(glyph,0u);
    face.SetGlyphAdvance(glyph, 13.0f, new Offsetf(7.25f, 0.0f));
    face.SetGlyphAdvance(glyph, 17.0f, new Offsetf(9.5f, 0.0f));
    Eq(face.GlyphAdvance(glyph, 13.0f),new Offsetf(7.25f, 0.0f));
    Eq(face.GlyphAdvance(glyph, 17.0f),new Offsetf(9.5f, 0.0f));
}

[GuiTest("gui/text-contract/raster/color-face-keeps-monochrome-glyph-modulation")]
public static void SourceCase21()
{
    foreach (bool advanced in new bool[]{false,true})
    {
        using TextContractFixture fixture = Make(advanced);
        FontFace? face = PreloadFamily(fixture.Services,
            "Skr Test Color"
        );
        Check.That(face != null);
        if (face is null)
        {
            continue;
        }
        Ne(face.GlyphIndex((uint)'A'),0u);
        Ne(face.GlyphIndex((uint)'★'),0u);

        TextFontRasterConfig config =
            fixture.Services.DefaultFontRasterConfig();
        config.Mode = ETextRasterMode.Bitmap;
        config.LcdMode = false;
        config.Hinting = ETextHinting.None;
        fixture.Services.SetDefaultFontRasterConfig(config);

        TextLine line = fixture.Services.CreateLine();
        TextStyle style = MakeStyle("");
        style.FontFaces.Add(face.Id());
        line.AddString("A★", style);
        Check.That(line.Shape());

        TextRenderResult result = new();
        Check.That(line.Paint(Offsetf.Zero(), result));
        Eq((ulong)result.Commands.Count,2u);
        Eq((ulong)result.Rects.Count,2u);
        Eq(result.Commands[(int)(0)].PixelMode,ETextPixelMode.Gray);
        Eq(result.Commands[(int)(1)].PixelMode,ETextPixelMode.Colored);
        TextAtlasView gray_atlas = fixture.Services.Atlas(
            result.Commands[(int)(0)].AtlasIndex
        );
        TextAtlasView color_atlas = fixture.Services.Atlas(
            result.Commands[(int)(1)].AtlasIndex
        );
        Eq(gray_atlas.Format,ETextAtlasFormat.L8);
        Eq(color_atlas.Format,ETextAtlasFormat.RGBA8);
        Check.That(atlas_has_coverage(gray_atlas));
        Check.That(atlas_has_coverage(color_atlas));
    }
}

[GuiTest("gui/text-contract/raster/color-face-sdf-lcd-fallback")]
public static void SourceCase22()
{
    using TextContractFixture fixture = Make(true);
    FontFace? face = PreloadFamily(fixture.Services, "Skr Test Color");
    Check.That(face != null);

    TextFontRasterConfig config =
        fixture.Services.DefaultFontRasterConfig();
    config.Mode = ETextRasterMode.Sdf;
    config.LcdMode = true;
    config.SubpixelPositioning = ETextSubpixelPositioning.Quarter;
    fixture.Services.SetDefaultFontRasterConfig(config);

    uint ordinary_glyph = face.GlyphIndex((uint)'A');
    uint color_glyph = face.GlyphIndex((uint)'★');
    Check.That(ordinary_glyph != 0u);
    Check.That(color_glyph != 0u);
    if (ordinary_glyph == 0u || color_glyph == 0u)
    {
        return;
    }
    Offsetf color_advance = face.GlyphAdvance(
        color_glyph,
        16.0f
    );
    face.SetGlyphAdvance(
        ordinary_glyph,
        16.0f,
        new Offsetf(10.25f, 0.0f)
    );

    TextLine mixed = fixture.Services.CreateLine();
    TextStyle style = MakeStyle("", 16.0f);
    style.FontFaces.Add(face.Id());
    mixed.AddString("A★", style);
    Check.That(mixed.Shape());

    TextRenderResult first = new();
    TextRenderResult second = new();
    Check.That(mixed.Paint(new Offsetf(0.13f, 0.0f), first));
    Check.That(mixed.Paint(new Offsetf(0.38f, 0.0f), second));
    Eq((ulong)first.Commands.Count,2u);
    Eq((ulong)first.Rects.Count,2u);
    Eq(first.Commands[(int)(0)].PixelMode,ETextPixelMode.GrayLcd);
    Eq(first.Commands[(int)(1)].PixelMode,ETextPixelMode.Colored);
    Eq(fixture.Services.Atlas(first.Commands[(int)(0)].AtlasIndex).Format,ETextAtlasFormat.RGBA8);
    Eq(fixture.Services.Atlas(first.Commands[(int)(1)].AtlasIndex).Format,ETextAtlasFormat.RGBA8);
    ulong first_lcd_signature = 0u;
    ulong second_lcd_signature = 0u;
    ulong first_color_signature = 0u;
    ulong second_color_signature = 0u;
    Check.That(atlas_rect_signature(
        fixture.Services.Atlas(first.Commands[(int)(0)].AtlasIndex),
        first.Rects[(int)(0)], ref first_lcd_signature));
    Check.That(atlas_rect_signature(
        fixture.Services.Atlas(second.Commands[(int)(0)].AtlasIndex),
        second.Rects[(int)(0)], ref second_lcd_signature));
    Check.That(atlas_rect_signature(
        fixture.Services.Atlas(first.Commands[(int)(1)].AtlasIndex),
        first.Rects[(int)(1)], ref first_color_signature));
    Check.That(atlas_rect_signature(
        fixture.Services.Atlas(second.Commands[(int)(1)].AtlasIndex),
        second.Rects[(int)(1)], ref second_color_signature));
    Ne(first_lcd_signature,second_lcd_signature);
    Eq(first_color_signature,second_color_signature);

    config.SubpixelPositioning = ETextSubpixelPositioning.Disabled;
    fixture.Services.SetDefaultFontRasterConfig(config);
    TextLine color = fixture.Services.CreateLine();
    color.AddString("★", style);
    Check.That(color.Shape());
    TextRenderResult aligned = paint_glyph(color,
        new Offsetf(0.4f, 0.0f)
    );
    TextRenderResult continuous = paint_glyph(color,
        new Offsetf(0.4f, 0.0f),
        1.0f,
        null
    );
    Eq(aligned.Commands[(int)(0)].PixelMode,ETextPixelMode.Colored);
    Check.Near(
        continuous.Rects[(int)(0)].Rect.Left - aligned.Rects[(int)(0)].Rect.Left,
        0.4f,
        0.0001f
    );
    TextAtlasView lcd_color_atlas = fixture.Services.Atlas(
        aligned.Commands[(int)(0)].AtlasIndex
    );
    Check.That(atlas_has_coverage(lcd_color_atlas));
    Check.Near(
        aligned.Rects[(int)(0)].Rect.Width(),
        aligned.Rects[(int)(0)].Uv.Width() *
            (float)(lcd_color_atlas.Width),
        0.001f
    );

    config.LcdMode = false;
    fixture.Services.SetDefaultFontRasterConfig(config);
    TextLine gray_color = fixture.Services.CreateLine();
    gray_color.AddString("★", style);
    Check.That(gray_color.Shape());
    TextRenderResult gray = paint_glyph(gray_color,
        new Offsetf(0.4f, 0.0f)
    );
    Eq(gray.Commands[(int)(0)].PixelMode,ETextPixelMode.Colored);
    TextAtlasView gray_color_atlas = fixture.Services.Atlas(
        gray.Commands[(int)(0)].AtlasIndex
    );
    Check.That(atlas_has_coverage(gray_color_atlas));
    Check.That((color_advance.X) > (0.0f));
    Check.That((aligned.Rects[(int)(0)].Rect.Width()) < (color_advance.X * 1.75f));
    Check.That((gray.Rects[(int)(0)].Rect.Width()) < (color_advance.X * 1.75f));
    Check.Near(
        gray.Rects[(int)(0)].Rect.Width(),
        gray.Rects[(int)(0)].Uv.Width() *
            (float)(gray_color_atlas.Width),
        0.001f
    );
    Eq(face.GlyphAdvance(color_glyph, 16.0f),color_advance);
}

[GuiTest("gui/text-contract/raster/sdf-geometry-scales-with-font-size")]
public static void SourceCase23()
{
    using TextContractFixture fixture = Make(true);
    FontFace? face = PreloadFamily(fixture.Services, "Skr Test Latin");
    Check.That(face != null);

    TextFontRasterConfig config =
        fixture.Services.DefaultFontRasterConfig();
    config.Mode = ETextRasterMode.Sdf;
    config.SdfPpem = TextFontRasterConfig.KMediumPpem;
    fixture.Services.SetDefaultFontRasterConfig(config);

    var paint_at_size = (float font_size, float pixel_ratio) => {
        TextLine line = fixture.Services.CreateLine();
        TextStyle style = MakeStyle("");
        style.FontFaces.Add(face.Id());
        style.FontSize = font_size;
        line.AddString("A", style);
        Check.That(line.Shape());

        TextRenderResult result = new();
        Check.That(line.Paint(
            Offsetf.Zero(),
            result,
            new TextPaintDesc{ PixelRatio = pixel_ratio }
        ));
        Eq((ulong)result.Commands.Count,1u);
        Eq((ulong)result.Rects.Count,1u);
        return result;
    };

    TextRenderResult small = paint_at_size(16.0f, 1.0f);
    TextRenderResult large = paint_at_size(48.0f, 2.0f);

    Check.That((large.Rects[(int)(0)].Rect.Width()) > (small.Rects[(int)(0)].Rect.Width()));
    Check.That((large.Rects[(int)(0)].Rect.Height()) > (small.Rects[(int)(0)].Rect.Height()));
}

[GuiTest("gui/text-contract/raster/sdf-lcd-mode-reaches-render-command")]
public static void SourceCase24()
{
    using TextContractFixture fixture = Make(true);
    FontFace? face = PreloadFamily(fixture.Services, "Skr Test Latin");
    Check.That(face != null);

    TextFontRasterConfig config =
        fixture.Services.DefaultFontRasterConfig();
    config.Mode = ETextRasterMode.Sdf;
    config.LcdMode = true;
    fixture.Services.SetDefaultFontRasterConfig(config);

    TextLine line = fixture.Services.CreateLine();
    TextStyle style = MakeStyle("");
    style.FontFaces.Add(face.Id());
    line.AddString("A", style);
    Check.That(line.Shape());

    TextRenderResult result = new();
    Check.That(line.Paint(Offsetf.Zero(), result));
    Eq((ulong)result.Commands.Count,1u);
    Eq(result.Commands[(int)(0)].PixelMode,ETextPixelMode.SdfLcd);

    config.LcdMode = false;
    fixture.Services.SetDefaultFontRasterConfig(config);
    TextRenderResult grayscale = new();
    Check.That(line.Paint(Offsetf.Zero(), grayscale));
    Eq((ulong)grayscale.Commands.Count,1u);
    Eq(grayscale.Commands[(int)(0)].PixelMode,ETextPixelMode.Sdf);
}

[GuiTest("gui/text-contract/raster/painting-does-not-change-later-layout")]
public static void SourceCase25()
{
    var run_case = (bool sdf, float pixel_ratio) => {
        TextContractFixture control = Make(true);
        TextContractFixture painted = Make(true);
        FontFace? control_face = PreloadFamily(control.Services,
            "Skr Test Latin"
        );
        FontFace? painted_face = PreloadFamily(painted.Services,
            "Skr Test Latin"
        );
        Check.That(control_face != null);
        Check.That(painted_face != null);

        TextFontRasterConfig config =
            painted.Services.DefaultFontRasterConfig();
        config.Mode = sdf ? ETextRasterMode.Sdf : ETextRasterMode.Bitmap;
        config.SdfPpem = TextFontRasterConfig.KMediumPpem;
        control.Services.SetDefaultFontRasterConfig(config);
        painted.Services.SetDefaultFontRasterConfig(config);

        var make_style = (FontFace face) => {
            TextStyle style = MakeStyle("", 11.0f);
            style.FontFaces.Add(face.Id());
            return style;
        };
        var shape = (
                               TextServices services,
                               Utf8StringView text,
                               TextStyle style
                           ) => {
            TextLine line = services.CreateLine();
            line.AddString(text, style);
            Check.That(line.Shape());
            return line;
        };

        TextStyle control_style = make_style(control_face);
        TextStyle painted_style = make_style(painted_face);
        TextLine control_warmup = shape(control.Services,
            "Left",
            control_style
        );
        TextLine painted_warmup = shape(painted.Services,
            "Left",
            painted_style
        );
        TextRenderResult result = new();
        Check.That(painted_warmup.Paint(
            Offsetf.Zero(),
            result,
            new TextPaintDesc{ PixelRatio = pixel_ratio }
        ));

        TextLine expected = shape(control.Services,
            "Center Right 2468",
            control_style
        );
        TextLine actual = shape(painted.Services,
            "Center Right 2468",
            painted_style
        );
        Near(actual.Size().Width,expected.Size().Width);
        Eq((ulong)actual.Glyphs().Length,(ulong)expected.Glyphs().Length);
        for (ulong i = 0u; i < (ulong)expected.Glyphs().Length; ++i)
        {
            Eq(actual.Glyphs()[(int)(i)].GlyphIndex,expected.Glyphs()[(int)(i)].GlyphIndex);
            Near(actual.Glyphs()[(int)(i)].Advance,expected.Glyphs()[(int)(i)].Advance);
        }
    };


    {
        run_case(true, 1.0f);
    }

    {
        run_case(false, 2.0f);
    }
}

[GuiTest("gui/text-contract/raster/fractional-size-scales-bitmap-quad")]
public static void SourceCase26()
{
    using TextContractFixture fixture = Make(true);
    FontFace? face = PreloadFamily(fixture.Services, "Skr Test Latin");
    Check.That(face != null);
    fixture.Services.SetAtlasGlyphPadding(0u);
    TextFontRasterConfig config =
        fixture.Services.DefaultFontRasterConfig();
    config.Mode = ETextRasterMode.Bitmap;
    fixture.Services.SetDefaultFontRasterConfig(config);

    var paint_at_size = (float font_size) => {
        TextLine line = fixture.Services.CreateLine();
        TextStyle style = MakeStyle("", font_size);
        style.FontFaces.Add(face.Id());
        line.AddString("A", style);
        Check.That(line.Shape());
        TextRenderResult result = new();
        Check.That(line.Paint(Offsetf.Zero(), result));
        Eq((ulong)result.Rects.Count,1u);
        return result.Rects[(int)(0)].Rect;
    };

    Rectf smaller = paint_at_size(16.1f);
    Rectf larger = paint_at_size(16.2f);
    Check.That((smaller.Width()) > (0.0f));
    Check.That((smaller.Height()) > (0.0f));
    Check.That((larger.Width()) > (smaller.Width()));
    Check.That((larger.Height()) > (smaller.Height()));
}

[GuiTest("gui/text-contract/atlas/configuration-and-lifecycle")]
public static void SourceCase27()
{
    ETextAtlasAllocationAlgorithm[] algorithms = {
        ETextAtlasAllocationAlgorithm.ShelfLinear,
        ETextAtlasAllocationAlgorithm.ShelfBestFit,
        ETextAtlasAllocationAlgorithm.GuillotineSplit,
    };

    foreach (ETextAtlasAllocationAlgorithm algorithm in algorithms)
    {
        using TextContractFixture fixture = Make(true);
        FontFace? face = PreloadFamily(fixture.Services,
            "Skr Test Latin"
        );
        Check.That(face != null);
        TextFontRasterConfig config =
            fixture.Services.DefaultFontRasterConfig();
        config.Mode = ETextRasterMode.Bitmap;
        fixture.Services.SetDefaultFontRasterConfig(config);
        TextLine line = fixture.Services.CreateLine();
        TextStyle style = MakeStyle("");
        style.FontFaces.Add(face.Id());
        line.AddString(
            "A",
            style
        );
        Check.That(line.Shape());

        if (fixture.Services.AtlasAllocationAlgorithm() != algorithm)
        {
            Check.That(fixture.Services.SetAtlasAllocationAlgorithm(algorithm));
        }
        fixture.Services.SetAtlasPageSize(
            ETextAtlasFormat.L8,
            new Sizei(256, 128)
        );
        fixture.Services.SetAtlasPageSize(
            ETextAtlasFormat.RGBA8,
            new Sizei(128, 128)
        );
        fixture.Services.SetAtlasGlyphPadding(3u);
        Eq(fixture.Services.AtlasAllocationAlgorithm(),algorithm);
        Eq(fixture.Services.AtlasPageSize(ETextAtlasFormat.L8),new Sizei(256, 128));
        Eq(fixture.Services.AtlasPageSize(ETextAtlasFormat.RGBA8),new Sizei(128, 128));
        Eq(fixture.Services.AtlasGlyphPadding(),3u);

        TextRenderResult result = new();
        Check.That(line.Paint(Offsetf.Zero(), result));
        Check.False(result.IsEmpty());
        Check.That((fixture.Services.AtlasCount()) > (0u));
        check_render_ranges(result);
        for (uint index = 0u;
             index < fixture.Services.AtlasCount();
             ++index)
        {
            TextAtlasView atlas = fixture.Services.Atlas(index);
            Eq(atlas.AtlasIndex,index);
            Check.That((atlas.Width) > (0u));
            Check.That((atlas.Height) > (0u));
            Check.That((atlas.Stride) >= (atlas.Width));
            Check.False(atlas.Pixels.IsEmpty);
            Check.That(atlas_has_coverage(atlas));
        }

        ETextAtlasAllocationAlgorithm other =
            algorithm == ETextAtlasAllocationAlgorithm.ShelfLinear ?
            ETextAtlasAllocationAlgorithm.ShelfBestFit :
            ETextAtlasAllocationAlgorithm.ShelfLinear;
        Check.False(fixture.Services.SetAtlasAllocationAlgorithm(other));
        Eq(fixture.Services.AtlasAllocationAlgorithm(),algorithm);

        fixture.Services.ClearAtlases();
        Eq(fixture.Services.AtlasCount(),0u);
        TextRenderResult rebuilt = new();
        Check.That(line.Paint(Offsetf.Zero(), rebuilt));
        Check.That((fixture.Services.AtlasCount()) > (0u));
    }
}

[GuiTest("gui/text-contract/render-result/command-coalescing")]
public static void SourceCase28()
{
    TextRenderResult result = new();
    result.BuildAppendRect(
        1u,
        ETextPixelMode.Gray,
        Rectf.Largest(),
        new TextLayoutSpanId(0u),
        Rectf.Zero(),
        Rectf.Zero()
    );
    Check.That(result.IsEmpty());

    Rectf first = Rectf.LTWH(1.0f, 2.0f, 3.0f, 4.0f);
    Rectf second = Rectf.LTWH(8.0f, 9.0f, 5.0f, 6.0f);
    result.BuildAppendRect(
        1u,
        ETextPixelMode.Gray,
        Rectf.Largest(),
        new TextLayoutSpanId(0u),
        first,
        Rectf.LTWH(0.0f, 0.0f, 0.5f, 0.5f)
    );
    result.BuildAppendRect(
        1u,
        ETextPixelMode.Gray,
        Rectf.Largest(),
        new TextLayoutSpanId(1u),
        second,
        Rectf.LTWH(0.5f, 0.5f, 0.5f, 0.5f)
    );
    Eq((ulong)result.Commands.Count,1u);
    Eq(result.Commands[0].RectCount,2u);
    Eq((ulong)result.Rects.Count,2u);
    Eq(result.Bounds,first.Unite(second));

    result.BuildAppendRect(
        1u,
        ETextPixelMode.Sdf,
        Rectf.Largest(),
        new TextLayoutSpanId(2u),
        Rectf.LTWH(20.0f, 20.0f, 2.0f, 2.0f),
        Rectf.Zero()
    );
    result.BuildAppendBox(
        new TextLayoutSpanId(3u),
        Rectf.LTWH(30.0f, 30.0f, 4.0f, 4.0f)
    );
    Eq((ulong)result.Commands.Count,3u);
    Check.That(result.Commands[^1].IsHexBoxFallback());
    check_render_ranges(result);

    result.Clear();
    Check.That(result.IsEmpty());
    Check.That((result.Commands.Count==0));
    Check.That((result.Rects.Count == 0));
}
}
