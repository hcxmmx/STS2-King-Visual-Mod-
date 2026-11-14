extends Control

signal settings_changed(white_king: bool, original_timing: bool)
signal opened_changed(value: bool)

const IconButton = preload("SettingsIconButton.gd")

var texts: Dictionary
var ui_font: SystemFont
var entry: Button
var panel: Panel
var dismiss: ColorRect
var content: VBoxContainer
var rows: Dictionary = {}
var section_labels: Dictionary = {}
var title: Label
var close_button: Button
var saved: Label
var rhythm_hint: Label
var white_king := false
var original_timing := false
var opened := false
var ui_scale := 1.0
var cancel_release := ""

func setup(definitions: Dictionary, language: String, white: bool, original: bool) -> void:
	white_king = white
	original_timing = original
	texts = definitions.translations[language]
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	ui_font = SystemFont.new()
	ui_font.font_names = PackedStringArray(["Inter", "Microsoft YaHei UI", "Yu Gothic UI", "Segoe UI"])
	theme = Theme.new()
	theme.default_font = ui_font
	theme.default_font_size = 16
	dismiss = ColorRect.new()
	dismiss.color = Color(0.02, 0.03, 0.05, 0.16)
	dismiss.mouse_filter = Control.MOUSE_FILTER_STOP
	add_child(dismiss)
	dismiss.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	dismiss.gui_input.connect(func(event: InputEvent):
		if event is InputEventMouseButton and event.pressed:
			set_open(false)
			dismiss.accept_event())
	panel = Panel.new()
	panel.mouse_filter = Control.MOUSE_FILTER_STOP
	var surface := _style(Color("151d29"), Color("354050"), 12)
	surface.shadow_color = Color(0, 0, 0, 0.3)
	surface.shadow_size = 24
	surface.shadow_offset = Vector2(0, 8)
	panel.add_theme_stylebox_override("panel", surface)
	add_child(panel)
	content = VBoxContainer.new()
	content.position = Vector2(24, 22)
	content.size = Vector2(328, 0)
	content.add_theme_constant_override("separation", 18)
	panel.add_child(content)
	var header := HBoxContainer.new()
	content.add_child(header)
	title = _label(header, texts.title, 24, Color("f2f4f8"))
	title.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	close_button = _icon_button(header, "close", func(): set_open(false))
	close_button.tooltip_text = texts.close
	close_button.custom_minimum_size = Vector2(32, 32)
	_section("skin", ["blue", "white"])
	_section("rhythm", ["fast", "original"])
	rhythm_hint = _label(content, "", 13, Color("9aa8ba"))
	rhythm_hint.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	saved = _label(content, texts.saved, 12, Color("78889c"))
	saved.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	entry = _icon_button(self, "gear", func(): set_open(not opened))
	entry.tooltip_text = texts.open
	entry.custom_minimum_size = Vector2(44, 44)
	_refresh()
	set_open(false)
	resized.connect(_layout)
	content.minimum_size_changed.connect(_layout)
	visibility_changed.connect(func():
		if not is_visible_in_tree(): set_open(false))
	var buttons: Array[Control] = [close_button, rows.blue, rows.white, rows.fast, rows.original, entry]
	for index in buttons.size():
		buttons[index].focus_next = buttons[index].get_path_to(buttons[(index + 1) % buttons.size()])
		buttons[index].focus_previous = buttons[index].get_path_to(buttons[(index - 1 + buttons.size()) % buttons.size()])
	_layout()

func _style(background: Color, border: Color, radius: int) -> StyleBoxFlat:
	var style := StyleBoxFlat.new()
	style.bg_color = background
	style.border_color = border
	style.set_border_width_all(1)
	style.set_corner_radius_all(radius)
	return style

func _label(parent: Node, text: String, font_size: int, color: Color) -> Label:
	var label := Label.new()
	label.text = text
	label.add_theme_font_size_override("font_size", font_size)
	label.add_theme_color_override("font_color", color)
	parent.add_child(label)
	return label

func _icon_button(parent: Node, icon: String, action: Callable) -> Button:
	var button := IconButton.new()
	button.icon_kind = icon
	button.focus_mode = Control.FOCUS_ALL
	button.add_theme_stylebox_override("normal", _style(Color("192331"), Color("354354"), 8))
	button.add_theme_stylebox_override("hover", _style(Color("273548"), Color("647d99"), 8))
	button.add_theme_stylebox_override("pressed", _style(Color("30435a"), Color("7f9dbd"), 8))
	button.add_theme_stylebox_override("focus", _focus_style())
	button.pressed.connect(action)
	parent.add_child(button)
	return button

func _focus_style() -> StyleBoxFlat:
	var focus := _style(Color(0, 0, 0, 0), Color("9ab9da"), 7)
	return focus

func _section(key: String, values: Array) -> void:
	var group := VBoxContainer.new()
	group.add_theme_constant_override("separation", 8)
	content.add_child(group)
	section_labels[key] = _label(group, texts[key], 15, Color("b3becc"))
	var track := PanelContainer.new()
	track.add_theme_stylebox_override("panel", _style(Color("0f1722"), Color("2b3747"), 8))
	group.add_child(track)
	var padding := MarginContainer.new()
	for side in ["left", "top", "right", "bottom"]: padding.add_theme_constant_override("margin_" + side, 4)
	track.add_child(padding)
	var choices := HBoxContainer.new()
	choices.add_theme_constant_override("separation", 4)
	padding.add_child(choices)
	for value in values:
		var button := Button.new()
		button.text = texts[value]
		button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		button.custom_minimum_size = Vector2(0, 36)
		button.add_theme_font_size_override("font_size", 16)
		button.add_theme_stylebox_override("focus", _focus_style())
		button.pressed.connect(func(): _select(key, value))
		choices.add_child(button)
		rows[value] = button

func _select(key: String, value: String) -> void:
	if key == "skin": white_king = value == "white"
	else: original_timing = value == "original"
	_refresh()
	settings_changed.emit(white_king, original_timing)

func _refresh() -> void:
	var selection := {"blue": not white_king, "white": white_king, "fast": not original_timing, "original": original_timing}
	for key in rows:
		var selected: bool = selection[key]
		var normal := _style(Color("33475f") if selected else Color(0, 0, 0, 0), Color("526f8e") if selected else Color(0, 0, 0, 0), 5)
		rows[key].add_theme_stylebox_override("normal", normal)
		rows[key].add_theme_stylebox_override("hover", _style(Color("304157"), Color("566e89"), 5))
		rows[key].add_theme_stylebox_override("pressed", _style(Color("3b5370"), Color("7999bb"), 5))
		rows[key].add_theme_color_override("font_color", Color("f1f5fa") if selected else Color("9eacbf"))
		rows[key].add_theme_color_override("font_hover_color", Color("f1f5fa"))
	rhythm_hint.text = texts.original_hint if original_timing else texts.fast_hint

func sync_settings(translations: Dictionary, white: bool, original: bool) -> void:
	texts = translations
	white_king = white
	original_timing = original
	title.text = texts.title
	entry.tooltip_text = texts.open
	close_button.tooltip_text = texts.close
	saved.text = texts.saved
	for key in section_labels: section_labels[key].text = texts[key]
	for key in rows: rows[key].text = texts[key]
	_refresh()

func _layout() -> void:
	if not is_instance_valid(content): return
	var width := minf(376, size.x - 32)
	content.size.x = width - 48
	panel.size = Vector2(width, maxf(330, content.get_combined_minimum_size().y + 44))
	panel.position = Vector2(size.x - width - 28, 154)
	entry.size = Vector2(44, 44)
	entry.position = Vector2(size.x - 72, 100)

func set_open(value: bool) -> void:
	var changed := opened != value
	opened = value
	panel.visible = value
	dismiss.visible = value
	entry.active = value
	entry.queue_redraw()
	if changed and not value and is_visible_in_tree():
		entry.grab_focus()
	elif value and white_king:
		rows.white.grab_focus()
	elif value:
		rows.blue.grab_focus()
	if changed: opened_changed.emit(value)

func _input(event: InputEvent) -> void:
	# The game's back button acts on release. Keep that release inside this popup.
	if not cancel_release.is_empty() and event.is_action_released(cancel_release):
		cancel_release = ""
		get_viewport().set_input_as_handled()
		return
	if not opened or not is_visible_in_tree(): return
	for action in ["ui_cancel", "mega_pause_and_back", "mega_back"]:
		if InputMap.has_action(action) and event.is_action_pressed(action):
			cancel_release = action
			set_open(false)
			get_viewport().set_input_as_handled()
			return
