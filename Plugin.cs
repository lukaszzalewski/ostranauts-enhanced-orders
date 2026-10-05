using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace EnhancedOrders
{
    [BepInPlugin(Guid, "Enhanced Orders", Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "natakou.ostranauts.enhancedorders";
        public const string Version = "1.0.0";
        private const int WindowId = 0x454e4f52; // "ENOR"

        internal static ConfigEntry<int> MaxCondition;
        internal static ConfigEntry<int> StopRestoreAt;
        internal static ConfigEntry<int> FontSize;
        internal static BepInEx.Logging.ManualLogSource Log;
        internal static bool MouseOverPanel;

        private static Rect _window = new Rect(0f, 12f, 320f, 0f);
        private static OrderPicker _lastActive;

        private void Awake()
        {
            Log = Logger;

            // First run: carry settings over from the separate Repair Threshold / Uninstall Picker mods.
            bool fresh = !File.Exists(Config.ConfigFilePath);
            var old = new Dictionary<string, int>();
            if (fresh)
            {
                ReadOld("natakou.ostranauts.repairthreshold.cfg", old);
                ReadOld("natakou.ostranauts.uninstallpicker.cfg", old);
            }

            MaxCondition = Config.Bind("Repair", "MaxConditionPercent", Old(old, "MaxConditionPercent", 75),
                new ConfigDescription(
                    "The Repair order only selects items whose condition is below this percentage. " +
                    "100 = vanilla behaviour (anything with wear).",
                    new AcceptableValueRange<int>(1, 100)));
            StopRestoreAt = Config.Bind("Repair", "StopRestoreAtPercent", Old(old, "StopRestoreAtPercent", 90),
                new ConfigDescription(
                    "Restore (wear repair) work stops once an item reaches this condition, and crew " +
                    "won't start restoring items already at or above it. 100 = vanilla behaviour.",
                    new AcceptableValueRange<int>(1, 100)));
            FontSize = Config.Bind("PDA", "FontSize", Old(old, "FontSize", 20),
                new ConfigDescription("Text size of the order panels in the PDA. Rows scale with it. " +
                                      "Takes effect the next time the PDA is built (load a save).",
                    new AcceptableValueRange<int>(10, 40)));
            foreach (OrderPicker p in OrderPicker.All) p.BindPresets(Config);
            if (old.Count > 0) Logger.LogInfo($"Imported {old.Count} setting(s) from the old separate mods.");

            // Patch class by class so a game update that breaks one patch doesn't take down the rest.
            var harmony = new Harmony(Guid);
            int failed = 0;
            foreach (Type t in AccessTools.GetTypesFromAssembly(typeof(Plugin).Assembly))
            {
                if (!t.IsDefined(typeof(HarmonyPatch), false)) continue;
                try { harmony.CreateClassProcessor(t).Patch(); }
                catch (Exception e)
                {
                    failed++;
                    Logger.LogError($"Patch {t.Name} failed (game update?), that feature is off: {e.Message}");
                }
            }
            Logger.LogInfo(failed == 0 ? $"Enhanced Orders {Version} loaded"
                : $"Enhanced Orders {Version} loaded with {failed} failed patch(es)");
        }

        private static void ReadOld(string file, Dictionary<string, int> into)
        {
            string path = Path.Combine(Paths.ConfigPath, file);
            if (!File.Exists(path)) return;
            foreach (string line in File.ReadAllLines(path))
            {
                int eq = line.IndexOf('=');
                if (eq <= 0 || line.TrimStart().StartsWith("#")) continue;
                if (int.TryParse(line.Substring(eq + 1).Trim(), out int v))
                    into[line.Substring(0, eq).Trim()] = v;
            }
        }

        private static int Old(Dictionary<string, int> old, string key, int def) =>
            old.TryGetValue(key, out int v) ? v : def;

        private void Update()
        {
            // Order closed or switched: drop any pick so it can't linger.
            OrderPicker active = OrderPicker.Active;
            if (_lastActive != null && _lastActive != active) _lastActive.SetPicking(false);
            _lastActive = active;
            PdaPanel.Tick();
        }

        // Fallback window, only used if the panels couldn't be added to the PDA.
        private void OnGUI()
        {
            OrderPicker p = OrderPicker.Active;
            if (!PdaPanel.Failed || p == null)
            {
                MouseOverPanel = false;
                return;
            }

            _window.x = (Screen.width - _window.width) / 2f;
            _window = GUILayout.Window(WindowId, _window, id => DrawWindow(p), p.Title, GUILayout.Width(320f));
            MouseOverPanel = _window.Contains(Event.current.mousePosition);
        }

        private static void DrawWindow(OrderPicker p)
        {
            GUILayout.Label(p.Status);
            if (p == OrderPicker.Repair)
            {
                PercentRow($"Repair only below {MaxCondition.Value}%", MaxCondition);
                PercentRow($"Stop restoring at {StopRestoreAt.Value}%", StopRestoreAt);
            }

            GUILayout.BeginHorizontal();
            bool pick = GUILayout.Toggle(p.Picking, "Pick area", GUILayout.Width(90f));
            if (pick != p.Picking) p.SetPicking(pick);
            for (int i = 0; i < OrderPicker.PresetSlots; i++)
            {
                string name = p.PresetName(i);
                if (name == null)
                {
                    GUI.enabled = p.SelectedCount > 0;
                    if (GUILayout.Button("+ save")) p.SavePreset(i);
                    GUI.enabled = true;
                }
                else if (GUILayout.Button(name))
                {
                    // Right-click clears the slot, left-click loads it.
                    if (Event.current.button == 1) p.ClearPreset(i);
                    else p.LoadPreset(i);
                }
            }
            GUILayout.EndHorizontal();

            string group = null;
            foreach (OrderPicker.Entry e in p.Candidates)
            {
                if (e.Group != group)
                {
                    group = e.Group;
                    bool all = p.GroupTicked(group);
                    if (GUILayout.Toggle(all, group) != all) p.ToggleGroup(group, !all);
                }
                bool on = p.Selected.Contains(e.Name);
                if (GUILayout.Toggle(on, $"    {e.Label}  x{e.Count}") != on) p.Toggle(e.Name, !on);
            }

            if (p.Candidates.Count == 0) return;
            GUILayout.BeginHorizontal();
            GUI.enabled = p.SelectedCount > 0;
            if (GUILayout.Button(p.ConfirmText)) p.Confirm();
            GUI.enabled = true;
            if (GUILayout.Button("Cancel")) p.SetPicking(false);
            GUILayout.EndHorizontal();
        }

        private static void PercentRow(string label, ConfigEntry<int> entry)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(170f));
            int v = Mathf.RoundToInt(GUILayout.HorizontalSlider(entry.Value, 1f, 100f));
            if (v != entry.Value) entry.Value = v;
            GUILayout.EndHorizontal();
        }

        // Generated per item from installables with the "Undamage" suffix: "ACT<item>Undamage"
        // starts the job, "ACT<item>UndamageAllow" is the step that re-queues itself until done.
        internal static bool IsRestore(string iaName) =>
            iaName != null && (iaName.EndsWith("Undamage", StringComparison.Ordinal)
                               || iaName.EndsWith("UndamageAllow", StringComparison.Ordinal));

        internal static bool RestoreDone(CondOwner co) =>
            StopRestoreAt.Value < 100 && co != null && ConditionPercent(co) >= StopRestoreAt.Value;

        // Same formula the game's tooltip uses for "Condition: X%".
        internal static double ConditionPercent(CondOwner co)
        {
            double max = co.GetCondAmount("StatDamageMax");
            if (max <= 0.0) return 100.0;
            return (max - co.GetCondAmount("StatDamage")) / max * 100.0;
        }

        internal static bool HasJob(CondOwner co, string order)
        {
            List<string> actions = co.GetJobActions(order);
            return actions != null && actions.Count > 0;
        }

        // Queues the first of co's job actions for this order that the selected crew can do.
        // Mirrors the inner loop of vanilla CrewSim.PaintOrder.
        internal static bool QueueJob(CrewSim cs, CondOwner co, string order)
        {
            foreach (string jobAction in co.GetJobActions(order))
            {
                Interaction ia = DataHandler.GetInteraction(jobAction);
                if (ia != null && ia.Triggered(CrewSim.GetSelectedCrew(), co, bStats: false, bIgnoreItems: true))
                {
                    cs.workManager.AddTask(new Task2
                    {
                        strDuty = ia.strDuty,
                        strInteraction = jobAction,
                        strTargetCOID = co.strID,
                        strName = order + "Job" + co.strID
                    });
                    return true;
                }
            }
            return false;
        }
    }
}
