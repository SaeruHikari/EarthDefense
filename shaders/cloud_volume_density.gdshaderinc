// Shared 3D density: visible volume and solar cloud shadows evaluate identical weather.
uniform sampler3D volume_noise : filter_linear, repeat_enable;
uniform bool cloud_volume_enabled = true;
// Lowered from 24.0 so the volume reads as translucent haze rather than a solid shell.
uniform float volume_extinction = 14.0;
// The packed noise field folds the generator's erosion and fine detail channels into one
// 8-bit channel; this is the combined weight the shader used to apply to them separately.
uniform float volume_noise_erosion_gain = 0.047;
uniform float volume_noise_scale = 1.05;
// Preserve noise in geographic coordinates when only the solid planet radius changes.
uniform float volume_reference_base_radius = 4.08;

vec3 volume_direction_from_uv(vec2 uv) {
	float longitude = (uv.x - 0.5) * 6.28318530718;
	float colatitude = uv.y * 3.14159265359;
	float ring = sin(colatitude);
	return vec3(sin(longitude) * ring, cos(colatitude), cos(longitude) * ring);
}
// The coarse altitude channel varies slowly along a ray, so the volume march keeps one
// sample for several steps: the caller passes a cache and a negative value forces a refresh.
// Shadow probes keep the original per-evaluation fetch by calling the wrapper below.
float cloud_volume_density_cached(vec3 position, float weather_mip, float detail_weight, inout float altitude_cache) {
	float radius = length(position);
	if (radius <= base_cloud_radius || radius >= base_cloud_radius + height_range) { return 0.0; }
	vec2 uv = cloud_advected_uv(cloud_direction_uv(position), 0.0);
	// Retain the original high-resolution weather silhouette. The coarse
	// channel controls altitude only; it must not blur or fill cloud filaments.
	vec4 weather = textureLod(cloud_map, uv, weather_mip);
	float coverage = weather.r * smoothstep(0.055, 0.23, weather.r);
	if (coverage < 0.008) { return 0.0; }
	if (altitude_cache < 0.0) { altitude_cache = textureLod(cloud_map, uv, max(weather_mip, 2.0)).a; }
	float top_altitude = altitude_cache;
	float noise_radius = radius - base_cloud_radius + volume_reference_base_radius;
	vec3 moving_position = volume_direction_from_uv(uv) * noise_radius;
	vec3 coordinate = moving_position * volume_noise_scale + vec3(0.0, cloud_time * 0.00028, 0.0);
	vec2 shape = textureLod(volume_noise, coordinate, 0.0).rg;
	// Stratiform volume: a thin lifted ribbon follows the original cloud top;
	// only the dense parts deepen. Every ribbon still has a finite 3D interior.
	float top = base_cloud_radius + height_range * max(top_altitude, 0.15);
	float thickness = mix(0.018, 0.028, smoothstep(0.25, 0.80, coverage)) + 0.055 * coverage * smoothstep(0.25, 0.82, top_altitude);
	top += (shape.r - 0.5) * 0.008 * coverage;
	float bottom = max(base_cloud_radius + 0.002, top - thickness);
	thickness = max(top - bottom, 0.012);
	float h = (radius - bottom) / thickness;
	if (h <= 0.0 || h >= 1.0) { return 0.0; }
	float profile = smoothstep(0.0, 0.20, h) * (1.0 - smoothstep(0.65, 1.0, h));
	float modulation = mix(0.74, 1.12, shape.r);
	float edge = 1.0 - smoothstep(0.18, 0.65, coverage);
	float erosion = shape.g * volume_noise_erosion_gain * edge * detail_weight;
	// Normalize the column mass as thickness changes: thin wisps remain visible
	// without turning all moderate coverage into fully opaque white paint.
	float column = max(coverage - erosion, 0.0) * mix(0.045, 0.10, smoothstep(0.22, 0.80, top_altitude));
	return column / thickness * profile * modulation;
}
vec2 volume_sphere_hit(vec3 origin, vec3 direction, float radius) {
	float b = dot(origin, direction);
	float discriminant = b * b - dot(origin, origin) + radius * radius;
	if (discriminant < 0.0) { return vec2(-1.0); }
	float root = sqrt(discriminant);
	return vec2(-b - root, -b + root);
}
float cloud_volume_density(vec3 position, float weather_mip, float detail_weight) {
	float altitude_cache = -1.0;
	return cloud_volume_density_cached(position, weather_mip, detail_weight, altitude_cache);
}
