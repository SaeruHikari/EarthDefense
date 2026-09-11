shader_type spatial;
render_mode blend_add, cull_back, depth_draw_never, shadows_disabled, ambient_light_disabled, specular_disabled;

#include "res://shaders/earth_air.gdshaderinc"

// Ground air is drawn before the clouds, whose shorter air columns are shaded separately.
uniform float planet_radius = 4.0;
uniform float hit_pulse : hint_range(0.0, 1.0) = 0.0;
// Zero retains the legacy, directionless global breathing effect.
uniform vec3 hit_direction_local = vec3(0.0);
uniform float hit_expansion = 0.035;
// Preserve the original world-space breathing displacement as the globe expands.
uniform float hit_expansion_reference_radius = 4.48;
uniform float atmosphere_radius = 4.48;
uniform float density_height = 0.040;
uniform vec3 sun_direction = vec3(-0.53, 0.53, 0.66);
uniform vec3 rayleigh_tint : source_color = vec3(0.19, 0.48, 0.82);
uniform float scattering_energy = 3.4;
uniform float density_strength = 0.88;
// RGB extinction follows the stronger scattering of short wavelengths.
uniform vec3 rayleigh_extinction = vec3(0.38, 0.82, 1.80);
varying vec3 world_vertex;
varying vec3 planet_center;
varying vec3 hit_direction_world;

void vertex() {
	VERTEX *= 1.0 + clamp(hit_pulse, 0.0, 1.0) * hit_expansion * hit_expansion_reference_radius / atmosphere_radius;
	world_vertex = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	planet_center = MODEL_MATRIX[3].xyz;
	hit_direction_world = mat3(MODEL_MATRIX) * hit_direction_local;
}

vec2 ray_sphere(vec3 origin, vec3 direction, float radius) {
	float b = dot(origin, direction);
	float c = dot(origin, origin) - radius * radius;
	float discriminant = b * b - c;
	if (discriminant < 0.0) { return vec2(-1.0); }
	float root = sqrt(max(discriminant, 0.0));
	return vec2(-b - root, -b + root);
}

// Curved exponential column approximation: plane-parallel near the zenith,
// Gaussian tangent column at the horizon. Negative mu includes the lower sun
// path through both sides of the tangent; Earth shadow is handled separately.
float solar_exponential_column(float radius, float height, float mu, float scale_height, float local_density) {
	float positive_mu = abs(mu);
	float denominator = 0.5 * positive_mu + sqrt(0.25 * positive_mu * positive_mu + 0.63661977 * scale_height / radius);
	float outward_column = local_density * scale_height / max(denominator, 0.001);
	if (mu >= 0.0) { return outward_column; }
	float tangent_radius = radius * sqrt(max(1.0 - mu * mu, 0.0));
	float tangent_height = max(height - radius + tangent_radius, 0.0);
	float tangent_column = exp(-tangent_height / scale_height) * sqrt(1.57079633 * max(tangent_radius, 0.001) * scale_height);
	return max(2.0 * tangent_column - outward_column, 0.0);
}
void fragment() {
	float pulse = clamp(hit_pulse, 0.0, 1.0);
	float shell_radius = atmosphere_radius + pulse * hit_expansion * hit_expansion_reference_radius;
	float height_stretch = 1.0 + pulse * 0.40;
	vec3 camera_position = INV_VIEW_MATRIX[3].xyz;
	vec3 origin = camera_position - planet_center;
	vec3 direction = normalize(world_vertex - camera_position);
	vec2 atmosphere_hit = ray_sphere(origin, direction, shell_radius);
	float entry = max(atmosphere_hit.x, 0.0);
	float exit_point = atmosphere_hit.y;
	vec2 ground_hit = ray_sphere(origin, direction, planet_radius);
	if (ground_hit.x > 0.0) { exit_point = min(exit_point, ground_hit.x); }
	float step_length = max(exit_point - entry, 0.0) / 12.0;
	vec3 sunlight = normalize(sun_direction);
	float world_scale = air_profile_scale;
	float optical_depth = 0.0;
	float illuminated_depth = 0.0;
	vec3 illuminated_scattering = vec3(0.0);
	for (int sample_index = 0; sample_index < 12; sample_index++) {
		float t = entry + (float(sample_index) + 0.5) * step_length;
		vec3 sample_position = origin + direction * t;
		float radius = length(sample_position);
		float height = max(radius - planet_radius, 0.0);
		float outer_fade = 1.0 - smoothstep(shell_radius - 0.07 * world_scale * height_stretch, shell_radius, radius);
		float lower_density = exp(-height / (0.020 * world_scale * height_stretch));
		float upper_density = exp(-height / (0.115 * world_scale * height_stretch));
		float density = (lower_density + 0.105 * upper_density) * outer_fade;
		float solar_elevation = dot(normalize(sample_position), sunlight);
		float daylight = earth_air_sun_visibility(radius, solar_elevation, planet_radius);
		float sample_depth = density * step_length / world_scale;
		float sun_depth = (solar_exponential_column(radius, height, solar_elevation, 0.020 * world_scale * height_stretch, lower_density)
			+ 0.105 * solar_exponential_column(radius, height, solar_elevation, 0.115 * world_scale * height_stretch, upper_density)) / world_scale;
		// Beer-Lambert extinction on both the incoming sun path and the partial
		// camera path yields blue daylight and a softer, warmer terminator.
		vec3 transmittance = exp(-rayleigh_extinction * (sun_depth + optical_depth + 0.5 * sample_depth));
		illuminated_scattering += sample_depth * daylight * transmittance;
		optical_depth += sample_depth;
		illuminated_depth += sample_depth * daylight;
	}
	float daylight_fraction = illuminated_depth / max(optical_depth, 0.00001);
	float phase = 0.75 * (1.0 + pow(dot(direction, sunlight), 2.0));
	float impact_radius = length(cross(origin, direction));
	// Almost no frontal veiling. The highest density is compressed at the limb.
	float horizon_weight = pow(smoothstep(planet_radius - 0.40 * world_scale, planet_radius + 0.004 * world_scale, impact_radius), 3.0);
	float opacity = (1.0 - exp(-optical_depth * density_strength)) * mix(0.008, 1.0, horizon_weight);
	opacity *= mix(0.42, 1.0, daylight_fraction);
	// A narrow, pale sunrise core follows the horizon, never the shell boundary.
	float core_distance = (impact_radius - (planet_radius + (0.018 + pulse * 0.04) * world_scale)) / (0.018 * world_scale);
	float horizon_core = exp(-core_distance * core_distance);
	vec3 night_tint = vec3(0.012, 0.036, 0.075);
	vec3 spectral_scattering = illuminated_scattering / max(optical_depth, 0.00001);
	vec3 scatter_color = night_tint * (1.0 - daylight_fraction) + air_rayleigh_tint * phase * spectral_scattering;
	vec3 pale_core = vec3(0.38, 0.68, 1.0) * horizon_core * pow(daylight_fraction, 1.5) * 1.55 * sqrt(max(spectral_scattering, vec3(0.0)));
	ALBEDO = vec3(0.0);
	vec3 normal_emission = scatter_color * scattering_energy + pale_core;
	float flash_weight = smoothstep(0.0, 0.5, pulse) * 0.92;
	vec3 impact_emission = vec3(3.0, 0.16, 0.065) * (0.8 + 0.2 * phase);
	// Evaluate one spherical direction per fragment, reusing the existing ray
	// intersection. The twelve atmospheric integration samples stay unchanged.
	float local_pulse = 0.0;
	if (dot(hit_direction_world, hit_direction_world) > 0.0001 && pulse > 0.0) {
		float surface_t = ground_hit.x > 0.0 ? ground_hit.x : clamp(-dot(origin, direction), entry, exit_point);
		vec3 surface_direction = normalize(origin + direction * surface_t);
		float alignment = dot(surface_direction, normalize(hit_direction_world));
		float broad = smoothstep(0.258819, 0.984808, alignment);
		float core = smoothstep(0.939693, 1.0, alignment);
		local_pulse = pulse * (0.7 * broad * broad + 0.3 * core);
	}
	// Distant regions retain their existing red breathing intensity. The impact
	// adds a red-only crest and a light veil instead of an overexposed white disk.
	EMISSION = mix(normal_emission, impact_emission, flash_weight) + vec3(1.25, 0.035, 0.014) * local_pulse;
	// A small frontal floor keeps the actual impact legible even where ordinary
	// Rayleigh air intentionally has almost no frontal opacity. It is local only.
	float local_opacity = local_pulse * 0.22 * mix(0.65, 1.0, sqrt(clamp(optical_depth * density_strength * 3.0, 0.0, 1.0)));
	ALPHA = clamp(opacity * (1.0 + pulse * 0.4) + local_opacity, 0.0, 0.18 + pulse * 0.06);
}
