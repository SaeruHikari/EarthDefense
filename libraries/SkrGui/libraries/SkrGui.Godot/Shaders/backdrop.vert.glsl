#version 450
layout(std140,set=0,binding=1) uniform Backdrop {
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
layout(location=0) in vec2 position_logic;
layout(location=1) in vec2 uv;
layout(location=2) in vec4 color;
layout(location=0) out vec2 out_uv;
layout(location=1) out vec2 out_position_logic;
layout(location=2) out vec4 out_color;
void main(){out_uv=uv;out_position_logic=position_logic;out_color=color;gl_Position=pc.logic_to_clip*vec4(position_logic,0.0,1.0);}
