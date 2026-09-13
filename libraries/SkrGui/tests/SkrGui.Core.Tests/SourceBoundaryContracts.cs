namespace SkrGui.Tests;
internal static class SourceBoundaryContracts
{
 [GuiTest("migration/backend/source-nested-constructor-defaults")]
 private static void Defaults()
 {Check.Equal(new SRGBColor(0,0,0,1),new VisualRenderDesc().ClearColor);Check.Equal(new SRGBColor(0,0,0,1),new VisualRenderTargetDesc().ClearColor);Check.Equal(1f,new VisualPaintDesc().PixelRatio);Check.Equal(uint.MaxValue,new VisualAtlasDraw().AtlasIndex);Check.Equal(ETextPixelMode.Sdf,new VisualTextDraw().PixelMode);}
 [GuiTest("migration/metadata/source-type-guids")]
 private static void TypeGuids()
 {Check.Equal(103,SourceTypeRegistry.Types.Count);Check.Equal(new Guid("93813235-f2fc-437c-aab5-3927fd2ed4d9"),SourceTypeRegistry.GetGuid(typeof(SizedBox)));Check.Equal(103,SourceTypeRegistry.Types.Values.Distinct().Count());Check.False(SourceTypeRegistry.TryGetGuid(typeof(string),out _));}
 [GuiTest("migration/math/source-matrix-float-equality")]
 private static void MatrixEquality() { var a=new Float3x3(float.NaN,0,0,0,1,0,0,0,1);var b=a;Check.False(a==b);Check.That(Float3x3.Identity()==Float3x3.Identity()); }
}
