from pathlib import Path
import re
ROOT=Path(__file__).resolve().parents[1]
BASE=Path('D:/Code/ExtremeEngine-CppSLJIT/engine/modules/gui/core/include/SkrGuiCore/math/shape')
def pascal(s):return ''.join(p[:1].upper()+p[1:] for p in s.strip('_').split('_'))
def block(src,start):
    end=start+1;depth=1
    while depth:
        if src[end]=='{':depth+=1
        elif src[end]=='}':depth-=1
        end+=1
    return src[start:end]
def functions(src, cls):
    pat=re.compile(r'inline\s+([\w:]+)\s+'+cls+r'::(\w+)\s*\((.*?)\)\s*(?:const\s*)?(?:noexcept\s*)?\{',re.S)
    return [(m[1],m[2],m[3],block(src,m.end()-1),src[:m.start()].count('\n')+1) for m in pat.finditer(src)]
def helpers(src):
    result=[]
    for m in re.finditer(r'struct\s+(\w+Helper)\s*\{',src):
        b=block(src,m.end()-1);ms=[]
        for n in re.finditer(r'static\s+(?:const\s+)?([\w:&]+)\s+(\w+)\s*\((.*?)\)\s*(?:noexcept\s*)?\{',b,re.S):ms.append((n[1],n[2],n[3],block(b,n.end()-1),src[:m.start()].count('\n')+b[:n.start()].count('\n')+1))
        consts=re.findall(r'static constexpr (float|double|uint32_t) (\w+) = ([^;]+);',b)
        result.append((m[1],ms,consts))
    return result

def init_lists(s):
    # Positional C++ descriptor construction maps to equivalent C# constructors.
    pat=re.compile(r'(ShapeSegmentCountSampleDesc|ShapeToleranceSampleDesc|ShapeStepLengthSampleDesc)\s*\{')
    for m in reversed(list(pat.finditer(s))):
        b=block(s,m.end()-1)
        s=s[:m.start()]+'new '+m[1]+'('+b[1:-1].strip().rstrip(',')+')'+s[m.end()-1+len(b):]
    return s

def rename_shadowed_locals(text):
    declarations=list(re.finditer(r'\b(?:float|double|uint|int)\s+(count|boundary_index|boundary_t|u)\s*=',text))
    repeated={m[1] for m in declarations if sum(n[1]==m[1] for n in declarations)>1}
    for serial,m in reversed(list(enumerate(declarations))):
        if m[1] not in repeated:continue
        stack=[]
        for i,ch in enumerate(text[:m.start()]):
            if ch=='{':stack.append(i)
            elif ch=='}' and stack:stack.pop()
        if not stack:continue
        end=stack[-1]+len(block(text,stack[-1]));name=m[1];new=name+'_scope'+str(serial)
        text=text[:m.start()]+re.sub(r'\b'+name+r'\b',new,text[m.start():end])+text[end:]
    return text

for cls,filename,fields in [('Circle','circle.hpp',[('Offsetf','center'),('float','radius')]),('Arc','arc.hpp',[('Offsetf','center'),('float','radius'),('float','start_angle'),('float','sweep_angle')]),('Ellipse','ellipse.hpp',[('Offsetf','center'),('float','radius_x'),('float','radius_y'),('float','rotation')]),('EllipticalArc','elliptical_arc.hpp',[('Offsetf','center'),('float','radius_x'),('float','radius_y'),('float','rotation'),('float','start_angle'),('float','sweep_angle')]),('Superellipse','superellipse.hpp',[('Offsetf','center'),('float','radius_x'),('float','radius_y'),('float','rotation'),('float','exponent')]),('SuperellipseArc','superellipse_arc.hpp',[('Offsetf','center'),('float','radius_x'),('float','radius_y'),('float','rotation'),('float','exponent'),('float','start_angle'),('float','sweep_angle')])]:
    src=(BASE/filename).read_text(encoding='utf-8');funcs=functions(src,cls);hs=helpers(src)
    names={n:pascal(n) for _,n,_,_,_ in funcs}
    sampler_type = 'SuperellipseSampler' if cls.startswith('Superellipse') else ('EllipseSampler' if cls in ['Ellipse','EllipticalArc'] else 'CircleSampler')
    static_methods=set(re.findall(r'static\s+[\w:&]+\s+(\w+)\s*\(',src[:src.index('// impl')]))
    for _,f,_ in hs:names.update({n:pascal(n) for _,n,_,_,_ in f})
    if 'canonical_arc' in names:names['canonical_arc']='MakeCanonicalArc'
    external=['is_finite','is_empty','is_point','is_valid','normalized','length_squared','length','radians','normalize','cw_normal','ccw_normal','cross','dot','unite','lerp','sample_point','set_angle','create_sampler','sample_closed_uniform','sample_open_uniform','sample_closed_uniform_with_sampler','sample_open_uniform_with_sampler','calc_geometric_tolerance','calc_circular_segment_count','calc_step_length_segment_count','estimate_step_length_tolerance','sample_closed_adaptive','sample_closed_by_step_length','sample_closed_adaptive_with_sampler','sample_closed_by_step_length_with_sampler','sample_open_adaptive','sample_open_by_step_length','sample_open_adaptive_with_sampler','sample_open_by_step_length_with_sampler','hold','fast_quadrant_segment_count','estimate_arc','resolve_max_depth','sample_closed_quadrants','sample_arc','point_at','distance','start_point','end_point','split_to_cubic_beziers','split_to_cubic_beziers_fast','canonical_aspect','accumulate_quadrant_spans','is_full_sweep','count_from_quarter','count_from_arc_accumulated']
    def conv(s):
        s=re.sub(r'//[^\n]*','',s)
        s=re.sub(r'std::forward<[^>]+>\((\w+)\)',r'\1',s)
        s=s.replace('std::numeric_limits<double>::infinity()','double.PositiveInfinity').replace('std::numeric_limits<float>::infinity()','float.PositiveInfinity')
        s=s.replace('std::numeric_limits<float>::quiet_NaN()','float.NaN').replace('std::numeric_limits<uint32_t>::max()','uint.MaxValue')
        s=re.sub(r'static_cast<([^>]+)>\(',r'(\1)(',s)
        for a,b in [('std::isfinite','float.IsFinite'),('std::isnan','float.IsNaN'),('std::sqrt','MathF.Sqrt'),('std::fabs','MathF.Abs'),('std::abs','MathF.Abs'),('std::sin','MathF.Sin'),('std::cos','MathF.Cos'),('std::tan','MathF.Tan'),('std::acos','MathF.Acos'),('std::pow','MathF.Pow'),('std::ceil','MathF.Ceiling'),('std::fmod','CppMath.Fmod'),('std::clamp','CppMath.Clamp'),('std::min','CppMath.Min'),('std::max','CppMath.Max')]:s=s.replace(a,b)
        s=s.replace('skr::math::kPi2','(MathF.PI * 2)').replace('skr::math::kHalfPi','(MathF.PI * .5f)').replace('skr::math::kPi','MathF.PI')
        s=s.replace('ShapeSampleHelper::_k_circle_tolerance_scale','.6f')
        s=s.replace('SKR_VERIFY','Debug.Assert').replace('SKR_ASSERT','Debug.Assert');s=re.sub(r'&&\s*("[^"\n]*")\s*\)',r', \1)',s)
        for a,b in [('std::round','VgScalar.Round'),('std::floor','VgScalar.Floor'),('std::atan2','VgScalar.Atan2'),('std::atan','VgScalar.Atan'),('std::log','VgScalar.Log'),('std::exp','VgScalar.Exp')]:s=s.replace(a,b)
        for fn in ['Abs','Sqrt','Sin','Cos','Tan','Acos','Pow','Ceiling']:s=s.replace('MathF.'+fn,'VgScalar.'+fn)
        s=s.replace('float.IsFinite','VgScalar.IsFinite').replace('float.IsNaN','VgScalar.IsNaN')
        s=s.replace('int32_t','int')
        s=s.replace('uint','uint').replace('uint64_t','ulong').replace('constexpr','const')
        s=re.sub(r'\bconst\s+','',s);s=re.sub(r'\bauto\b','var',s)
        for a,b in names.items():s=re.sub(r'\b'+re.escape(a)+r'\b',b,s)
        for name in external:
            s=s.replace('.'+name+'(','.'+pascal(name)+'(').replace('::'+name+'(','.'+pascal(name)+'(')
        for _,field in fields:s=re.sub(r'\b'+field+r'\b',pascal(field),s)
        for a in ['point','angle','t','distance','valid','x','y','segment_count','direction','tolerance','step_length','max_depth']:s=re.sub(r'\.'+a+r'\b','.'+pascal(a),s)
        s=s.replace('ShapeEstimateArcHelper::canonical_arc','ShapeEstimateArcHelper.MakeCanonicalArc')
        s=re.sub(r'sizeof\(kSegmentModels\)\s*/\s*sizeof\(kSegmentModels\[0\]\)', 'kSegmentModels.Length',s)
        s=s.replace('skr::math::nearly_equal','VgScalar.NearlyEqual')
        s=s.replace('std::copysign','VgScalar.CopySign')
        s=s.replace('::','.')
        s=re.sub(r'(\d+)\.f\b',r'\1f',s)
        s=re.sub(r'Offsetf (\w+)\(([^;]+)\);',r'Offsetf \1 = new Offsetf(\2);',s)
        s=re.sub(r'(?<!\w)(Offsetf|Sizef|CircleSampler|EllipseSampler|SuperellipseSampler)\(',r'new \1(',s)
        s=re.sub(r'return\s*\{([^{}]*)\};',lambda m:'return new '+cls+'('+m[1].strip().rstrip(',')+');',s)
        s=s.replace('ProjectResult result = {};','ProjectResult result = new();').replace('CanonicalArc arc = {};','CanonicalArc arc = new();')
        s=s.replace('(sizeof(kAspectModels) / sizeof(kAspectModels[0]))','kAspectModels.Length')
        s=s.replace('WeightFunc&& weight_func','Func<double,double,double,double> weight_func')
        s=s.replace('AspectModel&','AspectModel').replace('SegmentModel&','SegmentModel')
        s=s.replace('Superellipse&','Superellipse').replace('SuperellipseArc&','SuperellipseArc').replace('CubicBezier&','CubicBezier')
        s=re.sub(r'Functor&{1,2} functor','Action<Offsetf,float> functor',s)
        s=re.sub(r'Func&{1,2} func','Action<CubicBezier> func',s)
        s=s.replace('float& x_abs','ref float x_abs').replace('float& y_abs','ref float y_abs')
        s=s.replace('(sizeof(kSegmentModels) / sizeof(kSegmentModels[0]))','kSegmentModels.Length')
        s=re.sub(r'for \(SegmentModel (\w+) : (\w+)\)',r'foreach (SegmentModel \1 in \2)',s)
        s=re.sub(r'for \(AspectModel (\w+) : (\w+)\)',r'foreach (AspectModel \1 in \2)',s)
        s=re.sub(r'\[&\]\(([^)]*)\) noexcept',lambda m:'('+m[1].replace('&','')+') =>',s)
        for f in ['aspect','start_angle','sweep_angle','max_aspect','coeff0','coeff1','coeff2','quarter_safety','arc_safety','arc_bias','arc_distribution_power','max_exponent','coeff','distribution_exponent']:s=re.sub(r'\.'+f+r'\b','.'+pascal(f),s)
        s=re.sub(r'\[&\]\(float t\) noexcept\s*\{\s*return ([^;]+);\s*\}',r'(float t) => \1',s)
        s=re.sub(r'\[&\]\(CircleSampler& sampler, float t\) noexcept',r'(CircleSampler sampler, float t) =>',s)
        s=re.sub(r'for \(float (\w+) : (\w+)\)',r'foreach (float \1 in \2)',s)
        s=re.sub(r'float (\w+)\[\] = \{([^}]+)\};',r'float[] \1 = [\2];',s)
        s=s.replace('new new Offsetf','new Offsetf').replace('*this','this')
        marker='float delta = CppMath.Fmod(StartAngle - angle'
        if marker in s:
            i=s.index(marker);s=s[:i]+re.sub(r'\bdelta\b','reverse_delta',s[i:])
        s=re.sub(r'\(Offsetf\s*,', '(Offsetf ignored,',s)
        s=re.sub(r'\(CubicBezier\s*\)', '(CubicBezier ignored)',s)
        for m in reversed(list(re.finditer(r'\bSample\s*\{',s))):
            b=block(s,m.end()-1);s=s[:m.start()]+'new Sample('+b[1:-1].strip().rstrip(',')+')'+s[m.end()-1+len(b):]
        s=re.sub(r'(EvalAxisAbs\([^;]*?),\s*x_abs,\s*y_abs\)',r'\1, ref x_abs, ref y_abs)',s)
        s=s.replace('>> 1u','>> 1')
        s=init_lists(s)
        return rename_shadowed_locals(s)
    out=['// Source: SkrGuiCore/math/shape/'+filename+' @ 611561f8.','// Full geometry, sampling, projection and cubic conversion bodies.','using System.Diagnostics;','namespace SkrGui;']
    if cls=='EllipticalArc':out += ['public enum EEllipticalArcSize : byte { Small, Large }','public enum EEllipticalArcSweep : byte { CW, CCW }']
    for helper,fs,consts in hs:
        out += ['internal static class '+helper,'{']
        if helper=='ShapeEstimateArcHelper':out += ['    public struct CanonicalArc { public double StartAngle,SweepAngle,Aspect=1; public CanonicalArc() {} }']
        if helper=='SuperellipseSampleHelper':out += ['    public readonly record struct Sample(Offsetf Point,float T);']
        if helper=='SuperellipseEstimateHelper':
            out += ['    public readonly record struct SegmentModel(double MaxExponent,double[] Coeff,double QuarterSafety,double ArcSafety,double ArcBias,double DistributionExponent);']
            a=src.index('{',src.index('kSegmentModels[]'));data=block(src,a)[1:-1];rows=[];i=0
            while i<len(data):
                if data[i]!='{':i+=1;continue
                row=block(data,i);i+=len(row);inside=row[1:-1].strip().rstrip(',');inside=re.sub(r'\{([^}]+)\}',r'new double[] { \1 }',inside);rows.append('new('+conv(inside)+')')
            out += ['    private static readonly SegmentModel[] kSegmentModels = [ '+', '.join(rows)+'];']
        if helper=='EllipseEstimateHelper':
            fs_names=['MaxAspect','Coeff0','Coeff1','Coeff2','QuarterSafety','ArcSafety','ArcBias','ArcDistributionPower']
            out += ['    public readonly record struct AspectModel('+', '.join('double '+f for f in fs_names)+');']
            glob=re.search(r'kEllipseGlobalModel = \{([^}]+)\}',src,re.S)[1]
            out += ['    private static readonly AspectModel kEllipseGlobalModel = new('+conv(glob).strip()+');']
            arr=re.search(r'kAspectModels\[\] = \{(.*?)\n    \};',src,re.S)[1]
            out += ['    private static readonly AspectModel[] kAspectModels = [ '+', '.join('new('+conv(row).strip()+')' for row in re.findall(r'\{([^}]+)\}',arr))+'];']
        for typ,name,val in consts:out.append('    public const '+typ.replace('uint32_t','uint')+' '+name+' = '+conv(val)+';')
        for ret,name,args,body,line in fs:
            args=conv(args).replace(cls+'&',cls);body=conv(body)
            body=body.replace('return new '+cls+'(', 'return new '+ret.replace('&','')+'(')
            out += [f'    // Original line {line}: {name}.','    public static '+ret.replace('uint32_t','uint').replace('&','')+' '+names[name]+'('+args.strip()+')']+['    '+r for r in body.splitlines()]
        out.append('}')
    out += ['public struct '+cls+' : IEquatable<'+cls+'>','{']
    for typ,f in fields:out.append('    public '+typ+' '+pascal(f)+';')
    if cls.startswith('Superellipse'):out += ['    public '+cls+'() { Exponent = 2; }']
    if 'struct ProjectResult' in src:out += ['    public struct ProjectResult { public Offsetf Point; public float Angle, T, Distance; public bool Valid; public readonly bool IsValid() => Valid; }']
    out += ['    public '+cls+'('+', '.join(t+' '+f for t,f in fields)+') { '+' '.join(pascal(f)+' = '+f+';' for _,f in fields)+' }']
    for ret,name,args,body,line in funcs:
        args=conv(args);body=conv(body)
        if name=='sample_with_sampler':args=args.replace('Action<Offsetf,float> functor','Action<Offsetf,float,'+sampler_type+'> functor')
        args=args.replace(cls+'&',cls).replace('Offsetf&','Offsetf').replace('ShapeSegmentCountSampleDesc&','ShapeSegmentCountSampleDesc').replace('ShapeToleranceSampleDesc&','ShapeToleranceSampleDesc').replace('ShapeStepLengthSampleDesc&','ShapeStepLengthSampleDesc')
        args=args.replace('Functor&& functor','Action<Offsetf, float, '+sampler_type+'> functor' if name=='sample_with_sampler' else 'Action<Offsetf, float> functor').replace('Func&& func','Action<CubicBezier> func')
        if name in ['project','signed_distance','distance','closest_point'] and cls in ['Ellipse','EllipticalArc']:args=args.replace('float tolerance','float tolerance = .01f')
        if name=='length' and cls=='EllipticalArc':args='float tolerance = .01f'
        if name=='ArcTo' and cls=='EllipticalArc':args=args.replace('float Rotation','float Rotation = 0').replace('EEllipticalArcSize arc_size','EEllipticalArcSize arc_size = EEllipticalArcSize.Small').replace('EEllipticalArcSweep sweep','EEllipticalArcSweep sweep = EEllipticalArcSweep.CW')
        if name=='CenterRadius' and cls=='Ellipse':args=args.replace('float Rotation','float Rotation = 0')
        if cls.startswith('Superellipse') and name=='length':args='float tolerance = .01f'
        if cls.startswith('Superellipse') and name in ['cubic_bezier_count','split_to_cubic_beziers']:args=args.replace('uint max_depth','uint max_depth = 10')
        if cls=='Superellipse' and name=='CenterRadius':args=args.replace('float Rotation','float Rotation = 0').replace('float Exponent','float Exponent = 2')
        if name=='create_sampler':args='float angle = 0'
        if name=='calc_tolerance':args='float tessellation_factor = 1, float pixel_ratio = 1'
        if cls=='EllipticalArc' and name=='rect':
            a=body.index('float cos_rotation');b=body.index('}',a);fragment=body[a:b].replace('cos_rotation','full_cos_rotation').replace('sin_rotation','full_sin_rotation');body=body[:a]+fragment+body[b:]
        ret=ret.replace(cls+'::','').replace('uint32_t','uint')
        body=body.replace('return new '+cls+'(', 'return new '+ret+'(')
        body=body.replace('std.atan2','VgScalar.Atan2').replace('std.asin','VgScalar.Asin')
        static=name in static_methods
        out += [f'    // Original line {line}: {name}.','    '+('internal' if cls.startswith('Superellipse') and name.startswith('_') else ('private' if name.startswith('_') else 'public'))+(' static' if static else '')+' '+ret+' '+names[name]+'('+args.strip()+')']+['    '+r for r in body.splitlines()]
    eq=' && '.join(pascal(f)+' == other.'+pascal(f) for _,f in fields)
    out += [f'    public readonly bool Equals({cls} other) => {eq};',f'    public override readonly bool Equals(object? obj) => obj is {cls} other && Equals(other);','    public override readonly int GetHashCode() => HashCode.Combine('+', '.join(pascal(f) for _,f in fields)+');',f'    public static bool operator ==({cls} lhs, {cls} rhs) => lhs.Equals(rhs);',f'    public static bool operator !=({cls} lhs, {cls} rhs) => !lhs.Equals(rhs);','}']
    target=ROOT/'libraries/SkrGui.Core/Math/Shape'/f'{cls}.cs';code='\n'.join(out)+'\n';code=re.sub(r'(?m)^(\s*)sampler,\s*$',r'\1ref sampler,',code);code=re.sub(r'\((CircleSampler|EllipseSampler|SuperellipseSampler) sampler, float t\)',r'(ref \1 sampler, float t)',code);target.write_text(code,encoding='utf-8');print(cls,len(funcs),'methods',len(hs),'helpers')
