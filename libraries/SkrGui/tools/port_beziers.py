from pathlib import Path
import re
ROOT=Path(__file__).resolve().parents[1]
BASE=Path('D:/Code/ExtremeEngine-CppSLJIT/engine/modules/gui/core/include/SkrGuiCore/math/shape')
def pascal(s): return ''.join(p[:1].upper()+p[1:] for p in s.strip('_').split('_'))
def bodies(src, cls):
    result=[]
    pat=re.compile(r'inline\s+([\w:]+)\s+'+cls+r'::(\w+)\s*\((.*?)\)\s*(?:const\s*)?noexcept\s*\{',re.S)
    for m in pat.finditer(src):
        start=m.end()-1;end=start+1;d=1
        while d:
            if src[end]=='{':d+=1
            elif src[end]=='}':d-=1
            end+=1
        result.append((m[1],m[2],m[3],src[start:end],src[:m.start()].count('\n')+1))
    return result

def reference_calls(s, refs):
    # Walk nested calls and rewrite only reference arguments, retaining expressions verbatim.
    matches=list(re.finditer(r'\b('+ '|'.join(refs)+r')\s*\(',s))
    for m in reversed(matches):
        start=m.end();i=start;depth=1;seps=[]
        while depth:
            if s[i]=='(':depth+=1
            elif s[i]==')':depth-=1
            elif s[i]==',' and depth==1:seps.append(i)
            i+=1
        bounds=[start]+[x+1 for x in seps];ends=seps+[i-1]
        for idx in reversed(refs[m[1]]):
            if idx>=len(bounds):continue
            p=bounds[idx]
            while s[p].isspace():p+=1
            s=s[:p]+'ref '+s[p:]
    return s

for cls,filename,fields in [('QuadBezier','quad_bezier.hpp',['start','control','end']),('CubicBezier','cubic_bezier.hpp',['start','control_1','control_2','end'])]:
    src=(BASE/filename).read_text(encoding='utf-8');funcs=bodies(src,cls);names={n:pascal(n) for _,n,_,_,_ in funcs}
    refs={'Split':[1,2],'ClosestPointOnSegment':[3],'AccumulateClosestPoint':[6,7,8]}
    if cls=='QuadBezier':refs['SolveAxisExtremum']=[3]
    else:refs.update({'SolveAxisExtrema':[4,5],'SolveQuadratic':[3,4]})
    def conv(s):
        s=re.sub(r'//[^\n]*','',s)
        s=re.sub(r'static_assert\([\s\S]*?\);','',s)
        s=re.sub(r'std::forward<[^>]+>\((\w+)\)',r'\1',s)
        s=s.replace('std::numeric_limits<float>::quiet_NaN()','float.NaN').replace('std::numeric_limits<float>::infinity()','float.PositiveInfinity')
        for a,b in [('std::isfinite','float.IsFinite'),('std::isnan','float.IsNaN'),('std::sqrt','MathF.Sqrt'),('std::fabs','MathF.Abs'),('std::copysign','MathF.CopySign'),('std::clamp','CppMath.Clamp')]:s=s.replace(a,b)
        s=s.replace('skr::math::lerp(start_t, end_t, segment_t)','(start_t + (end_t - start_t) * segment_t)')
        s=s.replace('std::swap(t0, t1);','(t0, t1) = (t1, t0);')
        s=s.replace('uint32_t','uint').replace('constexpr','const')
        s=re.sub(r'\bconst\s+','',s)
        s=s.replace('*this','this')
        for a,b in names.items():s=re.sub(r'\b'+re.escape(a)+r'\b',b,s)
        for f in fields:s=re.sub(r'\b'+f+r'\b',pascal(f),s)
        for a,b in [('segment_count','SegmentCount'),('max_depth','MaxDepth'),('direction','Direction'),('tolerance','Tolerance'),('step_length','StepLength'),('point','Point'),('distance','Distance'),('valid','Valid'),('t','T'),('x','X'),('y','Y')]:s=re.sub(r'\.'+a+r'\b','.'+b,s)
        for a,b in [('is_finite','IsFinite'),('length_squared','LengthSquared'),('length','Length'),('hold','Hold'),('lerp','Lerp'),('sample_open_uniform','SampleOpenUniform'),('sample_open_by_step_length','SampleOpenByStepLength'),('calc_bezier_tolerance','CalcBezierTolerance'),('resolve_max_depth','ResolveMaxDepth'),('estimate_step_length_tolerance','EstimateStepLengthTolerance')]:s=re.sub(r'(?<=\.)'+a+r'(?=\()',b,s);s=s.replace('::'+a+'(','.'+b+'(')
        s=s.replace('::','.')
        s=re.sub(r'(?<!\w)Offsetf\(', 'new Offsetf(',s)
        s=re.sub(r'(\d+)\.f\b',r'\1f',s)
        s=s.replace('_k_max_depth','KMaxDepth')
        s=re.sub(r'return\s*\{([^{}]*)\};',lambda m:'return new '+cls+'('+m[1].strip().rstrip(',')+');',s)
        s=re.sub(r'(left|right)\s*=\s*\{([^{}]*)\};',lambda m:m[1]+' = new '+cls+'('+m[2]+');',s)
        s=s.replace('ProjectResult result = {};','ProjectResult result = new();')
        s=s.replace('Functor&& functor','Action<Offsetf, float> functor')
        s=s.replace('[&](float t) noexcept { return PointAt(t); }','(float t) => value.PointAt(t)')
        return s
    out=['// Source: SkrGuiCore/math/shape/'+filename+' @ 611561f8.','// Full algorithm bodies translated by tools/port_beziers.py.','namespace SkrGui;',f'public struct {cls} : IEquatable<{cls}>','{','    public Offsetf '+', '.join(pascal(f) for f in fields)+';','    private const uint KMaxDepth = 16;','    public struct ProjectResult { public Offsetf Point; public float T, Distance; public bool Valid; public readonly bool IsValid() => Valid; }','    public '+cls+'('+', '.join('Offsetf '+f for f in fields)+') { '+' '.join(pascal(f)+' = '+f+';' for f in fields)+' }']
    for ret,name,args,body,line in funcs:
        args=conv(args);body=conv(body)
        if name=='sample' and 'ShapeToleranceSampleDesc' in args:
            body=re.sub(r'auto recurse_forward\s*=\s*\[&\]\(auto&& self,\s*'+cls+r'& curve,\s*float start_t,\s*float end_t,\s*uint depth\) noexcept -> void', 'void RecurseForward('+cls+' curve, float start_t, float end_t, uint depth)',body)
            body=re.sub(r'auto recurse_reverse\s*=\s*\[&\]\(auto&& self,\s*'+cls+r'& curve,\s*float start_t,\s*float end_t,\s*uint depth\) noexcept -> void', 'void RecurseReverse('+cls+' curve, float start_t, float end_t, uint depth)',body)
            split=body.index('void RecurseReverse')
            body=body[:split].replace('self(self,','RecurseForward(')+body[split:].replace('self(self,','RecurseReverse(')
            body=body.replace('recurse_forward(recurse_forward,','RecurseForward(').replace('recurse_reverse(recurse_reverse,','RecurseReverse(')
            body=body.replace('    };','    }')
        if 'value.PointAt' in body:body=body.replace('{','{\n    var value = this;',1)
        body=reference_calls(body,refs)
        args=args.replace('ShapeSegmentCountSampleDesc&','ShapeSegmentCountSampleDesc').replace('ShapeToleranceSampleDesc&','ShapeToleranceSampleDesc').replace('ShapeStepLengthSampleDesc&','ShapeStepLengthSampleDesc')
        args=re.sub(r'(float|Offsetf)& (best_distance_sq|best_t|best_point|segment_t|t0|t1|t)\b',r'ref \1 \2',args)
        args=args.replace(cls+'& left','ref '+cls+' left').replace(cls+'& right','ref '+cls+' right')
        args=args.replace(cls+'& curve',cls+' curve').replace('Offsetf&','Offsetf')
        if name in ['length','project','closest_point','distance']:args=args.replace('float tolerance','float tolerance = .01f')
        if name=='calc_tolerance':args='float tessellation_factor = 1, float pixel_ratio = 1'
        static=name.startswith('_') or name[:1].isupper() or name=='calc_tolerance'
        ret=ret.replace(cls+'::','').replace('uint32_t','uint')
        out += [f'    // Original line {line}: {name}.','    '+('private' if name.startswith('_') else 'public')+(' static' if static else '')+' '+ret+' '+names[name]+'('+args.strip()+')']
        out += ['    '+row for row in body.splitlines()]
    equal=' && '.join(pascal(f)+' == other.'+pascal(f) for f in fields)
    out += [f'    public readonly bool Equals({cls} other) => {equal};',f'    public override readonly bool Equals(object? obj) => obj is {cls} other && Equals(other);','    public override readonly int GetHashCode() => HashCode.Combine('+', '.join(pascal(f) for f in fields)+');',f'    public static bool operator ==({cls} lhs, {cls} rhs) => lhs.Equals(rhs);',f'    public static bool operator !=({cls} lhs, {cls} rhs) => !lhs.Equals(rhs);','}']
    target=ROOT/'libraries/SkrGui.Core/Math/Shape'/f'{cls}.cs';target.write_text('\n'.join(out)+'\n',encoding='utf-8')
    print(cls,len(funcs),'methods')
