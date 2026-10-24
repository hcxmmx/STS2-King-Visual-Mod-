extends SceneTree

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	var args := OS.get_cmdline_user_args()
	assert(args.size() == 1, "Expected candidate PCK path")
	assert(ProjectSettings.load_resource_pack(args[0]))
	var source: PackedScene = load("res://Hcxmmx_King/tscn/white_king.tscn")
	var script: GDScript = load("res://Hcxmmx_King/Presentation/KingScarf.gd")
	assert(source != null and script != null and script.can_instantiate())
	var data: Dictionary = JSON.parse_string(FileAccess.get_file_as_string("res://Hcxmmx_King/Presentation/presentation.json"))
	assert(data.unmapped_frames.is_empty())
	var scene := Node2D.new()
	root.add_child(scene)
	var checked := 0
	for white in [false, true]:
		for wind in [0.0, 1.0, -1.0, -2.0]:
			var actor: Node2D = source.instantiate()
			scene.add_child(actor)
			actor.scale = Vector2(4.0, 4.0)
			var body: AnimatedSprite2D = actor.get_node("Body")
			var scarf: Node2D = script.new()
			actor.add_child(scarf)
			actor.move_child(scarf, 0)
			scarf.bind_body(body, white, wind)
			scarf.set_process(false)
			assert(scarf.textures.size() == 2 and scarf.chains.size() == 5)
			assert(scarf.definitions == data.skins["KingWhite" if white else "KingDefault"].scarfs)
			assert(scarf.environment_wind == wind)
			for animation in body.sprite_frames.get_animation_names():
				body.play(animation)
				body.pause()
				if animation in data.attack_animations: scarf.weapon_use()
				for frame in range(body.sprite_frames.get_frame_count(animation)):
					body.frame = frame
					scarf._process(1.0 / 60.0)
					var texture := body.sprite_frames.get_frame_texture(animation, frame)
					assert(data.anchors.has(texture.resource_path), texture.resource_path)
					var point: Array = data.anchors[texture.resource_path]
					assert(scarf.anchor.is_equal_approx(body.transform * Vector2(point[0], point[1])))
					assert(scarf.is_front == data.front_frames[texture.resource_path])
					assert(scarf.z_index == (1 if scarf.is_front else 0))
					for chain in scarf.chains:
						for position in chain.points: assert(position.is_finite())
					checked += 1
				# All existing FX names must exist in the approved native scene.
				if data.vfx.has(animation):
					assert(actor.get_node("VFX_Slash").sprite_frames.has_animation(data.vfx[animation]))
			body.play("letha")
			assert(not scarf.visible, "Death must hide scarf immediately")
			scarf._process(1.0 / 60.0)
			assert(not scarf.visible, "Process must not show scarf during death")
			body.play("Idle")
			assert(scarf.visible, "Normal animation must restore scarf")
			scarf._process(1.0 / 60.0)
			var endpoint: Vector2 = scarf.chains[0].points[-1]
			actor.scale.x = -4.0
			scarf._process(0.0)
			assert(scarf.facing == -1.0)
			assert(scarf.chains[0].points[-1].is_equal_approx(Vector2(-endpoint.x, endpoint.y)))
			assert(scarf.chains[0].points[0].is_equal_approx(scarf._root(scarf.definitions[0])))
			scarf.set_skin(not white)
			assert(scarf.definitions == data.skins["KingDefault" if white else "KingWhite"].scarfs)
			body.visible = false
			scarf._process(0.0)
			assert(not scarf.visible)
			actor.free()
			assert(not is_instance_valid(scarf))
	print("RUNTIME_SCARF_PASS: candidate PCK; ", checked, " actual frame poses; 2 skins x 4 winds; flips, layers, FX names, skin switch, visibility, node cleanup")
	scene.free()
	quit()
