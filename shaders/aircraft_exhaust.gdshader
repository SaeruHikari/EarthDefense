shader_type spatial;
render_mode unshaded, blend_mix, cull_back, depth_draw_never, shadows_disabled;

// Steady propulsion, independent of weapon charge and per-instance combat data.
// One open surface with bounded HDR radiance cannot add its back faces/cap into
// a white disk as the aircraft turns. The existing scene bloom stays enabled.
uniform vec4 energy_color : source_color = vec4(0.22, 0.84, 0.70, 1.0);
uniform float radiance : hint_range(0.0, 2.0) = 1.45;

void fragment() {
	// Godot CylinderMesh reserves v=0..0.5 for the side wall, tip to nozzle.
	float axial = clamp(UV.y * 2.0, 0.0, 1.0);
	float density = smoothstep(0.015, 0.88, axial);
	float edge = pow(clamp(abs(dot(normalize(NORMAL), normalize(VIEW))), 0.0, 1.0), 0.45);
	// Forward+ unshaded consumes ALBEDO; use one bounded radiance contribution.
	ALBEDO = energy_color.rgb * radiance;
	EMISSION = vec3(0.0);
	ALPHA = density * edge * 0.64 * energy_color.a;
}
