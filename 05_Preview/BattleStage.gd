extends Node2D

var font: SystemFont
var targets: Array = []
var damage_numbers: Array = []
var hits := 0
var combat_time := 0.0

func _ready() -> void:
	font = SystemFont.new()
	font.font_names = PackedStringArray(["Microsoft YaHei UI", "Microsoft YaHei"])
	get_viewport().size_changed.connect(queue_redraw)
	reset_targets()

func reset_targets() -> void:
	targets.clear()
	for i in range(3): targets.append({"hp": 250, "flash": 0.0})
	damage_numbers.clear()
	hits = 0
	queue_redraw()

func target_position(index: int) -> Vector2:
	var size := get_viewport_rect().size
	return Vector2(size.x * (0.64 + index * 0.13), size.y * 0.85)

func hit_target(index: int, damage: int) -> void:
	targets[index].hp = maxi(0, targets[index].hp - damage)
	targets[index].flash = 0.18
	hits += 1
	damage_numbers.append({"position": target_position(index) + Vector2(-12, -170), "life": 0.8, "damage": damage})
	queue_redraw()

func advance(delta: float) -> void:
	combat_time += delta
	for target in targets: target.flash = maxf(0.0, target.flash - delta)
	for number in damage_numbers:
		number.life -= delta
		number.position.y -= delta * 55.0
	damage_numbers = damage_numbers.filter(func(number): return number.life > 0.0)
	queue_redraw()

func _draw() -> void:
	var size := get_viewport_rect().size
	var ground := size.y * 0.85
	draw_rect(Rect2(Vector2.ZERO, size), Color("142129"))
	# Stone courtyard, built from integer-aligned shapes so the source sprites keep their pixels.
	for row in range(5):
		for col in range(int(size.x / 105) + 1):
			var x := col * 105 - (52 if row % 2 else 0)
			draw_rect(Rect2(x + 2, 30 + row * 65, 101, 61), Color("1a2c34") if (col + row) % 3 else Color("1d3038"))
	for x in [size.x * 0.1, size.x * 0.52, size.x * 0.96]:
		draw_rect(Rect2(x - 18, 0, 36, ground), Color("24373d"))
		draw_rect(Rect2(x - 25, ground - 16, 50, 16), Color("35484b"))
	draw_rect(Rect2(0, ground, size.x, size.y - ground), Color("273b3d"))
	draw_rect(Rect2(0, ground, size.x, 4), Color("60706a"))
	for x in range(0, int(size.x), 85): draw_line(Vector2(x, ground + 4), Vector2(x - 40, size.y), Color("1d2c30"), 2)
	for i in range(targets.size()):
		var position := target_position(i)
		var target: Dictionary = targets[i]
		var flash: bool = target.flash > 0.0
		var wood := Color("dfb18a") if flash else Color("a27e59")
		var dark := Color("f0d3af") if flash else Color("574c42")
		var shake := Vector2(sin(target.flash * 160.0) * target.flash * 15.0, 0)
		draw_set_transform(position + shake)
		draw_ellipse_shadow()
		draw_rect(Rect2(-7, -138, 14, 130), dark)
		draw_rect(Rect2(-37, -112, 74, 14), dark)
		draw_rect(Rect2(-24, -124, 48, 59), wood)
		draw_rect(Rect2(-18, -157, 36, 29), wood.lightened(0.12))
		draw_rect(Rect2(-11, -147, 7, 5), Color("293535"))
		draw_rect(Rect2(5, -147, 7, 5), Color("293535"))
		draw_rect(Rect2(-6, -136, 12, 3), dark)
		draw_arc(Vector2(0, -97), 13, 0, TAU, 12, dark, 3)
		draw_rect(Rect2(-35, -8, 70, 8), dark)
		draw_rect(Rect2(-51, -196, 102, 9), Color("101b22"))
		draw_rect(Rect2(-50, -195, float(target.hp) / 250.0 * 100.0, 7), Color("ba6961"))
		draw_string(font, Vector2(-43, -210), "靶标 %s" % [char(65 + i)], HORIZONTAL_ALIGNMENT_LEFT, -1, 15, Color("c1cec9"))
		draw_string(font, Vector2(-31, -174), "%d / 250" % target.hp, HORIZONTAL_ALIGNMENT_LEFT, -1, 13, Color("92a8a6"))
		if flash:
			draw_line(Vector2(-34, -113), Vector2(32, -78), Color("ffe4a0"), 4)
			draw_line(Vector2(28, -116), Vector2(-25, -78), Color("fff0bc"), 2)
		draw_set_transform(Vector2.ZERO)
	for number in damage_numbers:
		var color := Color("ffd294")
		color.a = minf(1.0, number.life * 3.0)
		draw_string(font, number.position, "−%d" % number.damage, HORIZONTAL_ALIGNMENT_LEFT, -1, 25, color)

func draw_ellipse_shadow() -> void:
	draw_rect(Rect2(-49, -2, 98, 7), Color(0.02, 0.04, 0.05, 0.4))
