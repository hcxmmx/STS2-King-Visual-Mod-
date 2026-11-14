extends Node2D

# Ordinary physical damage recovered from Mob.onDamage and Fx.bloodHit.
# The source bloodless branch skips bloodHit and retains orange colorBlink.
# The preview uses the requested 2x size and reserves fxGib for lethal hits.
var data: Dictionary
var animations: Array = []
var gibs: Array[Texture2D] = []
var hits: Array[Dictionary] = []
var bloodless := false
var emitted := 0
var flashes: Array[Dictionary] = []

func configure(definitions: Dictionary) -> void:
	data = definitions
	texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	animations.clear()
	gibs.clear()
	for sequence in data.animations:
		var frames: Array[Texture2D] = []
		for path in sequence: frames.append(_load_tile(path))
		animations.append(frames)
	for path in data.gibs: gibs.append(_load_tile(path))

func _load_tile(path: String) -> Texture2D:
	if not FileAccess.file_exists(path): return null
	var image := Image.new()
	if image.load_png_from_buffer(FileAccess.get_file_as_bytes(path)) != OK: return null
	return ImageTexture.create_from_image(image)

func is_ready() -> bool:
	if animations.size() != 2 or gibs.size() != 2 or gibs.has(null): return false
	for frames in animations:
		if frames.is_empty() or frames.has(null): return false
	return true

func _sprite(layer: Node2D, texture: Texture2D) -> Sprite2D:
	var sprite := Sprite2D.new()
	sprite.texture = texture
	layer.add_child(sprite)
	return sprite

func flash_color() -> Color:
	return Color(data.bloodless_color if bloodless else data.blood_color)

func spawn_hit(center: Vector2, direction: Vector2, pixel_scale: float, floor_y: float, killed := false) -> bool:
	if not is_ready() or pixel_scale <= 0.0: return false
	emitted += 1
	var rng := RandomNumberGenerator.new()
	rng.seed = emitted * 7919
	var layer := Node2D.new()
	layer.position = center
	var effect_scale := pixel_scale * 2.0
	layer.scale = Vector2.ONE * effect_scale
	add_child(layer)
	var hit := {"layer": layer, "age": 0.0, "life": 0.15, "particles": [], "sprite": null,
		"frames": [], "floor": (floor_y - center.y) / effect_scale, "killed": killed}
	if not bloodless:
		var frames: Array = animations[rng.randi_range(0, animations.size() - 1)]
		var sprite := _sprite(layer, frames[0])
		var size := rng.randf_range(0.75, 1.0)
		# These source images extend towards negative X. Mirror for an incoming
		# left-to-right attack so the spray travels behind the struck target.
		sprite.scale = Vector2(size * (1 if direction.x < 0 else -1), size)
		hit.sprite = sprite
		hit.frames = frames
		hit.life = float(frames.size()) / float(data.fps)
		for index in range(15 if killed else 0):
			var small := index <= 9
			var particle := _sprite(layer, gibs[rng.randi_range(0, gibs.size() - 1)])
			particle.centered = false
			particle.offset = -particle.texture.get_size() * Vector2(0.5, 0.1)
			particle.position = Vector2(rng.randf_range(-5, 5), rng.randf_range(-5, 5))
			particle.rotation = rng.randf_range(0, TAU)
			particle.scale = Vector2.ONE * (rng.randf_range(1, 2) if small else 1.0)
			var shade := rng.randf_range(0.4, 1.0)
			particle.modulate = Color(shade, shade, shade, rng.randf_range(0.6, 1.0))
			var life := rng.randf_range(0.4, 2.0)
			hit.life = maxf(hit.life, life)
			var speed := rng.randf_range(1, 5) if small else rng.randf_range(3, 9)
			hit.particles.append({"sprite": particle, "velocity": Vector2(signf(direction.x) * speed, -rng.randf_range(1, 3)),
				"gravity": 0.25 if small else 0.32, "friction": rng.randf_range(0.96, 0.99),
				"spin": rng.randf_range(-0.2, 0.2), "life": life, "alpha": particle.modulate.a})
	if hits.size() >= 32: _dispose(hits.pop_front())
	hits.append(hit)
	return true

func _dispose(hit: Dictionary) -> void:
	var layer: Node2D = hit.layer
	layer.hide()
	layer.queue_free()

func clear() -> void:
	for hit in hits: _dispose(hit)
	hits.clear()
	for flash in flashes: _restore_flash(flash)
	flashes.clear()

func flash_target(target: CanvasItem) -> void:
	if not is_instance_valid(target): return
	for flash in flashes:
		if flash.target.get_ref() == target:
			flash.age = 0.0
			_apply_flash(flash)
			return
	var flash := {"target": weakref(target), "base": target.modulate, "last": target.modulate, "age": 0.0}
	flashes.append(flash)
	_apply_flash(flash)

func _apply_flash(flash: Dictionary) -> bool:
	var target: CanvasItem = flash.target.get_ref()
	if not is_instance_valid(target) or target.modulate != flash.last: return false
	var amount := 0.9 * maxf(0.0, 1.0 - float(flash.age) / float(data.blink_seconds))
	# Keep the game's existing skeletal material and its alpha. Node modulation
	# approximates the source color-matrix blink without replacing enemy shaders.
	flash.last = flash.base * Color.WHITE.lerp(flash_color(), amount)
	target.modulate = flash.last
	return true

func _restore_flash(flash: Dictionary) -> void:
	var target: CanvasItem = flash.target.get_ref()
	if is_instance_valid(target) and target.modulate == flash.last: target.modulate = flash.base

func _exit_tree() -> void:
	for flash in flashes: _restore_flash(flash)

func advance(delta: float) -> void:
	var pending_flashes: Array[Dictionary] = []
	for flash in flashes:
		flash.age += delta
		if flash.age >= float(data.blink_seconds): _restore_flash(flash)
		elif _apply_flash(flash): pending_flashes.append(flash)
	flashes = pending_flashes
	if hits.is_empty(): return
	# Source particle motion is measured per 30 Hz tick. Substeps keep coarse
	# manual frame advances consistent with normal preview playback.
	var steps := maxi(1, ceili(delta * 120.0))
	var step := delta / float(steps)
	for tick in range(steps):
		for hit in hits:
			hit.age += step
			if hit.sprite != null:
				var frame := int(float(hit.age) * float(data.fps))
				hit.sprite.visible = frame < hit.frames.size()
				if hit.sprite.visible: hit.sprite.texture = hit.frames[frame]
			for particle in hit.particles:
				var sprite: Sprite2D = particle.sprite
				sprite.visible = hit.age < float(particle.life)
				if not sprite.visible: continue
				var tmod := step * 30.0
				particle.velocity *= pow(float(particle.friction), tmod)
				particle.velocity.y += float(particle.gravity) * tmod
				sprite.position += particle.velocity * tmod
				if sprite.position.y >= float(hit.floor):
					sprite.position.y = hit.floor
					particle.velocity = Vector2.ZERO
				else: sprite.rotation += float(particle.spin) * tmod
				sprite.modulate.a = float(particle.alpha) * minf(1.0, (float(particle.life) - float(hit.age)) / 0.2)
	for hit in hits:
		if hit.age >= hit.life: _dispose(hit)
	hits = hits.filter(func(hit): return hit.age < hit.life)
