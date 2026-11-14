extends RefCounted

static var texture_cache: Dictionary = {}

static func install(actor: Node2D, animations: Dictionary) -> void:
	# Packed base resources are shared; each preview owns its appended animations.
	for node_name in ["Body", "VFX_Slash"]:
		var sprite: AnimatedSprite2D = actor.get_node(node_name)
		sprite.sprite_frames = sprite.sprite_frames.duplicate()
	for name in animations:
		var definition: Dictionary = animations[name]
		var sprite: AnimatedSprite2D = actor.get_node(definition.node)
		assert(not sprite.sprite_frames.has_animation(name), "Would replace a baseline animation: " + name)
		sprite.sprite_frames.add_animation(name)
		sprite.sprite_frames.set_animation_loop(name, false)
		sprite.sprite_frames.set_animation_speed(name, float(definition.fps))
		for path in definition.frames:
			if not texture_cache.has(path):
				var picture := Image.new()
				var error := picture.load_png_from_buffer(FileAccess.get_file_as_bytes(path))
				assert(error == OK and not picture.is_empty(), "Missing raw frame: " + path)
				var created := ImageTexture.create_from_image(picture)
				created.take_over_path(path)
				texture_cache[path] = created
			var texture: Texture2D = texture_cache[path]
			sprite.sprite_frames.add_frame(name, texture)
