extends Button

var icon_kind := "gear"
var active := false

func _ready() -> void:
	mouse_entered.connect(queue_redraw)
	mouse_exited.connect(queue_redraw)
	focus_entered.connect(queue_redraw)
	focus_exited.connect(queue_redraw)

func _draw() -> void:
	var center := size * 0.5
	var ink := Color("eef2f7") if active or is_hovered() or has_focus() else Color("aeb9c7")
	if icon_kind == "close":
		draw_line(center + Vector2(-5, -5), center + Vector2(5, 5), ink, 1.7, true)
		draw_line(center + Vector2(5, -5), center + Vector2(-5, 5), ink, 1.7, true)
		return
	var outline := PackedVector2Array()
	for tooth in range(8):
		var angle := float(tooth) * TAU / 8.0
		for point in [[-0.5, 8.0], [-0.24, 10.5], [0.24, 10.5], [0.5, 8.0]]:
			outline.append(center + Vector2.from_angle(angle + float(point[0]) * TAU / 8.0) * float(point[1]))
	outline.append(outline[0])
	draw_polyline(outline, ink, 1.7, true)
	draw_arc(center, 3.5, 0, TAU, 24, ink, 1.7, true)
