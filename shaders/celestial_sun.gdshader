shader_type spatial;
render_mode cull_back, depth_draw_opaque, shadows_disabled, ambient_light_disabled, specular_disabled;

uniform sampler2D photosphere_map : source_color, hint_default_white, filter_linear_mipmap_anisotropic, repeat_enable;
uniform float simulation_time = 0.0;
uniform float radiant_energy = 1.90;

void fragment() {
	vec2 uv = vec2(UV.x + simulation_time * 0.000025, clamp(UV.y, 0.0002, 0.9998));
	vec3 photo = texture(photosphere_map, uv).rgb;
	float heat = dot(photo, vec3(0.299, 0.587, 0.114));
	vec3 details = texture(photosphere_map, uv + vec2(0.21, 0.0)).rgb;
	float activity = dot(details, vec3(0.299, 0.587, 0.114));
	float facing = clamp(dot(normalize(NORMAL), normalize(VIEW)), 0.0, 1.0);
	float limb_darkening = 0.43 + 0.57 * pow(facing, 0.44);
	// Preserve the actual solar-map contrast through filmic tone mapping.
	vec3 photosphere = photo * (0.75 + heat * 1.10);
	photosphere += vec3(1.0, 0.30, 0.035) * pow(max(activity - 0.46, 0.0), 2.0) * 0.75;
	float slow_pulse = 1.0 + 0.018 * sin(simulation_time * 0.19 + UV.y * 17.0);
	ALBEDO = vec3(0.0);
	EMISSION = photosphere * radiant_energy * limb_darkening * slow_pulse;
}
