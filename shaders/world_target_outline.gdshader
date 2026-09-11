shader_type spatial;
render_mode unshaded, cull_front, blend_mix, depth_draw_never, shadows_disabled;

// Apparent width stays in logical pixels as the viewport resolution changes.
uniform sampler2D scene_depth : hint_depth_texture, repeat_disable, filter_nearest;
uniform vec2 target_depth_interval = vec2(-1.0);
uniform vec2 logical_view_size = vec2(1440.0, 900.0);
uniform float outline_width = 1.5;
uniform float strength = 0.0;
uniform vec4 outline_color : source_color = vec4(0.52, 1.0, 0.87, 0.86);
uniform float luminance = 1.15;
uniform bool lunar_relief = false;
uniform float radial_offset = 0.0;
uniform sampler2D height_map : hint_default_black, filter_linear_mipmap_anisotropic, repeat_enable;

void vertex() {
	VERTEX += normalize(VERTEX) * radial_offset;
	if (lunar_relief) {
		float altitude = (textureLod(height_map, UV, 0.0).r * 65535.0 - 20000.0) * (0.5 / 1737400.0);
		VERTEX *= 1.0 + altitude;
	}
	vec4 clip = PROJECTION_MATRIX * MODELVIEW_MATRIX * vec4(VERTEX, 1.0);
	vec3 view_normal = MODELVIEW_NORMAL_MATRIX * NORMAL;
	vec2 projected_normal = (PROJECTION_MATRIX * vec4(view_normal, 0.0)).xy;
	vec2 direction_pixels = projected_normal * logical_view_size;
	float direction_length = length(direction_pixels);
	if (direction_length > 0.00001) {
		clip.xy += direction_pixels / direction_length * (2.0 / logical_view_size) * outline_width * clip.w;
	}
	POSITION = clip;
}

void fragment() {
	float sampled_depth = textureLod(scene_depth, SCREEN_UV, 0.0).r;
	vec4 scene_view = INV_PROJECTION_MATRIX * vec4(SCREEN_UV * 2.0 - 1.0, sampled_depth, 1.0);
	float scene_distance = -scene_view.z / scene_view.w;
	if (scene_distance >= target_depth_interval.x && scene_distance <= target_depth_interval.y) {
		discard;
	}
	ALBEDO = outline_color.rgb;
	EMISSION = outline_color.rgb * luminance;
	ALPHA = outline_color.a * strength;
}
