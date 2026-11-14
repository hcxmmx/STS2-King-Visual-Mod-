extends RefCounted

enum Mode { FAST, ORIGINAL }

# Contact times are presentation targets, never changes to the game's damage wait.
# Stage-specific targets retain differences between thrusts and heavy finishing blows.
const CONTACTS := {
	"katana": [0.10, 0.10, 0.13],
	"pan": [0.12, 0.10, 0.12, 0.135],
	"vampire_killer": [0.13, 0.15, 0.18],
	"broad_sword": [0.18, 0.21, 0.23],
	"scythe": [0.24, 0.24, 0.20, 0.20],
	"halberd": [0.21, 0.11, 0.15],
	"perfect_halberd": [0.19, 0.11, 0.22, 0.14],
	"king_scepter": [0.09],
	"rapier": [0.11, 0.075, 0.12],
	"tombstone": [0.21, 0.22, 0.24],
	"scissors": [0.22, 0.23],
	"nunchuck_pan": [0.07, 0.09, 0.11, 0.09, 0.06, 0.085, 0.085, 0.14, 0.14, 0.20],
}

static func profile(source: Dictionary, weapon: String, stage: int, mode: int) -> Dictionary:
	var result := source.duplicate(true)
	result.source_body_fps = float(source.body_fps)
	result.source_body_delay = float(source.body_delay)
	result.source_fx_time = float(source.fx_time)
	result.source_fx_fps = 30.0 * float(source.animation_speed)
	result.fx_fps = 30.0 * float(source.animation_speed)
	result.recovery_fps = float(source.body_fps)
	result.handoff_after = 0.06
	result.play_charge = true
	if source.get("ranged", false):
		var charge := float(source.charge if mode == Mode.ORIGINAL else source.fast_charge)
		result.body_fps = float(source.hit_frame) / charge
		result.recovery_fps = float(source.body_fps) * (1.0 if mode == Mode.ORIGINAL else 1.4)
		result.release_time = charge
		result.fx_time = charge
		result.fx_visible_time = charge
		result.handoff_after = 0.035
		result.play_charge = mode == Mode.ORIGINAL
		return result
	if mode == Mode.ORIGINAL: return result
	var contact: float = CONTACTS[weapon][stage]
	result.body_delay = minf(float(source.body_delay) * 0.15, 0.02)
	result.body_fps = float(source.hit_frame) / (contact - float(result.body_delay))
	result.recovery_fps = float(source.body_fps) * 1.4
	result.charge = contact
	result.handoff_after = 0.045 if contact >= 0.18 else 0.035
	result.lock_after = result.handoff_after
	# Imported first visible FX is anchored to contact. The original creation gate
	# can precede it by transparent frames; do not reuse that gate for trimmed FX.
	result.fx_time = maxf(0.0, contact - 0.025)
	result.release_time = result.fx_time
	result.fx_visible_time = contact
	result.fx_fps *= 1.25
	if result.get("fx_clock", "independent") == "body":
		# Preserve the authored body/FX phase throughout the time warp, not just
		# at one contact anchor. Retain the original transparent-frame offset.
		var first_source_time: float = float(result.source_fx_time) + float(result.fx_lead_frames) / float(result.source_fx_fps)
		var first_body_frame: float = (first_source_time - float(result.source_body_delay)) * float(result.source_body_fps)
		result.fx_visible_time = frame_time(result, first_body_frame)
	# Full-length charge samples over short windups sound like unfinished attacks.
	# Keep the release and impact samples intact, at their original pitch.
	result.play_charge = false
	return result

static func contact_time(profile: Dictionary) -> float:
	return float(profile.body_delay) + float(profile.hit_frame) / float(profile.body_fps)

static func frame_at(profile: Dictionary, elapsed: float) -> int:
	var contact := contact_time(profile)
	if elapsed <= contact:
		return maxi(0, int((elapsed - float(profile.body_delay)) * float(profile.body_fps) + 0.0000001))
	return int(float(profile.hit_frame) + (elapsed - contact) * float(profile.recovery_fps) + 0.0000001)

static func frame_time(profile: Dictionary, frame: float) -> float:
	if frame <= float(profile.hit_frame):
		return float(profile.body_delay) + frame / float(profile.body_fps)
	return contact_time(profile) + (frame - int(profile.hit_frame)) / float(profile.recovery_fps)

static func duration(profile: Dictionary, count: int) -> float:
	return frame_time(profile, count)

static func fx_frame_at(profile: Dictionary, elapsed: float) -> int:
	if profile.get("fx_clock", "independent") != "body":
		return int(floor((elapsed - float(profile.fx_visible_time)) * float(profile.fx_fps) + 0.0000001))
	var contact := contact_time(profile)
	var body_progress := (elapsed - float(profile.body_delay)) * float(profile.body_fps)
	if elapsed > contact:
		body_progress = float(profile.hit_frame) + (elapsed - contact) * float(profile.recovery_fps)
	var source_time := float(profile.source_body_delay) + body_progress / float(profile.source_body_fps)
	return int(floor((source_time - float(profile.source_fx_time)) * float(profile.source_fx_fps) - float(profile.fx_lead_frames) + 0.0000001))
