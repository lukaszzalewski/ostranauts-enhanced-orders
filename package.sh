#!/usr/bin/env bash
# Builds the Steam Workshop item for Enhanced Orders.
#
#   ./package.sh           build Release and assemble dist/EnhancedOrders/
#   ./package.sh --stage   also copy it into the game's Mods folder and mark it "|edit" in
#                          loading_order.json, so Main Menu -> MODS shows an UPLOAD button
#
# Item layout is what the BepInEx Mod Loader (Workshop 3741030124) syncs from: it copies
# <item>/BepInEx/plugins/* into the game's BepInEx/plugins/Workshop/<id>/. No BepInEx/config/
# is shipped, because the loader would copy it over players' own settings and presets.
#
# mod_info.json is generated from workshop/mod_info.json + workshop/description.bbcode
# (strNotes, used as the Workshop description on first upload) + Plugin.Version. After the
# first upload the game writes strWorkshopID into the staged copy; --stage copies it back
# into workshop/mod_info.json so later uploads update the same item. Commit that change.
set -euo pipefail
cd "$(dirname "$0")"

GAME_DIR="${GAME_DIR:-$HOME/.steam/debian-installation/steamapps/common/Ostranauts}"
MODS="$GAME_DIR/Ostranauts_Data/Mods"
NAME=EnhancedOrders
OUT="dist/$NAME"
DOTNET="${DOTNET:-$(command -v dotnet || echo "$HOME/.dotnet/dotnet")}"

stage=0
[[ "${1:-}" == "--stage" ]] && stage=1

# Pull a Workshop ID the game wrote into an earlier staged copy back into the template.
if [[ -f "$MODS/$NAME/mod_info.json" ]]; then
    python3 - "$MODS/$NAME/mod_info.json" workshop/mod_info.json <<'PY'
import json, sys
staged = json.load(open(sys.argv[1]))
staged = staged[0] if isinstance(staged, list) else next(iter(staged.values()))
wid = (staged.get("strWorkshopID") or "").strip()
tpl = json.load(open(sys.argv[2]))
if wid and tpl[0].get("strWorkshopID") != wid:
    tpl[0]["strWorkshopID"] = wid
    open(sys.argv[2], "w").write(json.dumps(tpl, indent=2) + "\n")
    print(f"Saved Workshop ID {wid} into workshop/mod_info.json - commit it.")
PY
fi

"$DOTNET" build -c Release -nologo -v q
version=$(sed -n 's/.*public const string Version = "\(.*\)";.*/\1/p' Plugin.cs)

rm -rf "$OUT"
mkdir -p "$OUT/BepInEx/plugins/$NAME" "$OUT/data"
cp bin/Release/net472/$NAME.dll "$OUT/BepInEx/plugins/$NAME/"
touch "$OUT/data/.keep"
cp workshop/preview.png "$OUT/"
python3 - workshop/mod_info.json workshop/description.bbcode "$version" "$OUT/mod_info.json" <<'PY'
import json, sys
info = json.load(open(sys.argv[1]))
info[0]["strModVersion"] = sys.argv[3]
info[0]["strNotes"] = open(sys.argv[2]).read().strip()
if not info[0].get("strWorkshopID"):
    del info[0]["strWorkshopID"]   # a blank ID makes the game create a new item anyway
open(sys.argv[4], "w").write(json.dumps(info, indent=2, ensure_ascii=False) + "\n")
PY

size=$(stat -c %s "$OUT/preview.png")
(( size < 1000000 )) || { echo "preview.png is $size bytes; the Workshop limit is 1 MB" >&2; exit 1; }
echo "Packaged $NAME $version -> $OUT"

(( stage )) || exit 0

[[ -d "$GAME_DIR/Ostranauts_Data" ]] || { echo "Game not found at $GAME_DIR (set GAME_DIR)" >&2; exit 1; }
mkdir -p "$MODS"
rm -rf "$MODS/$NAME"
cp -r "$OUT" "$MODS/$NAME"

order="$MODS/loading_order.json"
[[ -f "$order" ]] && cp "$order" "$order.bak"
python3 - "$order" "$NAME" <<'PY'
import json, os, sys
path, name = sys.argv[1], sys.argv[2]
data = json.load(open(path)) if os.path.exists(path) else \
    [{"strName": "Mod Loading Order", "aLoadOrder": ["core"], "aIgnorePatterns": []}]
order = data[0].setdefault("aLoadOrder", ["core"])
order[:] = [e for e in order if e.split("|")[0] != name]
order.append(name + "|edit")
open(path, "w").write(json.dumps(data, indent=2) + "\n")
PY
echo "Staged in $MODS/$NAME and marked |edit in loading_order.json."
echo "Upload: launch the game -> Main Menu -> MODS -> Enhanced Orders -> UPLOAD."
