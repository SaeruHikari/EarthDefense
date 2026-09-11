shader_type spatial;
render_mode blend_add, cull_back, depth_draw_never, shadows_disabled, ambient_light_disabled, specular_disabled;

instance uniform vec4 energy_color : source_color = vec4(0.5, 1.0, 0.85, 1.0);
instance uniform float visibility = 1.0;
uniform float energy = 6.0;
uniform float shell = 0.0;
uniform bool effects_batched = false;
varying vec4 effect_state;

void vertex() {
    effect_state = INSTANCE_CUSTOM;
}

void fragment() {
	float rim = pow(1.0 - abs(dot(normalize(NORMAL), normalize(VIEW))), 3.0);
	float profile = mix(1.0, rim, shell);
	ALBEDO = vec3(0.0);
	EMISSION = (effects_batched ? effect_state.rgb : energy_color.rgb) * energy;
	ALPHA = profile * (effects_batched ? effect_state.a : visibility);
}
