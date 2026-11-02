class_name LingdawnpaoFrostExecutionEffect
extends Node2D

## Visuals only. The C# adapter owns the captured texture and death lifecycle.
signal shattered
signal cracked
signal finished

const CRACK_TIME := 0.42
const BURST_TIME := 0.60
const DURATION := 1.65
const ICE = preload("res://lingdawnpao/vfx/frost_execution/ice.gdshader")
const CRACK_MASK = preload("res://lingdawnpao/vfx/frost_execution/cracks.gdshader")

var fragments: Array = []
var edges: Array = []
var seeds: Array[Vector2] = []
var actor_texture: Texture2D
var actor_image: Image
var used_rect: Rect2i
var bounds: Rect2
var original: Sprite2D
var original_material: ShaderMaterial
var crack_layer: CrackDrawing
var mist_layer: MistDrawing
var elapsed := -1.0
var manual_mode := false
var _shattered := false
var _cracked := false
var _finished := false
var _rng := RandomNumberGenerator.new()
var source_cell_area := 0.0

class CrackDrawing extends Node2D:
    var segments: Array = []
    var progress := 0.0
    var extent := 300.0

    func _draw() -> void:
        for edge in segments:
            var a: Vector2 = edge[0]
            var b: Vector2 = edge[1]
            var midpoint := (a + b) * 0.5
            var start: float = clampf(midpoint.distance_to(Vector2(0, -extent * 0.52)) / extent, 0, 1) * 0.58
            var amount := smoothstep(start, start + 0.18, progress)
            if amount > 0.001:
                var side := Vector2(-(b-a).y, (b-a).x).normalized()
                var zig := midpoint + side * sin(midpoint.x * 0.13 + midpoint.y * 0.09) * 1.6
                draw_polyline(PackedVector2Array([a, zig, b]), Color(0.22, 0.55, 0.72, amount * 0.6), 3.3, true)
                draw_polyline(PackedVector2Array([a, zig, b]), Color(0.85, 0.98, 1.0, amount), 1.35, true)

class MistDrawing extends Node2D:
    var effect_time := -1.0
    var width := 280.0
    var height := 300.0

    func _draw() -> void:
        var freeze := smoothstep(0.0, 0.18, effect_time)
        var fade := 1.0 - smoothstep(0.55, 1.2, effect_time)
        if freeze * fade < 0.001:
            return
        for i in range(7):
            var direction := -1.0 if i % 2 == 0 else 1.0
            var local_age := maxf(0.0, effect_time - 0.04 * i)
            var pos := Vector2(direction * (width * 0.14 + local_age * (17 + i * 4)), -5 - 5 * sin(i * 1.7) - local_age * 8)
            var radius := (16 + i * 2) * (0.5 + freeze * 0.5)
            for ring in range(9, 0, -1):
                var fraction := ring / 9.0
                draw_circle(pos, radius * fraction, Color(0.49, 0.73, 0.86, 0.009 * freeze * fade * (1.2 - fraction)), true, -1.0, true)
        # The frozen silhouette flashes at fracture, followed by fast, narrow chips.
        var age := effect_time - BURST_TIME
        var impact := maxf(0.0, 1.0 - absf(age - 0.012) / 0.065)
        if impact > 0.0:
            draw_ellipse_glow(Vector2(0, -height * 0.48), Vector2(width * 0.36, height * 0.27), impact * 3.0)
        if age >= 0.0 and age < 0.30:
            var center := Vector2(0, -height * 0.5)
            var amount := pow(1.0 - age / 0.30, 2.0)
            for i in range(16):
                var angle := i * TAU / 16.0 + sin(i * 17.0) * 0.17
                var direction := Vector2(cos(angle), sin(angle))
                var side := Vector2(-direction.y, direction.x)
                var speed := 280.0 + 85.0 * sin(i * 13.0)
                var point := center + direction * (minf(width, height) * 0.12 + speed * age)
                draw_colored_polygon(PackedVector2Array([point + direction * 17.0, point + side * 2.7, point - direction * 8.0, point - side * 2.7]), Color(0.73, 0.95, 1.0, amount * 0.85))

    func draw_ellipse_glow(center: Vector2, size: Vector2, amount: float) -> void:
        for i in range(8, 0, -1):
            var p := PackedVector2Array()
            for j in range(36):
                var angle := TAU * j / 36
                p.append(center + Vector2(cos(angle), sin(angle)) * size * (i / 8.0))
            draw_colored_polygon(p, Color(0.72, 0.92, 1.0, amount * 0.022))

func configure(texture: Texture2D, display_size: Vector2, seed_value: int = 17, manual: bool = false) -> void:
    actor_texture = texture
    actor_image = texture.get_image()
    used_rect = actor_image.get_used_rect()
    var fit := minf(display_size.x / used_rect.size.x, display_size.y / used_rect.size.y)
    display_size = Vector2(used_rect.size) * fit
    bounds = Rect2(Vector2(-display_size.x * 0.5, -display_size.y), display_size)
    manual_mode = manual
    _rng.seed = seed_value

    mist_layer = MistDrawing.new()
    mist_layer.width = display_size.x
    mist_layer.height = display_size.y
    add_child(mist_layer)

    original_material = ShaderMaterial.new()
    original_material.shader = ICE
    _set_source_uv(original_material)
    original = Sprite2D.new()
    original.texture = actor_texture
    original.region_enabled = true
    original.region_rect = Rect2(used_rect)
    original.position = bounds.get_center()
    original.scale = display_size / Vector2(used_rect.size)
    original.material = original_material
    add_child(original)

    _build_cells()
    crack_layer = CrackDrawing.new()
    crack_layer.segments = edges
    crack_layer.extent = maxf(display_size.x, display_size.y)
    var crack_material := ShaderMaterial.new()
    crack_material.shader = CRACK_MASK
    crack_material.set_shader_parameter("actor_mask", texture)
    crack_material.set_shader_parameter("bounds_origin", bounds.position)
    crack_material.set_shader_parameter("bounds_size", bounds.size)
    crack_material.set_shader_parameter("source_uv_origin", Vector2(used_rect.position) / actor_texture.get_size())
    crack_material.set_shader_parameter("source_uv_size", Vector2(used_rect.size) / actor_texture.get_size())
    crack_layer.material = crack_material
    add_child(crack_layer)
    set_process(not manual_mode)
    seek(-1.0)

func _set_source_uv(mat: ShaderMaterial) -> void:
    mat.set_shader_parameter("actor_uv_origin", Vector2(used_rect.position) / actor_texture.get_size())
    mat.set_shader_parameter("actor_uv_size", Vector2(used_rect.size) / actor_texture.get_size())

func _clip(poly: PackedVector2Array, normal: Vector2, offset: float) -> PackedVector2Array:
    var result := PackedVector2Array()
    if poly.is_empty():
        return result
    for i in range(poly.size()):
        var a := poly[i]
        var b := poly[(i + 1) % poly.size()]
        var da := a.dot(normal) - offset
        var db := b.dot(normal) - offset
        if da <= 0.0001:
            result.append(a)
        if (da < 0) != (db < 0):
            result.append(a.lerp(b, da / (da - db)))
    return result

func _alpha_at(point: Vector2) -> float:
    var uv := (point - bounds.position) / bounds.size
    var image_size := actor_image.get_size()
    var x := clampi(int(uv.x * used_rect.size.x + used_rect.position.x), 0, image_size.x - 1)
    var y := clampi(int(uv.y * used_rect.size.y + used_rect.position.y), 0, image_size.y - 1)
    return actor_image.get_pixel(x, y).a

func _area(poly: PackedVector2Array) -> float:
    var sum := 0.0
    for i in range(poly.size()):
        sum += poly[i].cross(poly[(i + 1) % poly.size()])
    return absf(sum) * 0.5

func _build_cells() -> void:
    var aspect := bounds.size.x / bounds.size.y
    var cols := clampi(roundi(sqrt(32 * aspect)), 3, 8)
    var rows := clampi(roundi(32.0 / cols), 4, 11)
    for y in range(rows):
        for x in range(cols):
            seeds.append(bounds.position + Vector2((x + _rng.randf_range(0.25, 0.75)) / cols, (y + _rng.randf_range(0.25, 0.75)) / rows) * bounds.size)
    var rect_poly := PackedVector2Array([bounds.position, bounds.position + Vector2(bounds.size.x, 0), bounds.end, bounds.position + Vector2(0, bounds.size.y)])
    var edge_keys := {}
    for i in range(seeds.size()):
        var poly := rect_poly.duplicate()
        for j in range(seeds.size()):
            if i == j:
                continue
            var normal := seeds[j] - seeds[i]
            var cutoff := (seeds[j].length_squared() - seeds[i].length_squared()) * 0.5
            poly = _clip(poly, normal, cutoff)
            if poly.size() < 3:
                break
        if poly.size() < 3:
            continue
        source_cell_area += _area(poly)
        var center := Vector2.ZERO
        for p in poly:
            center += p
        center /= poly.size()
        var has_pixels := _alpha_at(center) > 0.02
        for p in poly:
            has_pixels = has_pixels or _alpha_at(center.lerp(p, 0.65)) > 0.02
        if not has_pixels:
            continue
        var local := PackedVector2Array()
        var uv := PackedVector2Array()
        var radius := 0.0
        for p in poly:
            local.append(p - center)
            uv.append((p - bounds.position) / bounds.size * Vector2(used_rect.size) + Vector2(used_rect.position))
            radius = maxf(radius, p.distance_to(center))
        var node := Polygon2D.new()
        node.polygon = local
        node.uv = uv
        node.texture = actor_texture
        node.position = center
        var material := ShaderMaterial.new()
        material.shader = ICE
        _set_source_uv(material)
        material.set_shader_parameter("freeze_amount", 1.0)
        material.set_shader_parameter("facet_shade", _rng.randf_range(0.82, 1.13))
        var cell_rect := Rect2(poly[0], Vector2.ZERO)
        for p in poly:
            cell_rect = cell_rect.expand(p)
        material.set_shader_parameter("shard_origin", (cell_rect.position - bounds.position) / bounds.size)
        material.set_shader_parameter("shard_size", cell_rect.size / bounds.size)
        node.material = material
        add_child(node)
        var outward := (center - Vector2(0, -bounds.size.y * 0.50)).normalized()
        var size_factor := clampf(maxf(bounds.size.x, bounds.size.y) / 300.0, 0.8, 1.55)
        var velocity := Vector2(outward.x * _rng.randf_range(190, 320), outward.y * 95 - _rng.randf_range(45, 110)) * size_factor
        var spin := _rng.randf_range(-4.2, 4.2)
        var bottom := -INF
        for p in local:
            bottom = maxf(bottom, p.y)
        var discriminant := velocity.y * velocity.y - 3120 * minf(0, center.y + bottom)
        var landing := maxf(0.02, (-velocity.y + sqrt(discriminant)) / 1560.0)
        fragments.append({"node":node,"center":center,"velocity":velocity,"spin":spin,"radius":radius,"material":material,"shade":material.get_shader_parameter("facet_shade"),"landing":landing})
        for k in range(poly.size()):
            var a := poly[k]
            var b := poly[(k + 1) % poly.size()]
            var ka := Vector2i(roundi(a.x * 10), roundi(a.y * 10))
            var kb := Vector2i(roundi(b.x * 10), roundi(b.y * 10))
            var first: Vector2i = ka if str(ka) < str(kb) else kb
            var second: Vector2i = kb if str(ka) < str(kb) else ka
            var key := str(first) + ":" + str(second)
            if not edge_keys.has(key):
                edge_keys[key] = true
                edges.append([a,b])

func seek(time: float) -> void:
    elapsed = time
    if time < 0.0:
        _shattered = false
        _cracked = false
        _finished = false
    var freeze := smoothstep(0.0, 0.18, time)
    var chunks_mix := smoothstep(0.14, 0.20, time)
    original.visible = time < 0.20
    original_material.set_shader_parameter("freeze_amount", freeze)
    original_material.set_shader_parameter("opacity", 1.0)
    var age := maxf(0.0, time - BURST_TIME)
    var impact_flash := (1.0 - smoothstep(0.0, 0.075, age)) if time >= BURST_TIME else 0.0
    # Brief local recoil during crystallization; the whole game keeps running.
    var recoil := Vector2(-3.5 * sin(clampf(time / 0.18, 0.0, 1.0) * PI), 0)
    original.position = bounds.get_center() + recoil
    for fragment in fragments:
        var node: Polygon2D = fragment.node
        var center: Vector2 = fragment.center
        var velocity: Vector2 = fragment.velocity
        node.visible = time >= 0.14 and time < DURATION
        var landing: float = fragment.landing
        node.position = center + recoil + velocity * age + Vector2(0, 780 * age * age)
        node.rotation = fragment.spin * minf(age, landing)
        node.scale = Vector2.ONE * lerpf(1.0, 0.73, smoothstep(0.28, 0.85, age))
        var actual_bottom := -INF
        for p in node.polygon:
            actual_bottom = maxf(actual_bottom, (p * node.scale).rotated(node.rotation).y)
        if age > landing:
            var after := age - landing
            node.position.x = center.x + velocity.x * landing + velocity.x * 0.2 * (1.0-exp(-after*7)) / 7.0
            var bounce := -6.0 * exp(-after * 10) * sin(minf(after * 20, PI))
            node.position.y = -actual_bottom + bounce
        node.position.y = minf(node.position.y, -actual_bottom)
        var fade := 1.0 - smoothstep(0.48, 0.90, age)
        var material: ShaderMaterial = fragment.material
        material.set_shader_parameter("opacity", chunks_mix * fade)
        material.set_shader_parameter("impact_flash", impact_flash)
        material.set_shader_parameter("facet_shade", fragment.shade * (0.94 + 0.06 * cos(node.rotation * 3)))
        material.set_shader_parameter("shard_amount", smoothstep(0.08, 0.30, age))
    crack_layer.position = recoil
    crack_layer.visible = time >= CRACK_TIME and time < BURST_TIME + 0.025
    crack_layer.progress = smoothstep(CRACK_TIME, BURST_TIME - 0.04, time)
    crack_layer.queue_redraw()
    mist_layer.effect_time = time
    mist_layer.queue_redraw()
    if time >= CRACK_TIME and not _cracked:
        _cracked = true
        cracked.emit()
    if time >= BURST_TIME and not _shattered:
        _shattered = true
        shattered.emit()
    if time >= DURATION and not _finished:
        _finished = true
        finished.emit()

func _process(delta: float) -> void:
    seek(maxf(0.0, elapsed) + delta)
    if elapsed >= DURATION:
        queue_free()
