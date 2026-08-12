shader_type canvas_item;

uniform vec4 dark_purple : source_color = vec4(0.12, 0.05, 0.20, 1.0);
uniform vec4 light_purple : source_color = vec4(0.22, 0.10, 0.35, 1.0);

uniform float grid_size : hint_range(10.0, 200.0) = 64.0;

uniform float scroll_speed_x : hint_range(0.0, 500.0) = 100.0;
uniform float vertical_amplitude : hint_range(0.0, 200.0) = 200.0;
uniform float vertical_frequency : hint_range(0.1, 10.0) = 0.5;

void fragment() {
    float offset_x = TIME * scroll_speed_x;

    float offset_y = sin(TIME * vertical_frequency) * vertical_amplitude;

    vec2 pos = FRAGCOORD.xy + vec2(-offset_x, offset_y);

    ivec2 grid_cell = ivec2(floor(pos / grid_size));

    bool is_even = (grid_cell.x + grid_cell.y) % 2 == 0;

    COLOR = is_even ? dark_purple : light_purple;
}