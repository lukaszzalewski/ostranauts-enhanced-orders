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
