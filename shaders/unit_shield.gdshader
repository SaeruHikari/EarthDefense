shader_type spatial;
render_mode ambient_light_disabled, specular_disabled, blend_add, cull_back, depth_draw_never, shadows_disabled;

varying float integrity;
varying float friendly;
void vertex() {
    integrity = clamp(INSTANCE_CUSTOM.r, 0.0, 1.0);
    friendly = INSTANCE_CUSTOM.g;
}
void fragment() {
    float rim = pow(1.0 - clamp(dot(normalize(NORMAL), normalize(VIEW)), 0.0, 1.0), 2.6);
    vec3 tint = mix(vec3(0.36, 0.55, 1.0), vec3(0.13, 0.88, 0.76), friendly);
    // Both radiance and opacity respond continuously to the real shield fraction.
    // No time flicker, sampling, per-unit light, or persistent zero-shield instance.
    ALBEDO = vec3(0.0);
    EMISSION = tint * (0.70 + 0.70 * integrity) * (0.25 + 1.5 * rim);
    ALPHA = integrity * (0.055 + 0.42 * rim);
}
