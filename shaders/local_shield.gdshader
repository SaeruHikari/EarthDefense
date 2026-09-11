shader_type spatial;
render_mode unshaded, blend_mix, cull_disabled, depth_draw_never, shadows_disabled, fog_disabled;
uniform float integrity : hint_range(0.0,1.0) = 1.0;
uniform float impact : hint_range(0.0,1.0) = 0.0;
uniform vec3 tint : source_color = vec3(0.28,0.84,0.94);
varying vec3 local_position;
varying vec3 world_position;
varying vec3 globe_center;

void vertex() {
    local_position = VERTEX;
    world_position = (MODEL_MATRIX * vec4(VERTEX,1.0)).xyz;
    globe_center = MODEL_MATRIX[3].xyz;
}
float hex_edge(vec2 p) {
    vec2 spacing = vec2(1.0,1.7320508);
    vec2 a = mod(p, spacing) - spacing * 0.5;
    vec2 b = mod(p - spacing * 0.5, spacing) - spacing * 0.5;
    vec2 cell = dot(a,a) < dot(b,b) ? a : b;
    cell = abs(cell);
    return abs(max(dot(cell,normalize(vec2(1.0,1.7320508))),cell.x)-0.5);
}
void fragment() {
    vec3 outward = normalize(world_position-globe_center);
    vec3 eye = normalize(INV_VIEW_MATRIX[3].xyz-world_position);
    float fresnel = pow(1.0-abs(dot(outward,eye)),2.6);
    float pixel = max(fwidth(UV.y),0.0001);
    float edge = smoothstep(1.0-pixel*2.5,1.0-pixel*0.4,UV.y);
    float hex = hex_edge(local_position.xz*2.6);
    float grid = 1.0-smoothstep(0.015,0.015+max(fwidth(hex),0.002),hex);
    float charged = smoothstep(0.0,0.16,integrity);
    float alpha = (0.010 + fresnel*0.12 + grid*0.022 + edge*0.25 + impact*(0.07+grid*0.14)) * charged;
    // A faint outline denotes a collapsed emitter without implying active coverage.
    alpha += edge*0.025*(1.0-charged);
    ALBEDO = mix(vec3(0.24,0.40,0.48),tint,charged);
    EMISSION = tint*(edge*0.30 + impact*0.7)*charged;
    ALPHA = clamp(alpha,0.0,0.48);
}
