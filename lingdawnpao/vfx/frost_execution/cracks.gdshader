shader_type canvas_item;
render_mode unshaded;

uniform sampler2D actor_mask : filter_linear, repeat_disable;
uniform vec2 bounds_origin;
uniform vec2 bounds_size;
uniform float opacity : hint_range(0.0, 1.0) = 1.0;
uniform vec2 source_uv_origin = vec2(0.0);
uniform vec2 source_uv_size = vec2(1.0);
varying vec2 actor_uv;
varying vec4 line_color;

void vertex() {
    actor_uv = (VERTEX - bounds_origin) / bounds_size;
    line_color = COLOR;
}

void fragment() {
    float mask = texture(actor_mask, source_uv_origin + actor_uv * source_uv_size).a;
    COLOR = vec4(line_color.rgb, line_color.a * mask * opacity);
}
