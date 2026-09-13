using static SkrGui.Tests.TextContractFixture;
namespace SkrGui.Tests;
// Source: all 14 cases of tests/text/text_font_face_contract_tests.cpp.
internal static class FontFaceContractTests
{
    private static FontFace Face(TextContractFixture f,string family="Skr Test Latin")
    {var result=PreloadFamily(f.Services,family);Check.NotNull(result);return result!;}
    private static TextLine Line(TextContractFixture f,FontFace face,string text,float size=16)
    {var line=f.Services.CreateLine();var style=MakeStyle("",size);style.FontFaces.Add(face.Id());line.AddString(text,style);Check.That(line.Shape());return line;}
    private static bool SamePathGeometry(VGPath a,VGPath b)
    {
        var x=a.Commands();var y=b.Commands();if(x.Count!=y.Count)return false;
        for(int i=0;i<x.Count;i++)
        {
            if(x[i].Type!=y[i].Type)return false;
            switch(x[i].Type)
            {
                case EVGPathCommandType.MoveTo:if(x[i].Move.Target!=y[i].Move.Target)return false;break;
                case EVGPathCommandType.LineTo:if(x[i].Line.Target!=y[i].Line.Target)return false;break;
                case EVGPathCommandType.QuadTo:if(x[i].Quad.Control!=y[i].Quad.Control||x[i].Quad.Target!=y[i].Quad.Target)return false;break;
                case EVGPathCommandType.CubicTo:if(x[i].Cubic.Control0!=y[i].Cubic.Control0||x[i].Cubic.Control1!=y[i].Cubic.Control1||x[i].Cubic.Target!=y[i].Cubic.Target)return false;break;
                case EVGPathCommandType.Close:break;default:return false;
            }
        }
        return true;
    }
    [GuiTest("gui/text-contract/font-face/metadata-glyphs-and-contour")]
    private static void MetadataGlyphsAndContour()
    {
        using var f=Make(true);var face=Face(f);
        Check.That(face.IsValid());Check.That(face.Id().IsValid());Check.Same(f.Provider,face.Provider());Check.That(face.Source().IsValid());
        Check.Equal(0u,face.FaceIndex());Check.Equal(1u,face.FaceCount());Check.Equal("Skr Test Latin",face.FamilyName().ToString());Check.False(face.StyleName().IsEmpty());
        Check.False(face.IsFixedWidth());Check.That(face.OpenTypeNames().Length!=0);uint a=face.GlyphIndex('A');Check.That(a!=0);Check.That(face.HasCodepoint('A'));
        Check.False(face.HasCodepoint(0x4e2d));Check.Equal((uint)'A',face.CodepointFromGlyphIndex(a));
        List<uint> points=[],glyphs=[];face.SupportedCodepoints(points);face.SupportedGlyphs(glyphs);Check.That(points.Contains('A'));Check.That(glyphs.Contains(a));
        var advance=face.GlyphAdvance(a,16);Check.That(advance.IsFinite());Check.That(advance.X>0);Check.That(face.GlyphOffset(a,16).IsFinite());Check.That(face.GlyphSize(a,16).IsFinite());
        Check.That(face.Kerning(a,face.GlyphIndex('V'),16).IsFinite());
        var small=face.Metrics(16);var large=face.Metrics(32);Check.That(small.Ascent>0);Check.That(small.Descent>=0);Check.That(large.Ascent>small.Ascent);Check.That(large.Descent>=small.Descent);
        var fractionalSmall=face.GlyphAdvance(a,16.1f);var fractionalLarge=face.GlyphAdvance(a,16.2f);Check.That(fractionalLarge.X>fractionalSmall.X);Check.Near(fractionalSmall.X,face.GlyphAdvance(a,16.1f).X);
        var path=new VGPath();Check.That(face.GlyphContour(a,32,path));Check.False(path.IsEmpty());
    }
    [GuiTest("gui/text-contract/font-face/metadata-and-support-overrides")]
    private static void MetadataAndSupportOverrides()
    {
        using var f=Make(true);var face=Face(f);
        face.SetFamilyName("Contract Family");face.SetStyleName("Contract Style");face.SetWeight(EFontWeight.Bold);face.SetStyle(EFontStyle.Italic);face.SetStretch(EFontStretch.Expanded);
        Check.Equal("Contract Family",face.FamilyName().ToString());Check.Equal("Contract Style",face.StyleName().ToString());Check.Equal(EFontWeight.Bold,face.Weight());Check.Equal(EFontStyle.Italic,face.Style());Check.Equal(EFontStretch.Expanded,face.Stretch());
        const string language="x-skr-contract",script="Zskr";List<Utf8StringView> values=[];
        face.SetLanguageSupportOverride(language,true);face.LanguageSupportOverrides(values);Check.That(values.Contains(language));Check.That(face.LanguageSupportOverride(language));Check.That(face.IsLanguageSupported(language));
        face.RemoveLanguageSupportOverride(language);values.Clear();face.LanguageSupportOverrides(values);Check.False(values.Contains(language));
        face.SetScriptSupportOverride(script,true);values.Clear();face.ScriptSupportOverrides(values);Check.That(values.Contains(script));Check.That(face.ScriptSupportOverride(script));Check.That(face.IsScriptSupported(script));
        face.RemoveScriptSupportOverride(script);values.Clear();face.ScriptSupportOverrides(values);Check.False(values.Contains(script));
    }
    [GuiTest("gui/text-contract/font-face/variation-and-opentype-overrides")]
    private static void VariationAndOpenTypeOverrides()
    {
        using var f=Make(true);var face=Face(f);uint weight=f.Services.OpenTypeNameToTag("wght"),optical=f.Services.OpenTypeNameToTag("opsz"),kern=f.Services.OpenTypeNameToTag("kern");
        var axes=face.VariationAxes().ToArray();Check.That(axes.Any(a=>a.Tag==weight));Check.That(axes.Any(a=>a.Tag==optical));
        var wa=axes.Single(a=>a.Tag==weight);var oa=axes.Single(a=>a.Tag==optical);Check.That(wa.MinValue<=wa.DefaultValue);Check.That(wa.MaxValue>=wa.DefaultValue);
        FontVariationCoordinate[] coords=[new(weight,wa.MinValue-500),new(optical,oa.MaxValue+500)];
        Check.That(face.SetVariationCoordinates(coords));
        Check.Equal(coords[0].Value,face.VariationCoordinates().ToArray().Single(c=>c.Tag==weight).Value);Check.Equal(coords[1].Value,face.VariationCoordinates().ToArray().Single(c=>c.Tag==optical).Value);
        FontVariationCoordinate[] reordered=[new(optical,oa.DefaultValue+1),new(weight,wa.DefaultValue+1)];
        Check.That(face.SetVariationCoordinates(reordered));Check.Equal(reordered[1].Value,face.VariationCoordinates().ToArray().Single(c=>c.Tag==weight).Value);Check.Equal(reordered[0].Value,face.VariationCoordinates().ToArray().Single(c=>c.Tag==optical).Value);
        Check.That(face.SetVariationCoordinates([]));Check.Equal(0,face.VariationCoordinates().Length);Check.That(face.OpenTypeFeatures().ToArray().Any(c=>c.Tag==kern));
        face.SetOpenTypeFeatureOverrides([new(kern,0)]);Check.Equal(0u,face.OpenTypeFeatureOverrides().ToArray().Single(c=>c.Tag==kern).Value);
        face.SetOpenTypeFeatureOverrides([]);Check.Equal(0,face.OpenTypeFeatureOverrides().Length);
    }
    [GuiTest("gui/text-contract/font-face/public-metric-and-glyph-overrides")]
    private static void PublicMetricAndGlyphOverrides()
    {
        using var f=Make(true);var face=Face(f);var config=f.Services.DefaultFontRasterConfig();config.Mode=ETextRasterMode.Bitmap;f.Services.SetDefaultFontRasterConfig(config);
        uint a=face.GlyphIndex('A');Check.That(a!=0);var line=Line(f,face,"A");
        face.SetSpacing(ETextSpacing.Glyph,3.5f);face.SetSpacing(ETextSpacing.Space,2.5f);face.SetSpacing(ETextSpacing.Top,1.5f);face.SetSpacing(ETextSpacing.Bottom,.5f);
        Check.Equal(3.5f,face.Spacing(ETextSpacing.Glyph));Check.Equal(2.5f,face.Spacing(ETextSpacing.Space));Check.Equal(1.5f,face.Spacing(ETextSpacing.Top));Check.Equal(.5f,face.Spacing(ETextSpacing.Bottom));Check.False(line.IsReady());Check.That(line.Shape());
        var metrics=new FontFaceMetrics(){Ascent=13,Descent=4,LineGap=2,UnderlinePosition=1,UnderlineThickness=.75f,Scale=1};
        face.SetMetrics(18,metrics);var actual=face.Metrics(18);Check.Equal(metrics.Ascent,actual.Ascent);Check.Equal(metrics.Descent,actual.Descent);Check.Equal(metrics.LineGap,actual.LineGap);
        face.SetGlyphAdvance(a,18,new(77,0));face.SetGlyphOffset(a,18,new(2,3));face.SetGlyphSize(a,18,new(12,14));
        Check.Equal(new Offsetf(77,0),face.GlyphAdvance(a,18));Check.Equal(new Offsetf(2,3),face.GlyphOffset(a,18));Check.Equal(new Sizef(12,14),face.GlyphSize(a,18));
        face.RemoveGlyph(a,18);Check.That(face.GlyphAdvance(a,18)!=new Offsetf(77,0));face.SetGlyphAdvance(a,20,new(66,0));face.ClearGlyphs(20);Check.That(face.GlyphAdvance(a,20)!=new Offsetf(66,0));
    }
    [GuiTest("gui/text-contract/font-face/sdf-glyph-overrides-scale-with-size")]
    private static void SdfOverridesScaleWithSize()
    {
        using var f=Make(true);var face=Face(f);var config=f.Services.DefaultFontRasterConfig();config.Mode=ETextRasterMode.Sdf;config.SdfPpem=32;f.Services.SetDefaultFontRasterConfig(config);
        uint a=face.GlyphIndex('A'),v=face.GlyphIndex('V');Check.That(a!=0);Check.That(v!=0);
        var metrics=new FontFaceMetrics(){Ascent=20,Descent=6,LineGap=4,UnderlinePosition=2,UnderlineThickness=1,Scale=1};
        face.SetMetrics(16,metrics);face.SetGlyphAdvance(a,16,new(20,0));face.SetGlyphOffset(a,16,new(4,6));face.SetGlyphSize(a,16,new(8,10));face.SetKerning(a,v,16,new(2,0));
        foreach(float size in new[]{16f,32f})
        {
            float scale=size/32;var actual=face.Metrics(size);Check.Equal(20*scale,actual.Ascent);Check.Equal(6*scale,actual.Descent);Check.Equal(4*scale,actual.LineGap);Check.Near(scale,actual.Scale);
            Check.Equal(new Offsetf(20*scale,0),face.GlyphAdvance(a,size));Check.Equal(new Offsetf(4*scale,6*scale),face.GlyphOffset(a,size));Check.Equal(new Sizef(8*scale,10*scale),face.GlyphSize(a,size));Check.Equal(new Offsetf(2*scale,0),face.Kerning(a,v,size));
        }
        metrics.Scale=7;face.SetMetrics(16,metrics);Check.Near(.5f,face.Metrics(16).Scale);
    }
    [GuiTest("gui/text-contract/font-face/glyph-raster-rect-overrides-affect-paint")]
    private static void RasterOverridesAffectPaint()
    {
        using var f=Make(true);var face=Face(f);f.Services.SetAtlasGlyphPadding(0);var config=f.Services.DefaultFontRasterConfig();config.Mode=ETextRasterMode.Bitmap;f.Services.SetDefaultFontRasterConfig(config);
        const float size=18;uint glyph=face.GlyphIndex('A');Check.That(glyph!=0);var advance=face.GlyphAdvance(glyph,size);var offset=face.GlyphOffset(glyph,size);var bitmapSize=face.GlyphSize(glyph,size);
        var line=Line(f,face,"A",size);var before=new TextRenderResult();Check.That(line.Paint(Offsetf.Zero(),before));Check.Equal(1,before.Rects.Count);
        offset+=new Offsetf(3,4);bitmapSize+=new Sizef(5,6);face.SetGlyphOffset(glyph,size,offset);face.SetGlyphSize(glyph,size,bitmapSize);Check.That(line.IsReady());
        var after=new TextRenderResult();Check.That(line.Paint(Offsetf.Zero(),after));Check.Equal(1,after.Rects.Count);
        Check.Equal(offset,face.GlyphOffset(glyph,size));Check.Equal(bitmapSize,face.GlyphSize(glyph,size));Check.Equal(advance,face.GlyphAdvance(glyph,size));
        Check.Near(3,after.Rects[0].Rect.Left-before.Rects[0].Rect.Left);Check.Near(4,after.Rects[0].Rect.Top-before.Rects[0].Rect.Top);
        Check.Near(bitmapSize.Width,after.Rects[0].Rect.Width());Check.Near(bitmapSize.Height,after.Rects[0].Rect.Height());
    }
    [GuiTest("gui/text-contract/font-face/glyph-raster-override-before-first-load")]
    private static void RasterOverrideBeforeFirstLoad()
    {
        using var f=Make(true);var face=Face(f);f.Services.SetAtlasGlyphPadding(0);var config=f.Services.DefaultFontRasterConfig();config.Mode=ETextRasterMode.Bitmap;f.Services.SetDefaultFontRasterConfig(config);
        const float size=19;uint glyph=face.GlyphIndex('A');Check.That(glyph!=0);Offsetf offset=new(4,-11);Sizef bitmapSize=new(13,17);
        face.SetGlyphOffset(glyph,size,offset);face.SetGlyphSize(glyph,size,bitmapSize);Check.Equal(0u,f.Services.AtlasCount());
        var line=Line(f,face,"A",size);Check.That(face.GlyphAdvance(glyph,size).X>0);var rendered=new TextRenderResult();Check.That(line.Paint(Offsetf.Zero(),rendered));Check.Equal(1,rendered.Rects.Count);
        Check.Near(bitmapSize.Width,rendered.Rects[0].Rect.Width());Check.Near(bitmapSize.Height,rendered.Rects[0].Rect.Height());Check.Equal(offset,face.GlyphOffset(glyph,size));Check.Equal(bitmapSize,face.GlyphSize(glyph,size));
    }
    [GuiTest("gui/text-contract/font-face/invalid-glyph-overrides-round-trip")]
    private static void InvalidGlyphOverridesRoundTrip()
    {
        using var f=Make(true);var face=Face(f);var config=f.Services.DefaultFontRasterConfig();config.Mode=ETextRasterMode.Bitmap;f.Services.SetDefaultFontRasterConfig(config);
        const uint glyph=0x00ffffff;const float size=19;Offsetf advance=new(17,0),offset=new(2,-3);Sizef bitmapSize=new(4,5);
        face.SetGlyphAdvance(glyph,size,advance);face.SetGlyphOffset(glyph,size,offset);face.SetGlyphSize(glyph,size,bitmapSize);Check.Equal(0u,f.Services.AtlasCount());
        Check.Equal(advance,face.GlyphAdvance(glyph,size));Check.Equal(offset,face.GlyphOffset(glyph,size));Check.Equal(bitmapSize,face.GlyphSize(glyph,size));Check.Equal(0u,f.Services.AtlasCount());
        face.RemoveGlyph(glyph,size);Check.Equal(Offsetf.Zero(),face.GlyphAdvance(glyph,size));Check.Equal(Offsetf.Zero(),face.GlyphOffset(glyph,size));Check.Equal(Sizef.Zero(),face.GlyphSize(glyph,size));Check.Equal(0u,f.Services.AtlasCount());
    }
    [GuiTest("gui/text-contract/font-face/baseline-offset-refreshes-dependent-line")]
    private static void BaselineRefreshesLine()
    {
        foreach(bool advanced in new[]{false,true})
        {
            using var f=Make(advanced);var face=Face(f);var line=Line(f,face,"A",20);Check.Equal(1,line.Glyphs().Length);float before=line.Glyphs()[0].Offset.Y;
            face.SetBaselineOffset(.25f);Check.False(line.IsReady());Check.That(line.Shape());Check.Equal(1,line.Glyphs().Length);Check.That(line.Glyphs()[0].Offset.Y>before);
        }
    }
    [GuiTest("gui/text-contract/font-face/baseline-offset-is-font-height-fraction")]
    private static void BaselineIsFontHeightFraction()
    {
        foreach(bool advanced in new[]{false,true})
        {
            using var f=Make(advanced);var face=Face(f);var config=f.Services.DefaultFontRasterConfig();config.Mode=ETextRasterMode.Bitmap;f.Services.SetDefaultFontRasterConfig(config);
            const float size=20;uint glyph=face.GlyphIndex('A');Check.That(glyph!=0);var offset=face.GlyphOffset(glyph,size);var line=Line(f,face,"AAAAAAAA",size);
            line.SetMaxWidth(line.LineWidth()*.55f);line.SetTextOverrunBehavior(ETextOverrunBehavior.TrimEllipsisForce);Check.That(line.Shape());
            Check.That(line.Glyphs().Length!=0);Check.That(line.EllipsisGlyphs().Length!=0);float glyphY=line.Glyphs()[0].Offset.Y,ellipsisY=line.EllipsisGlyphs()[0].Offset.Y;
            var before=new TextRenderResult();Check.That(line.Paint(Offsetf.Zero(),before,new(){DeviceOffset=null}));Check.That(before.Rects.Count!=0);float paintY=before.Rects[0].Rect.Top;
            var metrics=face.Metrics(size);float shift=.25f*(metrics.Ascent+metrics.Descent);face.SetBaselineOffset(.25f);Check.False(line.IsReady());Check.Equal(0u,f.Services.AtlasCount());Check.That(line.Shape());
            Check.That(line.Glyphs().Length!=0);Check.That(line.EllipsisGlyphs().Length!=0);Check.Near(shift,line.Glyphs()[0].Offset.Y-glyphY);Check.Near(0,line.EllipsisGlyphs()[0].Offset.Y-ellipsisY);Check.Equal(offset,face.GlyphOffset(glyph,size));
            var after=new TextRenderResult();Check.That(line.Paint(Offsetf.Zero(),after,new(){DeviceOffset=null}));Check.That(after.Rects.Count!=0);Check.Near(shift,after.Rects[0].Rect.Top-paintY);
        }
    }
    [GuiTest("gui/text-contract/font-face/transform-affects-raster-and-contour")]
    private static void TransformAffectsRasterAndContour()
    {
        using var f=Make(true);var face=Face(f);f.Services.SetAtlasGlyphPadding(0);var config=f.Services.DefaultFontRasterConfig();config.Mode=ETextRasterMode.Bitmap;f.Services.SetDefaultFontRasterConfig(config);
        const float size=32;uint glyph=face.GlyphIndex('A');Check.That(glyph!=0);var advance=face.GlyphAdvance(glyph,size);var contour=new VGPath();Check.That(face.GlyphContour(glyph,size,contour));Check.That(contour.Commands().Count!=0);
        var line=Line(f,face,"A",size);var original=new TextRenderResult();Check.That(line.Paint(Offsetf.Zero(),original));Check.Equal(1,original.Rects.Count);
        var transform=Float3x3.Identity() with {M00=1.5f};face.SetTransform(transform);Check.False(line.IsReady());Check.That(line.Shape());Check.Equal(advance,face.GlyphAdvance(glyph,size));
        var transformedContour=new VGPath();Check.That(face.GlyphContour(glyph,size,transformedContour));Check.Equal(contour.Commands().Count,transformedContour.Commands().Count);Check.False(SamePathGeometry(contour,transformedContour));
        var transformed=new TextRenderResult();Check.That(line.Paint(Offsetf.Zero(),transformed));Check.Equal(1,transformed.Rects.Count);
        Check.That(transformed.Rects[0].Rect.Left!=original.Rects[0].Rect.Left||transformed.Rects[0].Rect.Width()!=original.Rects[0].Rect.Width());
        face.SetTransform(Float3x3.Identity());Check.That(line.Shape());var restored=new VGPath();Check.That(face.GlyphContour(glyph,size,restored));Check.Equal(contour.Commands().Count,restored.Commands().Count);Check.That(SamePathGeometry(contour,restored));
    }
    [GuiTest("gui/text-contract/font-face/collection-face-index")]
    private static void CollectionFaceIndex()
    {
        using var f=Make(true);var latin=Face(f,"Skr Test Collection Latin");var hebrew=Face(f,"Skr Test Collection Hebrew");
        Check.Equal(2u,latin.FaceCount());Check.Equal(2u,hebrew.FaceCount());Check.Equal(0u,latin.FaceIndex());Check.Equal(1u,hebrew.FaceIndex());
        Check.That(latin.HasCodepoint('A'));Check.False(latin.HasCodepoint(0x5d0));Check.That(hebrew.HasCodepoint(0x5d0));
        var id=latin.Id();Check.That(latin.SetFaceIndex(1));Check.Equal(id,latin.Id());Check.Equal(1u,latin.FaceIndex());Check.That(latin.HasCodepoint(0x5d0));
        Check.False(latin.SetFaceIndex(2));Check.Equal(1u,latin.FaceIndex());
    }
    [GuiTest("gui/text-contract/font-face/mutation-refreshes-dependent-text")]
    private static void MutationRefreshesDependentText()
    {
        foreach(bool advanced in new[]{false,true})
        {
            using var f=Make(advanced);var face=Face(f,"Skr Test Collection Latin");var canonical=Face(f,"Skr Test Collection Hebrew");Check.False(ReferenceEquals(face,canonical));
            var style=new TextStyle(){FontSize=18};style.FontFaces.Add(face.Id());var line=f.Services.CreateLine();line.AddString("\u05d0",style);Check.That(line.Shape());
            var paragraph=f.Services.CreateParagraph();paragraph.AddString("\u05d0",style);Check.That(paragraph.Shape());Check.That(line.Glyphs().Length!=0);Check.Equal(ETextGlyphKind.HexBox,line.Glyphs()[0].Kind);
            var unrelated=Face(f);var otherStyle=new TextStyle(){FontSize=18};otherStyle.FontFaces.Add(unrelated.Id());
            var otherLine=f.Services.CreateLine();otherLine.AddString("AB",otherStyle);Check.That(otherLine.Shape());var otherParagraph=f.Services.CreateParagraph();otherParagraph.AddString("AB",otherStyle);Check.That(otherParagraph.Shape());
            Check.That(face.SetFaceIndex(1));Check.False(line.IsReady());Check.False(paragraph.IsReady());Check.That(otherLine.IsReady());Check.That(otherParagraph.IsReady());
            Check.That(line.Shape());Check.That(paragraph.Shape());Check.That(line.Glyphs().Length!=0);Check.Equal(ETextGlyphKind.Glyph,line.Glyphs()[0].Kind);Check.Equal(face.Id(),line.Glyphs()[0].FontFace);Check.That(line.Glyphs()[0].GlyphIndex!=0);
            Check.Same(unrelated,otherLine.RunFontFace(0));Check.Same(unrelated,otherParagraph.RunFontFace(0));Check.Same(canonical,Face(f,"Skr Test Collection Hebrew"));
            var replacement=Face(f,"Skr Test Collection Latin");Check.False(ReferenceEquals(replacement,face));Check.Equal(0u,replacement.FaceIndex());
        }
    }
    [GuiTest("gui/text-contract/font-face/color-palettes")]
    private static void ColorPalettes()
    {
        using var f=Make(true);var face=Face(f,"Skr Test Color");Check.That(face.HasColorGlyphs());Check.Equal(2u,face.PaletteCount());
        Check.False(face.PaletteName(0).IsEmpty());Check.False(face.PaletteName(1).IsEmpty());Check.Equal(2,face.PaletteColors(0).Length);Check.Equal(2,face.PaletteColors(1).Length);
        Check.That(face.PaletteColors(0)[0].IsFinite());Check.That(face.PaletteColors(1)[1].IsFinite());Check.That(face.SetUsedPalette(1));Check.Equal(1u,face.UsedPalette());Check.False(face.SetUsedPalette(2));Check.Equal(1u,face.UsedPalette());
        SRGBColor[] custom=[new(.1f,.2f,.3f,1),new(.8f,.7f,.6f,1)];face.SetCustomPaletteColors(custom);Check.Equal(2,face.CustomPaletteColors().Length);
        Check.Equal(custom[0],face.CustomPaletteColors()[0]);Check.Equal(custom[1],face.CustomPaletteColors()[1]);Check.That(face.SetUsedPalette(0));Check.Equal(2,face.CustomPaletteColors().Length);
    }
}
