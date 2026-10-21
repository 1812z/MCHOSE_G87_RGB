"""Validate/reassemble observed G87 GIF reports without touching hardware."""
import collections
import json
from pathlib import Path
import statistics
import subprocess

ROOT = Path(__file__).resolve().parents[1]
result = subprocess.run([
    r"C:\Program Files\Wireshark\tshark.exe", "-r", str(ROOT / "g87-gif.pcap"),
    "-Y", "usb.device_address == 5 && usb.data_fragment",
    "-T", "fields", "-e", "frame.time_relative", "-e", "usb.data_fragment",
], capture_output=True, text=True, check=True)
frames = []
pending = []
errors = collections.Counter()
for line in result.stdout.splitlines():
    time, raw = line.split("\t")
    data = bytes.fromhex(raw.replace(":", ""))
    if len(data) != 20 or data[:2] != bytes([0x13, 0x88]):
        errors["other reports"] += 1
        continue
    if sum(data[:-1]) & 255 != data[-1]:
        errors["checksum"] += 1
    count, index, length = data[2:5]
    length -= 0x10
    if not 0 <= length <= 14:
        errors["length"] += 1
        continue
    if index == 0:
        pending = []
    if index != len(pending):
        errors["partial frame packets"] += 1
        continue
    pending.append((float(time), data, data[5:19] if index + 1 < count else data[5:5+length]))
    if index + 1 == count:
        payload = b"".join(p[2] for p in pending)
        cursor, colors = 0, {}
        # Official all-black frames use a four-byte marker, without a key list.
        if payload == bytes([0, 0, 0, 1]):
            cursor = len(payload)
        while cursor < len(payload):
            if cursor + 4 > len(payload):
                errors["truncated color group"] += 1
                break
            rgb = list(payload[cursor:cursor+3])
            keys = payload[cursor+3]
            cursor += 4
            if cursor + keys > len(payload):
                errors["truncated key list"] += 1
                break
            for key in payload[cursor:cursor+keys]:
                colors[key] = rgb
            cursor += keys
        frames.append({"time": pending[0][0], "reports": [p[1].hex() for p in pending],
                       "colors": colors, "payloadBytes": len(payload)})
        pending = []
print("Complete frames:", len(frames))
print("Validation findings:", dict(errors))
if len(frames) > 1:
    dt = [b["time"]-a["time"] for a,b in zip(frames,frames[1:])]
    print("Median frame interval / FPS:", round(statistics.median(dt), 4), round(1/statistics.median(dt), 2))
print("Packets per frame:", dict(collections.Counter(len(f["reports"]) for f in frames)))
print("Observed hardware indices:", sorted({int(k) for f in frames for k in f["colors"]}))
print("First decoded frame:", json.dumps(frames[0]["colors"]) if frames else "none")
if errors.get("checksum") or errors.get("length") or errors.get("truncated color group") or errors.get("truncated key list"):
    raise SystemExit("Validation failed; no decoded output saved")
if frames:
    output = ROOT / "tools" / "G87Probe" / "captured-frames.json"
    output.write_text(json.dumps(frames, indent=2), encoding="utf-8")
    print("Saved validated frames:", output)
