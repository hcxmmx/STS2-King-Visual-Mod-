extends "res://Hcxmmx_King/Presentation/ScarfRig.gd"

const DATA_PATH := "res://Hcxmmx_King/Presentation/presentation.json"
var body: AnimatedSprite2D
var data: Dictionary
var white_skin := false
var cached_frame_path := ""

func bind_body(sprite: AnimatedSprite2D, white: bool, wind: float) -> void:
	body = sprite
	data = JSON.parse_string(FileAccess.get_file_as_string(DATA_PATH))
	environment_wind = wind
	_update_pose()
	set_skin(white)
	body.frame_changed.connect(_update_pose)
	body.animation_changed.connect(_update_pose)

func set_skin(white: bool) -> void:
	if not definitions.is_empty() and white_skin == white: return
	white_skin = white
	var key := "KingWhite" if white else "KingDefault"
	configure(data.skins[key].scarfs)

func _update_pose() -> void:
	if not is_instance_valid(body): return
	# Hide on the animation signal, before the next render/process tick.
	visible = body.visible and body.animation != &"letha"
	var direction := -1.0 if get_parent().scale.x < 0.0 else 1.0
	if direction != facing: cached_frame_path = ""
	set_facing(direction)
	var frame := mini(body.frame, body.sprite_frames.get_frame_count(body.animation) - 1)
	if frame < 0: return
	var texture := body.sprite_frames.get_frame_texture(body.animation, frame)
	if texture == null: return
	var path := texture.resource_path
	if path == cached_frame_path: return
	cached_frame_path = path
	var point: Array = data.anchors.get(path, [0.0, -50.0])
	# Body offset/scale are retained from the approved scene.
	anchor = body.transform * Vector2(point[0], point[1])
	set_front(data.front_frames.get(path, false))

func _process(delta: float) -> void:
	_update_pose()
	super._process(delta)
