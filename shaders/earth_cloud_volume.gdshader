shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_back, shadows_disabled, fog_disabled;
#include "res://shaders/cloud_weather.gdshaderinc"
#include "res://shaders/cloud_volume_density.gdshaderinc"
#include "res://shaders/earth_air.gdshaderinc"

uniform float planet_radius = 4.0;
uniform float atmosphere_radius = 4.48;
uniform sampler2D scene_depth : hint_depth_texture, repeat_disable, filter_nearest;
uniform mat3 world_to_cloud = mat3(1.0);
uniform vec3 cloud_center = vec3(0.0);
uniform vec3 local_sun_direction = vec3(-0.53, 0.53, 0.66);
uniform int volume_steps = 40;
uniform int volume_shadow_steps = 4;
uniform float volume_sun_energy = 1.08;
uniform vec3 cloud_night_ambient = vec3(0.032, 0.046, 0.064);
varying vec3 proxy_world_position;

void vertex() {
	proxy_world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}
float volume_phase(float cosine, float anisotropy) {
	float square = anisotropy * anisotropy;
	return (1.0 - square) / pow(max(1.0 + square - 2.0 * anisotropy * cosine, 0.08), 1.5);
}
vec3 cloud_source_light(vec3 p, vec3 ray, vec3 sun, float weather_mip) {
	float radius = length(p);
	float sun_height = dot(p / radius, sun);
	float daylight = earth_air_sun_visibility(radius, sun_height, planet_radius);
	// A restrained blue-gray night floor keeps the unlit hemisphere readable.
	// Only the ambient term changes; sunlight, density and view integration do not.
	float height_fraction = clamp((radius - base_cloud_radius) / height_range, 0.0, 1.0);
	vec3 night_ambient = cloud_night_ambient * mix(0.82, 1.06, height_fraction);
	if (daylight < 0.001) { return night_ambient; }
	vec2 hit = volume_sphere_hit(p, sun, base_cloud_radius + height_range);
	float distance_to_sun = min(max(hit.y, 0.0), 1.40);
	int count = clamp(volume_shadow_steps, 1, 6);
	
	float optical = 0.0;
	for (int index = 0; index < 6; index++) {
		if (index >= count) { break; }
		float u0 = float(index) / float(count);
		float u1 = float(index + 1) / float(count);
		float near_t = u0 * u0 * distance_to_sun;
		float far_t = u1 * u1 * distance_to_sun;
		float light_step = far_t - near_t;
		vec3 probe = p + sun * ((near_t + far_t) * 0.5);
		optical += cloud_volume_density(probe, weather_mip + 1.3, 0.55) * light_step * volume_extinction;
	}
	float transmission = exp(-optical);
	// A bounded two-lobe phase retains both soft front-lit cloud faces and
	// brighter thin edges when looking toward the sun.
	float cosine = dot(ray, sun);
	float phase = clamp(0.78 * volume_phase(cosine, 0.40) + 0.22 * volume_phase(cosine, -0.30), 0.38, 2.4);
	float scattered_bounce = 0.18 * (1.0 - exp(-optical * 0.7));
	vec3 sun_color = mix(vec3(1.0, 0.58, 0.32), vec3(0.98, 0.985, 1.0), smoothstep(-0.16, 0.24, sun_height));
	vec3 ambient = mix(night_ambient, vec3(0.085, 0.12, 0.18) * mix(0.65, 1.0, height_fraction), daylight);
	return ambient + sun_color * phase * (transmission * 0.95 + scattered_bounce) * daylight * volume_sun_energy;
}
vec4 foreground_air(vec3 origin, vec3 ray, float cloud_distance, vec3 sun) {
	vec2 bounds = volume_sphere_hit(origin, ray, atmosphere_radius);
	float start = max(bounds.x, 0.0);
	float end = min(bounds.y, cloud_distance);
	if (end <= start) { return vec4(0.0, 0.0, 0.0, 1.0); }
	float step_length = (end - start) * 0.25;
	float optical = 0.0;
	float illuminated = 0.0;
	for (int i = 0; i < 4; i++) {
		vec3 p = origin + ray * (start + (float(i) + 0.5) * step_length);
		float radius = length(p);
		float density = earth_air_density(max(radius - planet_radius, 0.0), air_profile_scale);
		density *= 1.0 - smoothstep(atmosphere_radius - 0.07 * air_profile_scale, atmosphere_radius, radius);
		float light = earth_air_sun_visibility(radius, dot(p / radius, sun), planet_radius);
		optical += density * step_length / air_profile_scale;
		illuminated += density * step_length / air_profile_scale * light;
	}
	float daylight = illuminated / max(optical, 0.00001);
	float impact = length(cross(origin, ray));
	float horizon = pow(smoothstep(planet_radius - 0.40 * air_profile_scale, planet_radius + 0.004 * air_profile_scale, impact), 3.0);
	float alpha = (1.0 - exp(-optical * 0.88)) * mix(0.008, 1.0, horizon) * mix(0.42, 1.0, daylight);
	float phase = 0.75 * (1.0 + pow(dot(ray, sun), 2.0));
	float core_distance = (impact - planet_radius - 0.018 * air_profile_scale) / (0.018 * air_profile_scale);
	vec3 pale_core = vec3(0.38, 0.68, 1.0) * exp(-core_distance * core_distance) * pow(daylight, 1.5) * 1.55;
	vec3 scatter = mix(vec3(0.012, 0.036, 0.075), air_rayleigh_tint * phase, daylight) * 3.4 * min(alpha, 0.18) + pale_core * min(alpha, 0.18);
	return vec4(scatter, exp(-optical * 0.88));
}
void fragment() {
	if (!clouds_enabled || !cloud_volume_enabled || volume_extinction <= 0.00001) { discard; }
	vec3 camera_world = INV_VIEW_MATRIX[3].xyz;
	vec3 world_ray = normalize(proxy_world_position - camera_world);
	vec3 origin = world_to_cloud * (camera_world - cloud_center);
	vec3 ray = normalize(world_to_cloud * world_ray);
	vec3 sun = normalize(local_sun_direction);
	vec2 outer = volume_sphere_hit(origin, ray, base_cloud_radius + height_range);
	if (outer.y <= 0.0) { discard; }
	float scene_distance = outer.y;
	float depth = textureLod(scene_depth, SCREEN_UV, 0.0).r;
	if (depth > 0.0000001) {
		vec4 view_position = INV_PROJECTION_MATRIX * vec4(SCREEN_UV * 2.0 - 1.0, depth, 1.0);
		view_position.xyz /= view_position.w;
		scene_distance = min(scene_distance, length(view_position.xyz));
	}
	// The analytic solid Earth is only a fallback; actual opaque scene depth
	// above already includes mountains, hangars and individual aircraft.
	vec2 ground = volume_sphere_hit(origin, ray, planet_radius);
	if (ground.x > 0.0) { scene_distance = min(scene_distance, ground.x); }
	vec2 inner = volume_sphere_hit(origin, ray, base_cloud_radius);
	float a0 = max(outer.x, 0.0);
	float b0 = min(outer.y, scene_distance);
	float a1 = b0;
	float b1 = b0;
	if (inner.y > 0.0) {
		b0 = min(b0, inner.x);
		a1 = max(max(inner.y, outer.x), 0.0);
		b1 = min(outer.y, scene_distance);
	}
	float length0 = max(b0 - a0, 0.0);
	float length1 = max(b1 - a1, 0.0);
	float total_length = length0 + length1;
	if (total_length <= 0.00001) { discard; }
	// Short front-facing columns need fewer samples than long grazing rays.
	float target_step = (cloud_quality == 0) ? 0.012 : ((cloud_quality == 1) ? 0.006 : 0.004);
	int step_budget = clamp(volume_steps, 12, 96);
	if (cloud_quality > 0 && total_length > 0.45) { step_budget = max(step_budget, (total_length > 0.90) ? 96 : 64); }
	int steps = clamp(int(ceil(total_length / target_step)), 12, step_budget);
	float step_length = total_length / float(steps);
	float jitter = fract(52.9829189 * fract(dot(floor(FRAGCOORD.xy), vec2(0.06711056, 0.00583715)))) - 0.5;
	float weather_mip = (cloud_quality == 0) ? 2.0 : 0.0;
	float transmittance = 1.0;
	vec3 radiance = vec3(0.0);
	float depth_weighted = 0.0;
	float weight_sum = 0.0;
	vec3 source = vec3(0.0);
	float previous_light_t = -100.0;
	for (int index = 0; index < 96; index++) {
		if (index >= steps || transmittance < 0.012) { break; }
		float along = (float(index) + 0.5 + jitter * 0.40) * step_length;
		float t = (along < length0) ? (a0 + along) : (a1 + along - length0);
		vec3 p = origin + ray * t;
		float density = cloud_volume_density(p, weather_mip, 1.0);
		if (density < 0.0004) { continue; }
		if (t - previous_light_t > 0.012) {
			source = cloud_source_light(p, ray, sun, weather_mip);
			previous_light_t = t;
		}
		float alpha_step = 1.0 - exp(-volume_extinction * density * step_length);
		float weight = transmittance * alpha_step;
		radiance += source * weight;
		depth_weighted += t * weight;
		weight_sum += weight;
		transmittance *= 1.0 - alpha_step;
	}
	float alpha = 1.0 - transmittance;
	if (alpha < 0.001) { discard; }
	float effective_distance = depth_weighted / max(weight_sum, 0.00001);
	vec4 front_air = foreground_air(origin, ray, effective_distance, sun);
	// B already contains the existing full atmospheric pass. This compensates
	// only the front air lost when B is multiplied by cloud transmittance.
	ALBEDO = front_air.a * radiance / alpha + front_air.rgb;
	ALPHA = alpha;
}
