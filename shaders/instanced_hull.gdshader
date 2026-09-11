shader_type spatial;
render_mode cull_disabled;

void fragment() {
	// Color and luminous parts are baked per vertex, so a whole hull is a single
	// opaque surface instead of a separate draw call for every small component.
	ALBEDO = COLOR.rgb;
	ROUGHNESS = UV2.x;
	METALLIC = UV2.y;
	EMISSION = COLOR.rgb * COLOR.a * 8.0;
}
