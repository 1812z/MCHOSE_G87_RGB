"""Check the registered layout against the user's physical calibration."""
import json
from pathlib import Path
import re

root = Path(__file__).resolve().parents[1]
source = (root / "src/Artemis.Plugins.MchoseG87/G87Keyboard.cs").read_text(encoding="utf-8")
calibration = json.loads((root / "g87-key-calibration.json").read_text(encoding="utf-8"))
aliases = {
    "esc": "escape", "`": "graveaccentandtilde", "shift": "leftshift",
    "leftctrl": "leftctrl", "win": "leftgui", "alt": "leftalt",
    "printscreen": "printscreen", "del": "delete", "left": "arrowleft",
    "sl": "scrolllock", "up": "arrowup", "down": "arrowdown",
    "pb": "pausebreak", "pgup": "pageup", "pgdn": "pagedown", "right": "arrowright",
}

def normalize(name):
    name = name.lower().replace(" ", "")
    return aliases.get(name, name)

slots, names = [], []
for key_names, hardware in re.findall(r'Row\([^,]+,\s*\[([^\]]+)\],\s*\[([^\]]+)\]', source):
    row_names = re.findall(r'"([^"]+)"', key_names)
    row_slots = [int(s) for s in hardware.split(",")]
    assert len(row_names) == len(row_slots)
    for name, slot in zip(row_names, row_slots):
        assert normalize(calibration[str(slot)]) == normalize(name), (slot, name, calibration.get(str(slot)))
    slots.extend(row_slots)
    names.extend(row_names)
assert len(slots) == len(set(slots)) == len(set(names)) == 87
unmapped = sorted(set(range(108)) - set(slots))
print("PASS: all 87 unique keys match physical calibration; no duplicate hardware slots.")
print("Unregistered matrix slots:", unmapped)
