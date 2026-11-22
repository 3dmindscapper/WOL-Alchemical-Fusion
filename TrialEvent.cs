using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WolHybridBosses
{
    // v0.8.0: infusion trials (ROADMAP 7b). Sometimes a trial portal opens on a stage (like the rival portal). It leads
    // to a trial room (one of the stage's own fight rooms, built off-map like the rival arena) where all your arcana
    // are forced into one element ("Trial of Frost") for three waves of the stage's enemies. Clear it: a pre-infused
    // arcana of that element drops (unlocked only), plus gold. A way-back portal is there from the start: leaving
    // early ends the trial and costs nothing but the time.
    internal class TrialEvent : MonoBehaviour
    {
        internal static TrialEvent Current;
        static readonly Vector2 RoomOffset = new Vector2(0f, -1140f);
        static readonly ElementType[] Elements = { ElementType.Fire, ElementType.Water, ElementType.Ice, ElementType.Earth, ElementType.Air, ElementType.Lightning };

        ElementType element;
        Room room;
        readonly List<Vector2> spots = new List<Vector2>();
        Vector2 entrancePos, arrival;
        GameObject entrance;
        bool started, finished;
        readonly List<string> enemyKeys = new List<string>();
        readonly List<Enemy> alive = new List<Enemy>();

        // LevelGenerator.levelGeneratedEventHandlers (after the rival event): the stage is built.
        internal static void OnLevelGenerated()
        {
            try
            {
                if (!Plugin.TrialsEnabled.Value || Level.isBossRushMode) return;
                if (UnityEngine.Random.value >= Plugin.TrialChance.Value) return;
                Vector2? spot = PickSpot();
                if (!spot.HasValue) { Plugin.Log.LogWarning("TRIAL: no spot for the portal on this stage"); return; }
                Open(spot.Value, Elements[UnityEngine.Random.Range(0, Elements.Length)], true);
            }
            catch (Exception ex) { Plugin.Log.LogWarning("TRIAL setup failed: " + ex); }
        }

        // A stage checkpoint away from the start and from the rival portal.
        static Vector2? PickSpot()
        {
            Vector2 start = Level.startPosition;
            Vector2? rival = RivalEvent.Current != null ? (Vector2?)RivalEvent.Current.EntrancePos : null;
            List<Vector2> ok = new List<Vector2>();
            foreach (Checkpoint cp in FindObjectsOfType<Checkpoint>())
            {
                if (cp == null || cp.transform.root.name.StartsWith("HybridBosses")) continue;
                Vector2 p = cp.transform.position;
                float d = Vector2.Distance(p, start);
                if (d < 12f || d > 80f) continue;
                if (rival.HasValue && Vector2.Distance(p, rival.Value) < 15f) continue;
                ok.Add(p);
            }
            if (ok.Count == 0) return null;
            return ok[UnityEngine.Random.Range(0, ok.Count)];
        }

        // Ctrl+F6 test key: a trial portal right beside you (elements in turn).
        static int testIndex;
        internal static void OpenHere()
        {
            Player p = GameController.playerScripts != null && GameController.playerScripts.Length > 0 ? GameController.playerScripts[0] : null;
            if (p == null) return;
            Open((Vector2)p.transform.position + new Vector2(-2.5f, 0f), Elements[testIndex++ % Elements.Length], false);
        }

        static void Open(Vector2 at, ElementType el, bool announce)
        {
            if (Current != null) Current.Close();
            GameObject go = new GameObject("HybridBosses_TrialEvent");
            TrialEvent t = go.AddComponent<TrialEvent>();
            t.element = el;
            t.entrancePos = at;
            if (!t.Build()) { Destroy(go); return; }
            Current = t;
            string name = Infusion.Name(el);
            if (announce) IntroUI.Show("A trial portal has opened", "Somewhere on this floor  ·  Trial of " + name, Infusion.ColorOf(el), null, 4f);
        }

        bool Build()
        {
            room = RivalEvent.BuildRoom(Level.startPosition + RoomOffset, true, spots);
            if (room == null || spots.Count == 0) { Plugin.Log.LogWarning("TRIAL: could not build the room"); if (room != null) Destroy(room.gameObject); return false; }
            arrival = RivalEvent.Central(room, spots);
            entrance = RivalEvent.MakePortal(entrancePos, arrival, Infusion.ColorOf(element));
            StartCoroutine(Settle());
            Plugin.Log.LogInfo("TRIAL of " + Infusion.Name(element) + ": portal at " + entrancePos + " -> " + room.name);
            return true;
        }

        // After physics settles: land on open floor, and put the way-back portal beside the arrival.
        IEnumerator Settle()
        {
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            arrival = RivalEvent.Roomy(arrival, RivalEvent.Mean(spots), room);
            if (entrance != null)
            {
                entrancePos = RivalEvent.Roomy(entrancePos, entrancePos);
                entrance.transform.position = entrancePos;
                Portal ep = entrance.GetComponent<Portal>();
                if (ep != null) ep.targetLocation = arrival;
            }
            Vector2 back = RivalEvent.OpenSpot(room, arrival, new Vector2(0f, -5f), new List<Vector2> { arrival });
            RivalEvent.MakePortal(back, entrancePos + new Vector2(0f, -1.5f), new Color(0.85f, 0.85f, 0.85f));
            // The stage's own enemy kinds, for the waves (the names Enemy.Spawn takes).
            // v0.8.1: only the humanoid enemy kinds (Knight, Rogue, Archer, Mage, Summoner, Lancer and their elemental
            // versions), by clean name: object names carry "(!)", " (3)" and the like, and the stage also holds
            // familiars, zones and other non-fighters that can't be spawned on their own.
            foreach (Enemy e in FindObjectsOfType<Enemy>())
            {
                if (e == null || e is MiniBoss || e is Boss || Rivals.IsRival(e) || OwnerContext.OwnerOf(e) != null) continue;
                if (!ChaosEnemies.IsChaosType(e)) continue;
                foreach (string k in new[] { CleanName(e.gameObject.name), e.GetType().Name })
                    if (k.Length > 0 && !enemyKeys.Contains(k)) enemyKeys.Add(k);
            }
            foreach (string k in Fallback) if (!enemyKeys.Contains(k)) enemyKeys.Add(k);
            Plugin.Log.LogInfo("TRIAL enemies: " + string.Join(", ", enemyKeys.ToArray()));
        }

        bool PlayerInside()
        {
            Player p = GameController.playerScripts != null && GameController.playerScripts.Length > 0 ? GameController.playerScripts[0] : null;
            return p != null && !p.IsDead && Vector2.Distance(p.transform.position, arrival) < 30f;
        }

        void Update()
        {
            bool inside = room != null && PlayerInside();
            // The forced element only while the player is in the trial room (leaving early ends it).
            if (started && !finished && !inside) EndTrial(false);
            if (!started && inside) { started = true; StartCoroutine(Run()); }
        }

        IEnumerator Run()
        {
            string name = Infusion.Name(element);
            Infusion.ForcedElement = element;
            IntroUI.Show("Trial of " + name, "Your arcana are " + name + " until it ends  ·  3 waves  ·  the white portal leads back", Infusion.ColorOf(element), null, 3.5f);
            Plugin.Log.LogInfo("TRIAL of " + name + " started");
            yield return new WaitForSeconds(3f);
            int tier = Mathf.Max(0, GameController.tierCount);
            for (int wave = 0; wave < 3 && !finished; wave++)
            {
                if (SpawnWave(2 + wave + tier) == 0)
                {
                    // Nothing could be spawned: end it without a reward instead of "clearing" an empty room.
                    Plugin.Log.LogWarning("TRIAL: no enemy could be spawned, trial cancelled");
                    IntroUI.Show("The trial fizzles out", "No challengers answered  ·  the white portal leads back", new Color(0.8f, 0.8f, 0.8f), null, 3f);
                    EndTrial(false);
                    yield break;
                }
                yield return new WaitForSeconds(1f);
                float guard = Time.time + 120f; // a wave that can't be finished (an enemy stuck out of reach) moves on
                while (!finished && Time.time < guard)
                {
                    alive.RemoveAll(e => e == null || e.health == null || e.health.CurrentHealthValue <= 0);
                    if (alive.Count == 0) break;
                    yield return new WaitForSeconds(0.3f);
                }
                if (finished) yield break;
                if (wave < 2) yield return new WaitForSeconds(1.5f);
            }
            if (!finished) EndTrial(true);
        }

        static readonly string[] Fallback = { "Knight", "Archer", "Rogue", "Mage", "Lancer", "Summoner" };

        // "FrostMage(!)" / "Knight (3)" / "Archer(Clone)" -> "FrostMage" / "Knight" / "Archer".
        static string CleanName(string n)
        {
            if (string.IsNullOrEmpty(n)) return "";
            int cut = n.IndexOf('(');
            if (cut >= 0) n = n.Substring(0, cut);
            return n.Trim();
        }

        // A name that spawned nothing is dropped, and the next one is tried, so a wave is never empty while any kind
        // works. Returns how many spawned.
        int SpawnWave(int count)
        {
            List<Vector2> used = new List<Vector2> { arrival };
            int spawned = 0, attempts = 0;
            while (spawned < count && attempts < count * 4 && enemyKeys.Count > 0)
            {
                attempts++;
                Vector2 off = ArcanaCaster.Rotate(Vector2.right * UnityEngine.Random.Range(4.5f, 7f), UnityEngine.Random.value * 360f);
                Vector2 at = RivalEvent.OpenSpot(room, arrival, off, used);
                string key = enemyKeys[UnityEngine.Random.Range(0, enemyKeys.Count)];
                Enemy e = null;
                try { e = Enemy.Spawn(key, at, null, false, true, false, false, false); } catch (Exception) { e = null; }
                if (e == null) { enemyKeys.Remove(key); Plugin.Log.LogWarning("TRIAL could not spawn " + key + " (dropped from the list)"); continue; }
                used.Add(at);
                alive.Add(e);
                ArcanosBridge.Fx("Teletransporte", at);
                spawned++;
            }
            Plugin.Log.LogInfo("TRIAL wave: " + spawned + " of " + count + " spawned");
            return spawned;
        }

        void EndTrial(bool cleared)
        {
            if (finished) return;
            finished = true;
            Infusion.ForcedElement = null;
            string name = Infusion.Name(element);
            if (!cleared) { Plugin.Log.LogInfo("TRIAL of " + name + " left early"); return; }
            Plugin.Log.LogInfo("TRIAL of " + name + " cleared");
            try { LootManager.DropGold(arrival, 30 + 15 * Mathf.Max(0, GameController.tierCount)); } catch (Exception) { }
            string id = RewardArcana();
            if (id != null)
            {
                Infusion.SetPending(id, element);
                try { LootManager.DropSkill(arrival + new Vector2(0f, 1.5f), 1, id, 0f, 0f, null, false, false); }
                catch (Exception ex) { Plugin.Log.LogWarning("TRIAL reward drop failed: " + ex.Message); }
            }
            IntroUI.Show("Trial of " + name + " cleared", id != null ? "A " + name + "-infused " + IntroUI.SkillName(id) + " is yours" : "Well fought", Infusion.ColorOf(element),
                id != null ? new List<IntroEntry> { new IntroEntry(IconManager.GetSkillIcon(id), IntroUI.SkillName(id)) } : null, 3.5f);
        }

        // An unlocked arcana of another element that you don't own yet (it drops already infused with the trial's).
        string RewardArcana()
        {
            List<string> ids = new List<string>();
            string own = Infusion.Prefix(element);
            foreach (string p in new[] { "Flame", "Frost", "Earth", "Wind", "Thunder" })
            {
                if (p == own) continue;
                foreach (ArcanaDef d in ArcanaCatalog.Pool(p)) if (d.Port == ArcanosPort.None && !d.Basic && !ids.Contains(d.SkillID)) ids.Add(d.SkillID);
            }
            ids.RemoveAll(i => !ArcanosBridge.Unlocked(i));
            List<string> owned = HarmonyLib.Traverse.Create(typeof(LootManager)).Field("currentPlayerSkills").GetValue() as List<string>;
            if (owned != null) { List<string> notOwned = ids.FindAll(i => !owned.Contains(i)); if (notOwned.Count > 0) ids = notOwned; }
            return ids.Count > 0 ? ids[UnityEngine.Random.Range(0, ids.Count)] : null;
        }

        void Close()
        {
            Infusion.ForcedElement = null;
            if (entrance != null) Destroy(entrance);
            if (room != null) Destroy(room.gameObject);
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
            if (started && !finished) Infusion.ForcedElement = null;
        }
    }
}
