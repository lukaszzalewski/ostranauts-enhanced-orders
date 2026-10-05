using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using UnityEngine;

namespace EnhancedOrders
{
    // Pick-and-confirm for one PDA order. Flow: Picking on -> click/drag the ship to pick an
    // area (nothing is queued, a new pick replaces it) -> tick object types -> Confirm queues
    // the order on those types in that area. Picking off = the order's normal painting.
    // Ticks carry over between picks, so a preset loaded before picking pre-ticks the list.
    internal class OrderPicker
    {
        internal class Entry
        {
            public string Name;     // CondOwner.strName
            public string Label;
            public string Group;    // install menu group, see BuildGroups
            public int Count;
        }

        internal const int PresetSlots = 3;

        internal static readonly OrderPicker Uninstall = new OrderPicker(
            "Uninstall", "Uninstall Picker", "UNINSTALL",
            co => Plugin.HasJob(co, "Uninstall"),
            () => "Uninstalling: everything (vanilla)",
            (cs, co) => Plugin.QueueJob(cs, co, "Uninstall"));

        internal static readonly OrderPicker Repair = new OrderPicker(
            "Repair", "Repair Picker", "REPAIR",
            co => Plugin.HasJob(co, "Repair") && Plugin.ConditionPercent(co) < Plugin.MaxCondition.Value,
            () => $"Repairing: anything below {Plugin.MaxCondition.Value}%",
            (cs, co) => Plugin.QueueJob(cs, co, "Repair"));

        // Mirrors the Haul branch of vanilla CrewSim.PaintOrder.
        internal static readonly OrderPicker Haul = new OrderPicker(
            "Haul", "Haul Picker", "HAUL",
            co => WorkManager.CTHaul != null && WorkManager.CTHaul.Triggered(co),
            () => "Hauling: everything (vanilla)",
            (cs, co) =>
            {
                cs.workManager.AddTask(new Task2
                {
                    strDuty = "Haul",
                    strInteraction = "ACTHaulItem",
                    strTargetCOID = co.strID,
                    strName = "HaulJob" + co.strID
                });
                return true;
            });

        internal static readonly OrderPicker[] All = { Uninstall, Repair, Haul };

        internal readonly string Order;     // JsonInstallable.strName
        internal readonly string Title;
        private readonly string _verb;
        private readonly Func<CondOwner, bool> _eligible;
        private readonly Func<string> _vanillaStatus;
        private readonly Func<CrewSim, CondOwner, bool> _queue;
        private ConfigEntry<string>[] _presets = new ConfigEntry<string>[0];

        internal readonly HashSet<string> Selected = new HashSet<string>();
        internal List<Entry> Candidates = new List<Entry>();
        internal bool Picking;
        internal int Version; // bumped whenever Candidates changes
        private List<Vector3> _area = new List<Vector3>();

        private OrderPicker(string order, string title, string verb, Func<CondOwner, bool> eligible,
            Func<string> vanillaStatus, Func<CrewSim, CondOwner, bool> queue)
        {
            Order = order;
            Title = title;
            _verb = verb;
            _eligible = eligible;
            _vanillaStatus = vanillaStatus;
            _queue = queue;
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
            : SelectedCount == 0 ? "Tick what to " + _verb.ToLowerInvariant() + ", then confirm"
            : $"{SelectedCount} object(s) ready to {_verb.ToLowerInvariant()}";

        internal string ConfirmText => SelectedCount > 0 ? $"{_verb} {SelectedCount} SELECTED" : "NOTHING TICKED";

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

        internal bool GroupTicked(string group) =>
            Candidates.Where(e => e.Group == group).All(e => Selected.Contains(e.Name));

        internal void ToggleGroup(string group, bool on)
        {
            foreach (Entry e in Candidates.Where(e => e.Group == group)) Toggle(e.Name, on);
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

        // Picks the given tiles as the area and lists the object types in it, sorted by
        // install group then name.
        internal void Collect(CrewSim cs, IEnumerable<Vector3> positions)
        {
            _area = positions.ToList();
            var found = new Dictionary<string, Entry>();
            foreach (CondOwner co in ObjectsIn(cs))
            {
                if (!found.TryGetValue(co.strName, out Entry e))
                    found[co.strName] = e = new Entry
                    {
                        Name = co.strName, Label = co.FriendlyName, Group = BuildGroups.Of(co.strName)
                    };
                e.Count++;
            }

            Candidates = found.Values.OrderBy(e => BuildGroups.Rank(e.Group)).ThenBy(e => e.Label).ToList();
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
            if (cs == null || SelectedCount == 0) return;

            int queued = 0;
            foreach (CondOwner co in ObjectsIn(cs).ToList())
                if (Selected.Contains(co.strName) && _queue(cs, co))
                    queued++;
            Plugin.Log.LogInfo($"Queued {queued} {Order} job(s).");
            SetPicking(false);
        }

        // Presets: "<name>|<id>,<id>,..." in the config, PresetSlots per order.
        internal void BindPresets(ConfigFile config)
        {
            _presets = new ConfigEntry<string>[PresetSlots];
            for (int i = 0; i < PresetSlots; i++)
                _presets[i] = config.Bind("Presets", $"{Order}{i + 1}", "",
                    $"Saved {Order} selection {i + 1}. Set from the PDA panel.");
        }

        internal string PresetName(int slot)
        {
            string v = slot < _presets.Length ? _presets[slot].Value : "";
            int bar = v.IndexOf('|');
            return bar > 0 ? v.Substring(0, bar) : null;
        }

        // Saves the ticked types that are in the current list (or all ticks if nothing is listed).
        internal bool SavePreset(int slot)
        {
            List<Entry> ticked = Candidates.Where(e => Selected.Contains(e.Name)).ToList();
            List<string> names = Candidates.Count > 0 ? ticked.Select(e => e.Name).ToList() : Selected.ToList();
            if (names.Count == 0 || slot >= _presets.Length) return false;

            string first = ticked.Count > 0 ? ticked[0].Label : names[0];
            int bracket = first.IndexOf(" [", StringComparison.Ordinal);
            if (bracket > 0) first = first.Substring(0, bracket);
            string name = names.Count > 1 ? $"{first} +{names.Count - 1}" : first;
            _presets[slot].Value = name.Replace("|", "/") + "|" + string.Join(",", names);
            return true;
        }

        // Ticks the preset's types and turns picking on, ready for an area.
        internal void LoadPreset(int slot)
        {
            string v = slot < _presets.Length ? _presets[slot].Value : "";
            int bar = v.IndexOf('|');
            if (bar < 0) return;
            Selected.Clear();
            foreach (string n in v.Substring(bar + 1).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                Selected.Add(n);
            Picking = true;
        }

        internal void ClearPreset(int slot)
        {
            if (slot < _presets.Length) _presets[slot].Value = "";
        }
    }
}
