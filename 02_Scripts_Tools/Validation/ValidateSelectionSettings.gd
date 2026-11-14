extends SceneTree

func _initialize() -> void:
	_validate.call_deferred()

func _validate() -> void:
	var definitions: Dictionary = JSON.parse_string(FileAccess.get_file_as_string("res://settings_texts.json"))
	var scene := Control.new()
	root.add_child(scene)
	scene.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	for language in ["zhs", "eng", "jpn"]:
		var panel: Control = load("res://SelectionSettings.gd").new()
		scene.add_child(panel)
		panel.setup(definitions, language, false, false)
		await process_frame
		assert(not panel.opened)
		for text in definitions.translations[language].values():
			for character in str(text): assert(panel.ui_font.has_char(character.unicode_at(0)))
		assert(panel.entry.text.is_empty() and panel.entry.icon_kind == "gear")
		panel.entry.pressed.emit()
		assert(panel.opened and panel.dismiss.visible)
		panel.rows.white.pressed.emit()
		panel.rows.original.pressed.emit()
		assert(panel.white_king and panel.original_timing)
		assert(panel.rhythm_hint.text == definitions.translations[language].original_hint)
		panel.rows.blue.pressed.emit()
		panel.rows.fast.pressed.emit()
		assert(not panel.white_king and not panel.original_timing)
		panel.close_button.pressed.emit()
		assert(not panel.opened and not panel.dismiss.visible)
		panel.set_open(true)
		var cancel := InputEventAction.new()
		cancel.action = "ui_cancel"
		cancel.pressed = true
		panel._input(cancel)
		assert(not panel.opened)
		assert(panel.cancel_release == "ui_cancel")
		cancel.pressed = false
		panel._input(cancel)
		assert(panel.cancel_release.is_empty())
		var alternate: String = "eng" if language != "eng" else "jpn"
		panel.sync_settings(definitions.translations[alternate], true, true)
		assert(panel.title.text == definitions.translations[alternate].title)
		assert(panel.rows.white.text == definitions.translations[alternate].white)
		assert(panel.rhythm_hint.text == definitions.translations[alternate].original_hint)
		for width in [960, 1440, 1920]:
			panel.size = Vector2(width, 900)
			panel._layout()
			assert(panel.panel.position.x >= 0.0)
			assert(panel.panel.position.x + panel.panel.size.x * panel.ui_scale <= width)
			assert(panel.entry.position.y >= 100.0)
		panel.free()
	scene.free()
	print("SELECTION_SETTINGS_PASS: three locales, gear entry, skin/rhythm controls, close/escape, click blocker and responsive upper-right placement")
	quit()
