extends Node2D

var arrow: Texture2D
var flights: Array[Dictionary] = []
var definitions: Dictionary

func _ready() -> void:
	texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	definitions = JSON.parse_string(FileAccess.get_file_as_string("res://Hcxmmx_King/Presentation/combat_presentation.json")).ranged
	var image := Image.new()
	var error := image.load_png_from_buffer(FileAccess.get_file_as_bytes(definitions.arrow))
	if error != OK or image.is_empty():
		push_warning("[King] Unable to load arrow texture: " + str(definitions.arrow))
		return
	arrow = ImageTexture.create_from_image(image)

func shoot(owner_id: int, shot: int, target: int, origin: Vector2, point: Vector2, pixel_scale: float, mode: int) -> void:
	if not is_instance_valid(arrow): return
	if flights.size() >= 64: flights.pop_front()
	flights.append({"owner": owner_id, "shot": shot, "target_id": target, "origin": origin, "point": point,
		"scale": pixel_scale, "age": 0.0, "duration": float(definitions.flight_original if mode == 1 else definitions.flight_fast)})
	queue_redraw()

func retarget(owner_id: int, shot: int, target: int, point: Vector2) -> void:
	for flight in flights:
		if flight.owner == owner_id and flight.shot == shot and flight.target_id == target: flight.point = point

func clear_owner(owner_id: int) -> void:
	flights = flights.filter(func(flight): return flight.owner != owner_id)
	queue_redraw()

func advance(delta: float) -> void:
	for flight in flights: flight.age += delta
	flights = flights.filter(func(flight): return flight.age < flight.duration)
	queue_redraw()

func _process(delta: float) -> void:
	advance(delta)

func _draw() -> void:
	for flight in flights:
		var progress := clampf(float(flight.age) / float(flight.duration), 0.0, 1.0)
		var origin: Vector2 = flight.origin
		var point := origin.lerp(flight.point, progress)
		var direction := (Vector2(flight.point) - origin).normalized()
		draw_line(point - direction * 17.0 * float(flight.scale), point, Color(1.0, 0.96, 0.0, 0.35), maxf(1.0, float(flight.scale)), false)
		draw_set_transform(point, direction.angle(), Vector2.ONE * float(flight.scale))
		draw_texture(arrow, -arrow.get_size() / 2.0, Color(definitions.color))
		draw_set_transform(Vector2.ZERO)
