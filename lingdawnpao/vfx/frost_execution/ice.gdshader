shader_type canvas_item;
render_mode unshaded;

uniform float freeze_amount : hint_range(0.0, 1.0) = 0.0;
uniform float facet_shade : hint_range(0.6, 1.4) = 1.0;
uniform float opacity : hint_range(0.0, 1.0) = 1.0;
uniform float impact_flash : hint_range(0.0, 1.0) = 0.0;
uniform vec2 actor_uv_origin = vec2(0.0);
uniform vec2 actor_uv_size = vec2(1.0);
uniform float shard_amount : hint_range(0.0, 1.0) = 0.0;
uniform vec2 shard_origin = vec2(0.0);
uniform vec2 shard_size = vec2(1.0);

void fragment() {
    vec4 source = texture(TEXTURE, UV);
    vec2 actor_uv = (UV - actor_uv_origin) / actor_uv_size;
    float front = freeze_amount * 1.22 - (1.0 - actor_uv.y);
    float frozen = smoothstep(-0.04, 0.08, front) * step(0.001, freeze_amount);
    float luma = dot(source.rgb, vec3(0.24, 0.64, 0.12));
    vec3 ice_dark = vec3(0.16, 0.35, 0.51);
    vec3 ice_light = vec3(0.72, 0.89, 0.96);
    vec3 ice = mix(ice_dark, ice_light, clamp(luma * 0.84 + 0.18, 0.0, 1.0));
    ice *= facet_shade;
    float reflection = smoothstep(0.63, 0.66, fract(actor_uv.x * 0.83 - actor_uv.y * 0.46));
    reflection *= 1.0 - smoothstep(0.72, 0.79, fract(actor_uv.x * 0.83 - actor_uv.y * 0.46));
    ice += vec3(0.08, 0.12, 0.13) * reflection;
    vec2 shard_uv = (actor_uv - shard_origin) / max(shard_size, vec2(0.001));
    float shard_plane = mix(0.83, 1.18, step(shard_uv.x * 0.71 + 0.12, shard_uv.y));
    vec3 clean_ice = vec3(0.43, 0.70, 0.84) * facet_shade * shard_plane;
    clean_ice += vec3(0.11, 0.16, 0.17) * pow(clamp(1.0 - shard_uv.y, 0.0, 1.0), 3.0);
    ice = mix(ice, clean_ice, shard_amount);
    float neighbour_alpha = min(min(texture(TEXTURE, UV + vec2(TEXTURE_PIXEL_SIZE.x * 2.0, 0.0)).a,
                                    texture(TEXTURE, UV - vec2(TEXTURE_PIXEL_SIZE.x * 2.0, 0.0)).a),
                                min(texture(TEXTURE, UV + vec2(0.0, TEXTURE_PIXEL_SIZE.y * 2.0)).a,
                                    texture(TEXTURE, UV - vec2(0.0, TEXTURE_PIXEL_SIZE.y * 2.0)).a));
    float rim = smoothstep(0.0, 0.7, source.a - neighbour_alpha);
    ice = mix(ice, vec3(0.83, 0.96, 1.0), rim * 0.62);
    float freezing_edge = (1.0 - smoothstep(0.0, 0.04, abs(front))) * step(0.01, freeze_amount);
    vec3 result = mix(source.rgb, ice, frozen * 0.97);
    result += vec3(0.12, 0.23, 0.27) * freezing_edge;
    result = mix(result, vec3(0.91, 0.99, 1.0), impact_flash * 0.83);
    COLOR = vec4(result, source.a * opacity);
}
