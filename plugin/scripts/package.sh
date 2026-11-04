#!/usr/bin/env bash
# SPDX-License-Identifier: GPL-3.0-only
set -euo pipefail
root=$(cd "$(dirname "$0")/.." && pwd)
"$root/dotnet.sh" build "$root/src/Kin.Plugin.csproj" -c Release --nologo
stage="$root/dist/KinOfSheredyn"
out="$stage/BepInEx/plugins/KinOfSheredyn"
# Recreate our staging directory so obsolete dependencies cannot survive repackaging.
rm -rf -- "$stage"
mkdir -p "$out/rules" "$stage/licenses" "$out/assets"
# Only our assemblies are distributed. Newtonsoft.Json comes from the game.
# Each assembly is copied from its own project output; copy-local of the project reference is not reliable after incremental builds.
for pair in Kin.Plugin:KinOfSheredyn.dll Kin.Core:Kin.Core.dll; do
  project=${pair%%:*}; file=${pair#*:}
  cp "$root/src/bin/$project/Release/netstandard2.1/$file" "$out/$file"
done
cp "$root/rules/rules.json" "$root/rules/text.json" "$out/rules/"
# Audio paths are relative to the plugin audio directory, not assets/audio.
# Validate all required recordings before publishing the generated manifest.
python3 - "$root" "$out" <<'PY_AUDIO'
import json
import pathlib
import re
import shutil
import struct
import sys
import wave

root, out = map(pathlib.Path, sys.argv[1:])
source = root / "assets/audio"
# Authority: the plugin's own event table (verified identical to the Task 03 voice map).
authority = (root / "src/AudioCore.cs").read_text()
rows = re.findall(r'\["(Play_Leader_KinOfSheredyn_[A-Za-z0-9_]+)"\] = ([0-9]+)u', authority)
if len(rows) != 57 or len({name for name, _ in rows}) != 57 or len({int(i) for _, i in rows}) != 57:
    raise SystemExit("Audio authority must contain exactly 57 unique leader names and IDs")
if not source.is_dir() or source.is_symlink():
    raise SystemExit("Missing/nonlocal assets/audio directory")
for path in source.rglob("*"):
    if path.is_symlink() or (not path.is_dir() and not path.is_file()):
        raise SystemExit(f"Audio assets must be regular local files: {path}")
events = {}
for name, event_id in rows:
    event_id = int(event_id)
    if not 0 < event_id <= 0xffffffff:
        raise SystemExit(f"Invalid unsigned event ID: {name}")
    relative = f"leader/{name}.wav"
    path = source / relative
    try:
        # Require a complete RIFF payload, PCM16 mono, 48 kHz for this asset set.
        raw = path.read_bytes()
        if len(raw) < 12 or raw[:4] != b"RIFF" or raw[8:12] != b"WAVE" or struct.unpack_from("<I", raw, 4)[0] + 8 != len(raw):
            raise ValueError("invalid/truncated RIFF")
        with wave.open(str(path), "rb") as wav:
            if (wav.getnchannels(), wav.getsampwidth(), wav.getframerate(), wav.getcomptype()) != (1, 2, 48000, "NONE"):
                raise ValueError("expected 48 kHz mono PCM16")
            frames = wav.getnframes()
            if not frames or len(wav.readframes(frames)) != frames * 2:
                raise ValueError("empty/truncated PCM data")
    except (OSError, ValueError, EOFError, wave.Error) as error:
        raise SystemExit(f"Invalid required leader clip {path}: {error}")
    events[name] = {"id": event_id, "spatial": "2d", "volume": 1.0, "clips": [relative]}
manifest = {"schemaVersion": 1, "events": events}
# Round-trip the emitted document and validate every relative asset reference.
encoded = json.dumps(manifest, indent=2) + "\n"
decoded = json.loads(encoded)
for name, event in decoded["events"].items():
    if not event["clips"] or event["id"] != int(dict(rows)[name]):
        raise SystemExit(f"Invalid generated manifest entry: {name}")
    for clip in event["clips"]:
        parts = pathlib.PurePosixPath(clip)
        if parts.is_absolute() or ".." in parts.parts or not (source / clip).is_file():
            raise SystemExit(f"Invalid generated audio path: {clip}")
shutil.copytree(source, out / "audio", dirs_exist_ok=True)
(out / "audio/events.json").write_text(encoded)
print(f"Validated and staged {len(events)} leader audio events")
PY_AUDIO
cp "$root/LICENSE" "$stage/LICENSE"
cp "$root/LICENSE.harmonyx.txt" "$stage/licenses/HarmonyX-MIT.txt"
cp "$root/LICENSE.bepinex.txt" "$stage/licenses/BepInEx-MIT.txt"
cp "$root/../dist/DISCLAIMER.txt" "$stage/DISCLAIMER.txt"
{
  cat "$root/README.md"
  printf '\n## Save compatibility notice\n\n'
  cat "$root/../dist/DISCLAIMER.txt"
} > "$stage/README.md"
echo "Staged Workshop item: $stage"
