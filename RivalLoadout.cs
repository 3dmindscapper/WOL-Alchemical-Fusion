using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace WolHybridBosses
{
    // Stat bonuses a rival gets from its robe and relic. Multipliers start at 1, additive stats at 0.
    internal class RivalBonus
    {
        public float Hp = 1f, Damage = 1f, Cooldown = 1f, Speed = 1f;
        public float Armor, Evade, Crit, CritDamage;

        public void Clamp()
        {
            Hp = Mathf.Clamp(Hp, 0.6f, 2f);
            Damage = Mathf.Clamp(Damage, 0.6f, 1.8f);
            Cooldown = Mathf.Clamp(Cooldown, 0.5f, 1.5f);
            Speed = Mathf.Clamp(Speed, 0.7f, 1.5f);
            Armor = Mathf.Clamp(Armor, 0f, 0.5f);
            Evade = Mathf.Clamp(Evade, 0f, 0.4f);
            Crit = Mathf.Clamp(Crit, 0f, 0.5f);
            CritDamage = Mathf.Clamp(CritDamage, 0f, 1f);
        }

        public string Describe()
        {
            List<string> parts = new List<string>();
            if (Hp != 1f) parts.Add("HP " + Pct(Hp));
            if (Damage != 1f) parts.Add("Damage " + Pct(Damage));
            if (Cooldown != 1f) parts.Add("Cooldowns " + Pct(Cooldown));
            if (Speed != 1f) parts.Add("Speed " + Pct(Speed));
            if (Armor > 0f) parts.Add("Armor +" + Mathf.RoundToInt(Armor * 100) + "%");
            if (Evade > 0f) parts.Add("Evade +" + Mathf.RoundToInt(Evade * 100) + "%");
            if (Crit > 0f) parts.Add("Crit +" + Mathf.RoundToInt(Crit * 100) + "%");
            if (CritDamage > 0f) parts.Add("Crit dmg +" + Mathf.RoundToInt(CritDamage * 100) + "%");
            return parts.Count == 0 ? "no stat bonus" : string.Join(", ", parts.ToArray());
        }

        static string Pct(float m) { int p = Mathf.RoundToInt((m - 1f) * 100f); return (p >= 0 ? "+" : "") + p + "%"; }
    }

    internal static class RivalLoadout
    {
        // ---------- robes ----------

        // Picks a random robe. Its colour row on the player sheet goes on the rival; its stat mods become bonuses.
        internal static bool PickOutfit(RivalBonus bonus, out string outfitID, out int colorIndex)
        {
            outfitID = null; colorIndex = -1;
            List<Outfit> list = null;
            try { list = Outfit.OutfitList; } catch (Exception) { }
            if (list == null || list.Count == 0)
            {
                try { list = new List<Outfit>(Outfit.OutfitDict.Values); } catch (Exception) { }
            }
            if (list == null || list.Count == 0) { Plugin.Log.LogWarning("RIVAL no outfits available"); return false; }

            Outfit o = list[UnityEngine.Random.Range(0, list.Count)];
            outfitID = o.outfitID;
            colorIndex = o.outfitColorIndex;
            if (o.modList != null) foreach (OutfitModStat m in o.modList) ApplyOutfitMod(bonus, m);
            return true;
        }

        // Robe mods store a magnitude plus a direction flag (isIncrease): Vigor's "+HP" came through as multi 0.4,
        // which the old code read as "x0.4" (the "HP -40%" seen in testing). Now: |v| <= 1 is a percentage in the
        // direction of isIncrease; v > 1 is a real multiplier; big flat values (HP) are scaled to a player's ~500 HP.
        static readonly AccessTools.FieldRef<OutfitModStat, bool> IsIncrease = AccessTools.FieldRefAccess<OutfitModStat, bool>("isIncrease");

        static float Factor(OutfitModStat m, float v)
        {
            float mag = Mathf.Abs(v);
            if (mag <= 0f) return 1f;
            if (mag <= 1f) return IsIncrease(m) ? 1f + mag : 1f - mag;
            if (mag <= 3f) return mag;                       // already a multiplier (e.g. 1.25)
            return IsIncrease(m) ? 1f + mag / 500f : 1f - mag / 500f; // flat amount (e.g. +100 HP)
        }

        static float Amount(OutfitModStat m, float v)
        {
            float f = Frac(Mathf.Abs(v));
            return IsIncrease(m) ? f : 0f; // decreases to armor/evade/crit don't apply below zero anyway
        }

        static void ApplyOutfitMod(RivalBonus b, OutfitModStat m)
        {
            if (m == null) return;
            float add = m.hasAddValue && m.addModifier != null ? m.addModifier.modValue : 0f;
            float multi = m.hasMultiValue && m.multiModifier != null ? m.multiModifier.modValue : 0f;
            float v = m.hasMultiValue && multi != 0f ? multi : add;
            Plugin.Log.LogInfo("RIVAL robe mod " + m.modType + " add " + add + " multi " + multi + " increase " + IsIncrease(m));
            switch (m.modType)
            {
                case OutfitModStat.OutfitModType.Health: b.Hp *= Factor(m, v); break;
                case OutfitModStat.OutfitModType.Damage: b.Damage *= Factor(m, v); break;
                case OutfitModStat.OutfitModType.Cooldown: b.Cooldown *= Factor(m, v); break;
                case OutfitModStat.OutfitModType.Speed:
                case OutfitModStat.OutfitModType.Run: b.Speed *= Factor(m, v); break;
                case OutfitModStat.OutfitModType.Evade: b.Evade += Amount(m, v); break;
                case OutfitModStat.OutfitModType.Armor: b.Armor += Amount(m, v); break;
                case OutfitModStat.OutfitModType.CritChance: b.Crit += Amount(m, v); break;
                case OutfitModStat.OutfitModType.CritDamage: b.CritDamage += Amount(m, v); break;
            }
        }

        static float Frac(float v) { return Mathf.Abs(v) > 1f ? v / 100f : v; } // 10 -> 0.10

        // ---------- relics ----------
        // Relic classes whose effect translates to a plain rival stat. The relic shown is the real one (icon, name).
        static readonly Dictionary<string, Action<RivalBonus>> RelicEffects = new Dictionary<string, Action<RivalBonus>>
        {
            { "DamageUp", b => b.Damage *= 1.15f },
            { "FlatDamage", b => b.Damage *= 1.1f },
            { "ElementalDamageUp", b => b.Damage *= 1.15f },
            { "ProjectileDamageUp", b => b.Damage *= 1.15f },
            { "SigEleDamageUp", b => b.Damage *= 1.1f },
            { "DamageUpVsBoss", b => b.Damage *= 1.1f },
            { "GlassCannon", b => { b.Damage *= 1.35f; b.Hp *= 0.8f; } },
            { "MaxHPDownDamageUpPercent", b => { b.Damage *= 1.25f; b.Hp *= 0.85f; } },
            { "HealthUp", b => b.Hp *= 1.2f },
            { "ShieldUp", b => b.Hp *= 1.15f },
            { "VampireSoul", b => b.Hp *= 1.15f },
            { "RegenTopHPPercent", b => b.Hp *= 1.15f },
            { "ArmorUp", b => b.Armor += 0.15f },
            { "ArmorUpDamageDown", b => { b.Armor += 0.25f; b.Damage *= 0.9f; } },
            { "EvadeUp", b => b.Evade += 0.12f },
            { "EvadeUpCritDown", b => b.Evade += 0.15f },
            { "CritChanceUp", b => b.Crit += 0.15f },
            { "SuperCritChance", b => b.Crit += 0.25f },
            { "CritDamageUp", b => b.CritDamage += 0.4f },
            { "MoveSpeedUp", b => b.Speed *= 1.15f },
            { "EnemyPlayerMoveSpeedUp", b => b.Speed *= 1.15f },
            { "ReduceCooldown", b => b.Cooldown *= 0.85f },
            { "ReduceCDReduceDamage", b => { b.Cooldown *= 0.75f; b.Damage *= 0.9f; } },
            { "AttackSpeedItem", b => b.Cooldown *= 0.9f },
            { "FireAuraItem", b => b.Damage *= 1.1f },
            { "FrostAura", b => b.Damage *= 1.1f },
            { "LightningAura", b => b.Damage *= 1.1f },
            { "PoisonAura", b => b.Damage *= 1.1f },
            { "WindAuraItem", b => b.Damage *= 1.1f },
        };

        static Dictionary<string, string> relicClassById; // relic ID -> Item class name (eligible ones only)
        static Dictionary<string, Action<RivalBonus>> relicEffectById; // v0.9.20: relic ID -> its effect on a rival

        // v0.9.20 / v0.9.21: every relic a player can find (cursed ones too; not Hub-only, not a set piece or set) is usable
        // by rivals. v0.9.21: the relics whose code only touches their holder (armour, evade, resistances, move speed,
        // shields... 29 found by scanning every relic class for reads of the player, the shop / wallet / loot, global
        // statics or the shared skill-category stats) are put in the rival's own inventory and run for real, exactly as on
        // the player (minus the max-HP ones, which the rival's HP override would cancel). The rest would buff the player or
        // every enemy if run on a rival, so they keep a stat translation: their exact match (RelicEffects), else a smaller
        // bonus from what their name says they do, else from their category. Cursed relics add their trade-off: harder
        // hits, less health.
        static readonly HashSet<string> RealOnRival = new HashSet<string>
        {
            "AirResistanceUp", "ArmorHPUpWithCursedRelics", "ArmorUp", "ArmorUpWithInventoryCount", "CapDamageItem", "CritHealChanceUp",
            "CritHealChanceUpLow", "DefenseUpOnTakeDamage", "DoctorPrescription", "EarthResistanceUp", "ElementalResistanceUp",
            "ElementalResistanceUpOnHurt", "EvadeCrit", "EvadeUp", "EvadeUpEnemyDamageUp", "FireResistanceUp", "HeadgearItem", "IgnorePits",
            "LightningResistanceUp", "MoveSpeedUp", "OnTakeDamageItem", "OverhealGrantsShield", "ReduceHitStun", "SequenceDefense", "ShieldUp",
            "ShockShield", "WaterResistanceUp",
        };
        static HashSet<string> realIds = new HashSet<string>(), cursedIds = new HashSet<string>();

        internal static bool IsReal(string id) { return id != null && realIds.Contains(id); }
        internal static bool IsCursed(string id) { return id != null && cursedIds.Contains(id); }

        static Action<RivalBonus> EffectFor(string cls, Item.Category cat, bool cursed)
        {
            Action<RivalBonus> baseEffect = BaseEffect(cls, cat);
            if (!cursed) return baseEffect;
            return b => { baseEffect(b); b.Damage *= 1.15f; b.Hp *= 0.88f; };
        }

        static Action<RivalBonus> BaseEffect(string cls, Item.Category cat)
        {
            Action<RivalBonus> a;
            if (RelicEffects.TryGetValue(cls, out a)) return a;
            string n = cls;
            if (n.Contains("Resistance") || n.Contains("Armor") || n.Contains("Defense") || n.Contains("Shield") || n.Contains("Guard") || n.Contains("Immune")) return b => b.Armor += 0.08f;
            if (n.Contains("Evade") || n.Contains("Parry") || n.Contains("Invulnerab")) return b => b.Evade += 0.08f;
            if (n.Contains("Crit")) return b => b.Crit += 0.1f;
            if (n.Contains("Health") || n.Contains("HP") || n.Contains("Regen") || n.Contains("Heal") || n.Contains("Vampir") || n.Contains("Resurrect")) return b => b.Hp *= 1.1f;
            if (n.Contains("Cooldown") || n.Contains("CD") || n.Contains("Overdrive") || n.Contains("Charge") || n.Contains("Combo")) return b => b.Cooldown *= 0.92f;
            if (n.Contains("Speed") || n.Contains("Dash") || n.Contains("Move")) return b => b.Speed *= 1.08f;
            if (n.Contains("Damage") || n.Contains("Aura") || n.Contains("Chance") || n.Contains("Level") || n.Contains("Explosion") || n.Contains("Bomb") || n.Contains("Stanza")) return b => b.Damage *= 1.08f;
            switch (cat)
            {
                case Item.Category.Offense: return b => b.Damage *= 1.06f;
                case Item.Category.Defense: return b => b.Hp *= 1.08f;
                default: return b => b.Cooldown *= 0.95f;
            }
        }

        static void ApplyRelic(RivalBonus bonus, string id)
        {
            if (IsReal(id)) { if (IsCursed(id)) { bonus.Damage *= 1.15f; bonus.Hp *= 0.88f; } return; } // runs for real (EquipReal)
            Action<RivalBonus> a;
            if (relicEffectById != null && relicEffectById.TryGetValue(id, out a)) a(bonus);
        }

        static void BuildRelicTable()
        {
            relicClassById = new Dictionary<string, string>();
            relicEffectById = new Dictionary<string, Action<RivalBonus>>();
            realIds = new HashSet<string>();
            cursedIds = new HashSet<string>();
            IEnumerable ids = null;
            try { ids = Traverse.Create(typeof(LootManager)).Field("availableItemIDList").GetValue() as IEnumerable; } catch (Exception) { }
            if (ids == null || !ids.GetEnumerator().MoveNext())
            {
                try
                {
                    IDictionary all = Traverse.Create(typeof(LootManager)).Field("completeItemDict").GetValue() as IDictionary;
                    if (all != null) ids = all.Keys;
                }
                catch (Exception) { }
            }
            if (ids == null) return;
            foreach (object o in ids)
            {
                string id = o as string;
                if (string.IsNullOrEmpty(id)) continue;
                try
                {
                    Item it = Item.CreateItem(id);
                    if (it == null || it.hubOnly || it.combineOnly || it.isGroupItem) continue;
                    string cls = it.GetType().Name;
                    bool cursed = it.isCursed || Item.IsCursedItem(id);
                    relicClassById[id] = cls;
                    relicEffectById[id] = EffectFor(cls, it.category, cursed);
                    if (cursed) cursedIds.Add(id);
                    if (RealOnRival.Contains(cls)) realIds.Add(id);
                }
                catch (Exception) { }
            }
            Plugin.Log.LogInfo("RIVAL relic pool: " + relicClassById.Count + " relics (" + realIds.Count + " run for real on the rival, " + cursedIds.Count + " cursed)");
        }

        // v0.9.0 Echo: a given robe (the player's last run), with its colour row and stat mods.
        internal static bool UseOutfit(RivalBonus bonus, string id, out string outfitID, out int colorIndex)
        {
            outfitID = null; colorIndex = -1;
            if (string.IsNullOrEmpty(id)) return false;
            Outfit o = null;
            try { if (Outfit.OutfitDict != null && Outfit.OutfitDict.ContainsKey(id)) o = Outfit.OutfitDict[id]; } catch (Exception) { }
            if (o == null) try { foreach (Outfit x in Outfit.OutfitList) if (x != null && x.outfitID == id) { o = x; break; } } catch (Exception) { }
            if (o == null) return false;
            outfitID = o.outfitID;
            colorIndex = o.outfitColorIndex;
            if (o.modList != null) foreach (OutfitModStat m in o.modList) ApplyOutfitMod(bonus, m);
            return true;
        }

        // v0.9.0 Echo: the given relics (up to 4); returns the ones used (the first shows on the intro).
        internal static List<string> UseRelics(RivalBonus bonus, List<string> ids)
        {
            List<string> used = new List<string>();
            if (relicClassById == null || relicClassById.Count == 0) BuildRelicTable();
            if (relicClassById == null || ids == null) return used;
            foreach (string id in ids)
            {
                if (used.Count >= 4 || id == null || !relicClassById.ContainsKey(id) || used.Contains(id)) continue;
                ApplyRelic(bonus, id);
                used.Add(id);
            }
            return used;
        }

        // v0.9.21: the holder-only relics go into the rival's own inventory (Entity.inventory), where the game activates
        // them on it as it would on the player. Called once its stats and HP are set.
        internal static void EquipReal(Enemy e, List<string> ids)
        {
            if (e == null || ids == null || e.inventory == null) return;
            foreach (string id in ids)
            {
                if (!IsReal(id)) continue;
                try { e.inventory.AddItem(id, false, false); Plugin.Log.LogInfo("RIVAL wears " + id + " (real relic)"); }
                catch (Exception ex) { Plugin.Log.LogWarning("RIVAL could not wear " + id + ": " + ex.Message); }
            }
        }

        internal static string PickRelic(RivalBonus bonus)
        {
            if (relicClassById == null || relicClassById.Count == 0) BuildRelicTable();
            if (relicClassById == null || relicClassById.Count == 0) return null;
            List<string> ids = new List<string>(relicClassById.Keys);
            string id = ids[UnityEngine.Random.Range(0, ids.Count)];
            ApplyRelic(bonus, id);
            return id;
        }

        // ---------- applying to the rival ----------

        internal static void ApplyStats(Enemy e, RivalBonus b)
        {
            if (b.Armor > 0f) TryAddMod(e.health == null ? null : e.health.armorStat, "RivalArmor", b.Armor, VarStatModType.Additive);
            if (b.Evade > 0f) TryAddMod(e.health == null ? null : e.health.evadeStat, "RivalEvade", b.Evade, VarStatModType.Additive);
            if (b.Speed != 1f) TryAddMod(e.movement == null ? null : e.movement.moveSpeedStat, "RivalSpeed", b.Speed, VarStatModType.Multiplicative);
        }

        static void TryAddMod(object stat, string id, float value, VarStatModType type)
        {
            if (stat == null) return;
            try { Traverse.Create(stat).Method("AddMod", new object[] { new NumVarStatMod(id, value, 10, type, false) }).GetValue(); }
            catch (Exception ex) { Plugin.Log.LogWarning("RIVAL could not apply " + id + ": " + ex.Message); }
        }

        // Sets the robe's colour row on the rival's (player-sheet) sprite. Contestant.InitElement uses row 24.
        internal static void ApplyRobeColor(Enemy e, int colorIndex)
        {
            if (colorIndex < 0 || e.spriteRenderer == null) return;
            Material m = e.spriteRenderer.material;
            if (m != null && m.HasProperty("_PaletteIndex")) m.SetFloat("_PaletteIndex", colorIndex);
        }
    }
}
