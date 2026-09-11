shader_type spatial;
render_mode blend_add, cull_back, depth_draw_never, shadows_disabled, ambient_light_disabled, specular_disabled;

uniform float body_radius = 2.13;
uniform float atmosphere_radius = 2.27;
uniform float density_height = 0.018;
uniform vec3 sun_direction = vec3(-0.53, 0.53, 0.66);
uniform vec4 scatter_tint : source_color = vec4(0.70, 0.38, 0.21, 1.0);
uniform float scattering_energy = 1.45;
uniform float density_strength = 0.38;
varying vec3 world_vertex;
varying vec3 body_center;

void vertex() {
	world_vertex = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	body_center = MODEL_MATRIX[3].xyz;
}

vec2 ray_sphere(vec3 origin, vec3 direction, float radius) {
	float b = dot(origin, direction);
	float discriminant = b * b - dot(origin, origin) + radius * radius;
	if (discriminant < 0.0) { return vec2(-1.0); }
	float root = sqrt(max(discriminant, 0.0));
	return vec2(-b - root, -b + root);
}

void fragment() {
	vec3 camera_position = INV_VIEW_MATRIX[3].xyz;
	vec3 origin = camera_position - body_center;
	vec3 direction = normalize(world_vertex - camera_position);
	vec2 atmosphere_hit = ray_sphere(origin, direction, atmosphere_radius);
	float entry = max(atmosphere_hit.x, 0.0);
	float exit_point = atmosphere_hit.y;
	vec2 ground_hit = ray_sphere(origin, direction, body_radius);
	if (ground_hit.x > 0.0) { exit_point = min(exit_point, ground_hit.x); }
	float step_length = max(exit_point - entry, 0.0) / 8.0;
	float optical_depth = 0.0;
	float lit_depth = 0.0;
	vec3 sunlight = normalize(sun_direction);
	for (int sample_index = 0; sample_index < 8; sample_index++) {
		vec3 sample_position = origin + direction * (entry + (float(sample_index) + 0.5) * step_length);
		float height = max(length(sample_position) - body_radius, 0.0);
		float density = exp(-height / density_height) + 0.035 * exp(-height / (density_height * 3.5));
		density *= 1.0 - smoothstep(atmosphere_radius - density_height * 2.0, atmosphere_radius, length(sample_position));
		float daylight = smoothstep(-0.10, 0.20, dot(normalize(sample_position), sunlight));
		optical_depth += density * step_length;
		lit_depth += density * step_length * daylight;
	}
	float illumination = lit_depth / max(optical_depth, 0.00001);
	float impact = length(cross(origin, direction));
	float horizon = pow(smoothstep(body_radius * 0.83, body_radius * 1.005, impact), 3.0);
	float opacity = (1.0 - exp(-optical_depth * density_strength)) * mix(0.03, 1.0, horizon);
	float phase = 0.75 * (1.0 + pow(dot(direction, sunlight), 2.0));
	float rim = exp(-pow((impact - body_radius - density_height * 0.6) / (density_height * 1.5), 2.0));
	ALBEDO = vec3(0.0);
	EMISSION = scatter_tint.rgb * scattering_energy * (0.04 + illumination * phase) + mix(scatter_tint.rgb, vec3(1.0), 0.45) * rim * illumination * 1.35;
	ALPHA = clamp(opacity * mix(0.12, 1.0, illumination), 0.0, 0.22);
}
