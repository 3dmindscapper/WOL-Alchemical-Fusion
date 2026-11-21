using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace WolHybridBosses
{
    internal enum RivalTier { Easy, Medium, Hard, Final } // Final = final-stage champion

    // Rival-wizard events: the misc-room portal sometimes leads to an arena with three portals (Easy/Medium/Hard).
    // Picking one teleports you into the arena, the camera shows the rival and their arcana for ~4 seconds, then the fight starts.
    // The rival is the game's Contestant wizard (an ally by default), turned hostile and given player arcana.
    internal static class Rivals
    {
        static readonly HashSet<int> rivalIds = new HashSet<int>();
        internal static Enemy CurrentCaster; // set while a rival's Update runs (its own states call SetAttackInfo("Player", ...))

        internal static bool IsRival(Enemy e) { return e != null && rivalIds.Contains(e.GetInstanceID()); }

        internal static string TierName(RivalTier t) { return t.ToString(); }

        internal static Color TierColor(RivalTier t)
        {
            switch (t)
            {
                case RivalTier.Easy: return new Color(0.3f, 1f, 0.4f);
                case RivalTier.Medium: return new Color(1f, 0.8f, 0.2f);
                case RivalTier.Hard: return new Color(1f, 0.2f, 0.2f);
                default: return new Color(0.85f, 0.55f, 1f);
            }
        }

        internal static float TierHP(RivalTier t)
        {
            int diff = GameController.CurrentDifficulty;
            switch (t)
            {
                case RivalTier.Easy: return 500f + 50f * diff;
                case RivalTier.Medium: return 900f + 90f * diff;
                case RivalTier.Hard: return 1400f + 125f * diff;
                default: return 2600f + 200f * diff;
            }
        }

        static T ByTier<T>(RivalTier t, T easy, T medium, T hard, T final)
        {
            switch (t) { case RivalTier.Easy: return easy; case RivalTier.Medium: return medium; case RivalTier.Hard: return hard; default: return final; }
        }

        internal static int TierArcanaCount(RivalTier t) { return ByTier(t, 2, 3, 4, 5); }
        internal static float TierCooldown(RivalTier t) { return ByTier(t, 1.6f, 1.2f, 0.9f, 0.85f); }
        // Minimum gap between any two spells (the rival's own and borrowed arcana share it).
        internal static float TierCastGap(RivalTier t) { return ByTier(t, 3f, 2f, 1.3f, 1.1f); }
        internal static float TierDamage(RivalTier t) { return ByTier(t, 0.5f, 0.75f, 1f, 1f); }
        internal static float TierWindup(RivalTier t) { return ByTier(t, 1.5f, 1.2f, 1f, 1f); }
        internal static int TierSignatureHits(RivalTier t) { return ByTier(t, 12, 9, 6, 6); }
        // ---------- spell pacing ----------
        static readonly Dictionary<int, float> nextCastTime = new Dictionary<int, float>();

        internal static bool CanCast(Enemy e)
        {
            float t;
            return !nextCastTime.TryGetValue(e.GetInstanceID(), out t) || Time.time >= t;
        }

        internal static void MarkCast(Enemy e)
        {
            RivalMarker mk = e.GetComponent<RivalMarker>();
            float gap = mk != null ? TierCastGap(mk.Tier) : Plugin.ArcanaCastGap.Value;
            nextCastTime[e.GetInstanceID()] = Time.time + gap;
        }

        internal static void DelayCasts(Enemy e, float seconds) { nextCastTime[e.GetInstanceID()] = Time.time + seconds; }

        internal static float DamageFor(Enemy e)
        {
            RivalMarker mk = e.GetComponent<RivalMarker>();
            return mk != null ? TierDamage(mk.Tier) * mk.Bonus.Damage : 1f;
        }

        internal static void CritFor(Enemy e, out float chance, out float damage)
        {
            RivalMarker mk = e.GetComponent<RivalMarker>();
            chance = mk != null ? mk.Bonus.Crit : 0f;
            damage = mk != null ? mk.Bonus.CritDamage : 0f;
        }

        // The Contestant always has these three (its own states), shown in the intro next to the borrowed arcana.
        internal static readonly string[] SignatureArcana = { "ShootFireball", "SwordThrow", "SummonTornado" };
        internal const string BlizzardSkill = "FrostMageSummonBlizzard"; // Contestant's extra skill; player icon "SummonBlizzard"

        // After any extra skill fires: start the shared gap and reshuffle priorities, so the rival rotates
        // through its whole kit instead of always picking the same highest-priority skill.
        internal static void OnSkillUsed(Enemy e, Enemy.SkillState used)
        {
            MarkCast(e);
            if (e.availableEnemySkillsDict == null) return;
            foreach (Enemy.SkillState s in e.availableEnemySkillsDict.Values)
                s.priorityLevel = s == used ? 0 : UnityEngine.Random.Range(1, 10);
        }

        // Spawns a rival. Hostility is applied in Contestant.Start's postfix (Start undoes it otherwise).
        internal static Enemy Spawn(Vector2 pos, string prefix, RivalTier tier)
        {
            Enemy e = Enemy.Spawn("Contestant", pos, null, true, false, false, false, false);
            if (e == null) { Plugin.Log.LogWarning("RIVAL spawn failed (no Contestant prefab)"); return null; }
            ElementType el;
            if (ElementalGiants.TryGetElement(e, prefix, out el)) e.element = el; // InitElement (in Start) picks the palette from this
            RivalMarker mk = e.gameObject.AddComponent<RivalMarker>();
            mk.Prefix = prefix;
            mk.Tier = tier;
            rivalIds.Add(e.GetInstanceID());
            return e;
        }

        // Undo Contestant.Start's ally setup so it fights the player.
        internal static void MakeHostile(Enemy e, RivalMarker mk)
        {
            int enemyLayer = LayerMask.NameToLayer("Enemy"), playerLayer = LayerMask.NameToLayer("Player");
            int enemyFloor = LayerMask.NameToLayer("EnemyFloorContact"), allyFloor = LayerMask.NameToLayer("AllyFloorContact");
            foreach (Transform child in e.transform)
            {
                if (child.name == "AllyHurtBox") child.name = "EnemyHurtBox";
                if (child.name == "AllyFloorContact") child.name = "EnemyFloorContact";
                if (child.gameObject.layer == playerLayer) child.gameObject.layer = enemyLayer;
                else if (child.gameObject.layer == allyFloor) child.gameObject.layer = enemyFloor;
            }
            e.skillCategory = Enemy.globalEnemySkillCategory;
            GameController.allies.Remove(e.gameObject);
            if (!GameController.enemies.Contains(e.gameObject)) GameController.enemies.Add(e.gameObject);
            e.targetGroup = GameController.players;
            e.currentGroup = GameController.enemies;
            e.dropLoot = true;
            e.dropLootChest = mk.Tier != RivalTier.Final; // EnemyDropLoot spawns TreasureChest with chestLootTableID ("TreasureChestMiniBoss"); the final boss room has its own rewards

            // The Contestant also registers the Frost Mage's Blizzard as an extra skill. Keep it only on Water rivals.
            if (!SignatureAllowed(mk, "SummonBlizzard") && e.availableEnemySkillsDict != null) e.availableEnemySkillsDict.Remove(BlizzardSkill);

            // Robe (colour + stat mods) and relic (stat effect), like a player's loadout.
            if (Plugin.RivalRobes.Value && RivalLoadout.PickOutfit(mk.Bonus, out mk.OutfitID, out mk.RobeColor))
                RivalLoadout.ApplyRobeColor(e, mk.RobeColor);
            if (Plugin.RivalRelics.Value) mk.RelicID = RivalLoadout.PickRelic(mk.Bonus);
            mk.Bonus.Clamp();
            RivalLoadout.ApplyStats(e, mk.Bonus);

            // HP, the same way MiniBoss sets its health.
            e.health.healthStat.AddMod(new NumVarStatMod("RivalHP", TierHP(mk.Tier) * mk.Bonus.Hp, 10, VarStatModType.Override, true));

            // Its own spells are tuned as an ally's (5-8s cooldowns, all ready at spawn): slow them by tier.
            Traverse c = Traverse.Create(e);
            float cdm = TierCooldown(mk.Tier) * mk.Bonus.Cooldown;
            foreach (string f in new[] { "tornadoCooldown", "swordThrowCooldown", "fireballCooldown" })
                c.Field(f).SetValue(c.Field(f).GetValue<float>() * cdm);
            c.Field("dashCooldown").SetValue(c.Field("dashCooldown").GetValue<float>() * cdm);

            // Arcana: any player arcana of the rival's element (all elements for Chaos), picked for variety.
            List<ArcanaDef> pool = ArcanaCatalog.Pool(mk.Prefix);
            mk.Arcana = PickVaried(pool, TierArcanaCount(mk.Tier), mk.Prefix == "Chaos");
            EnemyArcana.Give(e, mk.Arcana, TierCooldown(mk.Tier) * mk.Bonus.Cooldown, TierWindup(mk.Tier));

            // Dash arcana: fired from its own dashes, like the player's.
            List<ArcanaDef> dashes = ArcanaCatalog.DashPool(mk.Prefix);
            if (dashes.Count > 0)
            {
                mk.Dash = dashes[UnityEngine.Random.Range(0, dashes.Count)].Scaled(TierCooldown(mk.Tier) * mk.Bonus.Cooldown, 1f);
                if (DashArcana.Give(e, mk.Dash) == null) mk.Dash = null;
            }

            // Signature meter: fills on hits that land on the player; full = next arcana empowered.
            Signature.Enable(e, TierSignatureHits(mk.Tier), mk.Prefix);

            Plugin.Log.LogInfo("RIVAL " + mk.Prefix + " pool: " + pool.Count + " arcana, dash " + (mk.Dash != null ? mk.Dash.SkillID : "none")
                + " (of " + dashes.Count + "), signature after " + TierSignatureHits(mk.Tier) + " hits");

            Plugin.Log.LogInfo("RIVAL " + mk.Prefix + " " + mk.Tier + " ready: HP " + Mathf.RoundToInt(TierHP(mk.Tier) * mk.Bonus.Hp)
                + ", arcana " + ArcanaList(mk.Arcana) + ", robe " + (mk.OutfitID ?? "none") + " (row " + mk.RobeColor + ")"
                + ", relic " + (mk.RelicID ?? "none") + ", bonuses: " + mk.Bonus.Describe());
        }

        // Intro row: borrowed arcana, then its signature spells, robe and relic, each with icon and name.
        internal static List<IntroEntry> IntroEntries(RivalMarker mk)
        {
            List<IntroEntry> list = new List<IntroEntry>();
            Color el = ElementalGiants.ElementColor(mk.Prefix);
            foreach (ArcanaDef d in mk.Arcana)
            {
                IntroEntry ie = new IntroEntry(IntroUI.SkillIcon(d.SkillID), IntroUI.SkillName(d.SkillID));
                if (ie.Icon == null) ie.Tint = el;
                list.Add(ie);
            }
            if (mk.Dash != null)
            {
                IntroEntry de = new IntroEntry(IntroUI.SkillIcon(mk.Dash.SkillID), IntroUI.SkillName(mk.Dash.SkillID) + " (dash)");
                if (de.Icon == null) de.Tint = el;
                list.Add(de);
            }
            List<string> sig = new List<string>(SignatureArcana);
            sig.Add("SummonBlizzard");
            foreach (string id in sig)
            {
                if (!SignatureAllowed(mk, id)) continue;
                IntroEntry ie = new IntroEntry(IntroUI.SkillIcon(id), IntroUI.SkillName(id));
                if (ie.Icon == null) ie.Tint = new Color(0.8f, 0.8f, 0.8f);
                list.Add(ie);
            }
            if (mk.OutfitID != null)
            {
                Sprite robe = null;
                try { robe = IconManager.GetWizardSprite(mk.OutfitID); } catch (Exception) { }
                IntroEntry ie = new IntroEntry(robe, IntroUI.OutfitName(mk.OutfitID));
                if (robe == null) ie.Tint = new Color(0.7f, 0.5f, 1f);
                list.Add(ie);
            }
            if (mk.RelicID != null) list.Add(new IntroEntry(IntroUI.ItemIcon(mk.RelicID), IntroUI.ItemName(mk.RelicID)));
            return list;
        }

        // Intro shared by arena rivals and final-stage champions: freeze the rival, camera on it, its arcana/robe/relic
        // appear one by one, then the fight starts (with a short delay before its first spell).
        internal static IEnumerator Intro(Enemy rival, string title, string subtitle)
        {
            if (rival == null) yield break;
            rival.enabled = false;
            RivalMarker mk = rival.GetComponent<RivalMarker>();
            RivalTier tier = mk != null ? mk.Tier : RivalTier.Medium;
            float intro = IntroUI.Show(title, subtitle + (mk != null ? "  ·  " + mk.Bonus.Describe() : ""), TierColor(tier),
                mk != null ? IntroEntries(mk) : null, Plugin.RivalIntroSeconds.Value);
            if (rival.health != null) rival.health.SetInvulnerabilityDuration(intro + 0.5f, false);
            CameraController cam = CameraController.Instance;
            if (cam != null) cam.OverrideAndMoveCameraToLocation(rival.transform, null, intro, true, false, false, false);
            yield return new WaitForSeconds(intro);
            if (rival != null)
            {
                DelayCasts(rival, 1.5f); // no opening burst
                rival.enabled = true;
            }
        }

        internal static string ArcanaList(List<ArcanaDef> defs)
        {
            StringBuilder sb = new StringBuilder();
            foreach (ArcanaDef d in defs) { if (sb.Length > 0) sb.Append(", "); sb.Append(EnemyArcana.DisplayName(d.SkillID)); }
            return sb.ToString();
        }

        // Picks n arcana spreading them over different elements (Chaos) or different kinds of cast (projectile, wave,
        // ground spell, self spell, basic...), so a loadout isn't three fireball-likes.
        internal static List<ArcanaDef> PickVaried(List<ArcanaDef> pool, int n, bool byElement)
        {
            List<ArcanaDef> shuffled = new List<ArcanaDef>(pool);
            Shuffle(shuffled);
            List<ArcanaDef> picked = new List<ArcanaDef>();
            HashSet<string> used = new HashSet<string>();
            foreach (ArcanaDef d in shuffled)
            {
                if (picked.Count >= n) break;
                string key = byElement ? d.Element : d.Delivery.ToString();
                if (used.Add(key)) picked.Add(d);
            }
            foreach (ArcanaDef d in shuffled)
            {
                if (picked.Count >= n) break;
                if (!picked.Contains(d)) picked.Add(d);
            }
            return picked;
        }

        // The Contestant's own spells are elemental too: keep each only for rivals of its element (Chaos keeps all).
        internal static bool SignatureAllowed(RivalMarker mk, string spell)
        {
            if (mk == null || mk.Prefix == "Chaos") return true;
            switch (spell)
            {
                case "ShootFireball": return mk.Prefix == "Flame";
                case "SummonTornado": return mk.Prefix == "Wind";
                case "SummonBlizzard": return mk.Prefix == "Frost";
                default: return true; // Sword Throw: neutral
            }
        }

        static void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                T tmp = list[i]; list[i] = list[j]; list[j] = tmp;
            }
        }

        internal static string StagePrefixOrRandom()
        {
            if (UnityEngine.Random.value < Plugin.ChaosRivalChance.Value) return "Chaos";
            string p = ElementalGiants.StagePrefix();
            return p ?? ElementalGiants.RandomPrefix();
        }

        // ---------- level hook: turn the misc room into an arena ----------

        internal static void TryReplaceMiscRoom()
        {
            if (!Plugin.RivalEvents.Value || !Level.miscRoomFound) return;
            if (UnityEngine.Random.value >= Plugin.RivalEventChance.Value) return;

            Component miscRoom = FindMiscRoom();
            if (miscRoom == null) { Plugin.Log.LogWarning("RIVAL misc room not found"); return; }

            // Hide the room's NPC and other interactables; keep its Portal (arrival/return points).
            Transform inter = miscRoom.transform.Find("Interactables");
            if (inter != null)
                foreach (Transform t in inter)
                    if (t.name != "Portal") t.gameObject.SetActive(false);

            RivalArena.Create(Level.miscRoomPortalInPosition, StagePrefixOrRandom());
            Plugin.Log.LogInfo("RIVAL event replaces the misc room on " + Level.tileSetName);
        }

        static Component FindMiscRoom()
        {
            IEnumerable list = Traverse.Create(typeof(Level)).Property("GridRoomTupleList").GetValue() as IEnumerable;
            if (list == null) return null;
            foreach (object tuple in list)
            {
                Component room = Traverse.Create(tuple).Property("Second").GetValue() as Component;
                if (room == null) continue;
                object type = Traverse.Create(room).Field("type").GetValue();
                if (type != null && type.ToString() == "Misc") return room;
            }
            return null;
        }
    }

    // Marks a spawned Contestant as a rival before its Start runs.
    internal class RivalMarker : MonoBehaviour
    {
        public string Prefix;
        public RivalTier Tier;
        public List<ArcanaDef> Arcana = new List<ArcanaDef>();
        public ArcanaDef Dash;
        public RivalBonus Bonus = new RivalBonus();
        public string OutfitID, RelicID;
        public int RobeColor = -1;
    }

    // Tags one of the three arena portals.
    internal class RivalPortal : MonoBehaviour
    {
        public RivalArena Arena;
        public RivalTier Tier;
    }

    // The arena controller: three portals -> pick -> intro (camera on rival + arcana preview) -> fight.
    internal class RivalArena : MonoBehaviour
    {
        string prefix;
        Vector2 arrival;
        readonly List<GameObject> portals = new List<GameObject>();
        bool chosen;
        Enemy rival;
        string banner;
        float bannerUntil;

        static readonly Vector2[] PortalOffsets = { new Vector2(-3f, 2.5f), new Vector2(0f, 3.5f), new Vector2(3f, 2.5f) };
        static readonly Vector2 FightOffset = new Vector2(0f, 1f);
        static readonly Vector2 RivalOffset = new Vector2(0f, 5.5f);

        internal static RivalArena Create(Vector2 arrivalPos, string elementPrefix)
        {
            GameObject go = new GameObject("HybridBosses_RivalArena");
            RivalArena a = go.AddComponent<RivalArena>();
            a.prefix = elementPrefix;
            a.arrival = arrivalPos;
            return a;
        }

        void Start()
        {
            GameObject prefab = Portal.Prefab;
            if (prefab == null) { Plugin.Log.LogWarning("RIVAL no portal prefab"); return; }
            for (int i = 0; i < 3; i++)
            {
                RivalTier tier = (RivalTier)i;
                GameObject go = Instantiate(prefab, arrival + PortalOffsets[i], Quaternion.identity) as GameObject;
                Portal p = go.GetComponent<Portal>();
                Traverse tp = Traverse.Create(p);
                tp.Field("targetLocation").SetValue((Vector3)(arrival + FightOffset));
                tp.Field("targetSpawnOffset").SetValue(Vector3.zero);
                tp.Field("color").SetValue(Rivals.TierColor(tier));
                tp.Field("createReturnPortal").SetValue(false);
                RivalPortal rp = go.AddComponent<RivalPortal>();
                rp.Arena = this;
                rp.Tier = tier;
                portals.Add(go);
            }
            IntroUI.Show(ElementalGiants.ElementColorName(prefix) + " rival awaits", "Easy (green)  ·  Medium (yellow)  ·  Hard (red)",
                ElementalGiants.ElementColor(prefix), null, 5f);
        }

        // Called from the Portal.OnTriggerEnter2D prefix when the player steps into one of our portals.
        internal void Choose(RivalTier tier)
        {
            if (chosen) return;
            chosen = true;
            Plugin.Log.LogInfo("RIVAL tier chosen: " + tier);
            StartCoroutine(Run(tier));
        }

        IEnumerator Run(RivalTier tier)
        {
            yield return new WaitForSeconds(1.2f); // let the teleport finish
            foreach (GameObject p in portals) if (p != null) Destroy(p);

            rival = Rivals.Spawn(arrival + RivalOffset, prefix, tier);
            if (rival == null) yield break;
            yield return null; // Start (and our hostile postfix) runs
            yield return null;

            yield return StartCoroutine(Rivals.Intro(rival, ElementalGiants.ElementColorName(prefix) + " Rival Wizard", tier.ToString()));
        }

        void Show(string text, float seconds)
        {
            banner = text;
            bannerUntil = Time.time + seconds;
        }

        static GUIStyle style;

        void OnGUI()
        {
            if (string.IsNullOrEmpty(banner) || Time.time > bannerUntil) return;
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label) { fontSize = 26, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                style.normal.textColor = Color.white;
            }
            Rect r = new Rect(0, Screen.height * 0.12f, Screen.width, 90);
            GUIStyle shadow = new GUIStyle(style); shadow.normal.textColor = Color.black;
            GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), banner, shadow);
            GUI.Label(r, banner, style);
        }
    }

    // ---------- patches ----------

    [HarmonyPatch(typeof(LevelGenerator), "SetExitAndStorePositions")]
    static class LevelGenPatch
    {
        static void Postfix()
        {
            try { Rivals.TryReplaceMiscRoom(); }
            catch (Exception ex) { Plugin.Log.LogError("RIVAL misc room hook failed: " + ex); }
        }
    }

    [HarmonyPatch(typeof(Portal), "OnTriggerEnter2D")]
    static class PortalPatch
    {
        static void Prefix(Portal __instance, Collider2D col)
        {
            RivalPortal rp = __instance.GetComponent<RivalPortal>();
            if (rp == null || rp.Arena == null || col == null) return;
            if (col.GetComponentInParent<Player>() == null) return;
            rp.Arena.Choose(rp.Tier);
        }
    }

    [HarmonyPatch(typeof(Contestant), "Start")]
    static class ContestantStartPatch
    {
        static void Postfix(Contestant __instance)
        {
            RivalMarker mk = __instance.GetComponent<RivalMarker>();
            if (mk == null) return;
            try { Rivals.MakeHostile(__instance, mk); }
            catch (Exception ex) { Plugin.Log.LogError("RIVAL MakeHostile failed: " + ex); }
        }
    }

    // Pacing for the Contestant's own spells: while the shared cast gap runs, its ready flags stay false.
    [HarmonyPatch(typeof(Contestant), "HandleCooldowns")]
    static class ContestantCooldownPatch
    {
        static readonly AccessTools.FieldRef<Contestant, bool> tornado = AccessTools.FieldRefAccess<Contestant, bool>("tornadoReady");
        static readonly AccessTools.FieldRef<Contestant, bool> sword = AccessTools.FieldRefAccess<Contestant, bool>("swordThrowReady");
        static readonly AccessTools.FieldRef<Contestant, bool> fireball = AccessTools.FieldRefAccess<Contestant, bool>("fireballReady");

        static void Postfix(Contestant __instance)
        {
            if (!Rivals.IsRival(__instance)) return;
            RivalMarker mk = __instance.GetComponent<RivalMarker>();
            bool gap = !Rivals.CanCast(__instance);
            // Off-theme signature spells never come up (no Fireball from a Thunder rival); the rest wait for the gap.
            if (gap || !Rivals.SignatureAllowed(mk, "SummonTornado")) tornado(__instance) = false;
            if (gap || !Rivals.SignatureAllowed(mk, "SwordThrow")) sword(__instance) = false;
            if (gap || !Rivals.SignatureAllowed(mk, "ShootFireball")) fireball(__instance) = false;
        }
    }

    // Every extra skill of a rival (Blizzard, borrowed arcana...) waits for the shared gap; the skill used then
    // starts the gap and the others' priorities are reshuffled.
    [HarmonyPatch(typeof(Enemy), "HandleExtraSkills")]
    static class RivalExtraSkillsPatch
    {
        static bool Prefix(Enemy __instance, ref bool __result)
        {
            if (!Rivals.IsRival(__instance) || Rivals.CanCast(__instance)) return true;
            __result = false;
            return false;
        }

        static void Postfix(Enemy __instance, bool __result)
        {
            if (__result && Rivals.IsRival(__instance))
                Rivals.OnSkillUsed(__instance, Traverse.Create(__instance).Field("currentPrioritySkill").GetValue() as Enemy.SkillState);
        }
    }

    // Each of the Contestant's own spells starts the shared cast gap.
    [HarmonyPatch]
    static class ContestantSpellEnterPatch
    {
        static IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            foreach (string n in ContestantSpellStates.Names)
            {
                Type t = AccessTools.TypeByName(n);
                System.Reflection.MethodInfo m = t == null ? null : AccessTools.DeclaredMethod(t, "OnEnter");
                if (m != null) yield return m;
            }
        }

        static void Postfix(object __instance)
        {
            Enemy e = Traverse.Create(__instance).Field("parent").GetValue() as Enemy;
            if (Rivals.IsRival(e)) Rivals.MarkCast(e);
        }
    }

    // Whenever one of the rival's own spell states runs (OnEnter or Update, however the FSM gets there), its
    // "Player" SetAttackInfo calls are attributed to the rival.
    [HarmonyPatch]
    static class ContestantSpellStates
    {
        internal static readonly string[] Names = { "Contestant+FireballState", "Contestant+TornadoState", "Contestant+SwordThrowState" };

        static IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            foreach (string n in Names)
            {
                Type t = AccessTools.TypeByName(n);
                if (t == null) continue;
                foreach (string mn in new[] { "OnEnter", "Update" })
                {
                    System.Reflection.MethodInfo m = AccessTools.DeclaredMethod(t, mn);
                    if (m != null) yield return m;
                }
            }
        }

        [HarmonyPriority(Priority.First)]
        static void Prefix(object __instance, out Enemy __state)
        {
            __state = Rivals.CurrentCaster;
            Enemy e = Traverse.Create(__instance).Field("parent").GetValue() as Enemy;
            if (Rivals.IsRival(e)) Rivals.CurrentCaster = e;
        }

        static void Postfix(Enemy __state) { Rivals.CurrentCaster = __state; }
    }

    // Contestant's own arcana (Fireball, Tornado, Sword Throw) load "Player" stats during its Update. Re-own them.
    [HarmonyPatch(typeof(Contestant), "Update")]
    static class ContestantUpdatePatch
    {
        static void Prefix(Contestant __instance) { if (Rivals.IsRival(__instance)) Rivals.CurrentCaster = __instance; }
        static void Postfix() { Rivals.CurrentCaster = null; }
    }

    [HarmonyPatch(typeof(Attack), "SetAttackInfo", new Type[] { typeof(string), typeof(string), typeof(int), typeof(bool) })]
    static class SetAttackInfoPatch
    {
        static void Postfix(Attack __instance, string newSkillCat, AttackInfo __result)
        {
            if (__result == null || newSkillCat == null || !newSkillCat.StartsWith("Player")) return;
            Enemy owner = Rivals.CurrentCaster ?? OwnerContext.Current;           // rival's own spells / owned arcana code running
            if (owner == null) EnemyArcana.OwnedByEnemy(__instance, out owner); // follow-up hits under a tagged arcana object
            if (owner != null) EnemyArcana.ReOwn(__result, owner);
        }
    }
}
