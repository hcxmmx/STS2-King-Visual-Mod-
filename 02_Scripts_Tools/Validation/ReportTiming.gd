extends SceneTree

func _initialize() -> void:
	assert(ProjectSettings.load_resource_pack("D:/King_Workspace/05_Preview/Generated/king_source.pck"))
	var scene: PackedScene = load("res://Hcxmmx_King/tscn/white_king.tscn")
	var actor := scene.instantiate()
	var frames: SpriteFrames = actor.get_node("Body").sprite_frames
	var hits := {"atkPanA": 8, "atkPanB": 5, "atkPanC": 6, "atkPanD": 9, "AtkKatanaB2": 6, "AtkKatanaB3": 5, "AtkKatanaB4": 3, "atkBroadSwordA": 8, "atkBroadSwordB": 12, "atkBroadSwordC": 16}
	for name in hits:
		var total := 0.0
		var hit_time := 0.0
		for index in range(frames.get_frame_count(name)):
			var seconds := frames.get_frame_duration(name, index) / frames.get_animation_speed(name)
			total += seconds
			if index < hits[name]: hit_time += seconds
		print("TIMING ", name, " fps=", frames.get_animation_speed(name), " frames=", frames.get_frame_count(name), " total=", snappedf(total, 0.001), " source_hit_frame_time=", snappedf(hit_time, 0.001))
	actor.free()
	quit()
