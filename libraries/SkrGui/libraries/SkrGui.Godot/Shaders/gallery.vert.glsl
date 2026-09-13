#version 450
layout(push_constant,std430) uniform Common{mat4 logic_to_clip;vec2 texture_size_pixel;float solid_color_scale;float pad0;} pc;
layout(location=0) in vec2 position_logic;
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
