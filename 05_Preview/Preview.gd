extends Control

const ScarfRig = preload("res://Generated/ScarfRig.gd")
const BattleStage = preload("res://BattleStage.gd")
const SCENE_PATH = "res://Hcxmmx_King/tscn/white_king.tscn"
const NAMES = {"pan": "平底锅", "katana": "武士刀", "broad_sword": "重剑", "vampire_killer": "吸血鬼杀手", "scythe": "巨镰", "halberd": "战戟", "perfect_halberd": "完美战戟"}
const STEP = 1.0 / 120.0

var data: Dictionary
var body: AnimatedSprite2D
var effect: AnimatedSprite2D
var actor: Node2D
var rig: Node2D
var arena: Node2D
var viewport: SubViewport
var weapon_select: OptionButton
var status: Label
var frame_info: Label
var events_label: Label
var pause_button: Button
var star_count: SpinBox
var loop_button: CheckButton
var clock := 0.0
var accumulator := 0.0
var paused := false
var time_scale := 1.0
var audio_enabled := true
var active_skin := "KingWhite"
var weapon_index := 0
var combo_index := 0
var current := "Idle"
var active: Dictionary = {}
var pending: Dictionary = {}
var swings := 0
var grouped := 0
var hit_sounds := 0
var fx_active: Dictionary = {}
var requests: Array = []
var damages: Array = []
var requested := 0
var resolved := 0
var visible_hits := 0
var interrupted := 0
var max_lag := 0.0
var scenario := "待机"
var scenario_count := 0
var scenario_kind := ""
var repeat_at := -1.0
var trace: Array[String] = []
var audio_streams: Dictionary = {}
var voices: Array[AudioStreamPlayer] = []
var rng := RandomNumberGenerator.new()
var inspect_frame := -1
var inspect_at := 0.0
var last_frame := 0
var wind := 1.0

func _ready() -> void:
	data = JSON.parse_string(FileAccess.get_file_as_string("res://Generated/battle_data.json"))
	assert(ProjectSettings.load_resource_pack(ProjectSettings.globalize_path("res://Generated/king_source.pck")))
	var scene: PackedScene = load(SCENE_PATH)
	_build_ui(scene)
	_apply_skin()
	_reset()
	if "--validate" in OS.get_cmdline_user_args() or "--validate-playback" in OS.get_cmdline_user_args() or "--validate-audio" in OS.get_cmdline_user_args():
		_validate.call_deferred()
	else:
		if "--capture-stardust" in OS.get_cmdline_user_args():
			audio_enabled = false
			_run("stardust", 50)
		_capture.call_deferred()

func _row(parent: Node, spacing := 10) -> HBoxContainer:
	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", spacing)
	parent.add_child(row)
	return row

func _label(parent: Node, text: String, size := 17) -> Label:
	var label := Label.new()
	label.text = text
	label.add_theme_font_size_override("font_size", size)
	parent.add_child(label)
	return label

func _button(parent: Node, text: String, action: Callable) -> Button:
	var button := Button.new()
	button.text = text
	button.custom_minimum_size.y = 38
	button.pressed.connect(action)
	parent.add_child(button)
	return button

func _build_ui(scene: PackedScene) -> void:
	var font := SystemFont.new()
	font.font_names = PackedStringArray(["Microsoft YaHei UI", "Microsoft YaHei"])
	theme = Theme.new()
	theme.default_font = font
	theme.default_font_size = 17
	var margin := MarginContainer.new()
	margin.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	for side in ["left", "right", "top", "bottom"]: margin.add_theme_constant_override("margin_" + side, 22)
	add_child(margin)
	var layout := VBoxContainer.new()
	layout.add_theme_constant_override("separation", 10)
	margin.add_child(layout)
	var heading := _row(layout)
	var title := _label(heading, "国王 · 模拟战斗", 27)
	title.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_label(heading, "死亡细胞原作基础节奏  /  尖塔加速模式", 16).modulate = Color("a4c6bc")
	_label(layout, "伤害与击中声跟随出牌；当前挥击完成后接下一段。密集攻击合并表现，保留原作挥击速度，不堆积长队。", 15).modulate = Color("99adb7")
	var settings := _row(layout)
	_label(settings, "皮肤")
	var skin := OptionButton.new()
	skin.add_item("蓝王")
	skin.add_item("白王 · 无暇者")
	skin.select(1)
	skin.item_selected.connect(func(index: int):
		active_skin = "KingDefault" if index == 0 else "KingWhite"
		_apply_skin())
	settings.add_child(skin)
	_label(settings, "武器")
	weapon_select = OptionButton.new()
	for i in range(data.weapons.size()):
		var id: String = data.weapons[i].id
		weapon_select.add_item(NAMES.get(id, id))
		if id == "pan": weapon_index = i
	weapon_select.select(weapon_index)
	weapon_select.item_selected.connect(func(index: int):
		weapon_index = index
		_reset())
	settings.add_child(weapon_select)
	var sound := CheckButton.new()
	sound.text = "音效"
	sound.button_pressed = true
	sound.toggled.connect(func(on: bool):
		audio_enabled = on
		if not on: _stop_audio())
	settings.add_child(sound)
	_label(settings, "环境风")
	var wind_select := OptionButton.new()
	for text in ["默认风 →", "反向风 ←", "强逆风 ←"]: wind_select.add_item(text)
	wind_select.item_selected.connect(func(index: int):
		wind = [1.0, -1.0, -2.0][index]
		rig.environment_wind = wind)
	settings.add_child(wind_select)
	var stage_container := SubViewportContainer.new()
	stage_container.stretch = true
	stage_container.size_flags_vertical = Control.SIZE_EXPAND_FILL
	stage_container.custom_minimum_size = Vector2(600, 390)
	layout.add_child(stage_container)
	viewport = SubViewport.new()
	viewport.size = Vector2i(1350, 500)
	viewport.world_2d = World2D.new()
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	stage_container.add_child(viewport)
	arena = BattleStage.new()
	viewport.add_child(arena)
	actor = scene.instantiate()
	arena.add_child(actor)
	body = actor.get_node("Body")
	body.stop()
	effect = actor.get_node("VFX_Slash")
	effect.stop()
	effect.hide()
	actor.get_node("SerenadePet/ChatLabel").hide()
	rig = ScarfRig.new()
	rig.texture_root = "res://Generated/"
	actor.add_child(rig)
	actor.move_child(rig, 0)
	rig.set_process(false)
	var actions := _row(layout)
	_button(actions, "打击 · 单次", _manual_attack)
	_button(actions, "原作连段", func(): _run("combo", data.weapons[weapon_index].stages.size()))
	_button(actions, "星尘 · X=", func(): _run("stardust", int(star_count.value)))
	star_count = SpinBox.new()
	star_count.min_value = 1
	star_count.max_value = 100
	star_count.value = 20
	star_count.custom_minimum_size.x = 88
	actions.add_child(star_count)
	_button(actions, "极限 · 星尘 50 连击", func(): _run("stardust", 50))
	_button(actions, "停止 / 重置", _reset)
	loop_button = CheckButton.new()
	loop_button.text = "循环演示"
	actions.add_child(loop_button)
	var playback := _row(layout)
	pause_button = _button(playback, "暂停", _toggle_pause)
	_button(playback, "上一帧", func(): _step_frame(-1))
	_button(playback, "下一帧", func(): _step_frame(1))
	_label(playback, "观察速度")
	var speed := OptionButton.new()
	for text in ["0.25×", "0.5×", "1×"]: speed.add_item(text)
	speed.select(2)
	speed.item_selected.connect(func(index: int): time_scale = [0.25, 0.5, 1.0][index])
	playback.add_child(speed)
	_button(playback, "受击", func(): _pose("stun"))
	_button(playback, "胜利", func(): _pose("travolta"))
	_button(playback, "死亡", func(): _pose("letha"))
	frame_info = _label(playback, "", 15)
	frame_info.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	frame_info.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	status = _label(layout, "", 18)
	events_label = _label(layout, "", 14)
	events_label.custom_minimum_size.y = 44
	events_label.modulate = Color("99adb7")
	_label(layout, "普通打击：0.075s 后结算；星尘基础连击间隔约 0.175s。数字/受击是模拟反馈，未模拟能力和遗物额外演出。", 13).modulate = Color("718a96")

func _apply_skin() -> void:
	var material := body.material.duplicate() as ShaderMaterial
	material.set_shader_parameter("palette_lut", load("res://Hcxmmx_King/Shaders/king_whiteKing_s.png" if active_skin == "KingWhite" else "res://Hcxmmx_King/Shaders/king_default_s.png"))
	material.set_shader_parameter("glow_color_1", Color(1, 0.9, 0.5) if active_skin == "KingWhite" else Color(1, 0.82, 0.2))
	material.set_shader_parameter("glow_color_2", Color.WHITE if active_skin == "KingWhite" else Color(0.2, 0.8, 1))
	body.material = material
	rig.configure(data.skins[active_skin].scarfs)

func _stop_audio() -> void:
	for player in voices:
		if is_instance_valid(player):
			player.stop()
			player.queue_free()
	voices.clear()

func _sound(profile: Dictionary, kind: String) -> void:
	if not audio_enabled or not profile.get("audio", {}).has(kind): return
	var cue: Dictionary = profile.audio[kind]
	if not audio_streams.has(cue.path):
		audio_streams[cue.path] = AudioStreamOggVorbis.load_from_buffer(FileAccess.get_file_as_bytes(cue.path))
	var stream: AudioStreamOggVorbis = audio_streams[cue.path]
	assert(stream != null)
	stream.loop = false
	voices = voices.filter(func(player): return is_instance_valid(player) and not player.is_queued_for_deletion())
	if voices.size() >= 8:
		voices[0].stop()
		voices[0].queue_free()
		voices.remove_at(0)
	var player := AudioStreamPlayer.new()
	player.stream = stream
	player.volume_db = float(cue.volume_db)
	player.set_meta("cue_kind", kind)
	add_child(player)
	player.finished.connect(player.queue_free)
	player.play()
	voices.append(player)
	if kind == "hit": hit_sounds += 1

func _reset() -> void:
	_stop_audio()
	clock = 0.0
	accumulator = 0.0
	combo_index = 0
	requests.clear()
	damages.clear()
	active.clear()
	pending.clear()
	swings = 0
	grouped = 0
	hit_sounds = 0
	fx_active.clear()
	trace.clear()
	requested = 0
	resolved = 0
	visible_hits = 0
	interrupted = 0
	max_lag = 0.0
	repeat_at = -1.0
	scenario = "待机"
	scenario_count = 0
	scenario_kind = ""
	current = "Idle"
	inspect_frame = -1
	paused = false
	arena.reset_targets()
	rng.seed = 1999
	rig.environment_wind = wind
	rig.reset()
	_update_pose()
	_update_ui()

func _event(text: String) -> void:
	trace.append("%5.2fs  %s" % [clock, text])
	if trace.size() > 3: trace.pop_front()

func _manual_attack() -> void:
	if current == "letha": _reset()
	paused = false
	inspect_frame = -1
	scenario = "连续手动出牌"
	_issue(clock, rng.randi_range(0, 2), 6)

func _run(kind: String, count: int) -> void:
	_reset()
	scenario_kind = kind
	scenario_count = count
	scenario = "星尘 · %d 连击" % count if kind == "stardust" else "原作连段"
	var at := 0.0
	var stages: Array = data.weapons[weapon_index].stages
	for i in range(count):
		requests.append({"time": at, "target": rng.randi_range(0, 2), "damage": 5 if kind == "stardust" else 6})
		var profile: Dictionary = data.timing[stages[i % stages.size()].body]
		at += float(data.spires_fast.interval) if kind == "stardust" else maxf(float(profile.charge), _visual_hit_time(profile)) + float(profile.lock_after)
	_step(0.0)

func _issue(at: float, target: int, damage: int) -> void:
	requested += 1
	# Reserve at most one following swing. Inputs share that swing's audio profile,
	# while each damage event retains its own target, amount and immediate hit cue.
	var attack: Dictionary
	if active.is_empty():
		attack = _reserve(at, target)
		_start_swing(attack, at)
	elif pending.is_empty():
		pending = _reserve(at, target)
		attack = pending
	else:
		pending.count += 1
		grouped += 1
		attack = pending
	damages.append({"time": at + float(data.spires_fast.attack_wait), "target": target, "damage": damage, "profile": attack.profile})
	_event("出牌 %d · %s" % [requested, "加入下一挥击" if attack != active else "开始挥击"])

func _reserve(at: float, target: int) -> Dictionary:
	var stages: Array = data.weapons[weapon_index].stages
	var stage: Dictionary = stages[combo_index % stages.size()]
	combo_index += 1
	return {"input_time": at, "target": target, "name": stage.body, "profile": data.timing[stage.body], "count": 1}

func _start_swing(attack: Dictionary, at: float) -> void:
	for player in voices:
		if is_instance_valid(player) and player.get_meta("cue_kind", "") == "charge":
			player.stop()
			player.queue_free()
	active = attack
	active.time = at
	active.struck = false
	active.released = false
	active.fx_started = false
	current = active.name
	swings += 1
	rig.weapon_use()
	_sound(active.profile, "charge")
	_event("挥击 %d · %s · 承接 %d 次攻击" % [swings, current, active.count])

func _visual_hit_time(profile: Dictionary) -> float:
	return float(profile.body_delay) + float(profile.hit_frame) / float(profile.body_fps)

func _attack_duration(profile: Dictionary, name: String) -> float:
	return maxf(float(profile.body_delay) + body.sprite_frames.get_frame_count(name) / float(profile.body_fps), _handoff_time(profile))

func _handoff_time(profile: Dictionary) -> float:
	# Keep the windup, contact, FX gate and a short follow-through before chaining.
	return maxf(_visual_hit_time(profile), _fx_visible_time(profile)) + 0.06

func _fx_visible_time(profile: Dictionary) -> float:
	return float(profile.get("fx_visible_time", profile.fx_time))

func _step(delta: float) -> void:
	clock += delta
	# A contact on the same tick as the next combo input belongs to the previous strike.
	_advance_active()
	while not requests.is_empty() and float(requests[0].time) <= clock + 0.0000001:
		var request: Dictionary = requests.pop_front()
		_issue(request.time, request.target, request.damage)
	while not damages.is_empty() and float(damages[0].time) <= clock + 0.0000001:
		var damage: Dictionary = damages.pop_front()
		arena.hit_target(damage.target, damage.damage)
		resolved += 1
		_sound(damage.profile, "hit")
	_advance_active()
	if requests.is_empty() and damages.is_empty() and active.is_empty() and scenario_count > 0 and repeat_at < 0.0:
		repeat_at = clock + 0.8
	if loop_button.button_pressed and repeat_at >= 0.0 and clock >= repeat_at:
		_run(scenario_kind, scenario_count)
	arena.advance(delta)
	_update_pose()
	rig.advance(delta)

func _advance_active() -> void:
	if not active.is_empty():
		var elapsed := clock - float(active.time)
		var profile: Dictionary = active.profile
		if not active.released and elapsed + 0.0000001 >= float(profile.fx_time):
			active.released = true
			_sound(profile, "release")
		if not active.fx_started and elapsed + 0.0000001 >= _fx_visible_time(profile):
			active.fx_started = true
			if profile.vfx != null:
				fx_active = {"name": profile.vfx, "time": float(active.time) + _fx_visible_time(profile), "fps": 30.0 * float(profile.animation_speed)}
		if not active.struck and elapsed + 0.0000001 >= _visual_hit_time(profile):
			active.struck = true
			visible_hits += 1
			max_lag = maxf(max_lag, clock - float(active.input_time) - float(data.spires_fast.attack_wait))
		if not pending.is_empty() and elapsed + 0.0000001 >= _handoff_time(profile):
			var next := pending
			pending = {}
			_start_swing(next, clock)
		elif elapsed >= _attack_duration(profile, current):
			active.clear()
			current = "Idle"

func _update_pose() -> void:
	var display := current
	var frame := 0
	var inspect_clock := clock
	var inspected_fx: Dictionary = fx_active
	if not active.is_empty():
		var profile: Dictionary = active.profile
		var elapsed := (clock - float(active.time)) if inspect_frame < 0 else inspect_at
		if inspect_frame >= 0:
			inspect_clock = float(active.time) + inspect_at
			inspected_fx = {}
			if profile.vfx != null and inspect_at >= _fx_visible_time(profile):
				inspected_fx = {"name": profile.vfx, "time": float(active.time) + _fx_visible_time(profile), "fps": 30.0 * float(profile.animation_speed)}
		if elapsed < float(profile.body_delay):
			display = "Idle"
			frame = int(clock * data.source_fps) % body.sprite_frames.get_frame_count("Idle")
		else:
			frame = mini(int((elapsed - float(profile.body_delay)) * float(profile.body_fps) + 0.0000001), body.sprite_frames.get_frame_count(current) - 1)
			if profile.reverse: frame = body.sprite_frames.get_frame_count(current) - 1 - frame
	else:
		frame = int(clock * data.source_fps)
		var count := body.sprite_frames.get_frame_count(display)
		if display == "stun" and frame >= count:
			current = "Idle"
			display = "Idle"
			count = body.sprite_frames.get_frame_count(display)
		frame = mini(frame, count - 1) if display in ["letha", "travolta"] else frame % count
		if inspect_frame >= 0: frame = inspect_frame
	body.animation = display
	body.frame = frame
	last_frame = frame
	effect.visible = not inspected_fx.is_empty() and inspect_clock + 0.0000001 >= float(inspected_fx.time)
	if effect.visible:
		effect.animation = inspected_fx.name
		var fx_frame := maxi(0, int((inspect_clock - float(inspected_fx.time)) * float(inspected_fx.fps)))
		effect.visible = fx_frame < effect.sprite_frames.get_frame_count(inspected_fx.name)
		if effect.visible: effect.frame = fx_frame
	var texture := body.sprite_frames.get_frame_texture(display, frame)
	var point: Array = data.anchors[texture.resource_path]
	rig.anchor = Vector2(point[0], point[1])
	rig.set_front(data.front_frames.get(texture.resource_path, false))
	rig.visible = current != "letha"

func _pose(name: String) -> void:
	_reset()
	current = name
	_update_pose()

func _toggle_pause() -> void:
	paused = not paused
	inspect_frame = -1
	if paused: _stop_audio()

func _step_frame(direction: int) -> void:
	paused = true
	_stop_audio()
	var name := current if not active.is_empty() else str(body.animation)
	inspect_frame = clampi((inspect_frame if inspect_frame >= 0 else last_frame) + direction, 0, body.sprite_frames.get_frame_count(name) - 1)
	if not active.is_empty(): inspect_at = float(active.profile.body_delay) + inspect_frame / float(active.profile.body_fps)
	_update_pose()

func _update_ui() -> void:
	if status == null: return
	pause_button.text = "播放" if paused else "暂停"
	status.text = "%s    结算 %d/%d  ·  挥中 %d  ·  合并 %d  ·  待接 %d  ·  最长滞后 %.0f ms" % [scenario, resolved, requested, visible_hits, grouped, int(pending.get("count", 0)), max_lag * 1000.0]
	status.modulate = Color("c7d9d1")
	frame_info.text = "%s   %d / %d 帧" % [current, body.frame + 1, body.sprite_frames.get_frame_count(body.animation)]
	events_label.text = "\n".join(trace.slice(maxi(0, trace.size() - 2)))

func _process(delta: float) -> void:
	var size := Vector2(viewport.size)
	actor.position = Vector2(size.x * 0.26, size.y * 0.85 - 8)
	actor.scale = Vector2(4, 4)
	if not paused:
		accumulator += minf(delta, 0.2) * time_scale
		while accumulator >= STEP:
			accumulator -= STEP
			_step(STEP)
	_update_ui()

func _input(event: InputEvent) -> void:
	if event is InputEventKey and event.pressed and not event.echo:
		if event.keycode == KEY_SPACE: _toggle_pause()
		elif event.keycode == KEY_RIGHT: _step_frame(1)
		elif event.keycode == KEY_LEFT: _step_frame(-1)
		else: return
		get_viewport().set_input_as_handled()

func _capture() -> void:
	await get_tree().create_timer(2.0).timeout
	await RenderingServer.frame_post_draw
	get_viewport().get_texture().get_image().save_png(ProjectSettings.globalize_path("res://Generated/battle-preview.png"))
	print("BATTLE_PREVIEW_READY")

func _validate() -> void:
	AudioServer.set_bus_mute(0, true)
	for weapon in data.weapons:
		for stage in weapon.stages:
			assert(body.sprite_frames.has_animation(stage.body))
			var profile: Dictionary = data.timing[stage.body]
			assert(is_equal_approx(profile.body_fps, 30.0 * profile.animation_speed))
			for kind in profile.audio:
				_sound(profile, kind)
			_stop_audio()
	# The whip's source FX begins at frame zero with transparent lead-in frames;
	# its imported visible-only sequence must not appear at that creation gate.
	for index in range(data.weapons.size()):
		if data.weapons[index].id != "vampire_killer": continue
		weapon_index = index
		for stage_index in range(data.weapons[index].stages.size()):
			_reset()
			combo_index = stage_index
			_manual_attack()
			var profile: Dictionary = active.profile
			assert(profile.fx_lead_frames == [6, 10, 17][stage_index])
			_step(_fx_visible_time(profile) - 0.00001)
			assert(active.released and not effect.visible)
			_step(0.00001)
			assert(effect.visible and effect.frame == 0)
			assert(body.frame == profile.fx_lead_frames)
			_step_frame(-1)
			assert(not effect.visible)
			_step_frame(1)
			assert(effect.visible and effect.frame == 0)
		print("WHIP_FX_SYNC_PASS: A/B/C visible FX begins with body frame 6/10/17; early release audio retained; inspection agrees")
	# The original left scythe deliberately crosses the B1/B2 FX names.
	for index in range(data.weapons.size()):
		if data.weapons[index].id != "scythe": continue
		weapon_index = index
		var source_fx := ["fxAtkScytheA1", "fxAtkScytheA2", "fxAtkScytheB2", "fxAtkScytheB1"]
		for stage_index in range(4):
			_reset()
			combo_index = stage_index
			_manual_attack()
			assert(not active.profile.reverse)
			assert(active.profile.vfx == source_fx[stage_index] and active.profile.source_fx == source_fx[stage_index])
			_step(_fx_visible_time(active.profile))
			assert(effect.visible and effect.animation == source_fx[stage_index])
		print("SCYTHE_FX_MAPPING_PASS: original body order A1/A2/B1/B2; FX A1/A2/B2/B1; forward playback")
	# Full original chains complete without losing their visible contact; repeated runs remain live.
	for index in range(data.weapons.size()):
		weapon_index = index
		_run("combo", data.weapons[index].stages.size())
		for i in range(2400): _step(STEP)
		assert(resolved == data.weapons[index].stages.size())
		assert(visible_hits == resolved and interrupted == 0, "%s visible=%d damage=%d interrupted=%d" % [data.weapons[index].id, visible_hits, resolved, interrupted])
	# Every game hit sounds, every started swing reaches contact, and only one group
	# may wait. Long streams cannot build an ever-growing animation queue.
	for index in range(data.weapons.size()):
		weapon_index = index
		_run("stardust", 50)
		var bound := 0.0
		for stage in data.weapons[index].stages:
			bound = maxf(bound, _handoff_time(data.timing[stage.body]))
		for i in range(1500): _step(STEP)
		assert(requested == 50 and resolved == 50)
		assert(hit_sounds == 50 and visible_hits == swings and interrupted == 0)
		assert(swings + grouped == 50 and grouped > 0)
		assert(max_lag <= 2.0 * bound + 2.0 * STEP)
		assert(requests.is_empty() and damages.is_empty() and active.is_empty() and pending.is_empty())
		print("STARDUST_STRESS_PASS ", data.weapons[index].id, ": damage=", resolved, " hit_sounds=", hit_sounds, " visible=", visible_hits, " merged=", grouped, " max_lag_ms=", roundi(max_lag * 1000.0))
	# Dense manual input also preserves contact; reset cancels the reserved group.
	_reset()
	for i in range(100):
		_manual_attack()
		for tick in range(6): _step(STEP)
	for i in range(480): _step(STEP)
	assert(resolved == 100 and hit_sounds == 100 and swings == visible_hits)
	assert(active.is_empty() and pending.is_empty())
	_manual_attack()
	_manual_attack()
	assert(not pending.is_empty())
	_reset()
	assert(pending.is_empty() and damages.is_empty())
	_pose("letha")
	assert(not rig.visible)
	_pose("Idle")
	assert(rig.visible)
	_manual_attack()
	_step_frame(1)
	assert(paused and body.frame == 1)
	var frame_before := body.frame
	_process(1.0)
	assert(body.frame == frame_before)
	_reset()
	# Native button callbacks and scenario replay must stay live across complete cycles.
	for button in find_children("*", "Button", true, false):
		if button.text == "星尘 · X=":
			star_count.value = 20
			button.pressed.emit()
	for i in range(900): _step(STEP)
	assert(requested == 20 and resolved == 20)
	loop_button.button_pressed = true
	_run("combo", data.weapons[weapon_index].stages.size())
	var loops := 0
	for i in range(2400):
		var before := clock
		_step(STEP)
		if clock < before: loops += 1
	assert(loops >= 2)
	loop_button.button_pressed = false
	_reset()
	print("BATTLE_PREVIEW_PASS: source-speed protected swings; one coalesced pending group; per-damage hit audio; 7 weapons X=50, manual burst, native buttons, loop, pause, death")
	get_tree().quit()
