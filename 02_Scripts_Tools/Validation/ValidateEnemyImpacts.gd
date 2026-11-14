extends SceneTree

func _initialize() -> void:
	_validate.call_deferred()

func _validate() -> void:
	var preview: Control = load("res://Preview.tscn").instantiate()
	preview.set_meta("skip_auto_capture", true)
	root.add_child(preview)
	preview.set_process(false)
	AudioServer.set_bus_mute(0, true)
	assert(preview.impacts.textures.size() == 4)
	assert(preview.data.impacts.stages.size() == 43)
	for mode in [0, 1]:
		preview._set_rhythm(mode)
		for weapon in range(preview.data.weapons.size()):
			preview.weapon_index = weapon
			preview._run("combo", preview.data.weapons[weapon].stages.size())
			var before: int = preview.original_impacts.emitted
			var limit := 0
			while preview.resolved < preview.requested or not preview.requests.is_empty() or not preview.damages.is_empty():
				preview._step(1.0 / 120.0)
				limit += 1
				assert(limit < 12000)
			assert(preview.original_impacts.emitted - before == preview.resolved)
			assert(preview.resolved == preview.data.weapons[weapon].stages.size())
		preview._run("stardust", 50)
		var before: int = preview.original_impacts.emitted
		for tick in range(1500): preview._step(1.0 / 120.0)
		assert(preview.resolved == 50 and preview.original_impacts.emitted - before == 50)
		assert(preview.original_impacts.hits.is_empty())
		preview._preview_ranged(20)
		before = preview.original_impacts.emitted
		for tick in range(600): preview._step(1.0 / 120.0)
		assert(preview.resolved == 20 and preview.original_impacts.emitted - before == 20)
	preview._preview_enemy_block()
	assert(preview.impacts.hits.size() == 1 and preview.impacts.hits[0].style == "armor")
	for index in range(50): preview._target_impact(index % 3, "arrow")
	assert(preview.original_impacts.hits.size() == 32)
	var age: float = preview.original_impacts.hits[0].age
	preview.original_impacts.advance(0.0)
	assert(preview.original_impacts.hits[0].age == age)
	preview.original_impacts.advance(3.0)
	assert(preview.original_impacts.hits.is_empty())
	preview._target_impact(0, "arrow")
	preview._reset()
	assert(preview.original_impacts.hits.is_empty())
	for bloodless in [false, true]:
		preview.original_impacts.bloodless = bloodless
		for killed in [false, true]:
			preview._preview_original_hit(killed)
			assert(preview.original_impacts.hits.size() == 1)
			var hit: Dictionary = preview.original_impacts.hits[0]
			assert(hit.particles.size() == (15 if killed and not bloodless else 0))
			assert(hit.frames.size() in ([0] if bloodless else [10, 19]))
			assert(hit.killed == killed)
			assert(hit.layer.scale == Vector2(8, 8))
			assert(preview.arena.targets[0].hp == (0 if killed else 244))
			assert(preview.arena.targets[0].flash_color == Color("f78505" if bloodless else "ff0000"))
			preview._step(0.16)
			assert(preview.arena.targets[0].flash <= 0.0)
			preview._step(3.0)
			assert(preview.original_impacts.hits.is_empty())
	preview.original_impacts.bloodless = false
	for direction in [Vector2.RIGHT, Vector2.LEFT]:
		preview.original_impacts.clear()
		preview.original_impacts.spawn_hit(Vector2.ZERO, direction, 4.0, 100.0, true)
		var hit: Dictionary = preview.original_impacts.hits[0]
		assert(hit.sprite.scale.x * direction.x < 0.0)
		for particle in hit.particles: assert(particle.velocity.x * direction.x > 0.0)
	preview._reset()
	preview.arena.targets[0].hp = 7
	assert(not preview.arena.hit_target(0, 6))
	assert(preview.arena.hit_target(0, 1))
	assert(not preview.arena.hit_target(0, 1))
	assert(preview.arena.targets[0].flash == 0.0)
	preview._target_impact(0, "slash")
	assert(preview.original_impacts.hits[-1].particles.is_empty())
	preview.free()
	print("ENEMY_IMPACT_PREVIEW_PASS: 43 stages / both rhythms, 50 hits, 20 arrows, blood on/off, 2x size, both spray directions, lethal-only gibs, no repeated death, physical blink, armor, density and cleanup")
	quit()
