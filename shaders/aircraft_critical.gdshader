shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled, shadows_disabled;
// Low-health signal is evaluated per aircraft once in the existing render feed.
// The GPU pulse avoids per-aircraft timers, materials, labels, or draw callbacks.
void fragment() {
	float pulse = smoothstep(0.05, 0.75, sin(TIME * 10.05309649));
	ALBEDO = vec3(1.0, 0.025, 0.012);
	EMISSION = vec3(0.65, 0.01, 0.005);
	ALPHA = 0.82 * pulse;
}
