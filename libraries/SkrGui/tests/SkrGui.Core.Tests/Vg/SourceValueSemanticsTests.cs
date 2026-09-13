namespace SkrGui.Tests;
public static class SourceValueSemanticsTests
{
 [GuiTest("extra/source-value-semantics/gradient-default-and-copy")]
 public static void GradientDefaultAndCopy()
 {
  Check.Equal(1f,new ColorGradientStop().Color.A);
  ColorGradient gradient=new ColorGradientSolid(new SRGBColor(1,0,0,1));var clone=new ColorGradient(gradient);clone.AsSolid()!.SetColor(new(0,1,0,1));
  Check.Equal(new SRGBColor(1,0,0,1),gradient.Sample(.5f));Check.Equal(new SRGBColor(0,1,0,1),clone.Sample(.5f));
 }
 [GuiTest("extra/source-value-semantics/sampler-ref-evaluation-and-snapshots")]
 public static void SamplerRefAndCopy()
 {
  var sampler=new CircleSampler(.25f);var copied=sampler;copied.SetAngle(.75f);Check.Equal(.25f,sampler.Angle());
  var captured=new List<CircleSampler>();
  ShapeSampleHelper.SampleOpenUniformWithSampler(2,EShapeSampleDirection.Forward,ref sampler,
   (ref CircleSampler state,float t)=>{state.SetAngle(t);return state.SamplePoint(Offsetf.Zero(),1);},
   (point,t,state)=>captured.Add(state));
  Check.Equal(1f,sampler.Angle());Check.Equal(3,captured.Count);Check.Equal(0f,captured[0].Angle());Check.Equal(.5f,captured[1].Angle());Check.Equal(1f,captured[2].Angle());
 }
 [GuiTest("extra/source-value-semantics/path-copy-and-float-key")]
 public static void PathCopyAndFloatKey()
 {
  var path=new VGPath{PointEqualsTolerance=.2f};path.MoveTo(new(0,0));path.LineTo(new(2,2));var copy=new VGPath(path);copy.LineTo(new(4,0));
  Check.Equal(2,path.Commands().Count);Check.Equal(3,copy.Commands().Count);Check.Equal(.2f,copy.PointEqualsTolerance);
  var transform=new PaintTransform();ref readonly Offsetf offset=ref transform.Offset();transform.ApplyOffset2D(new(5,6));Check.Equal(new Offsetf(5,6),offset);
  var flat=path.Flatten(new());var copiedFlat=new VGPathFlatten(flat);copiedFlat.Clear();Check.Equal(2,flat.Nodes().Count);Check.Equal(0,copiedFlat.Nodes().Count);
  Check.False(new VGFillVertexKey(float.NaN,0)==new VGFillVertexKey(float.NaN,0));Check.That(new VGFillVertexKey(-0f,1)==new VGFillVertexKey(0,1));
 }
}
