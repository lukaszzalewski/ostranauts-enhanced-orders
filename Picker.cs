using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EnhancedOrders
{
    // Pick-and-confirm for one PDA order. Flow: Picking on -> click/drag the ship to pick an
    // area (nothing is queued, a new pick replaces it) -> tick object types -> Confirm queues
    // the order on those types in that area. Picking off = the order's normal painting.
    internal class OrderPicker
    {
        internal class Entry
        {
            public string Name;     // CondOwner.strName
            public string Label;
            public int Count;
        }

        internal static readonly OrderPicker Uninstall = new OrderPicker(
            "Uninstall", "Uninstall Picker", "UNINSTALL",
            co => Plugin.HasJob(co, "Uninstall"),
            () => "Uninstalling: everything (vanilla)");

        internal static readonly OrderPicker Repair = new OrderPicker(
            "Repair", "Repair Picker", "REPAIR",
            co => Plugin.HasJob(co, "Repair") && Plugin.ConditionPercent(co) < Plugin.MaxCondition.Value,
            () => $"Repairing: anything below {Plugin.MaxCondition.Value}%");

        internal static readonly OrderPicker[] All = { Uninstall, Repair };

        internal readonly string Order;     // JsonInstallable.strName
        internal readonly string Title;
        private readonly string _verb;
        private readonly Func<CondOwner, bool> _eligible;
        private readonly Func<string> _vanillaStatus;

        internal readonly HashSet<string> Selected = new HashSet<string>();
        internal List<Entry> Candidates = new List<Entry>();
        internal bool Picking;
        internal int Version; // bumped whenever Candidates changes
        private List<Vector3> _area = new List<Vector3>();

        private OrderPicker(string order, string title, string verb, Func<CondOwner, bool> eligible,
            Func<string> vanillaStatus)
        {
            Order = order;
            Title = title;
            _verb = verb;
            _eligible = eligible;
            _vanillaStatus = vanillaStatus;
        }

        internal static OrderPicker For(string order) => All.FirstOrDefault(p => p.Order == order);

        // The picker for the order currently being painted, if any.
        internal static OrderPicker Active =>
            CrewSim.objInstance != null && CrewSim.objInstance.goPaintJob != null && CrewSim.jiLast != null
                ? For(CrewSim.jiLast.strName)
                : null;

        internal string Status =>
            !Picking ? _vanillaStatus()
            : Candidates.Count == 0 ? "Click or drag on the ship to pick an area"
            : Selected.Count == 0 ? "Tick what to " + _verb.ToLowerInvariant() + ", then confirm"
            : $"{SelectedCount} object(s) ready to {_verb.ToLowerInvariant()}";

        internal string ConfirmText => Selected.Count > 0 ? $"{_verb} {SelectedCount} SELECTED" : "NOTHING TICKED";

        internal int SelectedCount => Candidates.Where(e => Selected.Contains(e.Name)).Sum(e => e.Count);

        internal void SetPicking(bool on)
        {
            Picking = on;
            if (on) return;
            Selected.Clear();
            Candidates.Clear();
            _area.Clear();
            Version++;
        }

        internal void Toggle(string name, bool on)
        {
            if (on) Selected.Add(name);
            else Selected.Remove(name);
        }

        // Eligible objects in the area, each once (multi-tile objects are hit once per tile).
        private IEnumerable<CondOwner> ObjectsIn(CrewSim cs)
        {
            var seen = new HashSet<string>();
            foreach (Vector3 pos in _area)
                foreach (CondOwner co in cs.FindCOsAtWorldPosition(pos, null, bInteractive: false))
                    if (co != null && seen.Add(co.strID) && _eligible(co))
                        yield return co;
        }

        // Picks the given tiles as the area and lists the object types in it. Ticks
        // survive a re-pick for types that are still there.
        internal void Collect(CrewSim cs, IEnumerable<Vector3> positions)
        {
            _area = positions.ToList();
            var found = new Dictionary<string, Entry>();
            foreach (CondOwner co in ObjectsIn(cs))
            {
                if (!found.TryGetValue(co.strName, out Entry e))
                    found[co.strName] = e = new Entry { Name = co.strName, Label = co.FriendlyName };
                e.Count++;
            }
            Selected.IntersectWith(found.Keys);

            Candidates = found.Values.OrderBy(e => e.Label).ToList();
            // Different variants (e.g. damaged) can share a display name.
            foreach (var dup in Candidates.GroupBy(e => e.Label).Where(g => g.Count() > 1))
                foreach (Entry e in dup)
                    e.Label += $" [{e.Name}]";
            Version++;
        }

        // Queues the order for the ticked types in the picked area, then goes back to normal.
        internal void Confirm()
        {
            CrewSim cs = CrewSim.objInstance;
            if (cs == null || Selected.Count == 0) return;

            int queued = 0;
            foreach (CondOwner co in ObjectsIn(cs).ToList())
                if (Selected.Contains(co.strName) && Plugin.QueueJob(cs, co, Order))
                    queued++;
            Plugin.Log.LogInfo($"Queued {queued} {Order} job(s).");
            SetPicking(false);
        }
    }
}
