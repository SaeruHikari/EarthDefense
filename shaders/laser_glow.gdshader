shader_type spatial;
render_mode ambient_light_disabled, specular_disabled, blend_add, cull_disabled, depth_draw_never, shadows_disabled;
varying vec4 beam_state;
void vertex() { beam_state = INSTANCE_CUSTOM; }
void fragment() {
    float across = abs(UV.x * 2.0 - 1.0);
    float halo = exp(-across * across * 4.2) * (1.0 - smoothstep(0.78, 1.0, across));
    float core = exp(-across * across * 72.0);
    float ends = smoothstep(0.0, 0.025, UV.y) * smoothstep(0.0, 0.025, 1.0 - UV.y);
    vec3 tint = clamp(beam_state.rgb, vec3(0.0), vec3(1.0));
    vec3 bright_core = mix(tint, vec3(1.0), 0.32);
    ALBEDO = vec3(0.0);
    EMISSION = tint * halo * 2.0 + bright_core * core * 5.8;
    ALPHA = beam_state.a * ends * (halo * 0.22 + core * 0.78);
}
