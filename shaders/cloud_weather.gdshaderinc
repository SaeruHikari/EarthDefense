// Height, visible clouds and their shadows share a single geographic wind field.
uniform sampler2D cloud_map : hint_default_black, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D cirrus_map : hint_default_black, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D wind_map : hint_default_black, filter_linear_mipmap, repeat_enable;
uniform float cloud_time = 0.0;
uniform int cloud_quality = 1;
uniform bool clouds_enabled = true;
uniform float high_cloud_amount = 1.0;
uniform float cloud_detail_amount = 1.0;
uniform float cloud_relief_amount = 0.0;
uniform float base_cloud_radius = 4.08;
uniform float height_range = 0.20;
uniform float high_cloud_radius = 4.32;

vec2 cloud_uv_clamped(vec2 uv) {
	return vec2(uv.x, clamp(uv.y, 0.001, 0.999));
}
vec2 cloud_direction_uv(vec3 direction) {
	vec3 n = normalize(direction);
	return vec2(atan(n.x, n.z) / 6.28318530718 + 0.5, acos(clamp(n.y, -1.0, 1.0)) / 3.14159265359);
}
vec2 cloud_advected_uv(vec2 geographic_uv, float layer) {
	float latitude = (0.5 - geographic_uv.y) * 3.14159265359;
	float polar = smoothstep(0.06, 0.26, cos(latitude));
	vec2 uv = geographic_uv;
	uv.x -= cloud_time * mix(0.000085, 0.000135, layer) * (0.88 + 0.20 * sin(latitude * 3.0));
	if (cloud_detail_amount > 0.001 && cloud_quality > 0) {
		vec4 wind = textureLod(wind_map, cloud_uv_clamped(geographic_uv), 1.0);
		// One moving coordinate preserves sharp cloud silhouettes. No blending
		// of displaced copies of coverage or height during a flow reset.
		float phase = cloud_time * 0.025 + wind.b * 6.28318530718;
		vec2 curl = vec2(sin(phase), cos(phase * 0.73 + wind.a * 2.0));
		uv -= (wind.rg * 2.0 - 1.0) * curl * mix(0.00095, 0.0016, layer) * polar * cloud_detail_amount;
	}
	return cloud_uv_clamped(uv);
}
vec4 cloud_layer_data(vec2 geographic_uv, float layer) {
	vec2 uv = cloud_advected_uv(geographic_uv, layer);
	if (layer < 0.5) { return texture(cloud_map, uv); }
	return texture(cirrus_map, uv);
}
vec4 cloud_layer_data_lod(vec2 geographic_uv, float layer, float mip_level) {
	vec2 uv = cloud_advected_uv(geographic_uv, layer);
	if (layer < 0.5) { return textureLod(cloud_map, uv, mip_level); }
	return textureLod(cirrus_map, uv, mip_level);
}
float cloud_top_radius(vec4 data, float layer) {
	return mix(base_cloud_radius + height_range * data.a, high_cloud_radius + 0.015 * data.a, layer);
}
float cloud_optical_depth(vec4 data, float layer) {
	float core = smoothstep(0.055, 0.23, data.r);
	return mix(core * data.r * mix(1.35, 3.85, smoothstep(0.18, 0.82, data.a)), smoothstep(0.20, 0.58, data.r) * data.r * 0.34, layer);
}
