shader_type spatial;
render_mode blend_add, cull_back, depth_draw_never, shadows_disabled, fog_disabled, ambient_light_disabled, specular_disabled;

uniform float sun_radius = 8.0;
uniform float corona_radius = 15.2;
uniform float simulation_time = 0.0;
varying vec3 world_vertex;
varying vec3 sun_center;
varying vec3 solar_axis;

void vertex() {
	world_vertex = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	sun_center = MODEL_MATRIX[3].xyz;
	solar_axis = normalize(MODEL_MATRIX[1].xyz);
}

void fragment() {
	vec3 origin = INV_VIEW_MATRIX[3].xyz - sun_center;
	vec3 ray = normalize(world_vertex - INV_VIEW_MATRIX[3].xyz);
	vec3 nearest = origin - ray * dot(origin, ray);
	float impact = length(nearest);
	float altitude = max(impact / sun_radius - 1.0, 0.0);
	vec3 radial = normalize(nearest + vec3(0.00001));
	// World-space angular bands produce elongated streamers that keep their
	// orientation when the camera orbits. No billboard or full-screen tracing.
	float latitude = dot(radial, solar_axis);
	float azimuth = atan(radial.z, radial.x);
	float equatorial = 0.45 + 0.55 * pow(1.0 - latitude * latitude, 2.0);
	float streamers = 0.50 + 0.27 * sin(azimuth * 11.0 + latitude * 17.0 + simulation_time * 0.025)
		+ 0.14 * sin(azimuth * 23.0 - latitude * 13.0 - simulation_time * 0.019);
	float broad = exp(-altitude * 6.8) * (0.40 + streamers * equatorial * 0.60);
	float chromosphere = exp(-altitude * 49.0);
	float polar_plumes = pow(max(streamers, 0.0), 4.0) * exp(-altitude * 4.3) * 0.13;
	float inner_mask = smoothstep(sun_radius * 0.997, sun_radius * 1.006, impact);
	float outer_fade = 1.0 - smoothstep(corona_radius * 0.88, corona_radius, impact);
	vec3 coronal_color = mix(vec3(1.0, 0.45, 0.11), vec3(0.72, 0.83, 1.0), smoothstep(0.12, 0.62, altitude));
	ALBEDO = vec3(0.0);
	EMISSION = coronal_color * (broad * 2.2 + polar_plumes) + vec3(1.0, 0.20, 0.018) * chromosphere * 3.5;
	ALPHA = inner_mask * outer_fade * 0.60;
}
