shader_type spatial;
render_mode unshaded, blend_mix, cull_back, depth_draw_never, depth_test_disabled, shadows_disabled, fog_disabled;

uniform vec3 factory_normal = vec3(0.0, 0.0, 1.0);
uniform float patrol_angle = 0.15625;
uniform float attack_angle = 0.25;
uniform bool shield_mode = false;
uniform vec4 shield_tint : source_color = vec4(0.28, 0.86, 0.96, 0.78);
uniform vec4 patrol_tint : source_color = vec4(0.42, 0.94, 0.78, 0.88);
uniform vec4 attack_tint : source_color = vec4(1.0, 0.74, 0.38, 0.83);
varying vec3 local_direction;
varying vec3 world_position;
varying vec3 globe_center;

void vertex() {
    local_direction = normalize(VERTEX);
    world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    globe_center = MODEL_MATRIX[3].xyz;
}

float contour(float angle, float boundary, float pixel) {
    // A global cap has no boundary; avoid a spurious dot at its antipode.
    return (1.0 - smoothstep(pixel * 0.55, pixel * 1.6, abs(angle - boundary)))
        * (1.0 - step(3.1414, boundary));
}

void fragment() {
    vec3 outward = normalize(world_position - globe_center);
    vec3 to_camera = normalize(INV_VIEW_MATRIX[3].xyz - world_position);
    float facing = smoothstep(0.015, 0.18, dot(outward, to_camera));
    vec3 direction = normalize(local_direction);
    float angle = acos(clamp(dot(direction, factory_normal), -1.0, 1.0));
    float pixel = max(fwidth(angle), 0.000025);
    if (facing < 0.001 || angle > attack_angle + pixel * 2.0) { discard; }
    if (shield_mode) {
        float boundary = contour(angle, attack_angle, pixel);
        float inside = 1.0 - smoothstep(attack_angle - pixel, attack_angle + pixel, angle);
        ALBEDO = shield_tint.rgb;
        EMISSION = shield_tint.rgb * (boundary * 0.35 + inside * 0.04);
        ALPHA = (boundary * shield_tint.a + inside * 0.032) * facing;
    } else {
        float patrol_line = contour(angle, patrol_angle, pixel);
        float attack_line = contour(angle, attack_angle, pixel);

        vec3 reference = abs(factory_normal.y) < 0.95 ? vec3(0.0, 1.0, 0.0) : vec3(1.0, 0.0, 0.0);
        vec3 tangent = normalize(cross(reference, factory_normal));
        vec3 bitangent = cross(factory_normal, tangent);
        float azimuth = atan(dot(direction, bitangent), dot(direction, tangent) + 0.0000001);
        float dash = smoothstep(-0.12, 0.12, sin(azimuth * 48.0));
        attack_line *= dash;

        float inside_patrol = 1.0 - smoothstep(patrol_angle - pixel, patrol_angle + pixel, angle);
        float inside_attack = 1.0 - smoothstep(attack_angle - pixel, attack_angle + pixel, angle);
        float patrol_alpha = max(patrol_line * patrol_tint.a, inside_patrol * 0.026);
        float attack_alpha = max(attack_line * attack_tint.a, (inside_attack - inside_patrol) * 0.019);
        float alpha = max(patrol_alpha, attack_alpha) * facing;
        ALBEDO = mix(patrol_tint.rgb, attack_tint.rgb, step(patrol_alpha, attack_alpha));
        ALPHA = alpha;
    }
}
