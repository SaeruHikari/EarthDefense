shader_type spatial;
render_mode unshaded, blend_mix, cull_disabled, depth_draw_never, shadows_disabled;
uniform vec4 tint : source_color = vec4(0.6, 0.84, 0.88, 0.075);
varying vec3 world_position;
varying vec3 globe_center;
void vertex() {
    world_position = (MODEL_MATRIX * vec4(VERTEX,1.0)).xyz;
    globe_center = MODEL_MATRIX[3].xyz;
}
void fragment() {
    vec3 outward = normalize(world_position - globe_center);
    vec3 to_camera = normalize(INV_VIEW_MATRIX[3].xyz - world_position);
    float facing = smoothstep(0.02,0.30,dot(outward,to_camera));
    ALBEDO = tint.rgb;
    ALPHA = tint.a * facing;
}
