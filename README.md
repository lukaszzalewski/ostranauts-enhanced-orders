# Ostranauts – Enhanced Orders

BepInEx plugin that adds pick-and-confirm panels for the Uninstall, Repair and Haul
orders to PDA → Orders, below the job filter checkboxes. It replaces the separate *Uninstall Picker* and *Repair Threshold* mods (their
settings are imported on first ## Uninstall (UNIN), Repair (REPR) and Haul (HAUL)
1. **Pick area**: tick it, then click a tile or drag a box. Nothing is queued; the panel
   lists the object types in that area with counts, grouped like the install menu (HULL,
   HVAC, POWR, SENS, CTRL, FURN, APPS, MISC; anything not buildable is under OTHER).
   Another click or drag replaces the area.
2. Tick the types you want. A group's checkbox ticks or unticks everything in it.
3. **UNINSTALL / REPAIR / HAUL N SELECTED** queues just those objects in the picked area and
   returns to normal. **Cancel** (or unticking Pick area) drops the pick.

With Pick off, the orders paint as usual (PDA filter checkboxes apply). A pick is dropped
when its order is closed. Types are matched by exact object ID, so damaged variants show
up as separate entries.

### Saved selections
Each order has three preset slots next to Pick area:
- **+ SAVE** (empty slot) saves the current ticks; the slot is named after the first type.
- Click a saved slot to load it: its types are ticked and Pick area turns on, so the next
  pick comes up pre-ticked.
- Right-click a saved slot to clear it.

Presets are stored in the config under `[Presets]`.

ed variants show
up as separate entries.

## Repair thresholds
The Repair panel also has two sliders:
- **Repair only below** (`MaxConditionPercent`, default 75): Repair, picked or painted, only
  selects items below this condition. 100 = vanilla.
- **Stop restoring at** (`StopRestoreAtPercent`, default 90): restore work ends once an item
  reaches this condition, and crew won't auto-restore items already at or above it.

Condition % is the same number the item tooltip shows.

Settings live in `BepInEx/config/natakou.ostranauts.enhancedorders.cfg`; `FontSize`
(default 20) sets the panel text size and applies after loading a save. If the PDA layout
can't be found, the panels fall back to a window at the top of the screen.

## Build / install
Needs BepInEx 5 (win x64) in the game folder and, on Linux/Proton, the Steam launch option:

    WINEDLLOVERRIDES="winhttp=n,b" %command%

    dotnet build -c Release   # copies the dll into <game>/BepInEx/plugins

Override the game path with `-p:GameDir=...` (BepInEx references come from `<game>/BepInEx/core`,
or set `-p:BepInExCore=...`).

## Publishing to the Steam Workshop
Ostranauts doesn't load DLLs itself. Workshop players need BepInEx plus the
[BepInEx Mod Loader](https://steamcommunity.com/sharedfiles/filedetails/?id=3741030124),
which copies `<item>/BepInEx/plugins/*` from enabled Workshop items into the game's
`BepInEx/plugins/Workshop/<id>/`.

    ./package.sh           # build + assemble dist/EnhancedOrders/
    ./package.sh --stage   # + copy into Ostranauts_Data/Mods/, add "EnhancedOrders|edit" to loading_order.json

Then in game: Main Menu → MODS → Enhanced Orders → **UPLOAD** (needs the Steam overlay and the
Workshop agreement accepted). The first upload creates the item, using
`workshop/description.bbcode` as its description; the game writes the new `strWorkshopID` into
the staged `mod_info.json`, and the next `./package.sh` copies it into `workshop/mod_info.json`;
commit that so later uploads update the same item. Later uploads don't touch the description;
edit it on the Steam page. After the first upload, add the BepInEx Mod Loader as a
*Required item* on the Workshop page.

- Version: `Plugin.Version` in `Plugin.cs` (goes into `strModVersion`). Bump `strGameVersion` in
  `workshop/mod_info.json` when rebuilding for a game update.
- `workshop/preview.png` (< 1 MB) is a generated title card (`workshop/make_preview.py`);
  replace it with an in-game screenshot when you have one.
- If you subscribe to your own item, remove the dev build from `BepInEx/plugins/` (the
  `dotnet build` deploy) so only one copy loads.

## License
MIT, see [LICENSE](LICENSE).
