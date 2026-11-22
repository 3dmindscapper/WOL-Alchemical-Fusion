using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace WolHybridBosses
{
    // v1.1.1: the Primordial robes. Beating an Archon (the final champion that replaces Sura, or the Archon behind a
    // Hard rival portal) unlocks its element's robe in the wardrobe (LegendAPI, like the White Mantle). Wearing one:
    //   - your wizard takes that element's primordial look (the body on the element's ramp) with an aura of its colour;
    //   - arcana of that element (their current element: infusions count, either half of a dual) hit harder and cool
    //     down faster, and their element's status effect lands more often. Numbers per robe below.
    // An arcana's element is checked when you equip it (cooldowns, re-checked every second) and on each attack
    // (damage and status, once per attack info).
    internal sealed class PrimordialRobe
    {
        public string Prefix, Name, Element, Flavour;
        public string[] Matches;  // Infusion.Name values that count (Water also covers Ice)
        public float Damage, Cooldown, Status; // damage ×, cooldown ×, status chance +
        public string OutfitID { get { return "AlchemicalPrimordial" + Prefix; } }
        public bool Unlocked { get { return Progress.GetBool("robe." + Prefix); } }
    }

    internal static class PrimordialRobes
    {
        internal static readonly PrimordialRobe[] All =
        {
            new PrimordialRobe { Prefix = "Flame", Element = "Fire", Name = "Primordial Flame Robe", Matches = new[] { "Fire" }, Damage = 1.2f, Cooldown = 0.8f, Status = 0.15f, Flavour = "Woven from the first fire. It is still warm." },
            new PrimordialRobe { Prefix = "Frost", Element = "Water", Name = "Primordial Tide Robe", Matches = new[] { "Water", "Ice" }, Damage = 1.15f, Cooldown = 0.8f, Status = 0.2f, Flavour = "It drips, though it is never wet." },
            new PrimordialRobe { Prefix = "Earth", Element = "Earth", Name = "Primordial Stone Robe", Matches = new[] { "Earth" }, Damage = 1.25f, Cooldown = 0.85f, Status = 0.15f, Flavour = "Heavy as a mountain, patient as one too." },
            new PrimordialRobe { Prefix = "Wind", Element = "Wind", Name = "Primordial Gale Robe", Matches = new[] { "Wind" }, Damage = 1.15f, Cooldown = 0.75f, Status = 0.1f, Flavour = "It never quite stops moving." },
            new PrimordialRobe { Prefix = "Thunder", Element = "Thunder", Name = "Primordial Storm Robe", Matches = new[] { "Thunder" }, Damage = 1.25f, Cooldown = 0.85f, Status = 0.15f, Flavour = "Your hair stands on end just holding it." },
            new PrimordialRobe { Prefix = "Chaos", Element = "Chaos", Name = "Primordial Chaos Robe", Matches = new[] { "Chaos" }, Damage = 1.2f, Cooldown = 0.8f, Status = 0.15f, Flavour = "The Sovereign's mantle. Its pattern never repeats." },
            new PrimordialRobe { Prefix = "Radiant", Element = "Radiant", Name = "Primordial Dawn Robe", Matches = new[] { "Radiant" }, Damage = 1.2f, Cooldown = 0.8f, Status = 0f, Flavour = "The first morning, folded into cloth." },
            new PrimordialRobe { Prefix = "Prism", Element = "Prism", Name = "Primordial Prism Robe", Matches = new[] { "Prism" }, Damage = 1.2f, Cooldown = 0.8f, Status = 0f, Flavour = "Every colour at once, and none for long." },
        };

        internal static PrimordialRobe For(string prefix) { foreach (PrimordialRobe r in All) if (r.Prefix == prefix) return r; return null; }

        static string wornPrefix;
        internal static PrimordialRobe Worn { get { return wornPrefix != null ? For(wornPrefix) : null; } }

        internal static int UnlockedCount { get { int n = 0; foreach (PrimordialRobe r in All) if (r.Unlocked) n++; return n; } }

        static string Pct(float f) { return Mathf.RoundToInt(Mathf.Abs(f - 1f) * 100f) + "%"; }

        internal static string Describe(PrimordialRobe r)
        {
            string els = string.Join(" and ", r.Matches);
            return els + " arcana deal +" + Pct(r.Damage) + " damage and cool down " + Pct(r.Cooldown) + " faster"
                + (r.Status > 0f ? ", and their status lands " + Mathf.RoundToInt(r.Status * 100f) + "% more often" : "") + ". You take the " + r.Element + " Archon's look.";
        }

        // Plugin startup, beside the White Mantle (data only).
        internal static void TryRegister()
        {
            if (!MantleRobe.LegendApiPresent) return;
            int n = 0;
            foreach (PrimordialRobe r in All)
            {
                try { Register(r); n++; }
                catch (Exception ex) { Plugin.Log.LogWarning("ROBES: " + r.Name + " not registered: " + ex.Message); }
            }
            Plugin.Log.LogInfo("ROBES registered " + n + " Primordial robes");
        }

        static void Register(PrimordialRobe r)
        {
            List<OutfitModStat> mods = new List<OutfitModStat> { new OutfitModStat(LegendAPI.Outfits.CustomModType, 0f, 0f, 0f, false) };
            Outfit robe = new Outfit(r.OutfitID, 0, mods, false, false);
            PrimordialRobe captured = r;
            LegendAPI.Outfits.Register(new LegendAPI.OutfitInfo
            {
                outfit = robe,
                name = r.Name,
                unlockCondition = () => captured.Unlocked,
                customMod = (player, on, outfitChange, stat) =>
                {
                    if (on) wornPrefix = captured.Prefix;
                    else if (wornPrefix == captured.Prefix) wornPrefix = null;
                    Plugin.Log.LogInfo("ROBES " + captured.Name + (on ? " worn" : " taken off"));
                },
                customDesc = (showNumbers, stat) => showNumbers ? Describe(captured) : captured.Flavour,
            });
        }

        // An Archon fell: its robe (once).
        internal static void Unlock(string prefix)
        {
            PrimordialRobe r = For(prefix);
            if (r == null || r.Unlocked) return;
            Progress.SetBool("robe." + prefix, true);
            Plugin.Log.LogInfo("ROBES unlocked " + r.Name);
            IntroUI.Show(r.Name + " unlocked", "A new robe in the wardrobe  ·  " + string.Join(" and ", r.Matches) + " arcana grow stronger", ElementalGiants.ElementColor(prefix), null, 5f);
        }

        // ---------- effects ----------

        internal static bool Matches(PrimordialRobe r, Player.SkillState s)
        {
            if (r == null || s == null) return false;
            string a = Infusion.Name(Infusion.CurrentElement(s));
            ElementType second;
            string b = Infusion.TryGetSecond(s.skillID, out second) ? Infusion.Name(second) : null;
            foreach (string m in r.Matches) if (m == a || m == b) return true;
            return false;
        }

        static readonly HashSet<AttackInfo> done = new HashSet<AttackInfo>();

        // SetAttackInfo postfix (after the infusion has set the attack up): the worn robe's damage and status bonus.
        internal static void ApplyToAttack(Attack atk)
        {
            PrimordialRobe r = Worn;
            if (r == null || atk == null || atk.atkInfo == null || !Infusion.IsPlayersAttack(atk)) return;
            AttackInfo info = atk.atkInfo;
            if (done.Contains(info)) return;
            Player p = info.entity as Player ?? Elites.Player0;
            Player.SkillState s = null;
            if (p != null && p.skillsDict != null && !string.IsNullOrEmpty(info.skillID)) p.skillsDict.TryGetValue(info.skillID, out s);
            if (!Matches(r, s)) return;
            done.Add(info);
            if (done.Count > 8192) done.Clear();
            info.damage = Mathf.Max(1, Mathf.RoundToInt(info.damage * r.Damage));
            if (r.Status <= 0f) return;
            switch (r.Element)
            {
                case "Fire": info.burnChance = Mathf.Min(1f, info.burnChance + r.Status); break;
                case "Water": info.freezeChance = Mathf.Min(1f, info.freezeChance + r.Status); break; // freeze is water's status (slow is Air's)
                case "Earth": info.poisonChance = Mathf.Min(1f, info.poisonChance + r.Status); break;
                case "Wind": info.slowChance = Mathf.Min(1f, info.slowChance + r.Status); break;
                case "Thunder": info.shockChance = Mathf.Min(1f, info.shockChance + r.Status); break;
                case "Chaos": info.chaosChance = Mathf.Min(1f, info.chaosChance + r.Status); break;
            }
        }
    }

    [HarmonyPatch(typeof(Attack), "SetAttackInfo", new Type[] { typeof(string), typeof(string), typeof(int), typeof(bool) })]
    [HarmonyPriority(Priority.Last)]
    static class PrimordialRobeAttackPatch
    {
        static void Postfix(Attack __instance) { try { PrimordialRobes.ApplyToAttack(__instance); } catch (Exception) { } }
    }

    // The worn robe on the player: look, aura, cooldowns (lives on the persistent runner object, like the Mantle).
    internal class PrimordialRobeWearer : MonoBehaviour
    {
        readonly Dictionary<Material, Texture> originals = new Dictionary<Material, Texture>();
        readonly Dictionary<string, StatData> cooldowns = new Dictionary<string, StatData>();
        NumVarStatMod cdMod;
        OverdriveEffects aura;
        Player owner;
        string current;
        float next;

        void Update()
        {
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + 1f;
            Player p = Elites.Player0;
            PrimordialRobe r = PrimordialRobes.Worn;
            if (owner != null && owner != p) Clear();
            if (r == null && GrandmasterRobe.Worn && p != null) // the Grandmaster's gold aura
            {
                if (current != "Grandmaster") Clear();
                owner = p; current = "Grandmaster";
                if (aura == null || aura.transform.parent != p.transform) aura = WhiteAura.Attach(p.transform, p.spriteRenderer != null ? p.spriteRenderer.material : null, new Color(1.5f, 1.3f, 0.7f, 0.45f));
                return;
            }
            if (r == null || p == null) { if (current != null) Clear(); return; }
            if (current != r.Prefix) Clear();
            owner = p;
            current = r.Prefix;
            if (cdMod == null) cdMod = new NumVarStatMod("HB_Primordial", r.Cooldown, 10, VarStatModType.Multiplicative, false);
            Recolour(p, r);
            if (aura == null || aura.transform.parent != p.transform)
            {
                Color c = ElementalGiants.ElementColor(r.Prefix);
                aura = WhiteAura.Attach(p.transform, p.spriteRenderer != null ? p.spriteRenderer.material : null, new Color(c.r * 1.5f, c.g * 1.5f, c.b * 1.5f, 0.45f));
            }
            UpdateCooldowns(p, r);
        }

        void Recolour(Player p, PrimordialRobe r)
        {
            string key = ArchonLook.RampForPrefix(r.Prefix);
            if (key == null) return; // Prism: the aura only
            foreach (SpriteRenderer sr in p.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sr == null || !RecolorEngine.PaletteSprite(sr)) continue;
                Material m = sr.material;
                Texture pal = m.GetTexture("_Palette");
                if (pal == null || pal.name.EndsWith("(" + key + ")")) continue;
                Texture2D twin = RecolorEngine.PaletteTwin(pal, key);
                if (twin == null) continue;
                if (!originals.ContainsKey(m)) originals[m] = pal;
                m.SetTexture("_Palette", twin);
            }
        }

        void UpdateCooldowns(Player p, PrimordialRobe r)
        {
            HashSet<string> now = new HashSet<string>();
            if (p.assignedSkills != null)
                foreach (Player.SkillState s in p.assignedSkills)
                    if (s != null && !string.IsNullOrEmpty(s.skillID) && PrimordialRobes.Matches(r, s)) now.Add(s.skillID);
            foreach (string id in now)
            {
                if (cooldowns.ContainsKey(id)) continue;
                try
                {
                    StatData d = StatManager.GetSkillData("Player", id);
                    if (d == null) continue;
                    d.ModifyMod(StatData.cdStr, cdMod, true);
                    cooldowns[id] = d;
                }
                catch (Exception) { }
            }
            foreach (string id in new List<string>(cooldowns.Keys))
                if (!now.Contains(id)) { try { cooldowns[id].ModifyMod(StatData.cdStr, cdMod, false); } catch (Exception) { } cooldowns.Remove(id); }
        }

        void Clear()
        {
            foreach (KeyValuePair<Material, Texture> kv in originals) if (kv.Key != null) kv.Key.SetTexture("_Palette", kv.Value);
            originals.Clear();
            if (cdMod != null) foreach (KeyValuePair<string, StatData> kv in cooldowns) { try { kv.Value.ModifyMod(StatData.cdStr, cdMod, false); } catch (Exception) { } }
            cooldowns.Clear();
            cdMod = null;
            if (aura != null) Destroy(aura.gameObject);
            aura = null;
            owner = null;
            current = null;
        }

        void OnDestroy() { Clear(); }
    }

    // v1.1.1: the Grandmaster's Robe, for reaching the Rival League's top rank: your hits on wizards (rivals, the Echo,
    // Archons, the Primordial Alchemist, the Council and Sura) deal +30% damage. A gold aura, no recolour.
    internal static class GrandmasterRobe
    {
        internal const string OutfitID = "AlchemicalGrandmaster";
        internal const string Name = "Grandmaster's Robe";
        internal const float WizardDamage = 1.3f;
        internal static readonly Color Gold = new Color(1f, 0.85f, 0.45f);
        internal static bool Unlocked { get { return League.RankIndex >= League.TopRank; } }
        internal static bool Worn { get; private set; }

        internal static void TryRegister()
        {
            if (!MantleRobe.LegendApiPresent) return;
            try { Register(); Plugin.Log.LogInfo("ROBES registered " + Name); }
            catch (Exception ex) { Plugin.Log.LogWarning("ROBES: " + Name + " not registered: " + ex.Message); }
        }

        static void Register()
        {
            List<OutfitModStat> mods = new List<OutfitModStat> { new OutfitModStat(LegendAPI.Outfits.CustomModType, 0f, 0f, 0f, false) };
            LegendAPI.Outfits.Register(new LegendAPI.OutfitInfo
            {
                outfit = new Outfit(OutfitID, 0, mods, false, false),
                name = Name,
                unlockCondition = () => Unlocked,
                customMod = (player, on, outfitChange, stat) => { Worn = on; Plugin.Log.LogInfo("ROBES " + Name + (on ? " worn" : " taken off")); },
                customDesc = (showNumbers, stat) => showNumbers
                    ? "Your attacks deal +" + Mathf.RoundToInt((WizardDamage - 1f) * 100f) + "% damage to wizards: rivals, Archons, the Council and Sura."
                    : "Only the league's Grandmaster wears gold. Every rival knows it.",
            });
        }

        internal static bool IsWizard(Entity e) { return e is Contestant || e is Boss; }
    }

    // The bonus is applied per hit (it depends on who is hit): the damage is raised for this TakeDamage and put back.
    [HarmonyPatch(typeof(Health), "TakeDamage")]
    static class GrandmasterRobeHitPatch
    {
        static void Prefix(Health __instance, AttackInfo __0, ref int __state)
        {
            __state = -1;
            try
            {
                if (!GrandmasterRobe.Worn || __0 == null || !(__0.entity is Player)) return;
                Entity target = __instance.GetComponent<Entity>() ?? __instance.GetComponentInParent<Entity>();
                if (!GrandmasterRobe.IsWizard(target)) return;
                __state = __0.damage;
                __0.damage = Mathf.Max(1, Mathf.RoundToInt(__0.damage * GrandmasterRobe.WizardDamage));
            }
            catch (Exception) { }
        }

        static void Postfix(AttackInfo __0, int __state) { if (__state >= 0 && __0 != null) __0.damage = __state; }
    }
}