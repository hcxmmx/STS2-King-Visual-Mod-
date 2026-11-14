"""Extract original impact tiles and bind the shared enemy-hit presentation."""
from pathlib import Path


def prepare_impacts(assets: Path, generated: Path, weapons):
    from prepare_extra_weapons import Atlas

    atlas = Atlas(assets, "fxCommon")
    textures = {}
    for name in ["fxLineWhite", "fxLineWhiteSharp", "fxPixelWhite", "fxGlowWhite"]:
        image = atlas.frames(name)[0][1]
        target = generated / "ImpactAssets" / (name + ".png")
        target.parent.mkdir(parents=True, exist_ok=True)
        image.save(target)
        textures[name] = "res://Generated/ImpactAssets/" + target.name
    stages = {}
    blunt = {"pan", "nunchuck_pan", "king_scepter", "tombstone"}
    heavy = {"broad_sword", "scythe", "halberd", "perfect_halberd", "king_scepter", "tombstone"}
    for weapon in weapons:
        for index, stage in enumerate(weapon["stages"]):
            stages[stage["body"]] = {
                "style": "blunt" if weapon["id"] in blunt else "slash",
                "strength": 1.2 if weapon["id"] in heavy else 1.0,
                "angle": (-0.55 if index % 2 == 0 else 0.55),
            }
    # Source hitLines uses white tiles, short lifetimes and per-tick friction.
    # Counts, sizes and lifetimes below are explicit STS presentation tuning.
    result = {"textures": textures, "stages": stages, "limit": 32,
              "styles": {
                  "slash": {"count": 20, "life": 0.28, "spread": 0.65, "speed": 155.0, "color": "ffe3a0"},
                  "blunt": {"count": 25, "life": 0.32, "spread": 3.141593, "speed": 125.0, "color": "ffd385"},
                  "arrow": {"count": 9, "life": 0.22, "spread": 0.85, "speed": 115.0, "color": "fff0ce"},
                  "armor": {"count": 12, "life": 0.18, "spread": 1.3, "speed": 125.0, "color": "b8dcff"},
              }}
    print(f"IMPACT_ASSETS_READY: four original tiles, {len(stages)} stage bindings")
    return result
