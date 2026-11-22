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
        // How long a full meter stays ready before it runs out unused (the player's also decays).
        internal static float TierSignatureSeconds(RivalTier t) { return ByTier(t, 8f, 10f, 12f, 14f); }

        // Audit: every skill the rival can use, for the log (should match its intro).
        internal static string KitList(Enemy e, RivalMarker mk)
        {
            List<string> parts = new List<string>();
            if (e.availableEnemySkillsDict != null) foreach (string k in e.availableEnemySkillsDict.Keys) parts.Add(k);
            if (mk.Basic != null) parts.Add("basic:" + mk.Basic.SkillID);
            if (mk.Dash != null) parts.Add("dash:" + mk.Dash.SkillID);
            foreach (string s in SignatureArcana) if (SignatureAllowed(mk, s)) parts.Add("own:" + s);
            return string.Join(", ", parts.ToArray());
        }
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
            float gap = mk != null ? TierCastGap(mk.Tier) * mk.GapMultiplier : Plugin.ArcanaCastGap.Value;
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
        // arenaArchon: a Final-tier champion met through a rival arena's Hard portal (it drops a gold chest; the one that
        // replaces Sura drops nothing, the final room has its own rewards).
        internal static Enemy Spawn(Vector2 pos, string prefix, RivalTier tier, bool arenaArchon = false)
        {
            Enemy e = Enemy.Spawn("Contestant", pos, null, true, false, false, false, false);
            if (e == null) { Plugin.Log.LogWarning("RIVAL spawn failed (no Contestant prefab)"); return null; }
            ElementType el;
            if (ElementalGiants.TryGetElement(e, prefix, out el)) e.element = el; // InitElement (in Start) picks the palette from this
            RivalMarker mk = e.gameObject.AddComponent<RivalMarker>();
            mk.Prefix = prefix;
            mk.Tier = tier;
            mk.ArenaArchon = arenaArchon;
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
            // Chests by tier (RivalRewards): Easy 1 standard, Medium 2 standard, Hard or arena Archon 1 gold; none for the
            // champion that replaces Sura (the final room has its own rewards). The game's own drop (always a gold
            // "TreasureChestMiniBoss") is off.
            e.dropLootChest = false;
            RivalRewards.Attach(e, mk);

            // The Contestant also registers the Frost Mage's Blizzard as an extra skill. Keep it only on Water rivals.
            if (!SignatureAllowed(mk, "SummonBlizzard") && e.availableEnemySkillsDict != null) e.availableEnemySkillsDict.Remove(BlizzardSkill);

            // Robe (colour + stat mods) and relic (stat effect), like a player's loadout.
            // v0.9.0: an Echo wears the last run's robe and relics.
            bool echo = mk.Echo != null;
            if (echo)
            {
                if (RivalLoadout.UseOutfit(mk.Bonus, mk.Echo.Outfit, out mk.OutfitID, out mk.RobeColor)) RivalLoadout.ApplyRobeColor(e, mk.RobeColor);
                mk.RelicIDs = RivalLoadout.UseRelics(mk.Bonus, mk.Echo.Relics);
                mk.RelicID = mk.RelicIDs.Count > 0 ? mk.RelicIDs[0] : null;
                EchoRival.Infusions(e, mk);
            }
            else if (mk.League != null && RivalLoadout.UseOutfit(mk.Bonus, League.OutfitFor(mk.League), out mk.OutfitID, out mk.RobeColor))
                RivalLoadout.ApplyRobeColor(e, mk.RobeColor); // v1.1.0: a named league rival always wears its own robe
            else if (Plugin.RivalRobes.Value && RivalLoadout.PickOutfit(mk.Bonus, out mk.OutfitID, out mk.RobeColor))
                RivalLoadout.ApplyRobeColor(e, mk.RobeColor);
            if (!echo && Plugin.RivalRelics.Value) { mk.RelicID = RivalLoadout.PickRelic(mk.Bonus); if (mk.RelicID != null) mk.RelicIDs = new List<string> { mk.RelicID }; }
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

            // Basic attack: a basic arcana of its element replaces the Contestant's built-in Wind Slash basic (which has
            // no stats outside its ally category, so it swung for nothing and the rival looked stuck in place).
            // v0.6.0: Radiant / Prism rivals cast that element's arcana (topped up with base arcana that turn into it:
            // VariantMark converts their attacks), and wear its look.
            StageVariant variant = Variants.FromPrefix(mk.Prefix);
            bool exotic = variant == StageVariant.Radiant || variant == StageVariant.Prism;
            if (exotic) { VariantMark.Set(e, variant); if (variant == StageVariant.Prism) Variants.TintBody(e, variant); }
            List<ArcanaDef> pool = exotic ? Variants.CastPool(variant, false) : ArcanaCatalog.Pool(mk.Prefix);
            List<ArcanaDef> basics = (mk.Prefix == "Chaos" && ChaosStages.Planned) ? ArcanaCatalog.PoolExact("Chaos") : new List<ArcanaDef>(pool);
            basics.RemoveAll(d => d.Family != ArcanaFamily.Melee);
            if (basics.Count == 0) { basics = new List<ArcanaDef>(pool); basics.RemoveAll(d => d.Family != ArcanaFamily.Melee); }
            if (basics.Count == 0 && exotic) { basics = ArcanaCatalog.Pool("Chaos"); basics.RemoveAll(d => d.Family != ArcanaFamily.Melee); } // converted when swung
            ArcanaDef echoBasic = echo ? EchoRival.Def(mk.Echo.Basic) : null;
            if (echoBasic != null && echoBasic.Family == ArcanaFamily.Melee) basics = new List<ArcanaDef> { echoBasic }; // the last run's basic
            if (basics.Count > 0)
            {
                ArcanaDef b = basics[UnityEngine.Random.Range(0, basics.Count)].Scaled(1f, 1f);
                b.Basic = true;
                b.Windup = 0.3f;
                b.Cooldown = 0.5f; // short: while it cools down the rival can't swing and just stands in range
                b.MinRange = 0f;
                b.MaxRange = 4f;
                mk.Basic = b;
                EnemyArcana.Give(e, new[] { b });
            }
            RivalBasics.Register(e, mk);
            pool.RemoveAll(d => d.Family == ArcanaFamily.Melee); // basics only in the basic slot

            // Arcana: any player arcana of the rival's element (all elements for Chaos), picked for variety.
            if (mk.Prefix == "Chaos" && ChaosStages.Planned)
            {
                // Chaos stage rival: Chaos-element arcana first, topped up from the whole pool if there aren't enough.
                List<ArcanaDef> chaos = ArcanaCatalog.PoolExact("Chaos");
                chaos.RemoveAll(d => d.Family == ArcanaFamily.Melee);
                mk.Arcana = PickVaried(chaos, TierArcanaCount(mk.Tier), false);
                foreach (ArcanaDef d in PickVaried(pool, TierArcanaCount(mk.Tier), true))
                    if (mk.Arcana.Count < TierArcanaCount(mk.Tier) && !mk.Arcana.Contains(d)) mk.Arcana.Add(d);
            }
            else mk.Arcana = PickVaried(pool, TierArcanaCount(mk.Tier), mk.Prefix == "Chaos");
            if (echo) mk.Arcana = EchoRival.Arcana(mk, pool, TierArcanaCount(mk.Tier)); // the last run's arcana
            if (mk.Grudge != null) Grudges.InfuseKit(e, mk); // v0.9.17: a rematch rival: some of its arcana infused
            if (!echo) Grudges.DualKit(e, mk); // v0.9.18: Archons sometimes, Hard rivals rarely, cast dual-infused arcana
            mk.Signature = PickSignature(mk, (mk.Prefix == "Chaos" && ChaosStages.Planned) ? ArcanaCatalog.PoolExact("Chaos") : pool);
            EnemyArcana.Give(e, mk.Arcana, TierCooldown(mk.Tier) * mk.Bonus.Cooldown, TierWindup(mk.Tier));

            // Dash arcana: fired from its own dashes, like the player's.
            List<ArcanaDef> dashes = exotic ? Variants.DashPool(variant) : ArcanaCatalog.DashPool(mk.Prefix);
            ArcanaDef echoDash = echo ? EchoRival.Def(mk.Echo.Dash) : null;
            if (echoDash != null && echoDash.Family == ArcanaFamily.Dash) dashes = new List<ArcanaDef> { echoDash }; // the last run's dash
            if (dashes.Count > 0)
            {
                mk.Dash = dashes[UnityEngine.Random.Range(0, dashes.Count)].Scaled(TierCooldown(mk.Tier) * mk.Bonus.Cooldown, 1f);
                if (DashArcana.Give(e, mk.Dash) == null) mk.Dash = null;
            }

            // Signature meter: fills on hits that land on the player; full = the signature activates.
            Signature.Enable(e, TierSignatureHits(mk.Tier), mk.Prefix, TierSignatureSeconds(mk.Tier), mk.Signature);

            // Unstuck watchdog: a rival that "runs" without moving while the player is out of reach dashes at them.
            e.gameObject.AddComponent<RivalUnstuck>().Owner = e;

            // v0.9.21: holder-only relics run for real in the rival's own inventory (after its stats and HP are set).
            RivalLoadout.EquipReal(e, mk.RelicIDs);
            if (mk.Alchemist) WhiteAlchemist.Setup(e); // v1.1.0: white body and aura (after its robe colour)

            Plugin.Log.LogInfo("RIVAL " + mk.Prefix + " pool: " + pool.Count + " arcana, basic " + (mk.Basic != null ? mk.Basic.SkillID : "none")
                + ", dash " + (mk.Dash != null ? mk.Dash.SkillID : "none") + " (of " + dashes.Count + "), signature "
                + (mk.Signature != null ? mk.Signature.SkillID : "none") + " after " + TierSignatureHits(mk.Tier) + " hits, ready for "
                + TierSignatureSeconds(mk.Tier) + "s");
            Plugin.Log.LogInfo("RIVAL kit (everything it may cast): " + KitList(e, mk));

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
                bool isSig = mk.Signature != null && mk.Signature.SkillID == d.SkillID;
                IntroEntry ie = new IntroEntry(IntroUI.SkillIcon(d.SkillID), EchoRival.Named(mk, d) + (isSig ? " (signature)" : ""));
                if (ie.Icon == null) ie.Tint = el;
                list.Add(ie);
            }
            if (mk.Basic != null)
            {
                IntroEntry be = new IntroEntry(IntroUI.SkillIcon(mk.Basic.SkillID), EchoRival.Named(mk, mk.Basic) + " (basic)");
                if (be.Icon == null) be.Tint = el;
                list.Add(be);
            }
            if (mk.Dash != null)
            {
                IntroEntry de = new IntroEntry(IntroUI.SkillIcon(mk.Dash.SkillID), EchoRival.Named(mk, mk.Dash) + " (dash)");
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
            foreach (string rid in mk.RelicIDs) list.Add(new IntroEntry(IntroUI.ItemIcon(rid), IntroUI.ItemName(rid) + (RivalLoadout.IsCursed(rid) ? " (cursed)" : ""))); // v0.9.21: every relic it carries
            return list;
        }

        // Intro shared by arena rivals and final-stage champions: freeze the rival, camera on it, its arcana/robe/relic
        // appear one by one, then the fight starts (with a short delay before its first spell).
        internal static IEnumerator Intro(Enemy rival, string title, string subtitle)
        {
            if (rival == null) yield break;
            rival.enabled = false;
            BossBar bar = BossBar.Attach(rival, title); // the game's boss health bar, shown when the fight starts
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
                if (bar != null) bar.Show();
                if (tier == RivalTier.Final && Plugin.ChampionPhases.Value) ChampionPhase.Attach(rival, title); // phase 2 at half health
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
        // Its signature arcana, like the player's signature slot: one of its arcana that has a signature version in
        // the game (highest priority). If none of them has one, its last arcana is swapped for one that does.
        static ArcanaDef PickSignature(RivalMarker mk, List<ArcanaDef> pool)
        {
            ArcanaDef best = null;
            foreach (ArcanaDef d in mk.Arcana)
                if (Signature.Capable(d) && (best == null || d.Priority > best.Priority)) best = d;
            if (best != null) return best;
            List<ArcanaDef> options = new List<ArcanaDef>();
            foreach (ArcanaDef d in pool)
                if (Signature.Capable(d) && d.Family != ArcanaFamily.Melee && d.Family != ArcanaFamily.Dash
                    && !EnemyArcana.Broken.Contains(d.SkillID) && !mk.Arcana.Exists(a => a.SkillID == d.SkillID)) options.Add(d);
            if (options.Count == 0) return null;
            ArcanaDef pick = options[UnityEngine.Random.Range(0, options.Count)];
            if (mk.Arcana.Count > 0) mk.Arcana[mk.Arcana.Count - 1] = pick; else mk.Arcana.Add(pick);
            return pick;
        }

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
            if (mk == null || mk.Prefix == "Chaos" || mk.Prefix == "Prism") return true; // Prism mixes every element
            if (mk.Prefix == "Radiant") return false;
            string el;
            switch (spell)
            {
                case "ShootFireball": el = "Flame"; break;
                case "SummonTornado":
                case "SwordThrow": el = "Wind"; break; // Sword Throw is the spinning cyclone boomerang: a wind spell
                case "SummonBlizzard": el = "Frost"; break;
                default: return false; // anything else of its own: not part of an elemental kit
            }
            return mk.Prefix == el || mk.SecondPrefix == el; // a hybrid Archon (phase 2) also uses its second element's
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
            if (ChaosStages.Planned) return ChaosStages.PlannedInfo.Prefix; // a variant stage's rival is of that variant
            // The occasional off-element rival: Chaos, or (with Arcanos) Radiant / Prism by the same 50/35/15 split.
            if (UnityEngine.Random.value < Plugin.ChaosRivalChance.Value) return Variants.Info(Variants.RandomVariant()).Prefix;
            string p = ElementalGiants.StagePrefix();
            return p ?? ElementalGiants.RandomPrefix();
        }

        // (The event used to take over the stage's misc room; since v0.5.4 it has its own portal and rooms: RivalEvent.)
    }

    // Marks a spawned Contestant as a rival before its Start runs.
    internal class RivalMarker : MonoBehaviour
    {
        public string Prefix;
        public RivalTier Tier;
        public List<ArcanaDef> Arcana = new List<ArcanaDef>();
        public ArcanaDef Dash;
        public ArcanaDef Basic;
        public ArcanaDef Signature; // the one arcana that spends a full signature meter (its signature version)
        public float GapMultiplier = 1f; // champions' second phase casts faster
        public string SecondPrefix;      // an Archon's second element after its phase shift
        public EchoRecord Echo;          // v0.9.0: the Echo of the player's last run (its loadout)
        public GrudgeRecord Grudge;      // v0.9.17: a rematch rival
        public Dictionary<string, ElementType> Infused; // v0.9.17: its arcana cast infused (skill ID -> element)
        public Dictionary<string, ElementType> InfusedSecond; // v0.9.18: the second element of its dual arcana
        public bool ArenaArchon;         // a champion from a rival arena's Hard portal (not replacing Sura)
        public bool Alchemist;           // v1.1.0: the White Alchemist (final champion)
        public bool NoChest;             // v1.1.0: no chest of its own (tournament rounds; the tournament pays at the end)
        public LeagueRival League;       // v1.1.0: a named league rival
        public RivalBonus Bonus = new RivalBonus();
        public string OutfitID, RelicID;
        public List<string> RelicIDs = new List<string>(); // v0.9.21: every relic it carries (the Echo up to 4)
        public int RobeColor = -1;
    }

    // Tags the stage's rival portal (v0.9.2: it leads straight to the arena) with what it holds.
    internal class RivalPortal : MonoBehaviour
    {
        public RivalEvent Event;
        public RivalTier Tier;
        public bool IsEcho;              // v0.9.0: the Echo of your last run
    }

    // ---------- patches ----------

    // Stage generation: plan Chaos, and arm the rival event for the end of generation (the stage's rooms, start and
    // exit are placed; LevelGenerator.levelGeneratedEventHandlers fires right after, in the same coroutine).
    [HarmonyPatch(typeof(LevelGenerator), "SetExitAndStorePositions")]
    static class LevelGenPatch
    {
        static readonly System.Reflection.FieldInfo handlers = AccessTools.Field(typeof(LevelGenerator), "levelGeneratedEventHandlers");
        static Delegate oneShot;

        static void Postfix()
        {
            try { ChaosStages.PlanCurrentStage(); } catch (Exception ex) { Plugin.Log.LogError("CHAOS plan failed: " + ex); }
            try
            {
                if (handlers == null) return;
                if (oneShot == null)
                    oneShot = Delegate.CreateDelegate(handlers.FieldType, typeof(LevelGenPatch).GetMethod("Generated", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic));
                Delegate cur = handlers.GetValue(null) as Delegate;
                handlers.SetValue(null, Delegate.Combine(Delegate.Remove(cur, oneShot), oneShot));
            }
            catch (Exception ex) { Plugin.Log.LogError("RIVAL event hook failed: " + ex); }
        }

        // Runs once per stage, then unhooks itself.
        static void Generated()
        {
            try { handlers.SetValue(null, Delegate.Remove(handlers.GetValue(null) as Delegate, oneShot)); } catch (Exception) { }
            RivalEvent.OnLevelGenerated();
            TrialEvent.OnLevelGenerated(); // v0.8.0: infusion trials
        }
    }

    [HarmonyPatch(typeof(Portal), "OnTriggerEnter2D")]
    static class PortalPatch
    {
        static void Prefix(Portal __instance, Collider2D col)
        {
            RivalPortal rp = __instance.GetComponent<RivalPortal>();
            if (rp == null || rp.Event == null || col == null) return;
            if (col.GetComponentInParent<Player>() == null) return;
            rp.Event.Choose(rp.Tier, rp.IsEcho);
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
            if (!Rivals.IsRival(e)) return;
            Rivals.MarkCast(e);
            if (Plugin.DebugKeys.Value) Plugin.Log.LogInfo("RIVAL own spell: " + __instance.GetType().Name);
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
