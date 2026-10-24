extends SceneTree

func _initialize() -> void:
	assert(ProjectSettings.load_resource_pack("D:/King_Workspace/05_Preview/Generated/king_source.pck"))
	var actor: Node2D = load("res://Hcxmmx_King/tscn/white_king.tscn").instantiate()
	var result := {}
	for node_name in ["Body", "VFX_Slash"]:
		var sprite: AnimatedSprite2D = actor.get_node(node_name)
		var animations := {}
		for name in sprite.sprite_frames.get_animation_names():
			if "vampirekiller" not in name.to_lower(): continue
			var frames := []
			for i in range(sprite.sprite_frames.get_frame_count(name)):
				frames.append({"path": sprite.sprite_frames.get_frame_texture(name, i).resource_path, "duration": sprite.sprite_frames.get_frame_duration(name, i)})
			animations[name] = {"fps": sprite.sprite_frames.get_animation_speed(name), "frames": frames}
		result[node_name] = {"position": str(sprite.position), "offset": str(sprite.offset), "scale": str(sprite.scale), "animations": animations}
	FileAccess.open("D:/King_Workspace/artifacts/battle-preview/whip-frames.json", FileAccess.WRITE).store_string(JSON.stringify(result, "  "))
	actor.free()
	print("WHIP_INSPECT_READY")
	quit()
