shader_type canvas_item;

uniform float progress : hint_range(0.0, 1.0) = 0.0;
uniform float dither_size : hint_range(1.0, 16.0, 1.0) = 4.0;

void fragment() {
    int x = int(FRAGCOORD.x / dither_size) % 4;
    int y = int(FRAGCOORD.y / dither_size) % 4;

    float bayer[16] = float[](
        0.0 / 16.0,  8.0 / 16.0,  2.0 / 16.0, 10.0 / 16.0,
       12.0 / 16.0,  4.0 / 16.0, 14.0 / 16.0,  6.0 / 16.0,
        3.0 / 16.0, 11.0 / 16.0,  1.0 / 16.0,  9.0 / 16.0,
       15.0 / 16.0,  7.0 / 16.0, 13.0 / 16.0,  5.0 / 16.0
    );

    int index = y * 4 + x;
    float threshold = bayer[index];

    if (progress <= threshold) {
        discard;
    }

    COLOR = COLOR;
}