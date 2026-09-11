shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx, cull_back, depth_draw_opaque;

uniform sampler2D surface_map : source_color, hint_default_white, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D height_map : hint_default_black, filter_linear_mipmap_anisotropic, repeat_enable;
uniform bool use_lola_height = false;
uniform vec4 surface_tint : source_color = vec4(1.0);
uniform float surface_roughness = 0.94;
uniform float relief_strength = 1.4;

float lola_height(float sample_value) {
	// NASA LRO LOLA: unsigned sample, 0.5 m units, zero datum 20000;
	// ratio to the 1737400 m lunar datum works with every world radius.
	return (sample_value * 65535.0 - 20000.0) * (0.5 / 1737400.0);
}

void vertex() {
	if (use_lola_height) {
		float altitude = lola_height(textureLod(height_map, UV, 0.0).r);
		VERTEX *= 1.0 + altitude;
	}
}

void fragment() {
	vec2 texel = 1.0 / vec2(textureSize(surface_map, 0));
	vec2 uv = vec2(UV.x, clamp(UV.y, texel.y, 1.0 - texel.y));
	vec3 terrain = texture(surface_map, uv).rgb;
	float luminance = dot(terrain, vec3(0.2126, 0.7152, 0.0722));
	vec2 footprint = max(fwidth(uv), texel * 1.5);
	footprint.x = min(footprint.x, 0.01);
	vec2 slope = vec2(0.0);
	if (use_lola_height) {
		footprint = max(footprint, 1.0 / vec2(textureSize(height_map, 0)));
		float left = lola_height(texture(height_map, uv - vec2(footprint.x, 0.0)).r);
		float right = lola_height(texture(height_map, uv + vec2(footprint.x, 0.0)).r);
		float up = lola_height(texture(height_map, uv - vec2(0.0, footprint.y)).r);
		float down = lola_height(texture(height_map, uv + vec2(0.0, footprint.y)).r);
		float latitude_scale = max(sin(uv.y * 3.14159265), 0.08);
		slope = vec2((left - right) / (footprint.x * 12.5663706 * latitude_scale), (down - up) / (footprint.y * 6.2831853));
		slope *= 1.10;
	} else {
		// Photometric microrelief is deliberately shallow: albedo is not elevation.
		// Derivative-aware mip selection avoids shimmering in distant views.
		float left = dot(texture(surface_map, uv - vec2(footprint.x, 0.0)).rgb, vec3(0.299, 0.587, 0.114));
		float right = dot(texture(surface_map, uv + vec2(footprint.x, 0.0)).rgb, vec3(0.299, 0.587, 0.114));
		float up = dot(texture(surface_map, uv - vec2(0.0, footprint.y)).rgb, vec3(0.299, 0.587, 0.114));
		float down = dot(texture(surface_map, uv + vec2(0.0, footprint.y)).rgb, vec3(0.299, 0.587, 0.114));
		slope = vec2(left - right, down - up) * relief_strength;
	}
	float polar_fade = smoothstep(0.015, 0.08, sin(uv.y * 3.14159265));
	NORMAL_MAP = normalize(vec3(slope * polar_fade, 1.0)) * 0.5 + 0.5;
	NORMAL_MAP_DEPTH = 0.70;
	ALBEDO = terrain * surface_tint.rgb;
	ROUGHNESS = clamp(surface_roughness + (0.25 - luminance) * 0.10, 0.78, 1.0);
	METALLIC = 0.0;
	SPECULAR = 0.20;
}
