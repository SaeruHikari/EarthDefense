#version 450
layout(local_size_x=8,local_size_y=8,local_size_z=1) in;
layout(set=0,binding=0) uniform sampler2D gSourceTexture;
layout(rgba8,set=0,binding=1) uniform writeonly image2D gTargetTexture;
layout(push_constant,std430) uniform Blur{
uvec2 destination_size_pixel;uvec2 source_size_pixel;vec2 source_origin_pixel;
float blur_radius_pixel;float downsample_scale;uint mode;uint pad0;
} pc;
vec4 load_clamped(ivec2 point)
{
    const ivec2 max_point = ivec2(max(pc.source_size_pixel, uvec2(1, 1)) - uvec2(1, 1));
    return texelFetch(gSourceTexture,clamp(point,ivec2(0,0),max_point),0);
}

float gaussian_weight(float offset, float sigma)
{
    return exp(-(offset * offset) / max(2.0 * sigma * sigma, 0.0001));
}

vec4 downsample_pixel(uvec2 tid)
{
    const float scale = max(pc.downsample_scale, 1.0);
    const vec2 center = pc.source_origin_pixel + (vec2(tid) + vec2(0.5, 0.5)) * scale;
    const vec2 half_step = vec2(scale * 0.25, scale * 0.25);
    vec4 color = vec4(0.0, 0.0, 0.0, 0.0);
    color += load_clamped(ivec2(center + vec2(-half_step.x, -half_step.y)));
    color += load_clamped(ivec2(center + vec2(half_step.x, -half_step.y)));
    color += load_clamped(ivec2(center + vec2(-half_step.x, half_step.y)));
    color += load_clamped(ivec2(center + vec2(half_step.x, half_step.y)));
    return color * 0.25;
}

vec4 blur_pixel(uvec2 tid, ivec2 axis)
{
    const float scaled_radius = max(pc.blur_radius_pixel / max(pc.downsample_scale, 1.0), 0.0);
    const int radius = min(32, int(ceil(scaled_radius)));
    if (radius <= 0)
    {
        return load_clamped(ivec2(tid));
    }

    const float sigma = max(scaled_radius * 0.5, 1.0);
    float weight_sum = 0.0;
    vec4 color = vec4(0.0, 0.0, 0.0, 0.0);
    for (int i = -radius; i <= radius; ++i)
    {
        const float weight = gaussian_weight(float(i), sigma);
        color += load_clamped(ivec2(tid) + axis * i) * weight;
        weight_sum += weight;
    }
    return color / max(weight_sum, 0.0001);
}

void main(){
uvec2 tid=gl_GlobalInvocationID.xy;if(any(greaterThanEqual(tid,pc.destination_size_pixel)))return;
vec4 value;
if(pc.mode==0u)value=downsample_pixel(tid);
else if(pc.mode==1u)value=blur_pixel(tid,ivec2(1,0));
else value=blur_pixel(tid,ivec2(0,1));
imageStore(gTargetTexture,ivec2(tid),value);
}
