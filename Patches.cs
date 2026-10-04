using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace EnhancedOrders
{
    // Single click: CrewSim.PaintPos -> PaintOrder. Guard clicks that land on the fallback window
    // (the PDA panels are regular UI, which the game already keeps clicks out of).
    [HarmonyPatch(typeof(CrewSim), "PaintPos")]
    internal static class PaintPosPatch
    {
        private static bool Prefix() => !(Plugin.MouseOverPanel && OrderPicker.Active != null);
    }

    // Drag: CrewSim.PaintBounds runs PaintOrder per tile in a coroutine. When picking,
    // take the whole box as the area instead of queueing jobs.
    [HarmonyPatch(typeof(CrewSim), "PaintBounds")]
    internal static class PaintBoundsPatch
    {
        private static bool Prefix(CrewSim __instance, Bounds bnd)
        {
            OrderPicker p = OrderPicker.Active;
            if (p == null) return true;
            if (Plugin.MouseOverPanel) return false;
            if (!p.Picking) return true;

            var positions = new List<Vector3>();
            Vector3 vPos = bnd.min;
            for (float x = TileUtils.GridAlign(bnd.min.x); x <= bnd.max.x; x += 1f)
            {
                for (float y = TileUtils.GridAlign(bnd.min.y); y <= bnd.max.y; y += 1f)
                {
                    vPos.x = x;
                    vPos.y = y;
                    positions.Add(vPos);
                }
            }
            p.Collect(__instance, positions);
            return false;
        }
    }

    // Single click while picking picks that one tile. Otherwise Repair gets the condition
    // threshold (mirrors the vanilla loop plus the check) and everything else runs vanilla.
    [HarmonyPatch(typeof(CrewSim), "PaintOrder")]
    internal static class PaintOrderPatch
    {
        private static bool Prefix(CrewSim __instance, Vector2 vPos, JsonInstallable ji)
        {
            if (ji == null) return true;
            OrderPicker p = OrderPicker.For(ji.strName);
            if (p != null && p.Picking)
            {
                p.Collect(__instance, new[] { (Vector3)vPos });
                return false;
            }
            if (ji.strName != "Repair") return true;

            double max = Plugin.MaxCondition.Value;
            foreach (CondOwner co in __instance.FindCOsAtWorldPosition(vPos, null, bInteractive: false))
            {
                if (GUIPDA.ctJobFilter != null && !GUIPDA.ctJobFilter.Triggered(co)) continue;
                if (Plugin.ConditionPercent(co) >= max) continue;
                Plugin.QueueJob(__instance, co, ji.strName);
            }
            return false;
        }
    }

    // Every step of a restore job re-checks Triggered; failing it once the item is
    // past the stop threshold ends the job there instead of at 100%.
    [HarmonyPatch(typeof(Interaction), "TriggeredInternal")]
    internal static class RestoreStopPatch
    {
        private static void Postfix(Interaction __instance, CondOwner objThem, ref bool __result)
        {
            if (__result && Plugin.IsRestore(__instance.strName) && Plugin.RestoreDone(objThem))
                __result = false;
        }
    }

    // Keep crew from picking items that are already past the stop threshold as auto-tasks.
    [HarmonyPatch(typeof(CondOwner), "InsertUndamage")]
    internal static class AutoRestorePatch
    {
        private static bool Prefix(CondOwner co, bool doRepair) => doRepair || !Plugin.RestoreDone(co);
    }
}
