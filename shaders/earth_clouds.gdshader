shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_back, shadows_disabled;
#include "res://shaders/cloud_weather.gdshaderinc"
#include "res://shaders/earth_air.gdshaderinc"

uniform float planet_radius = 4.0;
uniform float cloud_radius = 4.11;
uniform float cloud_layer_kind = 0.0;
uniform vec3 cloud_night_ambient = vec3(0.032, 0.046, 0.064);
uniform float cloud_mesh_scale = 2.0;
uniform bool cloud_displacement_enabled = true;
uniform vec3 local_sun_direction = vec3(-0.53, 0.53, 0.66);
varying vec3 radial_local;
varying vec3 position_local;
varying vec3 position_world;
varying vec3 east_world;
varying vec3 south_world;
varying vec3 radial_world;

void vertex() {
	radial_local = normalize(VERTEX);
	vec4 shape = cloud_layer_data_lod(UV, cloud_layer_kind, 2.0);
	float radius = cloud_top_radius(shape, cloud_layer_kind);
	if (!cloud_displacement_enabled) { radius = mix(cloud_radius, high_cloud_radius, cloud_layer_kind); }
	VERTEX = radial_local * radius / cloud_mesh_scale;
	position_local = radial_local * radius;
	radial_world = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
	position_world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	float longitude = (UV.x - 0.5) * 6.28318530718;
	vec3 east = vec3(cos(longitude), 0.0, -sin(longitude));
	east_world = normalize((MODEL_MATRIX * vec4(east, 0.0)).xyz);
	south_world = normalize(cross(east_world, radial_world));
}
float cloud_self_occlusion(vec3 origin, vec3 sun, float probe_length) {
	vec3 probe = origin + sun * probe_length;
	vec4 ahead = cloud_layer_data(cloud_direction_uv(probe), 0.0);
	float height_excess = cloud_top_radius(ahead, 0.0) - length(probe);
	return smoothstep(0.006, 0.035, height_excess) * smoothstep(0.10, 0.55, ahead.r);
}
void fragment() {
	vec4 data = cloud_layer_data(UV, cloud_layer_kind);
	vec3 radial = normalize(radial_world);
	vec3 local_radial = normalize(radial_local);
	vec3 view = normalize(INV_VIEW_MATRIX[3].xyz - position_world);
	vec3 local_sun = normalize(local_sun_direction);
	vec3 sunlight = normalize((MODEL_MATRIX * vec4(local_sun, 0.0)).xyz);
	float radius = cloud_top_radius(data, cloud_layer_kind);
	vec2 gradient = data.gb * 2.0 - 1.0;
	float normal_strength = mix(height_range / base_cloud_radius * 16.0, 0.18, cloud_layer_kind);
	normal_strength *= mix(0.55, 1.0, cloud_detail_amount);
	vec3 normal = normalize(radial - (east_world * gradient.x + south_world * gradient.y) * normal_strength);
	float optical_depth = cloud_optical_depth(data, cloud_layer_kind);
	float sun_height = dot(local_radial, local_sun);
	float daylight = earth_air_sun_visibility(radius, sun_height, planet_radius);
	float direct = max(dot(normal, sunlight), 0.0);
	float occlusion = 0.0;
	float high_shade = 0.0;
	if (cloud_layer_kind < 0.5 && cloud_quality > 0 && data.r > 0.10 && daylight > 0.01) {
		vec3 top_position = local_radial * radius;
		float near_probe = cloud_self_occlusion(top_position, local_sun, 0.045);
		float far_probe = cloud_self_occlusion(top_position, local_sun, 0.115);
		occlusion = max(near_probe, far_probe * 0.78) * cloud_detail_amount;
		if (high_cloud_amount > 0.001) {
			float b = dot(top_position, local_sun);
			float distance_to_high = max(-b + sqrt(max(b * b - dot(top_position, top_position) + high_cloud_radius * high_cloud_radius, 0.0)), 0.0);
			vec4 high_data = cloud_layer_data(cloud_direction_uv(top_position + local_sun * distance_to_high), 1.0);
			high_shade = high_data.r * high_cloud_amount * 0.14;
		}
	}
	float direct_light = 0.76 * direct * (1.0 - occlusion * 0.72) * (1.0 - high_shade);
	vec3 sunrise_color = mix(vec3(1.0, 0.72, 0.49), vec3(0.96, 0.985, 1.0), smoothstep(-0.17, 0.24, sun_height));
	// Clouds are lit by direct sun and a restrained cool diffuse contribution.
	// Leave room for the changing slopes and sheltered valleys instead of pushing all cores to white.
	vec3 diffuse_sky = vec3(0.090, 0.115, 0.155) * (1.0 - occlusion * 0.32);
	vec3 day_color = sunrise_color * direct_light + diffuse_sky;
	day_color *= mix(1.0, 1.12, cloud_layer_kind);
	float sun_path = 1.0 / max(abs(sun_height), 0.24);
	float transmission = exp(-optical_depth * sun_path * 0.65);
	float forward_phase = 0.28 + 0.72 * pow(max(dot(-view, sunlight), 0.0), 4.0);
	float transmitted_light = (1.0 - exp(-optical_depth * sun_path)) * transmission * forward_phase * 0.34;
	day_color += sunrise_color * transmitted_light;
	vec3 color = mix(cloud_night_ambient * 1.06, day_color, daylight);
	float view_cosine = clamp(dot(radial, view), 0.0, 1.0);
	float grazing = pow(1.0 - view_cosine, 3.0);
	float silver = pow(max(dot(-view, sunlight), 0.0), 10.0) * (1.0 - data.r) * grazing * daylight * 0.12;
	color += vec3(0.80, 0.90, 1.0) * silver;
	// Only the air ABOVE this cloud lies in front of it. Lower clouds receive
	// slightly more blue aerial perspective; high cirrus remains distinct.
	float air_column = earth_air_column_above(max(radius - planet_radius, 0.0), air_profile_scale);
	float aerial = min(air_column * 2.8 / max(view_cosine, 0.10), 0.19);
	color = mix(color, air_rayleigh_tint * (0.28 + 0.50 * daylight), aerial * daylight);
	ALBEDO = color;
	float opacity = 1.0 - exp(-optical_depth * (1.0 + 0.22 * grazing));
	float cirrus_limb = smoothstep(0.035, 0.22, view_cosine);
	ALPHA = min(opacity, 0.985) * mix(1.0, high_cloud_amount * cirrus_limb, cloud_layer_kind) * float(clouds_enabled);
}
