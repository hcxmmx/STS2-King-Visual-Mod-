extends SceneTree
var probe := 0
func _initialize() -> void:
    run.call_deferred()
func assertion_probe() -> bool:
    probe += 1
    return true
func run() -> void:
    assert(assertion_probe())
    print("RELEASE_ENV debug=", OS.has_feature("debug"), " assertion_calls=", probe)
    if OS.has_feature("debug") or probe != 0:
        push_error("Expected release engine with disabled assertions")
        quit(2)
        return
    var args := OS.get_cmdline_user_args()
    if not ProjectSettings.load_resource_pack(args[0]):
        quit(3)
        return
    var script := load("res://Hcxmmx_King/Presentation/KingArrows.gd") as GDScript
    var layer: Node2D = script.new()
    root.add_child(layer)
    var texture: Texture2D = layer.get("arrow")
    if texture == null or texture.get_width() <= 0 or texture.get_height() <= 0:
        push_error("ARROW_RELEASE_FAIL: empty texture")
        layer.free()
        quit(4)
        return
    layer.shoot(1, 1, 0, Vector2(0, 0), Vector2(400, 200), 4.0, 0)
    if layer.flights.size() != 1:
        quit(5)
        return
    layer.advance(1.0)
    if not layer.flights.is_empty():
        quit(6)
        return
    print("ARROW_RELEASE_PASS: decoded ", texture.get_size(), "; shoot and cleanup with assertions disabled")
    layer.free()
    quit()
