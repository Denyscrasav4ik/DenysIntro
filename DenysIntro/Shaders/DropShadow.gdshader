shader_type canvas_item;

uniform vec4 drop_shadow_color : source_color = vec4(0.0, 0.0, 0.0, 0.5);
uniform vec2 shadow_direction = vec2(-1.0, 1.0);
uniform float shadow_length : hint_range(0.0, 100.0, 0.1) = 15.0;

varying vec2 v_local_uv_offset;
varying float v_max_offset;

void vertex() {
    vec2 dir = length(shadow_direction) > 0.0001 ? normalize(shadow_direction) : vec2(0.0);
    vec2 screen_offset_px = dir * shadow_length;

    mat2 world_to_local = inverse(mat2(MODEL_MATRIX[0].xy, MODEL_MATRIX[1].xy));
    vec2 local_offset_px = world_to_local * screen_offset_px;

    v_local_uv_offset = local_offset_px * TEXTURE_PIXEL_SIZE;
    v_max_offset = max(abs(v_local_uv_offset.x), abs(v_local_uv_offset.y));

    VERTEX *= 1.0 + 2.0 * v_max_offset;
}

vec4 sample_texture(sampler2D tex, vec2 uv) {
    if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0) {
        return vec4(0.0);
    } else {
        return texture(tex, uv);
    }
}

vec4 mixcolor(vec4 colA, vec4 colB) {
    return vec4(mix(colA.rgb, colB.rgb, colB.a), max(colA.a, colB.a));
}

void fragment() {
    vec2 uv = UV * (1.0 + 2.0 * v_max_offset) - vec2(v_max_offset);
    vec4 original_color = sample_texture(TEXTURE, uv);
    vec4 shadow_color = vec4(drop_shadow_color.rgb, sample_texture(TEXTURE, uv - v_local_uv_offset).a * drop_shadow_color.a);

    if (shadow_color.a > 0.0) {
        COLOR = mixcolor(shadow_color, original_color);
    } else {
        COLOR = original_color;
    }
}