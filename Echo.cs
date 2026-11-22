using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace WolHybridBosses
{
    // v0.9.0: the Echo of your last run. During a run the player's loadout is noted every few seconds (equipped
    // arcana with their roles and infusions, robe, relics, how far the run got); when the player is back in the Hub
    // it becomes the "last run" record (saved to BepInEx/config/osi.wol.hybridbosses.echo.txt). In the next runs the
    // rival portal's selector room offers an Echo portal: a rival wizard with exactly that loadout (arcana in their
    // infused colours, the robe and its stats, the stat relics), once per run.
    internal sealed class EchoRecord
    {
        public List<string[]> Arcana = new List<string[]>(); // { skillID, role (basic / dash / standard / signature), infused element name or "" }
        public string Outfit = "";
        public List<string> Relics = new List<string>();
        public int Tier, Stage, Loop;

        public string Basic { get { return Find("basic"); } }
        public string Dash { get { return Find("dash"); } }

        public List<string> Standard
        {
            get { List<string> l = new List<string>(); foreach (string[] a in Arcana) if (a[1] == "standard" || a[1] == "signature") l.Add(a[0]); return l; }
        }

        string Find(string role) { foreach (string[] a in Arcana) if (a[1] == role) return a[0]; return null; }

        public ElementType? InfusionOf(string id)
        {
            foreach (string[] a in Arcana) if (a[0] == id && a[2].Length > 0) return EchoRecorder.FromName(a[2]);
            return null;
        }

        public string Reached { get { return "tier " + (Tier + 1) + (Loop > 0 ? ", loop " + (Loop + 1) : ""); } }

        public string Serialize()
        {
            List<string> ar = new List<string>();
            foreach (string[] a in Arcana) ar.Add(a[0] + ":" + a[1] + ":" + a[2]);
            return "arcana=" + string.Join(",", ar.ToArray()) + "\noutfit=" + Outfit + "\nrelics=" + string.Join(",", Relics.ToArray())
                + "\nreached=" + Tier + "," + Stage + "," + Loop;
        }

        public static EchoRecord Parse(string[] lines)
        {
            EchoRecord r = new EchoRecord();
            foreach (string line in lines)
            {
                int eq = line.IndexOf('=');
                if (eq < 0) continue;
                string k = line.Substring(0, eq), v = line.Substring(eq + 1);
                if (k == "arcana")
                    foreach (string part in v.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        string[] f = part.Split(':');
                        if (f.Length >= 2) r.Arcana.Add(new[] { f[0], f[1], f.Length > 2 ? f[2] : "" });
                    }
                else if (k == "outfit") r.Outfit = v;
                else if (k == "relics") r.Relics.AddRange(v.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
                else if (k == "reached")
                {
                    string[] f = v.Split(',');
                    if (f.Length >= 3) { int.TryParse(f[0], out r.Tier); int.TryParse(f[1], out r.Stage); int.TryParse(f[2], out r.Loop); }
                }
            }
            return r.Arcana.Count > 0 ? r : null;
        }
    }

    internal class EchoRecorder : MonoBehaviour
    {
        static EchoRecord last;      // the previous run (fought as the Echo)
        static bool loaded;
        EchoRecord current;          // this run, noted as it goes
        bool wasInRun;
        float nextNote;
        internal static bool FoughtThisRun;

        static string FilePath { get { return System.IO.Path.Combine(BepInEx.Paths.ConfigPath, "osi.wol.hybridbosses.echo.txt"); } }

        internal static EchoRecord Last
        {
            get
            {
                if (!loaded)
                {
                    loaded = true;
                    try { if (System.IO.File.Exists(FilePath)) last = EchoRecord.Parse(System.IO.File.ReadAllLines(FilePath)); } catch (Exception) { last = null; }
                }
                return last;
            }
        }

        internal static bool Available { get { return Plugin.EchoEnabled.Value && Last != null && !FoughtThisRun; } }

        internal static void Ensure(GameObject host)
        {
            if (host.GetComponent<EchoRecorder>() == null) host.AddComponent<EchoRecorder>();
            if (host.GetComponent<BountyBoard>() == null) host.AddComponent<BountyBoard>();     // v0.9.16
            if (host.GetComponent<ModifiersMenu>() == null) host.AddComponent<ModifiersMenu>(); // v0.9.16
            if (host.GetComponent<LeagueMenu>() == null) host.AddComponent<LeagueMenu>();       // v1.1.1
            if (host.GetComponent<WhiteMantle>() == null) host.AddComponent<WhiteMantle>();     // v1.1.0
            if (host.GetComponent<PrimordialRobeWearer>() == null) host.AddComponent<PrimordialRobeWearer>(); // v1.1.1
        }

        void Update()
        {
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name ?? "";
            bool inHub = scene == "Hub" || scene == "Tutorial" || scene == "TitleScreen";
            if (inHub)
            {
                if (wasInRun) { wasInRun = false; Commit(); }
                FoughtThisRun = false;
                return;
            }
            if (Level.isBossRushMode || Time.time < nextNote) return;
            nextNote = Time.time + 4f;
            EchoRecord r = Note();
            if (r != null) { current = r; wasInRun = true; }
        }

        // The run is over (back in the Hub): it becomes the Echo of the next runs.
        void Commit()
        {
            if (current == null || current.Arcana.Count < 2) return;
            last = current;
            loaded = true;
            try { System.IO.File.WriteAllText(FilePath, current.Serialize()); } catch (Exception ex) { Plugin.Log.LogWarning("ECHO could not save: " + ex.Message); }
            Plugin.Log.LogInfo("ECHO recorded your run: " + current.Arcana.Count + " arcana, robe " + current.Outfit + ", " + current.Relics.Count + " relics, reached " + current.Reached);
            current = null;
        }

        // Ctrl+Shift+F11 test key: your current loadout becomes the Echo now.
        internal static void RecordNow()
        {
            EchoRecord r = Note();
            if (r == null) { Plugin.Log.LogInfo("ECHO: nothing to record (no equipped arcana)"); return; }
            last = r; loaded = true; FoughtThisRun = false;
            try { System.IO.File.WriteAllText(FilePath, r.Serialize()); } catch (Exception) { }
            Plugin.Log.LogInfo("ECHO recorded your current loadout (test): " + r.Arcana.Count + " arcana, robe " + r.Outfit + ", " + r.Relics.Count + " relics");
            IntroUI.Show("Echo recorded", "Your current loadout will wait behind the next rival portal", new Color(0.72f, 0.66f, 0.85f), null, 2.5f);
        }

        static EchoRecord Note()
        {
            Player p = GameController.playerScripts != null && GameController.playerScripts.Length > 0 ? GameController.playerScripts[0] : null;
            if (p == null || p.assignedSkills == null) return null;
            EchoRecord r = new EchoRecord();
            foreach (Player.SkillState s in p.assignedSkills)
            {
                if (s == null || string.IsNullOrEmpty(s.skillID)) continue;
                string role = s.isBasic ? "basic" : s.isDash ? "dash" : s.isSignature ? "signature" : "standard";
                ElementType el;
                r.Arcana.Add(new[] { s.skillID, role, Infusion.TryGet(s.skillID, out el) ? Infusion.Name(el) : "" });
            }
            try { if (p.equippedOutfit != null) r.Outfit = p.equippedOutfit.outfitID ?? ""; } catch (Exception) { }
            try { if (p.inventory != null) r.Relics.AddRange(p.inventory.GetItemIDList()); } catch (Exception) { }
            r.Tier = Mathf.Max(0, GameController.tierCount);
            r.Stage = GameController.stageCount;
            r.Loop = GameController.loopCount;
            return r.Arcana.Count > 0 ? r : null;
        }

        internal static ElementType? FromName(string n)
        {
            switch (n)
            {
                case "Fire": return ElementType.Fire;
                case "Water": return ElementType.Water;
                case "Ice": return ElementType.Ice;
                case "Earth": return ElementType.Earth;
                case "Wind": return ElementType.Air;
                case "Thunder": return ElementType.Lightning;
                case "Chaos": return ElementType.Chaos;
                case "Radiant": return ArcanosBridge.ElementFor(StageVariant.Radiant);
                case "Prism": return ArcanosBridge.ElementFor(StageVariant.Prism);
            }
            return null;
        }
    }

    // What the Echo rival casts: the record's arcana that enemies can cast, with their infusions.
    internal class EchoInfusions : MonoBehaviour
    {
        public Dictionary<string, ElementType> Map = new Dictionary<string, ElementType>();
        public Dictionary<string, ElementType> Second = new Dictionary<string, ElementType>(); // v0.9.18: dual arcana
        readonly Dictionary<string, int> count = new Dictionary<string, int>();

        // The element for this cast: a dual arcana alternates between its two.
        internal bool TryNext(string id, out ElementType el)
        {
            if (!Map.TryGetValue(id, out el)) return false;
            ElementType s;
            if (Second.TryGetValue(id, out s))
            {
                int n;
                count.TryGetValue(id, out n);
                count[id] = n + 1;
                if (n % 2 == 1) el = s;
            }
            return true;
        }
    }

    internal static class EchoRival
    {
        // The record's main element (its standard arcana, infusions counted as their new element), for colours / pools.
        internal static string Prefix(EchoRecord r)
        {
            Dictionary<string, int> n = new Dictionary<string, int>();
            foreach (string id in r.Standard)
            {
                ElementType? inf = r.InfusionOf(id);
                string p = inf.HasValue ? Infusion.Prefix(inf.Value) : null;
                if (p == null) { ArcanaDef d = ArcanaCatalog.Get(id); p = d != null ? d.Element : null; }
                if (p == null || Variants.FromPrefix(p) == StageVariant.Radiant || Variants.FromPrefix(p) == StageVariant.Prism) continue;
                int c; n.TryGetValue(p, out c); n[p] = c + 1;
            }
            string best = null; int bc = 0;
            foreach (KeyValuePair<string, int> kv in n) if (kv.Value > bc && kv.Key != "Neutral") { bc = kv.Value; best = kv.Key; }
            return best ?? Rivals.StagePrefixOrRandom();
        }

        // A castable def for one of the record's arcana (null if enemies can't cast it).
        // v0.9.1: the same checks as every other rival's pools (castable, not Broken). The record's plain "Dash" (the
        // default dash, not an arcana) got through before and broke the fight: the player vanished from view.
        internal static ArcanaDef Def(string id)
        {
            if (string.IsNullOrEmpty(id) || id == "Dash" || EnemyArcana.Broken.Contains(id)) return null;
            ArcanaDef d = ArcanaCatalog.Get(id);
            return d != null && d.Castable ? d : null;
        }

        // The standard arcana it casts: the record's own castable ones, topped up from its element when some can't be cast.
        internal static List<ArcanaDef> Arcana(RivalMarker mk, List<ArcanaDef> pool, int count)
        {
            List<ArcanaDef> list = new List<ArcanaDef>();
            foreach (string id in mk.Echo.Standard)
            {
                ArcanaDef d = Def(id);
                if (d != null && d.Family != ArcanaFamily.Melee && d.Family != ArcanaFamily.Dash && !list.Contains(d)) list.Add(d);
            }
            int want = Mathf.Max(count, list.Count);
            foreach (ArcanaDef d in Rivals.PickVaried(pool, want, false)) { if (list.Count >= want) break; if (!list.Contains(d)) list.Add(d); }
            return list;
        }

        internal static void Infusions(Enemy e, RivalMarker mk)
        {
            EchoInfusions ei = e.gameObject.AddComponent<EchoInfusions>();
            foreach (string[] a in mk.Echo.Arcana)
            {
                ElementType? el = mk.Echo.InfusionOf(a[0]);
                if (el.HasValue) ei.Map[a[0]] = el.Value;
            }
        }

        // "Fire-infused Ice Spear" for the intro row.
        internal static string Named(RivalMarker mk, ArcanaDef d)
        {
            ElementType? el = mk.Echo != null ? mk.Echo.InfusionOf(d.SkillID) : null;
            ElementType inf;
            if (!el.HasValue && mk.Infused != null && mk.Infused.TryGetValue(d.SkillID, out inf)) el = inf; // v0.9.17: rematch rivals
            ElementType inf2;
            if (el.HasValue && mk.InfusedSecond != null && mk.InfusedSecond.TryGetValue(d.SkillID, out inf2)) // v0.9.18: dual
                return Infusion.Name(el.Value) + " / " + Infusion.Name(inf2) + " dual " + IntroUI.SkillName(d.SkillID);
            return el.HasValue ? Infusion.Name(el.Value) + "-infused " + IntroUI.SkillName(d.SkillID) : Variants.Named(d, mk.Prefix);
        }
    }
}
