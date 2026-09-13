import pathlib,re
root=pathlib.Path(__file__).resolve().parents[1]
source=(pathlib.Path('D:/Code/ExtremeEngine-CppSLJIT/engine/modules/gui/core/tests/math/paint_transform_tests.cpp')).read_text()
header='''using System.Numerics;
namespace SkrGui.Tests;
// Source: all tests/math/paint_transform_tests.cpp cases. Numerics vectors/matrices preserve row-vector storage.
public static class PaintTransformContractTests {
const float kPi=MathF.PI,kFloatEpsilon=.0001f;
private static void check_offsetf(Offsetf p,float x,float y){Check.Near(x,p.X,kFloatEpsilon);Check.Near(y,p.Y,kFloatEpsilon);}
private static void check_rectf(Rectf r,float l,float t,float right,float b){Check.Near(l,r.Left,kFloatEpsilon);Check.Near(t,r.Top,kFloatEpsilon);Check.Near(right,r.Right,kFloatEpsilon);Check.Near(b,r.Bottom,kFloatEpsilon);}
private static void check_transformed_point(PaintTransform t,Offsetf p,float x,float y)=>check_offsetf(t.TransformPoint(p),x,y);
private static void expect_near(float a,float b,float e)=>Check.Near(b,a,e);
'''
def pascal(n):return ''.join(x[:1].upper()+x[1:] for x in n.split('_')).replace('2d','2D').replace('3d','3D')
s=source[source.index('SKR_TEST_CASE'):]
index=0
def casematch(m):
 global index
 index+=1;return f'[GuiTest("{m[1]}")]\npublic static void SourceCase{index:02}()'
s=re.sub(r'SKR_TEST_CASE\("([^"]+)"\)',casematch,s)
s=re.sub(r'\bconst\s+','',s)
s=re.sub(r'(PaintTransform\w*) (\w+) = \{\};',r'\1 \2 = new();',s)
s=s.replace('skr::math::float4','Vector4').replace('skr::math::float3x3','Float3x3').replace('skr::math::float3','Vector3').replace('skr::math::QuatF','Quaternion').replace('skr::math::RotatorF','RotatorF').replace('skr::math::TransformF','TransformF')
s=s.replace('Vector4x4','Matrix4x4')
s=s.replace('::','.')
s=re.sub(r'\.(\w+)',lambda m:'.'+pascal(m[1]) if m[1][0].islower() else m[0],s)
s=s.replace('Quaternion.AroundZ(kPi * 0.5f)','Quaternion.CreateFromAxisAngle(Vector3.UnitZ,kPi * .5f)')
s=s.replace('rotation.ToRotator()', 'new RotatorF(0,0,MathF.Atan2(2*(rotation.W*rotation.Z+rotation.X*rotation.Y),1-2*(rotation.Y*rotation.Y+rotation.Z*rotation.Z)))')
s=s.replace('Matrix4x4.FromRotationZ','Matrix4x4.CreateRotationZ').replace('Matrix4x4.FromTranslation','Matrix4x4.CreateTranslation')
s=re.sub(r'Float3x3\{([^}]+)\}',r'new Float3x3(\1)',s)
s=re.sub(r'(?<![\w.])(Offsetf|Vector3|Vector4)\(',r'new \1(',s)
s=s.replace('new Vector4(5.0f, 6.0f, 0.0f, 1.0f) * transform.ResolvedTransform()', 'Vector4.Transform(new Vector4(5,6,0,1),transform.ResolvedTransform())')
s=re.sub(r'transform_3d.Transform = TransformF\((.*?)\);',r'transform_3d.Transform = new TransformF { Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ,kPi*.5f),Position=new Vector3(5,0,0),Scale=new Vector3(2,1,1) };',s,flags=re.S)
s=s.replace('SKR_TEST_CHECK_FALSE','Check.False').replace('SKR_TEST_CHECK','Check.That')
(root/'tests/SkrGui.Core.Tests/Math/PaintTransformContractTests.cs').write_text(header+s+'\n}\n')
print(index,'source transform cases')
