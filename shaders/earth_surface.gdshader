shader_type spatial;
render_mode diffuse_burley, specular_disabled, cull_back, depth_draw_opaque;

uniform sampler2D day_map : source_color, hint_default_white, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D normal_map : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D specular_map : hint_default_black, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D night_map : source_color, hint_default_black, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D height_map : hint_default_black, filter_linear_mipmap, repeat_enable;
#include "res://shaders/cloud_weather.gdshaderinc"
#include "res://shaders/cloud_volume_density.gdshaderinc"
uniform float planet_radius = 4.0;
// The cached terrain contains radius 4 and its original relief in geometry_scale units.
uniform float baked_planet_radius = 4.0;
uniform float geometry_scale = 2.0;
uniform float max_displacement = 0.048;
uniform float cloud_radius = 4.11;
uniform float cloud_angle = 0.0;
uniform float terrain_normal_strength = 0.70;
uniform float ocean_fresnel_strength = 0.34;
uniform float city_light_energy = 2.35;
uniform vec3 sun_direction = vec3(-0.53, 0.53, 0.66);
uniform vec3 local_sun_direction = vec3(-0.53, 0.53, 0.66);
varying vec3 local_position;

vec2 direction_uv(vec3 direction) {
	vec3 n = normalize(direction);
	return vec2(atan(n.x, n.z) / 6.28318530718 + 0.5, acos(clamp(n.y, -1.0, 1.0)) / 3.14159265359);
}

void vertex() {
	vec3 radial = normalize(VERTEX);
	float old_radius = length(VERTEX) * geometry_scale;
	float new_radius = old_radius + planet_radius - baked_planet_radius;
	VERTEX = radial * new_radius / geometry_scale;
	// Keep the baked normal's tangential slope consistent with unchanged elevation
	// spread over a larger surface. Geographic texture normals remain independent.
	vec3 radial_normal = radial * dot(NORMAL, radial);
	NORMAL = normalize(radial_normal + (NORMAL - radial_normal) * old_radius / max(new_radius, 0.001));
	local_position = radial * new_radius;
}

void fragment() {
	float polar_margin = 0.5 / float(textureSize(day_map, 0).y);
	vec2 map_uv = vec2(UV.x, clamp(UV.y, polar_margin, 1.0 - polar_margin));
	vec3 color = texture(day_map, map_uv).rgb;
	float water = smoothstep(0.12, 0.78, texture(specular_map, map_uv).r);
	float polar_ice = smoothstep(0.30, 0.72, min(color.r, min(color.g, color.b))) * smoothstep(0.65, 0.82, abs(normalize(local_position).y));
	water *= 1.0 - polar_ice;
	float solar_elevation = dot(normalize(local_position), local_sun_direction);
	vec3 local_sun = normalize(local_sun_direction);
	float cloud_shade = 0.0;
	if (clouds_enabled && cloud_volume_enabled && volume_extinction > 0.0 && solar_elevation > -0.02) {
		vec2 bottom_hit = volume_sphere_hit(local_position, local_sun, base_cloud_radius);
		vec2 top_hit = volume_sphere_hit(local_position, local_sun, base_cloud_radius + height_range);
		float start = max(bottom_hit.y, 0.0);
		float end = max(top_hit.y, start);
		float step_length = (end - start) * 0.25;
		float optical_depth = 0.0;
		// Integrate the actual 3D weather column; coarser samples keep ground shadows soft.
		for (int sample_index = 0; sample_index < 4; sample_index++) {
			vec3 sample_position = local_position + local_sun * (start + (float(sample_index) + 0.5) * step_length);
			optical_depth += cloud_volume_density(sample_position, 2.0, 0.50) * step_length * volume_extinction;
		}
		if (high_cloud_amount > 0.001 && cloud_quality > 0) {
			vec2 high_hit = volume_sphere_hit(local_position, local_sun, high_cloud_radius);
			vec4 high_data = cloud_layer_data(cloud_direction_uv(local_position + local_sun * max(high_hit.y, 0.0)), 1.0);
			optical_depth += cloud_optical_depth(high_data, 1.0) * 0.12 * high_cloud_amount;
		}
		cloud_shade = 0.34 * (1.0 - exp(-optical_depth * 0.46)) * smoothstep(-0.02, 0.18, solar_elevation);
	}
	// Retain the source's actual reef/shelf distribution rather than painting a
	// uniform ocean. The ocean is opaque; clear-water color comes from selective
	// blue absorption and shallows, never transparency or additive white light.
	float shallow_water = smoothstep(0.055, 0.18, color.g);
	vec3 deep_tint = vec3(0.008, 0.035, 0.108);
	vec3 shallow_tint = vec3(0.021, 0.170, 0.200);
	vec3 absorbed_water = color * mix(vec3(0.57, 0.92, 1.11), vec3(0.54, 1.09, 1.12), shallow_water);
	vec3 ocean_body = mix(absorbed_water, mix(deep_tint, shallow_tint, shallow_water), 0.22);
	ocean_body = min(ocean_body, vec3(0.22, 0.52, 0.70));
	// A broad atmospheric environment response follows the view angle only.
	// There is deliberately no direct solar specular lobe or clearcoat sparkle.
	float view_cosine = clamp(dot(normalize(NORMAL), normalize(VIEW)), 0.0, 1.0);
	float fresnel = 0.02 + ocean_fresnel_strength * pow(1.0 - view_cosine, 5.0);
	vec3 soft_environment = vec3(0.050, 0.120, 0.205);
	vec3 ocean_color = mix(ocean_body, soft_environment, fresnel);
	float elevation = clamp((length(local_position) - planet_radius) / max(max_displacement, 0.0001), 0.0, 1.0);
	vec3 land_color = color * mix(vec3(0.95, 0.985, 0.95), vec3(1.035, 1.025, 1.005), elevation);
	ALBEDO = mix(land_color, ocean_color, water) * (1.0 - cloud_shade);
	ROUGHNESS = mix(0.95, 0.72, water);
	METALLIC = 0.0;
	SPECULAR = 0.0;
	CLEARCOAT = 0.0;
	// The source stores an unnormalized tangent vector with a constant blue
	// channel; normalize it explicitly before feeding Godot's normal pipeline.
	vec3 detail_normal = normalize(texture(normal_map, map_uv).rgb * 2.0 - 1.0) * 0.5 + 0.5;
	vec2 water_ripple = vec2(sin(UV.x * 4300.0 + UV.y * 1700.0 + cloud_angle * 180.0), sin(UV.y * 3900.0 - UV.x * 2200.0 + cloud_angle * 220.0));
	detail_normal.xy += water_ripple * water * 0.006;
	NORMAL_MAP = detail_normal;
	NORMAL_MAP_DEPTH = mix(terrain_normal_strength, 0.06, water);
	vec3 city_color = texture(night_map, map_uv).rgb;
	float city_signal = max(city_color.r, max(city_color.g, city_color.b));
	float city_mask = smoothstep(0.035, 0.15, city_signal);
	float night_side = 1.0 - smoothstep(-0.18, 0.08, solar_elevation);
	EMISSION = city_color * vec3(1.0, 0.72, 0.40) * city_mask * night_side * city_light_energy;
}
