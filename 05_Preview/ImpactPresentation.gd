extends Node2D

# Shared by the native preview and combat. Positions are captured at damage time,
# so a lethal hit can finish after its creature node has been released.
var definitions: Dictionary = {}
var textures: Dictionary = {}
var hits: Array[Dictionary] = []
var serial := 0
var emitted := 0
var ordinary: Node2D

func _ready() -> void:
	texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	var additive := CanvasItemMaterial.new()
	additive.blend_mode = CanvasItemMaterial.BLEND_MODE_ADD
	material = additive
	if definitions.is_empty():
		var path := "res://Hcxmmx_King/Presentation/combat_presentation.json"
		if FileAccess.file_exists(path):
			configure(JSON.parse_string(FileAccess.get_file_as_string(path)).get("impacts", {}))

func configure(data: Dictionary) -> void:
	definitions = data
	if data.has("ordinary"):
		if is_instance_valid(ordinary): ordinary.free()
		var path: String = get_script().resource_path.get_base_dir().path_join("OriginalEnemyHit.gd")
		var script := load(path) as GDScript
		if script != null:
			ordinary = script.new()
			add_child(ordinary)
			ordinary.configure(data.ordinary)
	textures.clear()
	for key in data.get("textures", {}):
		var path: String = data.textures[key]
		if not FileAccess.file_exists(path): continue
		var image := Image.new()
		if image.load_png_from_buffer(FileAccess.get_file_as_bytes(path)) == OK:
			textures[key] = ImageTexture.create_from_image(image)

func spawn_hit(center: Vector2, direction: Vector2, pixel_scale: float, body: String, armored := false, killed := false, floor_y := 0.0) -> bool:
	if not armored:
		return can_spawn(body) and ordinary.spawn_hit(center, direction, pixel_scale, floor_y, killed)
	var binding: Dictionary = definitions.get("stages", {}).get(body, {})
	var style: String = "armor" if armored else str(binding.get("style", body))
	if not definitions.get("styles", {}).has(style) or textures.size() != 4: return false
	var preset: Dictionary = definitions.styles[style]
	serial += 1
	var rng := RandomNumberGenerator.new()
	rng.seed = serial * 7919
	var angle := direction.angle() + (float(binding.get("angle", 0.0)) if style == "slash" else 0.0)
	var particles: Array[Dictionary] = []
	var strength := float(binding.get("strength", 1.0))
	for index in range(int(preset.count)):
		var heading := angle + rng.randf_range(-float(preset.spread), float(preset.spread))
		var speed := float(preset.speed) * rng.randf_range(0.4, 1.0) * strength
		particles.append({"velocity": Vector2.from_angle(heading) * speed,
			"life": float(preset.life) * rng.randf_range(0.5, 1.0),
			"friction": rng.randf_range(0.7, 0.9), "length": rng.randf_range(0.5, 1.5),
			"white": index % 3 == 0, "dot": index % 4 == 0})
	if hits.size() >= int(definitions.get("limit", 32)): hits.pop_front()
	hits.append({"position": center, "scale": pixel_scale, "age": 0.0,
		"life": float(preset.life), "angle": angle, "style": style,
		"color": Color(preset.color), "particles": particles, "strength": strength})
	emitted += 1
	queue_redraw()
	return true

func can_spawn(body: String) -> bool:
	return definitions.get("stages", {}).has(body) and textures.size() == 4 and is_instance_valid(ordinary) and ordinary.is_ready()

func flash_target(target: CanvasItem) -> void:
	if is_instance_valid(ordinary): ordinary.flash_target(target)

func clear() -> void:
	if is_instance_valid(ordinary): ordinary.clear()
	hits.clear()
	queue_redraw()

func _process(delta: float) -> void:
	advance(delta)

func advance(delta: float) -> void:
	if is_instance_valid(ordinary): ordinary.advance(delta)
	if hits.is_empty(): return
	for hit in hits: hit.age += delta
	hits = hits.filter(func(hit): return hit.age < hit.life)
	queue_redraw()

func _draw_tile(key: String, position: Vector2, size: Vector2, rotation_angle: float, tint: Color) -> void:
	var tile: Texture2D = textures[key]
	draw_set_transform(position, rotation_angle)
	draw_texture_rect(tile, Rect2(-size * 0.5, size), false, tint)

func _draw() -> void:
	for hit in hits:
		var age: float = hit.age
		var center: Vector2 = hit.position
		var scale_factor: float = hit.scale
		# A short contact core, followed by independently decaying source tiles.
		var flash := maxf(0.0, 1.0 - age / 0.065)
		if flash > 0.0:
			_draw_tile("fxGlowWhite", center, Vector2.ONE * 20.0 * scale_factor,
				0.0, Color(1.0, 0.8, 0.5, flash * 0.4))
			var core := Vector2(24, 3) if hit.style == "slash" else Vector2(11, 5)
			_draw_tile("fxLineWhiteSharp", center, core * scale_factor, hit.angle, Color(1, 1, 1, flash))
		for particle in hit.particles:
			if age >= float(particle.life): continue
			# Integrate source per-30-Hz-tick friction analytically for pause/seek.
			var friction: float = particle.friction
			var travel := (1.0 - pow(friction, age * 30.0)) / (30.0 * (1.0 - friction))
			var offset: Vector2 = particle.velocity * travel
			var alpha := pow(1.0 - age / float(particle.life), 0.6)
			var tint: Color = Color.WHITE if particle.white else hit.color
			tint.a = alpha
			var size := Vector2.ONE * 1.3 if particle.dot else Vector2(10.0 * particle.length * (1.0 - age / float(particle.life)), 1.1)
			_draw_tile("fxPixelWhite" if particle.dot else "fxLineWhite", center + offset * scale_factor,
				size * scale_factor, particle.velocity.angle(), tint)
	draw_set_transform(Vector2.ZERO)
