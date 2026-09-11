shader_type spatial;
render_mode diffuse_burley, specular_disabled, cull_back, depth_draw_opaque;

uniform sampler2D cloud_map : source_color, hint_default_white, filter_linear_mipmap_anisotropic, repeat_enable;
uniform float simulation_time = 0.0;

void fragment() {
	float drift = simulation_time * 0.00009;
	vec2 uv = vec2(UV.x + drift, clamp(UV.y, 0.0003, 0.9997));
	vec3 deck = texture(cloud_map, uv).rgb;
	float signal = dot(deck, vec3(0.299, 0.587, 0.114));
	// The real UV cloud photograph carries the long, fine atmospheric streaks.
	// This deck is opaque: Venus does not expose its radar-mapped surface.
	vec3 cloud_color = mix(vec3(0.53, 0.365, 0.175), vec3(0.94, 0.83, 0.58), smoothstep(0.07, 0.90, signal));
	ALBEDO = mix(deck * vec3(1.04, 1.0, 0.93), cloud_color, 0.55);
	ROUGHNESS = 1.0;
	METALLIC = 0.0;
	float dx = dFdx(signal);
	float dy = dFdy(signal);
	vec3 qx = dFdx(VERTEX);
	vec3 qy = dFdy(VERTEX);
	vec3 rx = cross(qy, NORMAL);
	vec3 ry = cross(NORMAL, qx);
	float determinant = dot(qx, rx);
	vec3 gradient = (dx * rx + dy * ry) * sign(determinant) / max(abs(determinant), 0.000001);
	NORMAL = normalize(NORMAL - gradient * 0.016);
}
