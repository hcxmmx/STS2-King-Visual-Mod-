extends SceneTree

const Timing = preload("res://RhythmTiming.gd")
const OUTPUT = "D:/King_Workspace/artifacts/weapon-expansion/scissor-alignment"

func _initialize() -> void:
	_capture.call_deferred()

func _capture() -> void:
	root.size = Vector2i(1440, 900)
	var preview: Control = load("res://Preview.tscn").instantiate()
	preview.set_meta("skip_auto_capture", true)
	root.add_child(preview)
	preview.audio_enabled = false
	preview._set_rhythm(Timing.Mode.FAST)
	for index in range(preview.data.weapons.size()):
		if preview.data.weapons[index].id == "scissors":
			preview.weapon_index = index
			preview.weapon_select.select(index)
	DirAccess.make_dir_recursive_absolute(OUTPUT)
	var report := []
	for stage in range(2):
		var hit: int = [15, 13][stage]
		for frame in [6, hit]:
			for legacy in [true, false]:
				preview._reset()
				preview.combo_index = stage
				preview._manual_attack()
				var profile: Dictionary = preview.active.profile
				if legacy:
					profile.fx_clock = "independent"
					profile.fx_visible_time = Timing.contact_time(profile)
					profile.fx_fps = 37.5
				preview._step(Timing.frame_time(profile, frame))
				preview.paused = true
				preview.scenario = "校准前" if legacy else "共用动作进度"
				preview._process(0.0)
				await RenderingServer.frame_post_draw
				var filename := "%s-%s-body%02d.png" % ["A" if stage == 0 else "B", "before" if legacy else "after", frame]
				root.get_texture().get_image().save_png(OUTPUT + "/" + filename)
				var source_fx_frame := -1
				if preview.effect.visible:
					source_fx_frame = int(profile.fx_lead_frames) + preview.effect.frame
				report.append({"stage": stage, "legacy": legacy, "body_frame": preview.body.frame,
					"fx_visible": preview.effect.visible, "source_fx_frame": source_fx_frame, "file": filename})
	FileAccess.open(OUTPUT + "/capture-report.json", FileAccess.WRITE).store_string(JSON.stringify(report, "  "))
	print("SCISSOR_ALIGNMENT_CAPTURE_PASS: native before/after poses and source frame indices captured")
	preview.queue_free()
	await process_frame
	quit()
