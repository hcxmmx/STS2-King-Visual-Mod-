extends Node

const Timing = preload("res://Hcxmmx_King/Presentation/RhythmTiming.gd")
const Extra = preload("res://Hcxmmx_King/Presentation/ExtraAnimations.gd")
const STEP := 1.0 / 120.0
static var data: Dictionary = {}

signal cue_requested(body_name: String, kind: String)
signal swing_started(body_name: String)
signal cancelled()
signal projectile_released(shot: int)

var actor: Node2D
var body: AnimatedSprite2D
var effect: AnimatedSprite2D
var mode := 0
var clock := 0.0
var accumulator := 0.0
var active: Dictionary = {}
var pending: Dictionary = {}
var fx: Dictionary = {}
var requested := 0
var started := 0
var contacts := 0
var merged := 0

func bind_actor(host: Node2D, rhythm: int) -> void:
	actor = host
	mode = rhythm
	if data.is_empty():
		data = JSON.parse_string(FileAccess.get_file_as_string("res://Hcxmmx_King/Presentation/combat_presentation.json"))
	Extra.install(actor, data.extra_animations)
	body = actor.get_node("Body")
	effect = actor.get_node("VFX_Slash")
	effect.stop()
	effect.hide()

func pending_body() -> String:
	return str(pending.get("name", ""))

func set_mode(rhythm: int) -> void:
	if rhythm == mode: return
	interrupt()
	mode = rhythm

func pending_shot() -> int:
	return int(pending.get("shot", 0))

func request_attack(name: String, shot := 0) -> void:
	assert(data.timing.has(name), "Missing runtime weapon timing: " + name)
	requested += 1
	if not pending.is_empty():
		pending.count += 1
		merged += 1
		return
	var source: Dictionary = data.timing[name]
	var attack := {"name": name, "profile": Timing.profile(source, source.weapon_id, int(source.stage_index), mode), "count": 1, "shot": shot}
	if active.is_empty(): _start(attack)
	else: pending = attack
	advance(0.0)

func _start(attack: Dictionary) -> void:
	if not fx.is_empty() and not fx.profile.get("fx_carry_over", true): fx.clear()
	active = attack
	active.time = clock
	active.released = false
	active.fx_started = false
	active.struck = false
	started += 1
	body.stop()
	body.animation = active.name
	body.frame = 0
	swing_started.emit(active.name)
	if active.profile.play_charge: cue_requested.emit(active.name, "charge")

func interrupt(play_idle := true) -> void:
	var was_attacking := not active.is_empty()
	active.clear()
	pending.clear()
	fx.clear()
	accumulator = 0.0
	if is_instance_valid(effect):
		effect.stop()
		effect.hide()
	if was_attacking and play_idle and is_instance_valid(body): body.play("Idle")
	cancelled.emit()

func _duration(profile: Dictionary, name: String) -> float:
	return maxf(Timing.duration(profile, body.sprite_frames.get_frame_count(name)), _handoff(profile))

func _handoff(profile: Dictionary) -> float:
	return maxf(Timing.contact_time(profile), float(profile.fx_visible_time)) + float(profile.handoff_after)

func advance(delta: float) -> void:
	clock += delta
	if not active.is_empty():
		var profile: Dictionary = active.profile
		var elapsed := clock - float(active.time)
		if not active.released and elapsed + 0.0000001 >= float(profile.get("release_time", profile.fx_time)):
			active.released = true
			if profile.get("ranged", false): projectile_released.emit(int(active.shot))
			cue_requested.emit(active.name, "release")
		if not active.fx_started and elapsed + 0.0000001 >= float(profile.fx_visible_time):
			active.fx_started = true
			if profile.vfx != null:
				fx = {"name": profile.vfx, "time": active.time, "profile": profile}
		if not active.struck and elapsed + 0.0000001 >= Timing.contact_time(profile):
			active.struck = true
			contacts += 1
		if not pending.is_empty() and elapsed + 0.0000001 >= _handoff(profile):
			var next := pending
			pending = {}
			_start(next)
		elif elapsed >= _duration(profile, active.name):
			active.clear()
			body.play("Idle")
	_render()

func _render() -> void:
	if not active.is_empty():
		var profile: Dictionary = active.profile
		var elapsed := clock - float(active.time)
		if elapsed < float(profile.body_delay):
			body.animation = "Idle"
			body.frame = int(clock * 30.0) % body.sprite_frames.get_frame_count("Idle")
		else:
			body.animation = active.name
			var frame := mini(Timing.frame_at(profile, elapsed), body.sprite_frames.get_frame_count(active.name) - 1)
			if profile.reverse: frame = body.sprite_frames.get_frame_count(active.name) - 1 - frame
			body.frame = frame
	effect.visible = not fx.is_empty()
	if effect.visible:
		var frame := Timing.fx_frame_at(fx.profile, clock - float(fx.time))
		effect.visible = frame >= 0 and frame < effect.sprite_frames.get_frame_count(fx.name)
		if effect.visible:
			effect.animation = fx.name
			effect.frame = frame

func _process(delta: float) -> void:
	if not is_instance_valid(actor) or not is_instance_valid(body): return
	if not actor.is_visible_in_tree():
		if not active.is_empty() or not pending.is_empty() or not fx.is_empty(): interrupt()
		return
	accumulator += minf(delta, 0.2)
	while accumulator >= STEP:
		accumulator -= STEP
		advance(STEP)
