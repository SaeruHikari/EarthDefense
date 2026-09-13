#version 450
#define BACKDROP_EFFECT_EPSILON 0.0001
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
float saturate(float v){return clamp(v,0.0,1.0);}
vec2 saturate(vec2 v){return clamp(v,vec2(0),vec2(1));}
vec3 saturate(vec3 v){return clamp(v,vec3(0),vec3(1));}
vec4 saturate(vec4 v){return clamp(v,vec4(0),vec4(1));}
layout(set=0,binding=0) uniform sampler2D gTexture;
layout(location=0) in vec2 in_uv;
layout(location=1) in vec2 in_position_logic;
layout(location=2) in vec4 in_color;
layout(location=0) out vec4 out_color;
vec2 safe_normalize(vec2 value, vec2 fallback)
{
    const float length_sq = dot(value, value);
    if (length_sq <= 0.000001)
    {
        return fallback;
    }
    return value / sqrt(length_sq);
}

float backdrop_luminance(vec3 color)
{
    return dot(color, vec3(0.2126, 0.7152, 0.0722));
}

bool backdrop_is_light_enabled(vec4 light)
{
    return light.a > BACKDROP_EFFECT_EPSILON && backdrop_luminance(light.rgb) > BACKDROP_EFFECT_EPSILON;
}

bool backdrop_is_grain_enabled(float grain)
{
    return grain > BACKDROP_EFFECT_EPSILON;
}

bool backdrop_is_refraction_enabled(float refraction)
{
    return refraction > BACKDROP_EFFECT_EPSILON;
}

bool backdrop_is_glass_enabled(vec4 glass, float transmittance)
{
    const float tint_delta = max(max(abs(glass.r - 1.0), abs(glass.g - 1.0)), abs(glass.b - 1.0));
    return transmittance < 1.0 - BACKDROP_EFFECT_EPSILON || tint_delta > BACKDROP_EFFECT_EPSILON;
}

float backdrop_smootherstep(float value)
{
    const float t = saturate(value);
    return t * t * t * (t * (t * 6.0 - 15.0) + 10.0);
}

vec2 corner_normal(vec2 shape_point, vec2 center, vec2 radius)
{
    const vec2 safe_radius = max(radius, vec2(1.0, 1.0));
    return safe_normalize((shape_point - center) / safe_radius, vec2(0.0, -1.0));
}

float corner_distance(vec2 shape_point, vec2 center, vec2 radius)
{
    const vec2 safe_radius = max(radius, vec2(1.0, 1.0));
    const float scale = min(safe_radius.x, safe_radius.y);
    return abs(length((shape_point - center) / safe_radius) - 1.0) * scale;
}

vec2 rect_edge_normal(vec2 shape_point, vec2 size)
{
    const float left = shape_point.x;
    const float top = shape_point.y;
    const float right = size.x - shape_point.x;
    const float bottom = size.y - shape_point.y;
    vec2 normal = vec2(-1.0, 0.0);
    float distance = left;
    if (top < distance)
    {
        distance = top;
        normal = vec2(0.0, -1.0);
    }
    if (right < distance)
    {
        distance = right;
        normal = vec2(1.0, 0.0);
    }
    if (bottom < distance)
    {
        normal = vec2(0.0, 1.0);
    }
    return normal;
}

float rect_edge_distance(vec2 shape_point, vec2 size)
{
    return min(min(shape_point.x, shape_point.y), min(size.x - shape_point.x, size.y - shape_point.y));
}

void backdrop_shape_info(vec2 uv, out float edge_distance, out vec2 edge_normal)
{
    const vec2 size = max(pc.shape_size_logic, vec2(1.0, 1.0));
    const vec2 shape_point = saturate(uv) * size;
    edge_distance = rect_edge_distance(shape_point, size);
    edge_normal = rect_edge_normal(shape_point, size);

    const vec2 tl = pc.radius_top_left_logic;
    if (tl.x > 0.0 && tl.y > 0.0 && shape_point.x < tl.x && shape_point.y < tl.y)
    {
        const vec2 center = tl;
        edge_distance = corner_distance(shape_point, center, tl);
        edge_normal = corner_normal(shape_point, center, tl);
        return;
    }

    const vec2 tr = pc.radius_top_right_logic;
    if (tr.x > 0.0 && tr.y > 0.0 && shape_point.x > size.x - tr.x && shape_point.y < tr.y)
    {
        const vec2 center = vec2(size.x - tr.x, tr.y);
        edge_distance = corner_distance(shape_point, center, tr);
        edge_normal = corner_normal(shape_point, center, tr);
        return;
    }

    const vec2 br = pc.radius_bottom_right_logic;
    if (br.x > 0.0 && br.y > 0.0 && shape_point.x > size.x - br.x && shape_point.y > size.y - br.y)
    {
        const vec2 center = size - br;
        edge_distance = corner_distance(shape_point, center, br);
        edge_normal = corner_normal(shape_point, center, br);
        return;
    }

    const vec2 bl = pc.radius_bottom_left_logic;
    if (bl.x > 0.0 && bl.y > 0.0 && shape_point.x < bl.x && shape_point.y > size.y - bl.y)
    {
        const vec2 center = vec2(bl.x, size.y - bl.y);
        edge_distance = corner_distance(shape_point, center, bl);
        edge_normal = corner_normal(shape_point, center, bl);
    }
}

vec3 backdrop_edge_modulation(vec2 uv)
{
    const vec4 light = saturate(pc.light_color);
    if (!backdrop_is_light_enabled(light))
    {
        return vec3(0.0, 0.0, 0.0);
    }

    float edge_distance = 0.0;
    vec2 edge_normal = vec2(0.0, -1.0);
    backdrop_shape_info(uv, edge_distance, edge_normal);

    const vec2 light_direction = safe_normalize(
        pc.light_direction,
        vec2(-0.70710677, -0.70710677)
    );
    const float edge = (1.0 - smoothstep(0.0, 5.0, edge_distance)) * 0.78;
    const float facing = saturate(dot(edge_normal, light_direction));
    const float opposite = saturate(-dot(edge_normal, light_direction));
    const float luminance = backdrop_luminance(light.rgb);
    const float shadow = luminance * light.a * edge * opposite * 0.58;
    return light.rgb * light.a * edge * facing - vec3(shadow, shadow, shadow);
}

vec4 backdrop_apply_glass(vec4 blurred_color, vec4 glass, float transmittance)
{
    const float opacity = saturate(blurred_color.a);
    const vec3 transmitted = blurred_color.rgb * glass.rgb;
    const vec3 solid = glass.rgb * glass.a * opacity;

    blurred_color.rgb = mix(solid, transmitted, transmittance);
    blurred_color.a = max(blurred_color.a, opacity * glass.a * (1.0 - transmittance));
    return blurred_color;
}

float backdrop_grain_hash(vec2 value)
{
    return fract(sin(dot(value, vec2(127.1, 311.7))) * 43758.5453123);
}

float backdrop_grain_noise(vec2 value)
{
    const vec2 cell = floor(value);
    const vec2 local = fract(value);
    const vec2 weight = local * local * (3.0 - 2.0 * local);

    const float v00 = backdrop_grain_hash(cell + vec2(0.0, 0.0));
    const float v10 = backdrop_grain_hash(cell + vec2(1.0, 0.0));
    const float v01 = backdrop_grain_hash(cell + vec2(0.0, 1.0));
    const float v11 = backdrop_grain_hash(cell + vec2(1.0, 1.0));
    const float vx0 = mix(v00, v10, weight.x);
    const float vx1 = mix(v01, v11, weight.x);
    return mix(vx0, vx1, weight.y);
}

float backdrop_grain_sand(vec2 uv)
{
    const vec2 size = max(pc.shape_size_logic, vec2(1.0, 1.0));
    const vec2 shape_point = saturate(uv) * size;
    const float grain_hash = backdrop_grain_hash(floor(shape_point * 0.95) + vec2(23.4, 7.6));
    const float fine_noise = backdrop_grain_noise(shape_point / 2.0 + vec2(58.2, 31.9));
    const float sub_noise = backdrop_grain_noise(shape_point / 3.0 + vec2(11.7, 87.5));
    return saturate(grain_hash * 0.50 + fine_noise * 0.34 + sub_noise * 0.16);
}

vec2 backdrop_grain_offset(vec2 uv)
{
    const float grain = saturate(pc.glass_grain);
    if (!backdrop_is_grain_enabled(grain))
    {
        return vec2(0.0, 0.0);
    }

    const vec2 size = max(pc.shape_size_logic, vec2(1.0, 1.0));
    const vec2 shape_point = saturate(uv) * size;
    const vec2 sand_cell = floor(shape_point * 0.9);
    const vec2 sand_noise = vec2(
                                  backdrop_grain_hash(sand_cell + vec2(13.7, 5.1)),
                                  backdrop_grain_hash(sand_cell + vec2(47.2, 19.4))
                              ) *
            2.0 -
        1.0;
    const vec2 fine_noise = vec2(
                                  backdrop_grain_noise(shape_point / 2.0 + vec2(91.3, 23.6)),
                                  backdrop_grain_noise(shape_point / 2.0 + vec2(8.4, 63.1))
                              ) *
            2.0 -
        1.0;

    return (sand_noise * 0.62 + fine_noise * 0.38) * grain * 0.9;
}

vec2 backdrop_safe_sample_uv(vec2 uv)
{
    const vec2 margin = min(pc.sample_uv_margin, vec2(0.45, 0.45));
    return clamp(uv, margin, vec2(1.0, 1.0) - margin);
}

float backdrop_sdf_lens_offset(float shape_sdf)
{
    const float distance = max(shape_sdf, 0.0);
    const float edge_width = 46.0;
    const float falloff = 1.0 - backdrop_smootherstep(distance / edge_width);
    return falloff * (14.0 + falloff * 8.0);
}

vec2 backdrop_refraction_offset(vec2 uv)
{
    const float refraction = saturate(pc.glass_refraction);
    if (!backdrop_is_refraction_enabled(refraction))
    {
        return vec2(0.0, 0.0);
    }

    float edge_distance = 0.0;
    vec2 edge_normal = vec2(0.0, -1.0);
    backdrop_shape_info(uv, edge_distance, edge_normal);

    const float offset_pixels = backdrop_sdf_lens_offset(edge_distance) * refraction;
    return edge_normal * offset_pixels;
}

vec4 backdrop_sample_refracted(vec2 sample_uv, vec2 refraction_uv)
{
    const vec2 texture_uv_scale = pc.texture_uv_scale;
    const vec2 center_uv = backdrop_safe_sample_uv(sample_uv);
    const vec4 center = texture(gTexture, center_uv * texture_uv_scale);

    const vec2 red_uv = backdrop_safe_sample_uv(sample_uv + refraction_uv * 0.24);
    const vec2 blue_uv = backdrop_safe_sample_uv(sample_uv - refraction_uv * 0.18);
    const vec4 red = texture(gTexture, red_uv * texture_uv_scale);
    const vec4 blue = texture(gTexture, blue_uv * texture_uv_scale);

    return vec4(
        red.r,
        center.g,
        blue.b,
        max(center.a, max(red.a, blue.a))
    );
}

vec3 backdrop_refraction_light_modulation(vec2 uv)
{
    const float refraction = saturate(pc.glass_refraction);
    const vec4 light = saturate(pc.light_color);
    if (!backdrop_is_refraction_enabled(refraction) || !backdrop_is_light_enabled(light))
    {
        return vec3(0.0, 0.0, 0.0);
    }

    float edge_distance = 0.0;
    vec2 edge_normal = vec2(0.0, -1.0);
    backdrop_shape_info(uv, edge_distance, edge_normal);

    const vec2 light_direction = safe_normalize(
        pc.light_direction,
        vec2(-0.70710677, -0.70710677)
    );
    const float edge = 1.0 - smoothstep(0.0, 16.0, edge_distance);
    const float facing = saturate(dot(edge_normal, light_direction) * 0.5 + 0.5);
    const float luminance = backdrop_luminance(light.rgb);
    const vec3 chroma = light.rgb - vec3(luminance, luminance, luminance);
    return chroma * edge * facing * light.a * refraction * 0.26;
}

vec3 backdrop_grain_light_modulation(vec2 uv)
{
    const float grain = saturate(pc.glass_grain);
    const vec4 light = saturate(pc.light_color);
    if (!backdrop_is_grain_enabled(grain) || !backdrop_is_light_enabled(light))
    {
        return vec3(0.0, 0.0, 0.0);
    }

    const vec2 size = max(pc.shape_size_logic, vec2(1.0, 1.0));
    const vec2 light_direction = safe_normalize(
        pc.light_direction,
        vec2(-0.70710677, -0.70710677)
    );
    const vec2 sample_step = light_direction * 2.0 / size;
    const float center = backdrop_grain_sand(uv);
    const float lit_side = backdrop_grain_sand(uv - sample_step);
    const float shadow_side = backdrop_grain_sand(uv + sample_step);
    const float slope = lit_side - shadow_side;
    const float sparkle = saturate((center - 0.58) * 2.4);
    const float highlight = saturate(slope * 6.0) * 0.048 + sparkle * 0.018;
    const float shadow = saturate(-slope * 6.0) * 0.030;
    const float strength = grain * light.a;
    const float luminance = backdrop_luminance(light.rgb);

    return light.rgb * strength * highlight - vec3(luminance * strength * shadow, luminance * strength * shadow, luminance * strength * shadow);
}


void main()
{
    const vec4 light = saturate(pc.light_color);
    const vec4 glass = saturate(pc.glass_color);
    const float transmittance = saturate(pc.glass_transmittance);
    const float grain = saturate(pc.glass_grain);
    const float refraction = saturate(pc.glass_refraction);
    const bool is_light_enabled = backdrop_is_light_enabled(light);
    const bool is_grain_enabled = backdrop_is_grain_enabled(grain);
    const bool is_refraction_enabled = backdrop_is_refraction_enabled(refraction);

    const vec2 local_uv = saturate(
        (in_position_logic - pc.backdrop_origin_logic) /
        max(pc.backdrop_size_logic, vec2(1.0, 1.0))
    );
    vec2 grain_uv = vec2(0.0, 0.0);
    if (is_grain_enabled)
    {
        grain_uv = backdrop_grain_offset(in_uv) / max(pc.backdrop_size_logic, vec2(1.0, 1.0));
    }

    const vec2 grain_sample_uv = backdrop_safe_sample_uv(local_uv + grain_uv);
    vec4 color = in_color * texture(gTexture, grain_sample_uv * pc.texture_uv_scale);
    if (is_refraction_enabled)
    {
        const vec2 refraction_uv = backdrop_refraction_offset(in_uv) / max(pc.backdrop_size_logic, vec2(1.0, 1.0));
        const vec2 sample_uv = backdrop_safe_sample_uv(local_uv + grain_uv + refraction_uv);
        color = in_color * backdrop_sample_refracted(sample_uv, refraction_uv);
    }

    if (backdrop_is_glass_enabled(glass, transmittance))
    {
        color = backdrop_apply_glass(color, glass, transmittance);
    }

    if (is_light_enabled)
    {
        vec3 modulation = backdrop_edge_modulation(in_uv);
        if (is_grain_enabled)
        {
            modulation += backdrop_grain_light_modulation(in_uv);
        }
        if (is_refraction_enabled)
        {
            modulation += backdrop_refraction_light_modulation(in_uv);
        }
        color.rgb = max(color.rgb + modulation * in_color.a, vec3(0.0, 0.0, 0.0));
    }
    out_color=color;
}
