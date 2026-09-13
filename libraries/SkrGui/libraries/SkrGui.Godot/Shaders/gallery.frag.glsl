#version 450
layout(push_constant,std430) uniform Common{mat4 logic_to_clip;vec2 texture_size_pixel;float solid_color_scale;float pad0;} pc;
float saturate(float v){return clamp(v,0.0,1.0);}
vec2 saturate(vec2 v){return clamp(v,vec2(0),vec2(1));}
vec3 saturate(vec3 v){return clamp(v,vec3(0),vec3(1));}
vec4 saturate(vec4 v){return clamp(v,vec4(0),vec4(1));}
#define GALLERY_TEXT_EFFECT_DIRECT 0
#define GALLERY_TEXT_EFFECT_GRAY_LCD 1
#define GALLERY_TEXT_EFFECT_SDF 2
#define GALLERY_TEXT_EFFECT_SDF_LCD 3
layout(location=0) in vec2 in_uv;
layout(location=1) in vec4 in_sdf_st;
layout(location=2) in vec4 in_color;
#if USE_TEXTURE
layout(set=0,binding=0) uniform sampler2D gTexture;
const float sdf_scale = 7.96875;
const float sdf_bias = 0.50196078431;
const float sdf_aa_factor = 0.65;
const float sdf_base_min = 0.125;
const float sdf_base_max = 0.25;
const float sdf_base_dev = -0.65;

vec4 gallery_premultiply_paint(vec4 color)
{
    return vec4(color.rgb * color.a, color.a);
}

float gallery_sdf_alpha(vec2 uv, vec2 sdf_st)
{
    const vec4 color = texture(gTexture, uv);
    const float distance = sdf_scale * (color.r - sdf_bias);

    const vec2 grad = dFdx(sdf_st);
    const float grad_len = length(grad);
    const float scale = 1.0 / grad_len;
    const float base = sdf_base_dev *
        (1.0 - (clamp(scale, sdf_base_min, sdf_base_max) - sdf_base_min) / (sdf_base_max - sdf_base_min));
    const float range = sdf_aa_factor * grad_len;
    return smoothstep(base - range, base + range, distance);
}

    #if TEXT_EFFECT == GALLERY_TEXT_EFFECT_SDF_LCD
void gallery_sdf_lcd(vec2 uv, vec4 sdf_st, vec4 paint, out vec4 out_color, out vec4 out_alpha)
{
    const vec2 grad = dFdx(sdf_st.xy);
    const vec2 offset = grad * sdf_st.zw;

    const vec4 red = texture(gTexture, uv - offset);
    const vec4 green = texture(gTexture, uv);
    const vec4 blue = texture(gTexture, uv + offset);
    const vec3 distance = sdf_scale * (vec3(red.r, green.r, blue.r) - sdf_bias);

    const float grad_len = length(grad);
    const float scale = 1.0 / grad_len;
    const float base = sdf_base_dev *
        (1.0 - (clamp(scale, sdf_base_min, sdf_base_max) - sdf_base_min) / (sdf_base_max - sdf_base_min));
    const float range = sdf_aa_factor * grad_len;
    const vec3 alpha = smoothstep(
        vec3(base - range, base - range, base - range),
        vec3(base + range, base + range, base + range),
        distance
    );

    out_color = vec4(paint.rgb * alpha.rgb, alpha.g);
    out_alpha = vec4(paint.a * alpha.rgb, alpha.g);
}
    #endif

    #if TEXT_EFFECT == GALLERY_TEXT_EFFECT_GRAY_LCD
void gallery_gray_lcd(vec2 uv, vec4 paint, out vec4 out_color, out vec4 out_alpha)
{
    const vec4 lcd_sample = texture(gTexture, uv);
    if (lcd_sample.a == 1.0)
    {
        out_color = vec4(paint.rgb * lcd_sample.rgb, lcd_sample.g);
        out_alpha = vec4(paint.a * lcd_sample.rgb, paint.a * lcd_sample.g);
    }
    else
    {
        out_color = vec4(0.0, 0.0, 0.0, 0.0);
        out_alpha = vec4(0.0, 0.0, 0.0, 0.0);
    }
}
    #endif
#endif
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
