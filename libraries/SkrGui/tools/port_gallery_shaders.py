import pathlib,re
root=pathlib.Path(__file__).resolve().parents[1]
src=pathlib.Path('D:/Code/ExtremeEngine-CppSLJIT/engine/modules/gui/samples/gallery_common/shaders')
dst=root/'libraries/SkrGui.Godot/Shaders';dst.mkdir(parents=True,exist_ok=True)
def types(s):
 for a,b in [('float4x4','mat4'),('float4','vec4'),('float3','vec3'),('float2','vec2'),('uint4','uvec4'),('uint2','uvec2'),('int2','ivec2')]:s=re.sub(r'\b'+a+r'\b',b,s)
 s=re.sub(r'(?<=\d)f\b','',s)
 s=s.replace('ddx(','dFdx(').replace('frac(','fract(').replace('lerp(','mix(')
 s=re.sub(r'gTexture.Sample\(gTextureSampler, ([^;\n]*)\)',r'texture(gTexture, \1)',s)
 s=s.replace('push_constants.','pc.')
 return s
sat='float saturate(float v){return clamp(v,0.0,1.0);}\nvec2 saturate(vec2 v){return clamp(v,vec2(0),vec2(1));}\nvec3 saturate(vec3 v){return clamp(v,vec3(0),vec3(1));}\nvec4 saturate(vec4 v){return clamp(v,vec4(0),vec4(1));}\n'
common='layout(push_constant,std430) uniform Common{mat4 logic_to_clip;vec2 texture_size_pixel;float solid_color_scale;float pad0;} pc;\n'
vertex='#version 450\n'+common+'''layout(location=0) in vec2 position_logic;
layout(location=1) in vec2 uv;
layout(location=2) in vec4 color;
layout(location=0) out vec2 out_uv;
layout(location=1) out vec4 sdf_st;
layout(location=2) out vec4 out_color;
void main(){
 out_uv=uv;sdf_st=vec4(uv,0.0,0.0);out_color=color;
 #if USE_TEXTURE && (TEXT_EFFECT == 2 || TEXT_EFFECT == 3)
 sdf_st=vec4(uv*pc.texture_size_pixel,vec2(1.0)/(3.0*pc.texture_size_pixel));
 #endif
 gl_Position=pc.logic_to_clip*vec4(position_logic,0.0,1.0);
}
'''
source=(src/'gallery_renderer.cxx').read_text()
start=source.index('const float sdf_scale')
end=source.index('[[push_constant]]')
functions=source[start:end]
functions=functions.rsplit('#endif',1)[0] # outer USE_TEXTURE guard has no opener in slice
functions=types(functions).replace('float4&','out vec4').replace('vec4&','out vec4')
functions=functions.replace('const vec2 offset = grad * sdf_st.zw;', 'const vec2 offset = grad * sdf_st.zw;')
fragment='#version 450\n'+common+sat+'''#define GALLERY_TEXT_EFFECT_DIRECT 0
#define GALLERY_TEXT_EFFECT_GRAY_LCD 1
#define GALLERY_TEXT_EFFECT_SDF 2
#define GALLERY_TEXT_EFFECT_SDF_LCD 3
layout(location=0) in vec2 in_uv;
layout(location=1) in vec4 in_sdf_st;
layout(location=2) in vec4 in_color;
#if USE_TEXTURE
layout(set=0,binding=0) uniform sampler2D gTexture;
'''+functions+'''#endif
#if USE_TEXTURE && (TEXT_EFFECT == 1 || TEXT_EFFECT == 3)
layout(location=0,index=0) out vec4 out_color;
layout(location=0,index=1) out vec4 out_alpha;
#else
layout(location=0) out vec4 out_color;
#endif
void main(){
#if USE_TEXTURE && TEXT_EFFECT == 3
gallery_sdf_lcd(in_uv,in_sdf_st,gallery_premultiply_paint(in_color),out_color,out_alpha);
#elif USE_TEXTURE && TEXT_EFFECT == 1
gallery_gray_lcd(in_uv,gallery_premultiply_paint(in_color),out_color,out_alpha);
#elif USE_TEXTURE && TEXT_EFFECT == 2
float alpha=gallery_sdf_alpha(in_uv,in_sdf_st.xy);out_color=gallery_premultiply_paint(in_color)*alpha;
#elif USE_TEXTURE
out_color=in_color*texture(gTexture,in_uv);
#else
out_color=in_color*pc.solid_color_scale;
#endif
}
'''
(dst/'gallery.vert.glsl').write_text(vertex)
(dst/'gallery.frag.glsl').write_text(fragment)
source=(src/'gallery_backdrop.cxx').read_text()
body=source[source.index('bool backdrop_is_light_enabled'):source.index('[[vertex_shader')]
prefix=source[source.index('float2 safe_normalize'):source.index('bool backdrop_is_light_enabled')]
body=types(prefix+body)
body=body.replace('float& edge_distance','out float edge_distance').replace('vec2& edge_normal','out vec2 edge_normal')
constants='''layout(std140,set=0,binding=1) uniform Backdrop {
mat4 logic_to_clip;
vec2 texture_uv_scale;vec2 pad0;
vec2 backdrop_origin_logic;vec2 backdrop_size_logic;
vec2 shape_size_logic;vec2 light_direction;
vec4 light_color;vec4 glass_color;
float glass_transmittance;float glass_grain;float glass_refraction;float pad1;
vec2 sample_uv_margin;vec2 pad2;
vec2 radius_top_left_logic;vec2 radius_top_right_logic;
vec2 radius_bottom_right_logic;vec2 radius_bottom_left_logic;
} pc;
'''
v='#version 450\n'+constants+'''layout(location=0) in vec2 position_logic;
layout(location=1) in vec2 uv;
layout(location=2) in vec4 color;
layout(location=0) out vec2 out_uv;
layout(location=1) out vec2 out_position_logic;
layout(location=2) out vec4 out_color;
void main(){out_uv=uv;out_position_logic=position_logic;out_color=color;gl_Position=pc.logic_to_clip*vec4(position_logic,0.0,1.0);}
'''
fs=source[source.index('float4 fs_main('):]
fs=fs[fs.index('{'):]
fs=types(fs).replace('input.position_logic','in_position_logic').replace('input.uv','in_uv').replace('input.color','in_color').replace('return color;','out_color=color;')
f='#version 450\n#define BACKDROP_EFFECT_EPSILON 0.0001\n'+constants+sat+'''layout(set=0,binding=0) uniform sampler2D gTexture;
layout(location=0) in vec2 in_uv;
layout(location=1) in vec2 in_position_logic;
layout(location=2) in vec4 in_color;
layout(location=0) out vec4 out_color;
'''+body+'\nvoid main()\n'+fs
(dst/'backdrop.vert.glsl').write_text(v);(dst/'backdrop.frag.glsl').write_text(f)
source=(src/'gallery_blur.cxx').read_text()
functions=source[source.index('float4 load_clamped'):source.index('[[numthreads')]
functions=types(functions)
functions=functions.replace('gSourceTexture.Load(clamp(point, ivec2(0, 0), max_point))','texelFetch(gSourceTexture,clamp(point,ivec2(0,0),max_point),0)')
c='#version 450\n'+'''layout(local_size_x=8,local_size_y=8,local_size_z=1) in;
layout(set=0,binding=0) uniform sampler2D gSourceTexture;
layout(rgba8,set=0,binding=1) uniform writeonly image2D gTargetTexture;
layout(push_constant,std430) uniform Blur{
uvec2 destination_size_pixel;uvec2 source_size_pixel;vec2 source_origin_pixel;
float blur_radius_pixel;float downsample_scale;uint mode;uint pad0;
} pc;
'''+functions+'''void main(){
uvec2 tid=gl_GlobalInvocationID.xy;if(any(greaterThanEqual(tid,pc.destination_size_pixel)))return;
vec4 value;
if(pc.mode==0u)value=downsample_pixel(tid);
else if(pc.mode==1u)value=blur_pixel(tid,ivec2(1,0));
else value=blur_pixel(tid,ivec2(0,1));
imageStore(gTargetTexture,ivec2(tid),value);
}
'''
(dst/'blur.comp.glsl').write_text(c)
read='#version 450\n'+'''layout(local_size_x=8,local_size_y=8,local_size_z=1) in;
layout(set=0,binding=0) uniform sampler2D gSourceTexture;
layout(std430,set=0,binding=1) writeonly buffer Pixels{uint values[];} output_pixels;
layout(push_constant,std430) uniform Readback{uvec2 size_pixel;uint pad0;uint pad1;} pc;
uint pack_rgba8(vec4 color){
uvec4 rgba=uvec4(round(clamp(color,vec4(0),vec4(1))*255.0));
return rgba.x|(rgba.y<<8u)|(rgba.z<<16u)|(rgba.w<<24u);
}
void main(){
uvec2 tid=gl_GlobalInvocationID.xy;if(any(greaterThanEqual(tid,pc.size_pixel)))return;
uint index=tid.y*pc.size_pixel.x+tid.x;output_pixels.values[index]=pack_rgba8(texelFetch(gSourceTexture,ivec2(tid),0));
}
'''
(dst/'readback.comp.glsl').write_text(read)
print('Translated all four gallery shaders; numerical formulas and branches retained.')
