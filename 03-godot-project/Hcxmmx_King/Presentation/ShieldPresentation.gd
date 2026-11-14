extends Node2D

var actor: Node2D
var body: AnimatedSprite2D
var data: Dictionary
var active := false
var elapsed := 0.0
var original := false
var variant: Dictionary = {}
var variant_index := 0
var particles: Array[Dictionary] = []
var textures: Dictionary = {}
var rng := RandomNumberGenerator.new()

func bind_actor(host: Node2D, definitions: Dictionary = {}) -> void:
	actor = host
	body = actor.get_node("Body")
	data = definitions
	if data.is_empty():
		data = JSON.parse_string(FileAccess.get_file_as_string("res://Hcxmmx_King/Presentation/combat_presentation.json")).shield
	rng.randomize()
	texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	var additive := CanvasItemMaterial.new()
	additive.blend_mode = CanvasItemMaterial.BLEND_MODE_ADD
	material = additive
	for key in data.tiles:
		var image := Image.new()
		var error := image.load_png_from_buffer(FileAccess.get_file_as_bytes(data.tiles[key]))
		assert(error == OK, "Missing shield particle texture")
		textures[key] = ImageTexture.create_from_image(image)

func block(index: int, rhythm: int) -> String:
	if not active:
		variant_index = index % data.variants.size()
		variant = data.variants[variant_index]
	active = true
	original = rhythm == 1
	elapsed = 0.0
	body.stop()
	body.animation = variant.hold
	body.frame = 0
	# BaseShield.startParry and Fx.frontShieldHit use inward charge streaks,
	# followed by two 16-particle arcs with different friction and lifetimes.
	particles.clear()
	_emit_arc(11, true, 4.0, 0.97, 0.09, 0.09, "fxLineWhite", Color(data.start_color), 1.0)
	_emit_arc(16, false, 3.0, 0.93, 0.1, 0.3, "fxParryLine", Color(data.impact_color), 2.0)
	_emit_arc(16, false, 2.0, 0.88, 0.3, 0.5, "fxParryLine", Color(data.impact_color), 1.0)
	queue_redraw()
	return str(variant.sound)

func _emit_arc(count: int, inward: bool, speed: float, friction: float, low: float, high: float, tile: String, color: Color, scale_y: float) -> void:
	for index in range(count):
		var angle := -0.9 + 1.8 * float(index) / float(count - 1)
		var direction := Vector2(cos(angle), sin(angle))
		var origin := Vector2(float(variant.fx_origin[0]), float(variant.fx_origin[1]))
		var position := origin + Vector2(direction.x * 30.0, direction.y * 25.6)
		particles.append({"position": position, "velocity": direction * (-speed if inward else speed),
			"friction": friction, "life": rng.randf_range(low, high),
			"angle": angle + PI / 2.0, "tile": tile, "color": color, "scale_y": scale_y})

func cancel(play_idle := false) -> void:
	if active and play_idle and is_instance_valid(body) and body.animation in [variant.hold, variant.end]:
		body.play("Idle")
	active = false
	particles.clear()
	queue_redraw()

func advance(delta: float) -> void:
	if not active: return
	if not actor.is_visible_in_tree() or body.animation not in [variant.hold, variant.end]:
		cancel(not actor.is_visible_in_tree())
		return
	elapsed = maxf(0.0, elapsed + delta)
	var hold := float(data.hold_original if original else data.hold_fast)
	if elapsed >= hold:
		var fps := float(data.recovery_fps) * (1.0 if original else 1.4)
		var frame := int((elapsed - hold) * fps)
		if frame >= body.sprite_frames.get_frame_count(variant.end):
			cancel(true)
			return
		body.animation = variant.end
		body.frame = frame
	else:
		body.animation = variant.hold
		body.frame = 0
	queue_redraw()

func _process(delta: float) -> void:
	if is_instance_valid(actor) and is_instance_valid(body): advance(minf(delta, 0.2))

func _draw() -> void:
	for particle in particles:
		if elapsed >= particle.life: continue
		var color: Color = particle.color
		color.a = 0.7 * (1.0 - elapsed / float(particle.life))
		var texture: Texture2D = textures[particle.tile]
		# Sum the source 30-Hz friction steps analytically. Pause, rewind and
		# different renderer frame rates therefore display the same particle age.
		var distance := (1.0 - pow(float(particle.friction), elapsed * 30.0)) / (1.0 - float(particle.friction))
		var position: Vector2 = particle.position + particle.velocity * distance
		draw_set_transform(position, particle.angle, Vector2(1.0, particle.scale_y))
		draw_texture(texture, -texture.get_size() / 2.0, color)
	draw_set_transform(Vector2.ZERO)
