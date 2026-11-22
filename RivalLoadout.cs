using System;
using System.Collections;
using System.Collections.Generic;
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

        static void BuildRelicTable()
        {
            relicClassById = new Dictionary<string, string>();
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
                    if (Item.IsCursedItem(id)) continue;
                    Item it = Item.CreateItem(id);
                    if (it == null) continue;
                    string cls = it.GetType().Name;
                    if (RelicEffects.ContainsKey(cls)) relicClassById[id] = cls;
                }
                catch (Exception) { }
            }
            Plugin.Log.LogInfo("RIVAL relic pool: " + relicClassById.Count + " stat relics");
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

        // v0.9.0 Echo: the given relics that translate to rival stats (up to 4); returns the first one, for the intro.
        internal static string UseRelics(RivalBonus bonus, List<string> ids)
        {
            if (relicClassById == null || relicClassById.Count == 0) BuildRelicTable();
            if (relicClassById == null || ids == null) return null;
            string first = null;
            int used = 0;
            foreach (string id in ids)
            {
                string cls;
                if (used >= 4 || id == null || !relicClassById.TryGetValue(id, out cls)) continue;
                RelicEffects[cls](bonus);
                if (first == null) first = id;
                used++;
            }
            return first;
        }

        internal static string PickRelic(RivalBonus bonus)
        {
            if (relicClassById == null || relicClassById.Count == 0) BuildRelicTable();
            if (relicClassById == null || relicClassById.Count == 0) return null;
            List<string> ids = new List<string>(relicClassById.Keys);
            string id = ids[UnityEngine.Random.Range(0, ids.Count)];
            RelicEffects[relicClassById[id]](bonus);
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
