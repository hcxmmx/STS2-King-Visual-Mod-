extends Node2D

# Port of King's original unlinked/non-isCape ScarfSegment branch.
# Evidence: original tool/ScarfSegment.hx:235-262,338-474.
var definitions: Array = []
var chains: Array = []
var textures: Array[Texture2D] = []
var anchor := Vector2(0, -50)
var brightness := 1.0
var playback_speed := 1.0
var running := true
var clock := 0.0
var accumulator := 0.0
var owner_velocity := Vector2.ZERO
var forced_motion := 0.0
var is_front := false
var environment_wind := 0.0 # World-space direction, independent of owner facing.
var facing := 1.0
var impulse_seconds := 0.0
var rng := RandomNumberGenerator.new()
var texture_root := "res://Hcxmmx_King/Presentation/"

func set_facing(value: float) -> void:
	if value == facing: return
	# Keep free endpoints in the same world position while the actor turns.
	var ratio := facing / value
	for chain in chains:
		var points: PackedVector2Array = chain.points
		var velocities: PackedVector2Array = chain.velocities
		for i in range(points.size()): points[i].x *= ratio
		for i in range(velocities.size()): velocities[i].x *= ratio
		chain.points = points
		chain.velocities = velocities
	facing = value

func set_front(value: bool) -> void:
	is_front = value
	z_index = 1 if value else 0
	# The attachment must also follow pose/layer changes while playback is paused.
	for k in range(chains.size()):
		var points: PackedVector2Array = chains[k].points
		points[0] = _root(definitions[k])
		chains[k].points = points
	queue_redraw()

func push_world(power: float) -> void:
	# ScarfSegment.push: original random 8..10, depth falloff, 0.4-second cooldown.
	impulse_seconds = 0.4
	for k in range(chains.size()):
		var velocities: PackedVector2Array = chains[k].velocities
		for j in range(velocities.size()):
			var depth := float(j) / float(velocities.size() - 1)
			velocities[j].x += rng.randf_range(8.0, 10.0) * power / facing * (0.3 + 0.7 * (1.0 - depth))
		chains[k].velocities = velocities

func weapon_use() -> void:
	# HeroWeaponsManager.onWeaponUse calls hero.pushScarf(-hero.dir).
	push_world(-facing)

func capture_state() -> Dictionary:
	return {"chains": _copy_chains(chains), "clock": clock, "accumulator": accumulator,
		"impulse_seconds": impulse_seconds, "rng_state": rng.state, "anchor": anchor,
		"facing": facing, "is_front": is_front, "owner_velocity": owner_velocity}

func restore_state(state: Dictionary) -> void:
	chains = _copy_chains(state.chains)
	clock = state.clock
	accumulator = state.accumulator
	impulse_seconds = state.impulse_seconds
	rng.state = state.rng_state
	anchor = state.anchor
	facing = state.facing
	owner_velocity = state.owner_velocity
	set_front(state.is_front)
	queue_redraw()

func _copy_chains(source: Array) -> Array:
	var result: Array = []
	for chain in source:
		var copy := {}
		for key in chain:
			# Packed arrays are reference types; snapshot them explicitly.
			copy[key] = chain[key].duplicate()
		result.append(copy)
	return result

func _ready() -> void:
	texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	var additive := CanvasItemMaterial.new()
	additive.blend_mode = CanvasItemMaterial.BLEND_MODE_ADD
	material = additive
	for i in range(2):
		# Reading bytes also works inside the game's PCK, without an editor import cache.
		var image := Image.new()
		var error := image.load_png_from_buffer(FileAccess.get_file_as_bytes(texture_root + "scarfGlow%d.png" % i))
		assert(error == OK, "Missing scarf texture")
		textures.append(ImageTexture.create_from_image(image))
	reset()

func configure(scarfs: Array) -> void:
	definitions = scarfs
	reset()

func _root(definition: Dictionary) -> Vector2:
	# Original headX - dir*3*(isFront?0:1) + dir*attachOffX;
	# the model parent already applies facing, so use facing-local coordinates.
	return anchor + Vector2(float(definition.attachOffX) - (0.0 if is_front else 3.0), 1.0 + float(definition.attachOffY))

func reset() -> void:
	chains.clear()
	accumulator = 0.0
	impulse_seconds = 0.0
	rng.seed = 1999 # Reproducible inspection; the original RNG stream is not claimed identical.
	for definition in definitions:
		assert(not definition.get("props", {}).get("isCape", false))
		var count := int(definition.count)
		var points := PackedVector2Array()
		var velocities := PackedVector2Array()
		var maximums := PackedFloat64Array()
		var run_factors := PackedFloat64Array()
		var thickness_phases := PackedFloat64Array()
		var lengths := PackedFloat64Array()
		var variants := PackedInt32Array()
		points.append(_root(definition))
		for j in range(count):
			points.append(_root(definition))
			velocities.append(Vector2.ZERO)
			maximums.append(0.0)
			run_factors.append(0.0)
			thickness_phases.append(0.0)
			lengths.append(0.0)
			variants.append(rng.randi_range(0, 1))
		chains.append({"points": points, "velocities": velocities, "maximums": maximums,
			"run_factors": run_factors, "thickness_phases": thickness_phases, "lengths": lengths, "variants": variants})
	queue_redraw()

func _process(delta: float) -> void:
	if running and visible:
		advance(delta * playback_speed)

func advance(delta: float) -> void:
	accumulator += minf(delta, 0.1)
	while accumulator >= 1.0 / 60.0:
		accumulator -= 1.0 / 60.0
		_step()
	queue_redraw()

func _step() -> void:
	clock += 1.0
	impulse_seconds = maxf(0.0, impulse_seconds - 1.0 / 60.0)
	if not is_zero_approx(environment_wind):
		# Hero.fixedUpdate: wind is applied separately from gravity, in world space.
		push_world(0.16 * (0.6 + 0.4 * cos(clock * 0.015)) * environment_wind)
	var impulse_motion := 0.6 if impulse_seconds > 0.0 else 0.0
	var movement := maxf(maxf(forced_motion, impulse_motion), clampf((absf(owner_velocity.x) + absf(owner_velocity.y)) / 0.28, 0.0, 1.0))
	for k in range(chains.size()):
		var definition: Dictionary = definitions[k]
		var chain: Dictionary = chains[k]
		var points: PackedVector2Array = chain.points
		var velocities: PackedVector2Array = chain.velocities
		var maximums: PackedFloat64Array = chain.maximums
		var run_factors: PackedFloat64Array = chain.run_factors
		var thickness_phases: PackedFloat64Array = chain.thickness_phases
		var lengths: PackedFloat64Array = chain.lengths
		var phase: float = definition.cosOffset
		var oscil: float = definition.get("props", {}).get("oscilFactor", 1.0)
		var count := int(definition.count)
		points[0] = _root(definition)
		for j in range(count):
			var depth := float(j) / float(count - 1)
			var target := movement * (0.85 + 0.15 * cos(clock * 0.07 + phase * 2.0))
			var run_factor := run_factors[j]
			run_factor += (target - run_factor) * (0.09 if target > 0.3 else 0.3)
			run_factors[j] = run_factor
			var velocity := velocities[j]
			velocity.y += (1.0 - run_factor) * float(definition.gravity) * (0.4 + 0.6 * (1.0 - depth))
			velocity.y += run_factor * oscil * cos(clock * 0.25 + phase * 2.0) * 0.4 * (1.0 - depth)
			velocity.y += run_factor * oscil * cos(clock * 0.04 + phase * 2.0 + depth * 6.28) * 0.1
			velocity.y += run_factor * oscil * cos(clock * 0.21 + phase * 2.0 + depth * 12.0) * 0.25 * depth
			var endpoint := points[j + 1] + velocity
			velocity *= float(definition.friction)
			var parent := points[j]
			var difference := endpoint - parent
			var length := difference.length()
			var target_max := lerpf(float(definition.minLength), float(definition.maxLength), run_factor) * (0.94 + 0.06 * cos(clock * 0.05 + phase * 2.0))
			maximums[j] += (target_max - maximums[j]) * 0.15
			if length > maximums[j] and length > 0.000001:
				endpoint = parent + difference * (maximums[j] / length)
				length = maximums[j]
			endpoint.y += run_factor * oscil * 0.5 * sin(clock * 0.15 + phase + depth * 2.5) * (1.0 - depth)
			points[j + 1] = endpoint
			velocities[j] = velocity
			lengths[j] = length
			thickness_phases[j] += 0.07 * movement
		chain.points = points
		chain.velocities = velocities
		chain.maximums = maximums
		chain.run_factors = run_factors
		chain.thickness_phases = thickness_phases
		chain.lengths = lengths

func _draw() -> void:
	if textures.is_empty(): return
	for k in range(chains.size()):
		var definition: Dictionary = definitions[k]
		var chain: Dictionary = chains[k]
		var points: PackedVector2Array = chain.points
		var rgb := int(definition.color)
		var color := Color(float((rgb >> 16) & 255) / 255.0, float((rgb >> 8) & 255) / 255.0, float(rgb & 255) / 255.0)
		var count := int(definition.count)
		var depth_scale: float = definition.get("props", {}).get("depthScaleFactor", 0.7)
		if is_zero_approx(depth_scale): depth_scale = 0.7
		for j in range(count):
			var depth := float(j) / float(count - 1)
			var scale_y := 0.1 + 0.6 * absf(cos(float(chain.thickness_phases[j]) + depth * 3.14 + float(definition.cosOffset) * 2.9)) * (0.3 + depth_scale * (1.0 - depth))
			scale_y *= float(definition.thickness)
			var sprite_width := float(chain.lengths[j]) + float(definition.get("extraSprLength", 3))
			var sprite_height := 13.0 * scale_y
			var tint := color * brightness
			tint.a = 0.75 if j == 0 else 1.0
			draw_set_transform(points[j], (points[j + 1] - points[j]).angle())
			# Original pivot=(0.1,0.5). Preserve the full textured halo, not a stretched line.
			draw_texture_rect(textures[int(chain.variants[j])], Rect2(-sprite_width * 0.1, -sprite_height * 0.5, sprite_width, sprite_height), false, tint)
	draw_set_transform(Vector2.ZERO)
