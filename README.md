# Ostranauts – Enhanced Orders

BepInEx plugin that adds pick-and-confirm panels to PDA → Orders, below the job filter
checkboxes. It replaces the separate *Uninstall Picker* and *Repair Threshold* mods (their
settings are imported on first run; don't install them alongside this).

## Uninstall (UNIN) and Repair (REPR)
1. **Pick objects from ship**: tick it, then click a tile or drag a box. Nothing is queued;
   the panel lists the object types in that area, with counts. Another click or drag
   replaces the area.
2. Tick the types you want.
3. **UNINSTALL / REPAIR N SELECTED** queues just those objects in the picked area and returns
   to normal. **Cancel** (or unticking Pick) drops the pick.

With Pick off, the orders paint as usual (PDA filter checkboxes apply). A pick is dropped
when its order is closed. Types are matched by exact object ID, so damaged variants show
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
