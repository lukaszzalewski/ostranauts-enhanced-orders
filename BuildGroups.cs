using System;
using System.Collections.Generic;

namespace EnhancedOrders
{
    // Maps object IDs to the PDA install menu groups (HULL, HVAC, ...). Install jobs list
    // their item (strActionCO) and outputs (aLootCOs / strLootOut); other jobs on an object
    // whose outputs are already mapped (e.g. uninstall -> loose item) map that object too,
    // which also catches installed and damaged variants.
    internal static class BuildGroups
    {
        // Same order as the PDA install menu.
        internal static readonly string[] Order = { "HULL", "HVAC", "POWR", "SENS", "CTRL", "FURN", "APPS", "MISC" };
        internal const string Other = "OTHER";

        private static Dictionary<string, string> _map;

        internal static string Of(string coName)
        {
            if (_map == null) Build();
            return _map != null && coName != null && _map.TryGetValue(coName, out string g) ? g : Other;
        }

        internal static int Rank(string group)
        {
            int i = Array.IndexOf(Order, group);
            return i < 0 ? Order.Length : i;
        }

        private static void Build()
        {
            var opts = Installables.dictJobBuildOptions;
            if (opts == null) return; // data not loaded yet; try again next time
            var map = new Dictionary<string, string>();

            foreach (string g in Order)
            {
                if (!opts.TryGetValue(g, out var jis)) continue;
                foreach (JsonInstallable ji in jis.Values)
                {
                    Add(map, ji.strActionCO, g);
                    foreach (string n in Outputs(ji)) Add(map, n, g);
                }
            }

            if (DataHandler.dictInstallables != null)
            {
                foreach (JsonInstallable ji in DataHandler.dictInstallables.Values)
                {
                    if (ji.strActionCO == null || map.ContainsKey(ji.strActionCO)) continue;
                    foreach (string n in Outputs(ji))
                    {
                        if (map.TryGetValue(n, out string g))
                        {
                            map[ji.strActionCO] = g;
                            break;
                        }
                    }
                }
            }
            _map = map;
            Plugin.Log.LogInfo($"Mapped {map.Count} object types to install groups.");
        }

        private static IEnumerable<string> Outputs(JsonInstallable ji)
        {
            if (ji.aLootCOs != null)
                foreach (string n in ji.aLootCOs) yield return Clean(n);
            if (ji.strLootOut != null && DataHandler.dictLoot != null
                && DataHandler.dictLoot.TryGetValue(ji.strLootOut, out Loot loot) && loot.aCOs != null)
                foreach (string n in loot.aCOs) yield return Clean(n);
        }

        // Loot entries look like "ItmName=1.0x1".
        private static string Clean(string entry)
        {
            if (entry == null) return null;
            int eq = entry.IndexOf('=');
            return (eq >= 0 ? entry.Substring(0, eq) : entry).Trim();
        }

        private static void Add(Dictionary<string, string> map, string name, string group)
        {
            if (!string.IsNullOrEmpty(name) && !map.ContainsKey(name)) map[name] = group;
        }
    }
}
