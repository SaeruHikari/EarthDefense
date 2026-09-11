shader_type spatial;
render_mode blend_add, cull_disabled, depth_draw_never, shadows_disabled, ambient_light_disabled, specular_disabled;

uniform vec4 energy_color : source_color = vec4(0.2, 0.9, 0.75, 1.0);
uniform float energy = 7.0;

void fragment() {
	// Cylinder UV.y runs from the narrow rear tip to the engine nozzle.
	float density = smoothstep(0.02, 0.85, UV.y);
	ALBEDO = vec3(0.0);
	EMISSION = energy_color.rgb * energy;
	ALPHA = density * 0.8;
}
