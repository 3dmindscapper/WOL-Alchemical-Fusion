using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace WolHybridBosses
{
    // v0.9.10 / v0.9.11: special mini-bosses made from regular enemies, in the mini-boss pool. When the game spawns a
    // giant (the mini-boss at the end of a section's first two levels), it sometimes (config
    // BossFights.MinibossChance) becomes one of these instead, in the same room with the same escort:
    //   Giant Cyclops      the Cyclops as a giant: thick beam, armour that breaks into stun phases (ElementalNeutrals.cs).
    //   Mimic King         a Large Mimic disguised as a chest. Armoured; each armour break spills gold and two Mimics.
    //   Cyclops Eye Court  the Giant Cyclops with four Laser Orbs circling it; each armour break destroys one orb.
    //   Elemental Conclave the five Zoners one after another, each stronger; the fifth, the Elemental Archon, is
    //                      armoured and its attacks cycle through the elements of the four it absorbed.
    // The enemy the game gets back from its spawn is the one that has to die last (the Conclave's Archon waits
    // hidden while the other four fight), so the room's mini-boss logic still works. Each drops the mini-boss chest.
    // Ctrl+F5 spawns the next one beside you.
    internal enum MinibossFight { GiantCyclops, MimicKing, EyeCourt, Conclave, HiveMother, Gravewarden }

    internal static class MinibossFights
    {
        static readonly string[] Zoners = { "FlameZoneEnemy", "FrostZoneEnemy", "EarthZoneEnemy", "WindZoneEnemy", "ThunderZoneEnemy" };
        static string conclaveKey;
        internal const int Count = 6;

        internal static string Title(MinibossFight f)
        {
            switch (f)
            {
                case MinibossFight.GiantCyclops: return "Giant Cyclops";
                case MinibossFight.MimicKing: return "The Mimic King";
                case MinibossFight.EyeCourt: return "The Cyclops Eye Court";
                case MinibossFight.HiveMother: return "The Hive Mother";
                case MinibossFight.Gravewarden: return "The Gravewarden";
                default: return "The Elemental Conclave";
            }
        }

        // The enemy name the game spawns instead of the giant.
        internal static string KeyFor(MinibossFight f)
        {
            switch (f)
            {
                case MinibossFight.MimicKing: return "MimicEnemyLarge";
                case MinibossFight.HiveMother: return "SuperBlob";
                case MinibossFight.Gravewarden: return "GhoulLarge";
                case MinibossFight.Conclave: conclaveKey = Zoners[UnityEngine.Random.Range(0, Zoners.Length)]; return conclaveKey; // the Archon
                default: return "Cyclops";
            }
        }

        internal static void Begin(Enemy e, MinibossFight f)
        {
            if (e == null) return;
            Plugin.Log.LogInfo("MINIBOSS " + Title(f) + " (" + e.GetType().Name + ")");
            if (f == MinibossFight.GiantCyclops) { GiantCyclops.Mark(e); return; }
            e.gameObject.AddComponent<BossEventEnemy>();
            if (f == MinibossFight.EyeCourt) GiantCyclops.Mark(e);
            if (f == MinibossFight.Conclave) e.gameObject.SetActive(false); // the Archon waits for the other four
            GameObject host = new GameObject("HybridBosses_Miniboss_" + f);
            host.transform.position = e.transform.position;
            MinibossRunner r = host.AddComponent<MinibossRunner>();
            r.Fight = f;
            r.Boss = e;
            r.ArchonKey = conclaveKey;
        }

        // Ctrl+F5: the next special mini-boss beside you.
        static int testIndex;
        internal static void TestHere()
        {
            Player p = Elites.Player0;
            if (p == null) return;
            MinibossFight f = (MinibossFight)(testIndex++ % Count);
            Vector3 at = p.transform.position + new Vector3(5f, 0f, 0f);
            if (f == MinibossFight.GiantCyclops) { GiantCyclops.SpawnAt(at); return; }
            MinibossSwap.Pending = f;
            try { Enemy.Spawn(KeyFor(f), at, null, false, true, false, false, false); }
            finally { MinibossSwap.Pending = null; }
        }

        internal static Enemy Spawn(string key, Vector2 at)
        {
            try { at = Entity.GetAvailableNearbyLocation(at, false, false); } catch (Exception) { }
            Enemy e = null;
            GiantCyclops.Pending = true; // never swapped itself
            try { e = Enemy.Spawn(key, at, null, false, true, false, false, false); } catch (Exception) { e = null; }
            finally { GiantCyclops.Pending = false; }
            if (e == null) { Plugin.Log.LogWarning("MINIBOSS could not spawn " + key); return null; }
            e.gameObject.AddComponent<BossEventEnemy>();
            ArcanosBridge.Fx("Teletransporte", at);
            return e;
        }

        // Eye Court: each armour break of its Giant Cyclops destroys one orb.
        internal static void OnCyclopsBreak(Enemy giant)
        {
            OrbitCourt court = giant != null ? giant.GetComponent<OrbitCourt>() : null;
            if (court == null) return;
            court.Orbs.RemoveAll(o => o == null || o.health == null || o.health.CurrentHealthValue <= 0);
            if (court.Orbs.Count == 0) return;
            Enemy orb = court.Orbs[0];
            court.Orbs.RemoveAt(0);
            try { GameObject fx = Explosion.Prefab; if (fx != null) { Explosion ex = Globals.ChaosInst<Explosion>(fx, null, orb.transform.position, null); if (ex != null) ex.Explode(1f); } } catch (Exception) { }
            BossKit.Remove(orb);
            IntroUI.Show("An eye of the court goes dark", court.Orbs.Count + " left", new Color(0.75f, 0.85f, 1f), null, 2f);
        }

        // v0.9.13: a special mini-boss (or its escort) matches the stage, like the giants.
        //   Chaos / Radiant / Prism stage: the boss becomes that variant's giant (look, 2 variant arcana, its attacks
        //     converted); a Chaos-type escort (Coffin Knight) is armed like the stage's humanoids; other escorts take
        //     the variant's look and attacks.
        //   Elemental stage: the stage's element (recoloured body, elemental attacks; arcana for the boss).
        //   Neutral stage: the boss rolls a random element (giant element chance); escorts follow the boss.
        // Returns the prefix used ("Flame", "Chaos", ...), or null when it stays neutral.
        internal static string MatchStage(Enemy e, bool boss, int arcana, string followPrefix = null)
        {
            if (e == null) return null;
            if (ChaosStages.Planned)
            {
                StageVariant v = ChaosStages.ActiveVariant != StageVariant.None ? ChaosStages.ActiveVariant : ChaosStages.PlannedVariant;
                if (v == StageVariant.None) v = StageVariant.Chaos;
                if (boss)
                {
                    if (e.GetComponent<ChaosArmed>() == null) ChaosGiants.Apply(e);
                    GiantIntro gi = e.GetComponent<GiantIntro>();
                    if (gi != null) UnityEngine.Object.Destroy(gi); // the fight shows its own card
                }
                else if (arcana > 0 && ChaosEnemies.IsChaosType(e)) ChaosEnemies.ArmRegular(e);
                else
                {
                    VariantMark.Set(e, v);
                    if (v == StageVariant.Chaos) { e.element = ElementType.Chaos; ElementalGiants.RecolorOrTint(e, "Chaos", ChaosRun.Color); }
                    else Variants.TintBody(e, v);
                    ElementalNeutrals.ConvertExisting(e, ElementType.Neutral, v); // v0.9.14: hitboxes set before this
                }
                return Variants.Info(v).Prefix;
            }
            string prefix = followPrefix ?? ElementalGiants.StagePrefix();
            if (prefix == null && boss && UnityEngine.Random.value < Plugin.ElementalGiantChance.Value) prefix = ElementalGiants.RandomPrefix();
            if (prefix == null) return null;
            ElementalNeutrals.Apply(e, prefix, arcana, boss ? 1f : 1.6f);
            return prefix;
        }

        // "Mini-boss  ·  Fire  ·  ..." for the intro card.
        internal static string Sub(string prefix, string text)
        {
            return "Mini-boss  ·  " + (prefix != null ? ElementalGiants.ElementColorName(prefix) + "  ·  " : "") + text;
        }

        internal static Color TitleColour(string prefix, Color fallback) { return prefix != null ? ElementalGiants.ElementColor(prefix) : fallback; }

        internal static string ZonerName(string key)
        {
            if (key.StartsWith("Flame")) return "Fire Zoner";
            if (key.StartsWith("Frost")) return "Frost Zoner";
            if (key.StartsWith("Earth")) return "Earth Zoner";
            if (key.StartsWith("Wind")) return "Wind Zoner";
            return "Thunder Zoner";
        }

        internal static ElementType ZonerElement(string key)
        {
            if (key.StartsWith("Flame")) return ElementType.Fire;
            if (key.StartsWith("Frost")) return ElementType.Water;
            if (key.StartsWith("Earth")) return ElementType.Earth;
            if (key.StartsWith("Wind")) return ElementType.Air;
            return ElementType.Lightning;
        }

        internal static List<string> OtherZoners(string archon)
        {
            List<string> l = new List<string>(Zoners);
            l.Remove(archon);
            for (int i = l.Count - 1; i > 0; i--) { int j = UnityEngine.Random.Range(0, i + 1); string t = l[i]; l[i] = l[j]; l[j] = t; }
            return l;
        }
    }

    // Runs one special mini-boss fight in the room where it spawned.
    internal class MinibossRunner : MonoBehaviour
    {
        public MinibossFight Fight;
        public Enemy Boss;
        public string ArchonKey;

        IEnumerator Start()
        {
            switch (Fight)
            {
                case MinibossFight.MimicKing: yield return StartCoroutine(MimicKing()); break;
                case MinibossFight.EyeCourt: yield return StartCoroutine(EyeCourt()); break;
                case MinibossFight.Conclave: yield return StartCoroutine(Conclave()); break;
                case MinibossFight.HiveMother: yield return StartCoroutine(HiveMother()); break;
                case MinibossFight.Gravewarden: yield return StartCoroutine(Gravewarden()); break;
            }
            Destroy(gameObject, 1f);
        }

        int Tier { get { return Mathf.Max(0, GameController.tierCount); } }

        IEnumerator MimicKing()
        {
            yield return null;
            yield return null;
            if (Boss == null) yield break;
            Boss.transform.localScale = Boss.transform.localScale * 1.4f;
            Color gold = new Color(1f, 0.82f, 0.3f);
            string pre = MinibossFights.MatchStage(Boss, true, 2); // v0.9.13
            BossKit.Setup(Boss, Mathf.RoundToInt((1100 + 400 * Tier) * Loop), MinibossFights.Title(MinibossFight.MimicKing), MinibossFights.Sub(pre, "it hides among the treasure"), MinibossFights.TitleColour(pre, gold));
            Enemy king = Boss;
            int tier = Tier;
            BossArmour.Attach(king, 0.2f, 3.5f, n =>
            {
                if (king == null) return;
                Vector2 p = king.transform.position;
                for (int i = 0; i < 3; i++) try { LootManager.DropGold(p + UnityEngine.Random.insideUnitCircle * 1.5f, 10 + 5 * tier); } catch (Exception) { }
                for (int i = 0; i < 2; i++) Follow(MinibossFights.Spawn("MimicEnemy", p + ArcanaCaster.Rotate(Vector2.right * 2.5f, UnityEngine.Random.value * 360f)), pre);
                IntroUI.Show("The Mimic King spills its hoard", "Gold, and more mouths", gold, null, 2f);
            });
            yield return StartCoroutine(UntilDead(king));
            Gold(king, 1.6f);
        }

        IEnumerator EyeCourt()
        {
            yield return null;
            yield return null;
            yield return null; // the giant's own setup (HP, element, armour)
            if (Boss == null) yield break;
            List<Enemy> orbs = new List<Enemy>();
            for (int i = 0; i < 4; i++)
            {
                Enemy o = MinibossFights.Spawn("LaserOrb", (Vector2)Boss.transform.position + ArcanaCaster.Rotate(Vector2.right * 4.5f, i * 90f));
                if (o != null) { orbs.Add(o); try { Traverse.Create(o).Field("allowRoam").SetValue(false); } catch (Exception) { } }
            }
            Boss.gameObject.AddComponent<OrbitCourt>().Orbs = orbs;
            // The Giant Cyclops already took the stage's element / variant itself; its orbs follow it (v0.9.13).
            ElementalNeutral en = Boss.GetComponent<ElementalNeutral>();
            string pre = en != null ? en.Prefix : ChaosStages.Planned ? Variants.Info(ChaosStages.PlannedVariant).Prefix : null;
            foreach (Enemy o in orbs) Follow(o, pre);
            BossKit.Setup(Boss, -1, MinibossFights.Title(MinibossFight.EyeCourt), MinibossFights.Sub(pre, "break its armour to break its court"), MinibossFights.TitleColour(pre, new Color(0.75f, 0.85f, 1f)));
            Enemy giant = Boss;
            yield return StartCoroutine(UntilDead(giant));
            foreach (Enemy o in orbs) BossKit.Remove(o);
            Gold(giant, 1.2f);
        }

        IEnumerator Conclave()
        {
            Enemy archon = Boss;
            Vector2 at = transform.position;
            Color c = new Color(0.85f, 0.7f, 1f);
            List<ElementType> absorbed = new List<ElementType>();
            yield return new WaitForSeconds(1f);
            IntroUI.Show(MinibossFights.Title(MinibossFight.Conclave), "Mini-boss  ·  five Zoners, one after another  ·  the last keeps what the others had", c, null, 3f);
            yield return new WaitForSeconds(1.5f);
            List<string> order = MinibossFights.OtherZoners(ArchonKey ?? "FlameZoneEnemy");
            for (int i = 0; i < order.Count; i++)
            {
                Enemy z = MinibossFights.Spawn(order[i], at + ArcanaCaster.Rotate(Vector2.right * 2f, UnityEngine.Random.value * 360f));
                if (z == null) continue;
                yield return null;
                yield return null;
                z.transform.localScale = z.transform.localScale * (1.2f + 0.08f * i);
                BossKit.Setup(z, Mathf.RoundToInt((260 + 100 * Tier) * (1f + 0.25f * i) * Loop), MinibossFights.ZonerName(order[i]) + " of the Conclave", (i + 1) + " of 5", Infusion.ColorOf(MinibossFights.ZonerElement(order[i])), false);
                if (i > 0) try { z.movement.moveSpeedStat.AddMod(new NumVarStatMod("HB_Conclave", 1f + 0.1f * i, 10, VarStatModType.Multiplicative, false)); } catch (Exception) { }
                yield return StartCoroutine(UntilDead(z));
                absorbed.Add(MinibossFights.ZonerElement(order[i]));
                yield return new WaitForSeconds(1.2f);
            }
            if (archon == null) yield break;
            absorbed.Add(MinibossFights.ZonerElement(ArchonKey ?? "FlameZoneEnemy"));
            archon.transform.position = at;
            archon.gameObject.SetActive(true);
            ArcanosBridge.Fx("Teletransporte", at);
            yield return null;
            yield return null;
            if (archon == null) yield break;
            archon.transform.localScale = archon.transform.localScale * 1.7f;
            // v0.9.13: on a Chaos / Radiant / Prism stage the Archon is also that variant's giant (Chaos joins its cycle).
            string variant = null;
            if (ChaosStages.Planned)
            {
                variant = MinibossFights.MatchStage(archon, true, 2);
                if (variant == "Chaos") absorbed.Add(ElementType.Chaos);
            }
            archon.gameObject.AddComponent<ConclaveCore>().Elements = absorbed;
            BossKit.Setup(archon, Mathf.RoundToInt((900 + 300 * Tier) * Loop), "The Elemental Archon", MinibossFights.Sub(variant, "every element it absorbed"), MinibossFights.TitleColour(variant, c));
            BossArmour.Attach(archon, 0.2f, 3.5f, null);
            yield return StartCoroutine(UntilDead(archon));
            Gold(archon, 1.4f);
        }

        // v0.9.12 Hive Mother: an armoured Super Blob. Each armour break bursts out four smaller blobs (spitters,
        // rollers, minis); any still alive after 6s crawl back and merge into her, healing 8% of her health each.
        IEnumerator HiveMother()
        {
            yield return null;
            yield return null;
            if (Boss == null) yield break;
            Enemy mother = Boss;
            mother.transform.localScale = mother.transform.localScale * 1.3f;
            Color green = new Color(0.55f, 0.95f, 0.45f);
            string pre = MinibossFights.MatchStage(mother, true, 2); // v0.9.13
            BossKit.Setup(mother, Mathf.RoundToInt((1000 + 380 * Tier) * Loop), MinibossFights.Title(MinibossFight.HiveMother), MinibossFights.Sub(pre, "kill her brood before it crawls back"), MinibossFights.TitleColour(pre, green));
            string[] brood = { "BlobSpitter", "BlobRoller", "BlobMini", "BlobSpitter" };
            BossArmour.Attach(mother, 0.2f, 3.5f, n =>
            {
                if (mother == null) return;
                for (int i = 0; i < brood.Length; i++)
                {
                    Enemy b = MinibossFights.Spawn(brood[i], (Vector2)mother.transform.position + ArcanaCaster.Rotate(Vector2.right * 2.5f, i * 90f + 45f));
                    if (b == null) continue;
                    Follow(b, pre);
                    BroodBlob bb = b.gameObject.AddComponent<BroodBlob>();
                    bb.Mother = mother;
                    bb.MergeAt = Time.time + 6f;
                }
                IntroUI.Show("The Hive Mother splits", "Kill the brood before it merges back", green, null, 2f);
            });
            yield return StartCoroutine(UntilDead(mother));
            foreach (BroodBlob bb in FindObjectsOfType<BroodBlob>()) if (bb != null && bb.Mother == mother) bb.Orphan();
            Gold(mother, 1.3f);
        }

        // v0.9.12 Gravewarden: a Macho Ghoul that keeps raising elemental Coffin Knights (up to 3 standing, one
        // every 9s) with two Ghouls at its side. Its armour can't break while any of its knights stands.
        IEnumerator Gravewarden()
        {
            yield return null;
            yield return null;
            if (Boss == null) yield break;
            Enemy warden = Boss;
            warden.transform.localScale = warden.transform.localScale * 1.35f;
            Color bone = new Color(0.8f, 0.75f, 0.95f);
            string pre = MinibossFights.MatchStage(warden, true, 2); // v0.9.13
            BossKit.Setup(warden, Mathf.RoundToInt((1100 + 400 * Tier) * Loop), MinibossFights.Title(MinibossFight.Gravewarden), MinibossFights.Sub(pre, "its armour holds while its knights stand"), MinibossFights.TitleColour(pre, bone));
            List<Enemy> knights = new List<Enemy>();
            string prefix = pre ?? ElementalGiants.RandomPrefix(); // its knights: its element (always elemental)
            bool told = false;
            BossArmour armour = BossArmour.Attach(warden, 0.2f, 3.5f, null);
            armour.CanBreak = () =>
            {
                knights.RemoveAll(k => k == null || k.health == null || k.health.CurrentHealthValue <= 0);
                if (knights.Count == 0) return true;
                if (!told) { told = true; IntroUI.Show("The Gravewarden's knights shield it", "Bring them down to break its armour", bone, null, 2f); }
                return false;
            };
            for (int i = 0; i < 2; i++) Follow(MinibossFights.Spawn("Ghoul", (Vector2)warden.transform.position + ArcanaCaster.Rotate(Vector2.right * 2.5f, i * 180f)), pre);
            float nextRaise = Time.time + 2f;
            while (warden != null && warden.health != null && warden.health.CurrentHealthValue > 0)
            {
                knights.RemoveAll(k => k == null || k.health == null || k.health.CurrentHealthValue <= 0);
                if (Time.time >= nextRaise && knights.Count < 3)
                {
                    nextRaise = Time.time + 9f;
                    Vector2 at = (Vector2)warden.transform.position + ArcanaCaster.Rotate(Vector2.right * 3f, UnityEngine.Random.value * 360f);
                    Enemy k = MinibossFights.Spawn("Coffin", at);
                    if (k != null)
                    {
                        knights.Add(k);
                        told = false;
                        StartCoroutine(Elementalize(k, prefix));
                    }
                }
                yield return new WaitForSeconds(0.25f);
            }
            foreach (Enemy k in knights) if (k != null && k.health != null && k.health.CurrentHealthValue > 0) BossKit.Remove(k);
            Gold(warden, 1.4f);
        }

        static IEnumerator Elementalize(Enemy e, string prefix)
        {
            yield return null;
            yield return null;
            if (e != null) MinibossFights.MatchStage(e, false, 1, prefix); // variant stages: armed like the stage's humanoids
        }

        // An escort takes the boss's element / the stage's variant (two frames in, once its own Start has run).
        void Follow(Enemy e, string prefix)
        {
            if (e == null || (prefix == null && !ChaosStages.Planned)) return;
            StartCoroutine(FollowLater(e, prefix));
        }

        static IEnumerator FollowLater(Enemy e, string prefix)
        {
            yield return null;
            yield return null;
            if (e != null) MinibossFights.MatchStage(e, false, 0, ChaosStages.Planned ? null : prefix);
        }

        static float Loop { get { return GameController.loopCount >= 1 ? 1.5f : 1f; } }

        static IEnumerator UntilDead(Enemy e)
        {
            float guard = Time.time + 900f;
            while (e != null && e.health != null && e.health.CurrentHealthValue > 0 && Time.time < guard) yield return new WaitForSeconds(0.25f);
        }

        void Gold(Enemy e, float mult)
        {
            Vector2 at = e != null ? (Vector2)e.transform.position : (Vector2)transform.position;
            try { LootManager.DropGold(at, Mathf.RoundToInt((30 + 15 * Tier) * mult)); } catch (Exception) { }
            Plugin.Log.LogInfo("MINIBOSS " + MinibossFights.Title(Fight) + " defeated");
        }
    }

    internal class BossEventEnemy : MonoBehaviour { } // part of a special mini-boss fight: never an elite, never re-elemented

    internal static class BossKit
    {
        // Boss health (hp < 0 keeps its own), the mini-boss chest (chest = false for the Conclave's first four),
        // the intro card and the boss bar.
        internal static void Setup(Enemy e, int hp, string title, string subtitle, Color colour, bool chest = true)
        {
            if (e == null || e.health == null) return;
            if (hp > 0)
            {
                try { e.health.healthStat.AddMod(new NumVarStatMod("HB_BossHP", hp, 10, VarStatModType.Override, true)); } catch (Exception) { }
                try { e.health.CurrentHealthValue = hp; } catch (Exception) { }
            }
            if (chest)
            {
                e.dropLoot = true;
                e.dropLootChest = true;
                e.chestLootTableID = "TreasureChestMiniBoss";
            }
            IntroUI.Show(title, subtitle, colour, null, 3f);
            try { BossBar.ShowFor(e, title); } catch (Exception) { }
            Plugin.Log.LogInfo("MINIBOSS " + title + " HP " + e.health.CurrentHealthValue);
        }

        // Takes an enemy out of the fight without a death (its loot) or a hole in the enemy list.
        internal static void Remove(Enemy e)
        {
            if (e == null) return;
            try { GameController.enemies.Remove(e.gameObject); } catch (Exception) { }
            ArcanosBridge.Fx("Teletransporte", e.transform.position);
            UnityEngine.Object.Destroy(e.gameObject);
        }
    }

    // Giant-style armour for any enemy: no hurt stagger or knockback while armoured; after it has lost Threshold of
    // its health since the armour went up, the armour breaks (OnBreak runs) and it's stunned for StunSeconds: held in
    // its Hurt state when it has one, slowed to a crawl when it doesn't; then the armour comes back.
    internal class BossArmour : MonoBehaviour
    {
        public float Threshold = 0.2f, StunSeconds = 3.5f;
        public Action<int> OnBreak;
        public Func<bool> CanBreak; // v0.9.12: the Gravewarden's armour holds while its knights stand
        Enemy enemy;
        int maxHp, hpAtArmour, breaks;
        bool armoured, started;
        float stunUntil, nextHold;
        BoolVarStatMod mod;
        NumVarStatMod slow;
        static readonly Color ArmourColour = new Color(0.75f, 0.85f, 1f);

        internal static BossArmour Attach(Enemy e, float threshold, float stun, Action<int> onBreak)
        {
            BossArmour a = e.gameObject.AddComponent<BossArmour>();
            a.Threshold = threshold; a.StunSeconds = stun; a.OnBreak = onBreak;
            return a;
        }

        IEnumerator Start()
        {
            enemy = GetComponent<Enemy>();
            mod = new BoolVarStatMod("HB_BossArmour" + GetInstanceID(), true, 10);
            slow = new NumVarStatMod("HB_BossStun" + GetInstanceID(), 0.15f, 10, VarStatModType.Multiplicative, false);
            yield return null;
            if (enemy == null || enemy.health == null) yield break;
            maxHp = Mathf.Max(1, enemy.health.CurrentHealthValue);
            outline = new OutlineCache(this);
            original = outline.Current ?? Color.black; // v0.9.15: its element outline, shown again while stunned
            SetArmour(true);
            started = true;
        }

        OutlineCache outline;
        Color original;
        internal bool Armoured { get { return armoured; } }

        void SetArmour(bool on)
        {
            armoured = on;
            try { enemy.health.ignoreHurtVarStat.Modify(mod, on); } catch (Exception) { }
            try { enemy.health.ignoreKnockbackVarStat.Modify(mod, on); } catch (Exception) { }
            try { if (!enemy.fsm.HasState("Hurt")) enemy.movement.moveSpeedStat.Modify(slow, !on); } catch (Exception) { } // no hurt state: slowed while stunned
            if (on) hpAtArmour = enemy.health.CurrentHealthValue;
            if (outline != null) outline.Set(on ? ArmourColour : original);
        }

        void Update()
        {
            if (!started || enemy == null || enemy.health == null || enemy.health.CurrentHealthValue <= 0) return;
            if (armoured)
            {
                if (hpAtArmour - enemy.health.CurrentHealthValue >= maxHp * Threshold && (CanBreak == null || CanBreak())) Break();
                return;
            }
            if (Time.time >= stunUntil) { SetArmour(true); return; }
            if (Time.time >= nextHold)
            {
                nextHold = Time.time + 0.25f;
                try
                {
                    string st = enemy.fsm.currentStateName ?? "";
                    if (!st.Contains("Hurt") && !st.Contains("Dead") && !st.Contains("Frozen") && !st.StartsWith("WolArcanos") && enemy.fsm.HasState("Hurt"))
                        enemy.fsm.ChangeState("Hurt", true);
                }
                catch (Exception) { }
            }
        }

        void Break()
        {
            breaks++;
            SetArmour(false);
            stunUntil = Time.time + StunSeconds;
            nextHold = 0f;
            try { CameraController.ShakeCamera(0.8f, false); } catch (Exception) { }
            try { ParticleEffect fx = PoolManager.GetPoolItem<ParticleEffect>("ArmorDissipateEffect"); if (fx != null) fx.Emit(15, (Vector3?)transform.position, null, null, 0f, null, null); } catch (Exception) { }
            Plugin.Log.LogInfo("MINIBOSS " + enemy.GetType().Name + " armour broken (" + breaks + ")");
            if (OnBreak != null) try { OnBreak(breaks); } catch (Exception ex) { Plugin.Log.LogWarning("MINIBOSS break effect failed: " + ex.Message); }
        }
    }

    // Hive Mother's brood: after MergeAt, a surviving blob crawls back to her and merges (heals her 8%).
    internal class BroodBlob : MonoBehaviour
    {
        public Enemy Mother;
        public float MergeAt;
        Enemy self;
        bool orphan;

        internal void Orphan() { orphan = true; }

        void Start() { self = GetComponent<Enemy>(); }

        void Update()
        {
            if (orphan || self == null || Mother == null || Time.time < MergeAt) return;
            if (self.health == null || self.health.CurrentHealthValue <= 0 || Mother.health == null || Mother.health.CurrentHealthValue <= 0) return;
            Vector2 to = (Vector2)Mother.transform.position - (Vector2)transform.position;
            if (to.magnitude < 1.4f)
            {
                int heal = Mathf.RoundToInt(Mother.health.healthStat.CurrentValue * 0.08f);
                try { Mother.health.RestoreHealth(Mathf.Max(1, heal), false, false, false, false); } catch (Exception) { }
                Plugin.Log.LogInfo("MINIBOSS Hive Mother absorbed a blob (+" + heal + ")");
                BossKit.Remove(self);
                return;
            }
            transform.position += (Vector3)(to.normalized * 3f * Time.deltaTime);
        }
    }

    // Eye Court: the Laser Orbs circle the Giant Cyclops.
    internal class OrbitCourt : MonoBehaviour
    {
        public List<Enemy> Orbs = new List<Enemy>();
        float angle;

        void Update()
        {
            Orbs.RemoveAll(o => o == null || o.health == null || o.health.CurrentHealthValue <= 0);
            if (Orbs.Count == 0) return;
            angle += 40f * Time.deltaTime;
            for (int i = 0; i < Orbs.Count; i++)
            {
                Vector2 to = (Vector2)transform.position + ArcanaCaster.Rotate(Vector2.right * 4.5f, angle + i * 360f / Orbs.Count);
                Vector3 now = Orbs[i].transform.position;
                Orbs[i].transform.position = Vector3.Lerp(now, new Vector3(to.x, to.y, now.z), 4f * Time.deltaTime);
            }
        }
    }

    // Conclave: the Elemental Archon's attacks cycle through the elements of the Zoners it absorbed.
    internal class ConclaveCore : MonoBehaviour
    {
        public List<ElementType> Elements = new List<ElementType>();
        int next;

        internal static bool ConvertAttack(Attack atk)
        {
            if (atk == null || atk.atkInfo == null) return false;
            Entity owner = atk.atkInfo.entity;
            if (owner == null) owner = atk.GetComponentInParent<Entity>();
            ConclaveCore core = owner != null ? owner.GetComponent<ConclaveCore>() : null;
            if (core == null || core.Elements.Count == 0) return false;
            ElementType el = core.Elements[core.next++ % core.Elements.Count];
            try
            {
                Infusion.ConvertEnemyInfo(atk.atkInfo, el);
                string key = Infusion.RampKey(el);
                if (key != null) CouncilCorruption.TintAttack(atk, true, StageVariant.Chaos, null, key);
            }
            catch (Exception) { }
            return true;
        }

        OutlineCache outline; // v0.9.15: cached materials (this runs every frame)
        BossArmour armour;

        void Update()
        {
            if (Elements.Count == 0) return;
            // v0.9.15: while its armour is up the pale armour outline shows (the shimmer used to hide it); the
            // shimmer runs while it's stunned.
            if (armour == null) armour = GetComponent<BossArmour>();
            if (armour != null && armour.Armoured) return;
            float t = Time.time * 0.8f;
            Color a = Infusion.ColorOf(Elements[Mathf.FloorToInt(t) % Elements.Count]), b = Infusion.ColorOf(Elements[(Mathf.FloorToInt(t) + 1) % Elements.Count]);
            if (outline == null) outline = new OutlineCache(this);
            outline.Set(Color.Lerp(a, b, t - Mathf.Floor(t)));
        }
    }
}
