shader_type spatial;
render_mode unshaded, cull_disabled, blend_add, depth_draw_never;
uniform vec4 energy_color : source_color = vec4(0.4,0.8,1.0,1.0);
uniform float surface_mode = 0.0;
uniform float pulse_rate = 2.0;
instance uniform float combat_batched = 0.0;
instance uniform float combat_energy = 0.0;
instance uniform float combat_charge = 0.0;
instance uniform float combat_phase = 0.0;
varying vec3 local_position;
varying float energy;
varying float charge;
varying float phase;
void vertex() {
 local_position = VERTEX;
 energy = mix(combat_energy, INSTANCE_CUSTOM.r, combat_batched);
 charge = mix(combat_charge, INSTANCE_CUSTOM.g, combat_batched);
 phase = mix(combat_phase, INSTANCE_CUSTOM.b, combat_batched);
 if (surface_mode > 1.5) {
  VERTEX += NORMAL * (0.001 + 0.004 * charge) * sin(TIME * pulse_rate + phase * 6.283 + VERTEX.z * 35.0);
 }
}
void fragment() {
 float rhythm = 0.76 + 0.24 * sin(TIME * pulse_rate + phase * 6.283);
 float opacity = 0.8;
 vec3 tint = energy_color.rgb;
 if (surface_mode < 0.5) {
  if (energy < 0.001) { discard; }
  float rim = pow(1.0 - abs(dot(normalize(NORMAL), normalize(VIEW))), 3.1);
  float bands = smoothstep(0.89,1.0,sin(local_position.y * 160.0 + local_position.z * 35.0 - TIME));
  opacity = energy * (rim * 0.30 + bands * 0.035);
  tint = mix(vec3(0.55,0.34,1.0), energy_color.rgb, energy);
 } else {
  opacity = 0.65 + 0.28 * rhythm;
  tint *= 0.65 + rhythm * 0.20 + charge * 1.4;
 }
 ALBEDO = tint;
 EMISSION = tint;
 ALPHA = opacity * energy_color.a;
}
