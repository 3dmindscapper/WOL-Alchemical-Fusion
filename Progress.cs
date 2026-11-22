using System;
using System.Collections.Generic;

namespace WolHybridBosses
{
    // v1.1.0: progress kept between runs (league records and rank, the White Alchemist's wins, the White Mantle).
    // BepInEx/config/osi.wol.hybridbosses.progress.txt, one "key=value" per line. Delete it to reset.
    internal static class Progress
    {
        static Dictionary<string, string> values;
        static string FilePath { get { return System.IO.Path.Combine(BepInEx.Paths.ConfigPath, "osi.wol.hybridbosses.progress.txt"); } }

        static void Load()
        {
            if (values != null) return;
            values = new Dictionary<string, string>();
            try
            {
                if (!System.IO.File.Exists(FilePath)) return;
                foreach (string line in System.IO.File.ReadAllLines(FilePath))
                {
                    int eq = line.IndexOf('=');
                    if (eq > 0) values[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
                }
            }
            catch (Exception ex) { Plugin.Log.LogWarning("PROGRESS could not read: " + ex.Message); }
        }

        static void Save()
        {
            if (Plugin.TestMode) return; // v1.1.1: test hotkeys on: kept in memory only
            try
            {
                List<string> lines = new List<string>();
                foreach (KeyValuePair<string, string> kv in values) lines.Add(kv.Key + "=" + kv.Value);
                lines.Sort();
                System.IO.File.WriteAllLines(FilePath, lines.ToArray());
            }
            catch (Exception ex) { Plugin.Log.LogWarning("PROGRESS could not save: " + ex.Message); }
        }

        internal static int GetInt(string key, int fallback = 0)
        {
            Load();
            string v;
            int n;
            return values.TryGetValue(key, out v) && int.TryParse(v, out n) ? n : fallback;
        }

        internal static void SetInt(string key, int value) { Load(); values[key] = value.ToString(); Save(); }
        internal static void AddInt(string key, int delta) { SetInt(key, GetInt(key) + delta); }
        internal static bool GetBool(string key) { return GetInt(key) != 0; }
        internal static void SetBool(string key, bool value) { SetInt(key, value ? 1 : 0); }
    }
}
