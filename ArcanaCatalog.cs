using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace WolHybridBosses
{
    // How the player's version of the arcana works, from its SkillState base class.
    internal enum ArcanaFamily { Projectile, Spell, Dash, Jump, Melee, Minion, Seeker, Ward, Excluded }

    // What the main prefab is (from its class chain).
    internal enum PrefabKind { Projectile, Wave, Minion, Seeker, Ward, Other }

    // One row of the generated vanilla table (tools/gencatalog.ps1).
    internal class GenArcana
    {
        public readonly string SkillID, ClassName, PrefabClass, PrefabPath;
        public readonly ArcanaFamily Family;
        public readonly PrefabKind Kind;
        public readonly bool ChainShots, Fan;
        public readonly string[] AllPrefabs;

        public GenArcana(string skillID, string className, ArcanaFamily family, string prefabClass, string prefabPath,
            PrefabKind kind, bool chainShots, bool fan, string[] allPrefabs)
        {
            SkillID = skillID; ClassName = className; Family = family; PrefabClass = prefabClass; PrefabPath = prefabPath;
            Kind = kind; ChainShots = chainShots; Fan = fan; AllPrefabs = allPrefabs ?? new string[0];
        }
    }

    // Every player arcana an enemy can cast: the generated vanilla table, other mods' arcana found at runtime,
    // each element read from the arcana's own stat data, and hand-tuned overrides where they exist.
    internal static partial class ArcanaCatalog
    {
        static List<ArcanaDef> all;
        static readonly Dictionary<string, ArcanaDef> byId = new Dictionary<string, ArcanaDef>();
        internal static readonly List<GenArcana> Modded = new List<GenArcana>();

        internal static IEnumerable<GenArcana> AllRows()
        {
            foreach (GenArcana g in Vanilla) yield return g;
            foreach (GenArcana g in Modded) yield return g;
        }

        // Every prefab class any arcana references: these get the ownership patches (OwnerContext).
        internal static IEnumerable<string> AllPrefabClasses()
        {
            HashSet<string> seen = new HashSet<string>();
            foreach (GenArcana g in AllRows())
            {
                if (g.PrefabClass != null && seen.Add(g.PrefabClass)) yield return g.PrefabClass;
                foreach (string p in g.AllPrefabs) if (seen.Add(p)) yield return p;
            }
        }

        internal static ArcanaDef Get(string skillID)
        {
            EnsureBuilt();
            ArcanaDef d;
            return byId.TryGetValue(skillID, out d) ? d : null;
        }

        // Arcana themed for an element prefix (Flame/Frost/Earth/Wind/Thunder), or every element for "Chaos".
        // Dash arcana are not in this pool: they fire from the caster's own dashes (DashArcana).
        internal static List<ArcanaDef> Pool(string prefix) { return Filter(prefix, false); }

        // Dash arcana of an element, for the caster's dash.
        internal static List<ArcanaDef> DashPool(string prefix) { return Filter(prefix, true); }

        // Arcana whose own element is exactly this one ("Chaos" here means Chaos-element arcana only, not every element).
        internal static List<ArcanaDef> PoolExact(string element)
        {
            List<ArcanaDef> list = new List<ArcanaDef>();
            foreach (ArcanaDef d in Filter("Chaos", false)) if (d.Element == element) list.Add(d);
            return list;
        }

        static List<ArcanaDef> Filter(string prefix, bool dashes)
        {
            EnsureBuilt();
            List<ArcanaDef> list = new List<ArcanaDef>();
            if (all == null) return list;
            HashSet<ArcanaFamily> off = ExcludedFamilies();
            foreach (ArcanaDef d in all)
            {
                if (EnemyArcana.Broken.Contains(d.SkillID) || off.Contains(d.Family)) continue;
                if ((d.Family == ArcanaFamily.Dash) != dashes) continue;
                if (!d.Castable) continue;
                if (prefix == "Chaos" || d.Element == prefix) list.Add(d);
            }
            return list;
        }

        static HashSet<ArcanaFamily> ExcludedFamilies()
        {
            HashSet<ArcanaFamily> set = new HashSet<ArcanaFamily> { ArcanaFamily.Excluded };
            foreach (string s in Plugin.ArcanaExcludeFamilies.Value.Split(',', ';', ' '))
            {
                if (s.Length == 0) continue;
                try { set.Add((ArcanaFamily)Enum.Parse(typeof(ArcanaFamily), s.Trim(), true)); } catch (Exception) { }
            }
            return set;
        }

        // ---------- building ----------

        static void EnsureBuilt()
        {
            if (all != null) return;
            Entity player = GameController.playerScripts != null && GameController.playerScripts.Length > 0 ? GameController.playerScripts[0] : null;
            if (player == null) return; // elements come from player stat data; try again once a run has started

            all = new List<ArcanaDef>();
            Dictionary<string, int> perElement = new Dictionary<string, int>();
            foreach (GenArcana g in AllRows())
            {
                if (g.Family == ArcanaFamily.Excluded || (g.PrefabClass == null && g.PrefabPath == null)) continue;
                if (byId.ContainsKey(g.SkillID)) continue;
                string element = ElementOf(player, g.SkillID);
                if (element == null) continue; // not a real player skill (sub-attack IDs etc.)

                ArcanaDef d;
                ArcanaDef tuned;
                if (EnemyArcana.Tuned.TryGetValue(g.SkillID, out tuned))
                {
                    d = tuned;
                    d.Element = element;
                    d.Family = g.Family;
                }
                else d = FromRow(g, element);
                all.Add(d);
                byId[d.SkillID] = d;
                int n; perElement.TryGetValue(element, out n); perElement[element] = n + 1;
            }
            // v0.6.3: Arcanos' Radiant / Prism arcana rebuilt for enemies (ArcanosArts). They replace any prefab-based
            // entry of the same arcana.
            try
            {
                foreach (ArcanaDef port in ArcanosArts.Defs())
                {
                    ArcanaDef old;
                    if (byId.TryGetValue(port.SkillID, out old)) { all.Remove(old); int o; perElement.TryGetValue(old.Element, out o); perElement[old.Element] = o - 1; }
                    all.Add(port);
                    byId[port.SkillID] = port;
                    int n; perElement.TryGetValue(port.Element, out n); perElement[port.Element] = n + 1;
                }
            }
            catch (Exception ex) { Plugin.Log.LogWarning("ARCANA Arcanos ports failed: " + ex.Message); }
            List<string> parts = new List<string>();
            foreach (KeyValuePair<string, int> kv in perElement) parts.Add(kv.Key + " " + kv.Value);
            Plugin.Log.LogInfo("ARCANA catalog: " + all.Count + " castable arcana (" + string.Join(", ", parts.ToArray()) + "), " + Modded.Count + " from other mods");
            try { LogVariantArcana(player); } catch (Exception ex) { Plugin.Log.LogWarning("ARCANA variant report failed: " + ex.Message); }
        }

        // v0.6.2: which of Arcanos' Radiant / Prism arcana enemies can cast, and why the others can't. Most are built
        // on Arcanos' own state classes (no prefab our enemy caster can fire), so variant casters fill in with infused
        // base arcana.
        static void LogVariantArcana(Entity player)
        {
            if (!ArcanosBridge.Present) return;
            foreach (StageVariant v in new[] { StageVariant.Radiant, StageVariant.Prism })
            {
                List<string> ids = ArcanosBridge.IdsOf(v);
                List<string> rows = new List<string>();
                int castable = 0;
                foreach (string id in ids)
                {
                    string why;
                    if (byId.ContainsKey(id)) { why = "castable"; castable++; }
                    else
                    {
                        GenArcana g = Modded.Find(m => m.SkillID == id);
                        if (g == null) why = "not found as a skill state";
                        else if (g.Family == ArcanaFamily.Excluded) why = "excluded kind";
                        else if (g.PrefabClass == null && g.PrefabPath == null) why = "no prefab (custom Arcanos state)";
                        else if (ElementOf(player, id) == null) why = "no player stat data";
                        else why = "filtered";
                    }
                    rows.Add(id + " (" + why + ")");
                }
                Plugin.Log.LogInfo("ARCANA " + v + ": " + ids.Count + " in Arcanos, " + castable + " castable by enemies: " + string.Join(", ", rows.ToArray()));
            }
        }

        static ArcanaDef FromRow(GenArcana g, string element)
        {
            Delivery del;
            switch (g.Family)
            {
                case ArcanaFamily.Minion:
                case ArcanaFamily.Seeker: del = Delivery.Summon; break;
                case ArcanaFamily.Ward: del = Delivery.Ward; break;
                case ArcanaFamily.Jump: del = Delivery.AtTarget; break;
                case ArcanaFamily.Dash: del = g.Kind == PrefabKind.Wave ? Delivery.Wave : Delivery.AtSelf; break;
                case ArcanaFamily.Melee: del = g.Kind == PrefabKind.Projectile ? Delivery.Projectile : g.Kind == PrefabKind.Wave ? Delivery.Wave : Delivery.Melee; break;
                default:
                    if (g.Kind == PrefabKind.Projectile) del = Delivery.Projectile;
                    else if (g.Kind == PrefabKind.Wave) del = Delivery.Wave;
                    else del = SelfCentred(g.SkillID) ? Delivery.AtSelf : Delivery.AtTarget;
                    break;
            }
            ArcanaDef d = new ArcanaDef(element, g.SkillID, g.PrefabClass, del);
            d.PrefabPath = g.PrefabPath;
            d.Family = g.Family;
            switch (del)
            {
                case Delivery.Projectile: d.Windup = 0.6f; d.Cooldown = 7f; d.MinRange = 2f; d.MaxRange = 10f; d.Priority = 5; break;
                case Delivery.Wave: d.Windup = 0.7f; d.Cooldown = 8f; d.MinRange = 0f; d.MaxRange = 9f; d.Priority = 5; break;
                case Delivery.AtTarget: d.Windup = 0.9f; d.Cooldown = 9f; d.MinRange = 1f; d.MaxRange = 9f; d.Priority = 6; break;
                case Delivery.AtSelf: d.Windup = 0.8f; d.Cooldown = 9f; d.MinRange = 0f; d.MaxRange = 4f; d.Priority = 7; break;
                case Delivery.Melee: d.Windup = 0.5f; d.Cooldown = 5f; d.MinRange = 0f; d.MaxRange = 3f; d.Priority = 4; break;
                case Delivery.Summon: d.Windup = 1f; d.Cooldown = 14f; d.MinRange = 0f; d.MaxRange = 14f; d.Priority = 6; break;
                case Delivery.Ward: d.Windup = 0.8f; d.Cooldown = 15f; d.MinRange = 0f; d.MaxRange = 7f; d.Priority = 7; break;
            }
            if (g.Family == ArcanaFamily.Melee) d.Cooldown = 4f; // basics: quick and light
            if (g.Family == ArcanaFamily.Dash) d.Cooldown = 2.5f; // per dash, not per AI pick
            if (g.Family == ArcanaFamily.Seeker) { d.Shots = 3; d.Cooldown = 12f; }
            else if (g.Fan) { d.Shots = 3; d.Spread = 30f; }
            else if (g.ChainShots) { d.Shots = 3; d.ShotDelay = 0.18f; }

            // Summons run as native enemy minions/seekers with the matching elemental Summoner's enemy stats
            // ("FlameSummonerMinion" etc.); without such stats (Chaos minion, Skeleton, Turret) they're not castable yet.
            if (del == Delivery.Summon)
            {
                string suffix = g.Family == ArcanaFamily.Minion ? "SummonerMinion" : "SummonerSeeker";
                d.EnemyStatID = element + suffix;
                d.Castable = (g.Kind == PrefabKind.Minion || g.Kind == PrefabKind.Seeker) && EnemyStatExists(d.EnemyStatID);
            }
            return d;
        }

        static bool EnemyStatExists(string id)
        {
            try { return StatManager.GetSkillData(Enemy.globalEnemySkillCategory, id) != null; } catch (Exception) { return false; }
        }

        static bool SelfCentred(string id)
        {
            foreach (string w in new[] { "Ring", "Nova", "Aura", "Shield", "Burner", "Swirl", "Spin", "Whirl", "Orbital", "Field", "Armor", "Wheel", "Defense", "Cooling" })
                if (id.Contains(w)) return true;
            return false;
        }

        // The arcana's element from its own stat data, mapped to this mod's prefixes. Null if it has no player data.
        static string ElementOf(Entity player, string skillID)
        {
            try
            {
                StatData sd = StatManager.GetSkillData("Player", skillID);
                if (sd == null) return null;
                AttackInfo ai = AttackInfo.GetInfoFromData(player, sd, 1, false);
                if (ai == null) return null;
                // v0.6.0: WoL Arcanos' Radiant ("Fulgor") and Prism ("Prisma") arcana, by its element or its own filing.
                string variant = ArcanosBridge.PrefixOf(skillID, ai.elementType);
                if (variant != null) return variant;
                switch (ai.elementType)
                {
                    case ElementType.Fire: return "Flame";
                    case ElementType.Water:
                    case ElementType.Ice: return "Frost";
                    case ElementType.Earth:
                    case ElementType.Plant:
                    case ElementType.Poison: return "Earth";
                    case ElementType.Air: return "Wind";
                    case ElementType.Lightning: return "Thunder";
                    case ElementType.Chaos: return "Chaos";
                    default: return "Neutral";
                }
            }
            catch (Exception) { return null; }
        }

        // ---------- other mods' arcana ----------

        // Finds Player.SkillState subclasses in other assemblies (LegendAPI skills etc.), reads their staticID and scans
        // their code for prefabs the same way the generator does. Call before patching so their prefabs get ownership too.
        internal static void DiscoverModded()
        {
            Type baseT = typeof(Player.SkillState);
            Assembly game = baseT.Assembly;
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm == game || asm == typeof(Plugin).Assembly) continue;
                string an = asm.GetName().Name;
                if (an.StartsWith("System") || an.StartsWith("Unity") || an.StartsWith("mscorlib") || an.StartsWith("Mono") || an.StartsWith("BepInEx") || an.StartsWith("0Harmony") || an.StartsWith("MMHOOK")) continue;
                Type[] types;
                try { types = asm.GetTypes(); } catch (ReflectionTypeLoadException ex) { types = ex.Types; } catch (Exception) { continue; }
                foreach (Type t in types)
                {
                    if (t == null || t.IsAbstract || !baseT.IsAssignableFrom(t)) continue;
                    try
                    {
                        FieldInfo sid = t.GetField("staticID", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                        string id = sid != null ? sid.GetValue(null) as string : null;
                        if (string.IsNullOrEmpty(id)) continue;
                        List<string> prefabs = IlScan.PrefabClasses(t);
                        string primary = prefabs.Count > 0 ? prefabs[0] : null;
                        Modded.Add(new GenArcana(id, t.Name, FamilyOf(t), primary, null, KindOf(primary), false, false, prefabs.ToArray()));
                    }
                    catch (Exception ex) { Plugin.Log.LogWarning("ARCANA could not read modded arcana " + t.FullName + ": " + ex.Message); }
                }
            }
            if (Modded.Count > 0) Plugin.Log.LogInfo("ARCANA found " + Modded.Count + " arcana from other mods");
        }

        static ArcanaFamily FamilyOf(Type t)
        {
            for (Type b = t; b != null; b = b.BaseType)
            {
                switch (b.Name)
                {
                    case "ProjectileAttackState": return ArcanaFamily.Projectile;
                    case "BaseDashState": return ArcanaFamily.Dash;
                    case "BaseJumpState": return ArcanaFamily.Jump;
                    case "MeleeAttackState": return ArcanaFamily.Melee;
                    case "UseBaseMinion": return ArcanaFamily.Minion;
                    case "UseSeekers": return ArcanaFamily.Seeker;
                    case "BaseWardState": return ArcanaFamily.Ward;
                    case "UseBuffSkill": return ArcanaFamily.Excluded;
                }
            }
            return ArcanaFamily.Spell;
        }

        static PrefabKind KindOf(string cls)
        {
            Type t = cls == null ? null : AccessTools.TypeByName(cls);
            for (Type b = t; b != null; b = b.BaseType)
            {
                if (b.Name == "BasicMinion") return PrefabKind.Minion;
                if (b.Name == "Seeker") return PrefabKind.Seeker;
                if (b.Name == "Ward") return PrefabKind.Ward;
                if (b.Name == "Wave") return PrefabKind.Wave;
                if (b.Name == "Projectile") return PrefabKind.Projectile;
            }
            return PrefabKind.Other;
        }
    }

    // Minimal IL reader for runtime discovery: which classes' static Prefab getters a type's code calls.
    internal static class IlScan
    {
        static Dictionary<short, OpCode> ops;

        static void Init()
        {
            if (ops != null) return;
            ops = new Dictionary<short, OpCode>();
            foreach (FieldInfo f in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                OpCode op = (OpCode)f.GetValue(null);
                ops[op.Value] = op;
            }
        }

        internal static List<string> PrefabClasses(Type t)
        {
            Init();
            List<string> found = new List<string>();
            List<MethodBase> methods = new List<MethodBase>();
            BindingFlags bf = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            methods.AddRange(t.GetMethods(bf));
            methods.AddRange(t.GetConstructors(bf));
            foreach (Type nt in t.GetNestedTypes(bf)) methods.AddRange(nt.GetMethods(bf));
            foreach (MethodBase m in methods)
            {
                byte[] il;
                try { MethodBody body = m.GetMethodBody(); if (body == null) continue; il = body.GetILAsByteArray(); } catch (Exception) { continue; }
                int i = 0;
                while (i < il.Length)
                {
                    short code = il[i++];
                    if (code == 0xFE && i < il.Length) code = (short)(0xFE00 | il[i++]);
                    OpCode op;
                    if (!ops.TryGetValue(code, out op)) break;
                    int size = OperandSize(op, il, i);
                    if ((op == OpCodes.Call || op == OpCodes.Callvirt) && size == 4)
                    {
                        try
                        {
                            MethodBase target = m.Module.ResolveMethod(BitConverter.ToInt32(il, i));
                            if (target != null && target.Name == "get_Prefab" && target.DeclaringType != null && !found.Contains(target.DeclaringType.Name))
                                found.Add(target.DeclaringType.Name);
                        }
                        catch (Exception) { }
                    }
                    i += size;
                }
            }
            return found;
        }

        static int OperandSize(OpCode op, byte[] il, int i)
        {
            switch (op.OperandType)
            {
                case OperandType.InlineNone: return 0;
                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar: return 1;
                case OperandType.InlineVar: return 2;
                case OperandType.InlineI8:
                case OperandType.InlineR: return 8;
                case OperandType.InlineSwitch: return 4 + 4 * BitConverter.ToInt32(il, i);
                default: return 4;
            }
        }
    }
}
