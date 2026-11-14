extends SceneTree

func _initialize() -> void:
	_validate.call_deferred()

func _validate() -> void:
	var preview: Control = load("res://Preview.tscn").instantiate()
	preview.set_meta("skip_auto_capture", true)
	root.add_child(preview)
	preview.set_process(false)
	AudioServer.set_bus_mute(0, true)
	for mode in [0, 1]:
		preview._set_rhythm(mode)
		preview._preview_ranged(1)
		var charge := 0.1 if mode == 1 else 0.07
		preview._step(charge - 0.0001)
		assert(preview.ranged.fired == 0)
		assert(preview.body.frame < 13)
		preview._step(0.0002)
		assert(preview.ranged.fired == 1 and preview.ranged.flights.size() == 1)
		assert(preview.body.frame >= 12)
		preview._preview_ranged(20)
		for tick in range(600): preview._step(1.0 / 120.0)
		assert(preview.requested == 20 and preview.resolved == 20)
		assert(preview.ranged.fired == 20 and preview.release_sounds == 20 and preview.hit_sounds == 20)
		assert(not preview.ranged.active and preview.ranged.flights.is_empty())
		assert(preview.body.animation == &"Idle")
		preview._preview_ranged(1)
		for click in range(3):
			preview._step(0.04)
			preview._preview_ranged(1)
		for tick in range(180): preview._step(1.0 / 120.0)
		assert(preview.requested == 4 and preview.resolved == 4 and preview.ranged.fired >= 2)
		assert(not preview.ranged.active and preview.ranged.flights.is_empty())
		preview._preview_ranged(1)
		preview._step(0.02)
		preview._pose("stun")
		assert(not preview.ranged.active and preview.ranged.damage_events.is_empty())
		assert(preview.body.animation == &"stun" and preview.last_hit_cue == "hero_dmg1.ogg")
		assert(preview.last_hit_owner == "国王受伤")
	print("RANGED_PREVIEW_PASS: both rhythms, release at source half-cycle, independent projectiles, 20 shots and damage sounds, drain, hurt interruption")
	preview.free()
	quit()
