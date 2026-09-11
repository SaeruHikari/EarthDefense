shader_type canvas_item;
render_mode unshaded;

uniform sampler2D sky_panorama : filter_linear_mipmap, repeat_enable;
uniform vec2 canvas_size = vec2(1440.0, 900.0);
uniform vec2 view_origin = vec2(0.0);
uniform vec2 view_size = vec2(1440.0, 900.0);
uniform float vertical_fov = 36.0;
uniform float horizontal_projection_scale = 1.0;
uniform vec3 camera_right = vec3(1.0,0.0,0.0);
uniform vec3 camera_up = vec3(0.0,1.0,0.0);
uniform vec3 camera_back = vec3(0.0,0.0,1.0);
uniform float motion_amount = 1.0;
uniform float sky_brightness = 1.35;

void fragment() {
    vec2 pixel = UV * canvas_size;
    vec2 centered = (pixel - view_origin - view_size * 0.5) / max(view_size.y * 0.5, 1.0);
    float lens = tan(radians(vertical_fov) * 0.5);
    vec3 ray = normalize(camera_right * centered.x * lens * horizontal_projection_scale - camera_up * centered.y * lens - camera_back);
    vec2 celestial_uv = vec2(atan(ray.x,-ray.z) / 6.28318530718 + 0.5, acos(clamp(ray.y,-1.0,1.0)) / 3.14159265359);
    vec3 sky = texture(sky_panorama,celestial_uv).rgb;
    float luminance = dot(sky,vec3(0.2126,0.7152,0.0722));
    float bright_star = smoothstep(0.19,0.48,luminance);
    float shimmer = 1.0 + motion_amount * bright_star * 0.045 * sin(TIME * 0.23 + celestial_uv.x * 280.0 + celestial_uv.y * 170.0);
    // This is a composition mask over a true world-direction sky, not a fixed
    // screen-space starfield. It keeps Earth and floating controls readable.
    float distance_from_earth = length(centered);
    float earth_clearance = mix(0.28,1.0,smoothstep(0.44,0.90,distance_from_earth));
    vec2 edge = min(UV,vec2(1.0)-UV);
    float edge_fade = mix(0.66,1.0,smoothstep(0.0,0.14,min(edge.x,edge.y)));
    vec3 base = vec3(0.018,0.029,0.051);
    vec3 detail = max(sky - base,vec3(0.0)) * sky_brightness * shimmer;
    COLOR = vec4(base + detail * earth_clearance * edge_fade,1.0);
}
