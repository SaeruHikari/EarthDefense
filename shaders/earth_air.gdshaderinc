// Shared density profile: ground atmosphere and the shorter air column above clouds.
// Density heights are fixed world-space altitudes, independent of sea-level radius.
uniform float air_profile_scale = 2.0;
uniform vec3 air_rayleigh_tint : source_color = vec3(0.18, 0.42, 0.78);
float earth_air_density(float height, float scale) {
	return exp(-height / (0.020 * scale)) + 0.105 * exp(-height / (0.115 * scale));
}
float earth_air_column_above(float height, float scale) {
	return 0.020 * scale * exp(-height / (0.020 * scale))
		+ 0.012075 * scale * exp(-height / (0.115 * scale));
}

float earth_air_sun_visibility(float radius, float solar_elevation, float ground_radius) {
	float night_elevation = min(solar_elevation, 0.0);
	float clearance = radius * sqrt(max(1.0 - night_elevation * night_elevation, 0.0)) - ground_radius;
	return smoothstep(-0.012, 0.022, clearance);
}
