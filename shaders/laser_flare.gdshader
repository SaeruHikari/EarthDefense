shader_type spatial;
render_mode ambient_light_disabled, specular_disabled, blend_add, cull_disabled, depth_draw_never, shadows_disabled;
varying vec4 beam_state;
void vertex() { beam_state = INSTANCE_CUSTOM; }
void fragment() {
    vec2 offset = UV * 2.0 - 1.0;
    float radius_squared = dot(offset, offset);
    float outer = exp(-radius_squared * 6.0) * (1.0 - smoothstep(0.6, 1.0, radius_squared));
    float core = exp(-radius_squared * 80.0);
    vec3 tint = clamp(beam_state.rgb, vec3(0.0), vec3(1.0));
    ALBEDO = vec3(0.0);
    EMISSION = tint * outer * 2.4 + mix(tint, vec3(1.0), 0.36) * core * 5.2;
    ALPHA = beam_state.a * (outer * 0.48 + core * 0.42);
}
