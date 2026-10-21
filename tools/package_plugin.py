"""Package the independent Artemis G87 plugin after a Release build."""
import json
from pathlib import Path
import zipfile

root = Path(__file__).resolve().parents[1]
project = root / "src" / "Artemis.Plugins.MchoseG87"
metadata = json.loads((project / "plugin.json").read_text(encoding="utf-8"))
build = project / "bin" / "Release" / "net10.0"
names = ["plugin.json", "Artemis.Plugins.MchoseG87.dll",
         "Artemis.Plugins.MchoseG87.deps.json", "Artemis.Plugins.MchoseG87.runtimeconfig.json", "HidSharp.dll"]
for name in names:
    if not (build / name).is_file():
        raise SystemExit(f"Missing build output: {name}")
output = root / f"Artemis.Plugins.MchoseG87-{metadata['Version']}.zip"
with zipfile.ZipFile(output, "w", zipfile.ZIP_DEFLATED) as archive:
    for name in names:
        archive.write(build / name, name)
with zipfile.ZipFile(output) as archive:
    assert archive.testzip() is None
print(f"Packaged: {output} ({output.stat().st_size:,} bytes)")
