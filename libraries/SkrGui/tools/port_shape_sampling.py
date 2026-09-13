"""Mechanical syntax mapping of the reviewed SkrGui shape-sampling algorithms.

Only writes the generated managed source under this migration workspace.
The original source is read-only. No sampling algorithm is substituted.
"""
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
SOURCE = Path('D:/Code/ExtremeEngine-CppSLJIT/engine/modules/gui/core/include/SkrGuiCore/math/shape/shape_sample.hpp')

def pascal(s):
    return ''.join(part[:1].upper() + part[1:] for part in s.strip('_').split('_'))

source = SOURCE.read_text(encoding='utf-8')
functions = []
pattern = re.compile(r'inline\s+(\w+)\s+ShapeSampleHelper::(\w+)\s*\((.*?)\)\s*noexcept\s*\{', re.S)
for match in pattern.finditer(source):
    if match[2].startswith('_assert_'):
        continue
    start = match.end() - 1
    depth = 1
    end = start + 1
    while depth:
        if source[end] == '{': depth += 1
        elif source[end] == '}': depth -= 1
        end += 1
    functions.append((match[1], match[2], match[3], source[start:end], source[:match.start()].count('\n') + 1))
names = {name: pascal(name) for _, name, _, _, _ in functions}

def convert(text):
    text = re.sub(r'_assert_(?:sampler_)?visitor<[^>]+>\(\);', '', text)
    text = re.sub(r'std::forward<[^>]+>\((\w+)\)', r'\1', text)
    text = text.replace('std::numeric_limits<uint32_t>::max()', 'uint.MaxValue')
    text = re.sub(r'static_cast<([^>]+)>\(', r'(\1)(', text)
    text = re.sub(r'\bconst\s+', '', text)
    text = text.replace('uint32_t', 'uint').replace('uint64_t', 'ulong')
    for name, new in names.items(): text = re.sub(r'\b' + re.escape(name) + r'\b', new, text)
    for old,new in [('std::isfinite','float.IsFinite'),('std::fabs','MathF.Abs'),('std::abs','MathF.Abs'),('std::acos','MathF.Acos'),('std::ceil','MathF.Ceiling'),('std::clamp','CppMath.Clamp'),('std::min','CppMath.Min'),('std::max','CppMath.Max'),('Offsetf::lerp','Offsetf.Lerp')]: text = text.replace(old,new)
    text = text.replace('::', '.')
    text = re.sub(r'(\d+)\.f\b',r'\1f', text)
    for old,new in [('length_squared','LengthSquared'),('length','Length'),('width','Width'),('height','Height')]: text = text.replace('.'+old+'()', '.'+new+'()')
    text = text.replace('Offsetf&', 'Offsetf').replace('Rectf&','Rectf')
    return text

out = ['// Source: SkrGuiCore/math/shape/shape_sample.hpp @ 611561f8.', '// Syntax-translated with tools/port_shape_sampling.py; source loops and recursion are retained.', 'namespace SkrGui;', 'public delegate Offsetf ShapeSampleEvaluator<TSampler>(ref TSampler sampler,float t);', '', 'public enum EShapeSampleDirection : byte { Forward, Reverse }', 'public struct ShapeSegmentCountSampleDesc { public uint SegmentCount; public EShapeSampleDirection Direction; public ShapeSegmentCountSampleDesc(uint count, EShapeSampleDirection direction = EShapeSampleDirection.Forward) { SegmentCount = count; Direction = direction; } }', 'public struct ShapeStepLengthSampleDesc { public float StepLength = 1; public EShapeSampleDirection Direction; public uint MaxDepth = 10; public ShapeStepLengthSampleDesc() { } public ShapeStepLengthSampleDesc(float step, EShapeSampleDirection direction = EShapeSampleDirection.Forward, uint maxDepth = 10) { StepLength = step; Direction = direction; MaxDepth = maxDepth; } }', 'public struct ShapeToleranceSampleDesc { public float Tolerance = .25f; public EShapeSampleDirection Direction; public uint MaxDepth = 10; public ShapeToleranceSampleDesc() { } public ShapeToleranceSampleDesc(float tolerance, EShapeSampleDirection direction = EShapeSampleDirection.Forward, uint maxDepth = 10) { Tolerance = tolerance; Direction = direction; MaxDepth = maxDepth; } }', 'public static class ShapeSampleHelper', '{', '    private const uint _k_default_max_depth = 10;', '    private const float _k_default_geometric_tolerance = .6f, _k_default_bezier_tolerance = .6f, _k_circle_tolerance_scale = .6f;']
for ret,name,args,body,line in functions:
    args,body = convert(args),convert(body)
    sampler = 'with_sampler' in name
    if sampler:
        args = args.replace('Sampler& sampler', 'ref TSampler sampler').replace('EvalFn&& eval','ShapeSampleEvaluator<TSampler> eval').replace('Functor&& functor','Action<Offsetf, float, TSampler> functor')
        body = body.replace('auto direct_eval = [&](float t) noexcept', 'Func<float, Offsetf> direct_eval = (float t) =>')
        body = body.replace('auto direct_functor = [&](Offsetf, float t) noexcept', 'Action<Offsetf, float> direct_functor = (Offsetf ignored, float t) =>')
        body=body.replace('eval(sampler,','eval(ref sampler,')
        body=re.sub(r'\bsampler\b','sampler_state',body)
        body='{\n TSampler sampler_state=sampler;\n try '+body+'\n finally {sampler=sampler_state;}\n}'
    else:
        args = re.sub(r'EvalFn&{1,2} eval', 'Func<float, Offsetf> eval', args)
        args = re.sub(r'Functor&{1,2} functor','Action<Offsetf, float> functor',args)
    return_type = ret.replace('uint32_t','uint')
    out.append(f'    // Original line {line}: {name}.')
    out.append('    public static '+return_type+' '+names[name]+('<TSampler>' if sampler else '')+'('+args.strip()+')')
    out.extend('    '+row for row in body.splitlines())
out.append('}')
target = ROOT / 'libraries/SkrGui.Core/Math/Shape/ShapeSample.cs'
target.parent.mkdir(parents=True, exist_ok=True)
target.write_text('\n'.join(out)+'\n',encoding='utf-8')
print(f'{len(functions)} complete algorithm bodies -> {target}')
