extends Node2D

signal cue_requested(kind: String)
signal damage_requested(target: int)

var body: AnimatedSprite2D
var data: Dictionary
var active := false
var clock := 0.0
var started_at := 0.0
var rhythm := 0
var released := false
var current: Dictionary = {}
var pending: Dictionary = {}
var flights: Array[Dictionary] = []
var damage_events: Array[Dictionary] = []
var arrow: Texture2D
var fired := 0

func bind_actor(host: Node2D, definitions: Dictionary) -> void:
	body = host.get_node("Body")
	data = definitions
	var image := Image.new()
	var error := image.load_png_from_buffer(FileAccess.get_file_as_bytes(data.arrow))
	if error != OK or image.is_empty():
		push_error("Unable to load preview arrow texture: " + str(data.arrow))
		return
	arrow = ImageTexture.create_from_image(image)
	texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST

func request(target: int, point: Vector2, mode: int) -> void:
	damage_events.append({"time": clock + 0.075, "target": target})
	var shot := {"target": target, "point": point, "mode": mode}
	if active: pending = shot
	else: _start(shot)

func _start(shot: Dictionary) -> void:
	current = shot
	rhythm = int(shot.mode)
	active = true
	released = false
	started_at = clock
	body.stop()
	body.animation = "FastBowPreview"
	body.frame = 0
	if rhythm == 1: cue_requested.emit("charge")

func cancel(play_idle := false) -> void:
	if active and play_idle and body.animation == &"FastBowPreview": body.animation = "Idle"
	active = false
	clock = 0.0
	fired = 0
	pending.clear()
	flights.clear()
	damage_events.clear()
	queue_redraw()

func advance(delta: float) -> void:
	clock += delta
	while not damage_events.is_empty() and float(damage_events[0].time) <= clock:
		var event: Dictionary = damage_events.pop_front()
		damage_requested.emit(int(event.target))
		cue_requested.emit("hit")
	if active:
		var elapsed := clock - started_at
		var charge := float(data.charge if rhythm == 1 else data.fast_charge)
		var count := body.sprite_frames.get_frame_count("FastBowPreview")
		var middle := float(count) * float(data.release_ratio)
		var fps := float(data.fps) * (1.0 if rhythm == 1 else 1.4)
		var frame := elapsed / charge * middle if elapsed < charge else middle + (elapsed - charge) * fps
		if not released and elapsed >= charge:
			released = true
			fired += 1
			var origin := Vector2(20.0, -45.0)
			flights.append({"origin": origin, "target": current.point, "time": started_at + charge,
				"duration": float(data.flight_original if rhythm == 1 else data.flight_fast)})
			cue_requested.emit("release")
		if released and not pending.is_empty() and elapsed >= charge + 0.035:
			var next := pending
			pending = {}
			_start(next)
		elif frame >= count:
			active = false
			body.animation = "Idle"
			body.frame = 0
		else:
			body.animation = "FastBowPreview"
			body.frame = mini(int(frame), count - 1)
	flights = flights.filter(func(flight): return clock - float(flight.time) < float(flight.duration))
	queue_redraw()

func _draw() -> void:
	for flight in flights:
		var progress := clampf((clock - float(flight.time)) / float(flight.duration), 0.0, 1.0)
		var origin: Vector2 = flight.origin
		var target: Vector2 = flight.target
		var point := origin.lerp(target, progress)
		var direction := (target - origin).normalized()
		draw_line(point - direction * 17.0, point, Color(1.0, 0.96, 0.0, 0.35), 1.0, false)
		draw_set_transform(point, direction.angle())
		draw_texture(arrow, -arrow.get_size() / 2.0, Color(data.color))
		draw_set_transform(Vector2.ZERO)
