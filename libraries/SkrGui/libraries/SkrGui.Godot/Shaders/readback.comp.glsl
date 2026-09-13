#version 450
layout(local_size_x=8,local_size_y=8,local_size_z=1) in;
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
