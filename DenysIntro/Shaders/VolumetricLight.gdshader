shader_type canvas_item;

uniform sampler2D screen_texture : hint_screen_texture, filter_linear_mipmap;

uniform vec2 center = vec2(0.5, 0.5);
uniform float blur_strength : hint_range(0.0, 1.0) = 0.15;
uniform int samples : hint_range(4, 64) = 20;

void fragment() {
    vec2 dir = SCREEN_UV - center;
    vec4 blurred_color = vec4(0.0);

    for (int i = 0; i < samples; i++) {
        float t = float(i) / float(samples - 1);

        float scale = 1.0 + (t - 0.5) * blur_strength;

        blurred_color += texture(screen_texture, center + dir * scale);
    }

    COLOR = blurred_color / float(samples);
}
