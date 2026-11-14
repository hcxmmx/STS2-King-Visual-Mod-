extends SceneTree

const SettingsPanel = preload("res://SelectionSettings.gd")

var screen: Control
var panel: Control
var definitions: Dictionary
var language := "zhs"
var white_king := false
var original_timing := false

func _initialize() -> void:
	_start.call_deferred()

func _start() -> void:
	root.title = "设置面板预览"
	AudioServer.set_bus_mute(0, true)
	assert(ProjectSettings.load_resource_pack(ProjectSettings.globalize_path("res://Generated/king_source.pck")))
	definitions = JSON.parse_string(FileAccess.get_file_as_string("res://settings_texts.json"))
	var config := ConfigFile.new()
	if config.load("user://settings-ui-preview.cfg") == OK:
		white_king = config.get_value("preview", "white_king", false)
		original_timing = config.get_value("preview", "original_timing", false)
	screen = Control.new()
	root.add_child(screen)
	screen.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	var background: Control = load("res://Hcxmmx_King/tscn/KingSelectUI.tscn").instantiate()
	screen.add_child(background)
	background.mouse_filter = Control.MOUSE_FILTER_IGNORE
	background.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	var languages := OptionButton.new()
	languages.position = Vector2(28, 28)
	for text in ["预览语言 · 简体中文", "Preview language · English", "プレビュー言語 · 日本語"]: languages.add_item(text)
	languages.item_selected.connect(func(index: int):
		language = ["zhs", "eng", "jpn"][index]
		_build_panel())
	screen.add_child(languages)
	_build_panel()

func _build_panel() -> void:
	if is_instance_valid(panel): panel.free()
	panel = SettingsPanel.new()
	screen.add_child(panel)
	panel.setup(definitions, language, white_king, original_timing)
	panel.settings_changed.connect(func(white: bool, original: bool):
		white_king = white
		original_timing = original
		var config := ConfigFile.new()
		config.set_value("preview", "white_king", white)
		config.set_value("preview", "original_timing", original)
		config.save("user://settings-ui-preview.cfg"))
	panel.set_open(true)
