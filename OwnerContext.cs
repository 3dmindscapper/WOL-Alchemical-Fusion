using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace WolHybridBosses
{
    // Generic ownership for player arcana cast by enemies. Player arcana prefabs are written for the player: they look
    // for targets in GameController.enemies, spawn follow-up hits, and load "Player" stats. While any code of an
    // enemy-owned arcana object runs (its Unity callbacks or coroutines), this context:
    //   - swaps GameController.enemies and GameController.players, so its targeting aims at the players;
    //   - tags anything it spawns (Awake/OnEnable of patched classes, Attack.Awake) with the same owner;
    //   - lets the SetAttackInfo patches re-own every "Player" stat load to the caster.
    internal static class OwnerContext
    {
        static readonly List<Enemy> stack = new List<Enemy>();
        static int swapDepth;
        static FieldInfo enemiesField, playersField;

        internal static Enemy Current { get { return stack.Count > 0 ? stack[stack.Count - 1] : null; } }

        internal static void Enter(Enemy owner, bool swapTargets)
        {
            stack.Add(owner);
            if (!swapTargets) return;
            if (swapDepth++ == 0) Swap();
        }

        internal static void Exit(bool swapTargets)
        {
            if (stack.Count > 0) stack.RemoveAt(stack.Count - 1);
            if (!swapTargets) return;
            if (swapDepth > 0 && --swapDepth == 0) Swap();
        }

        static void Swap()
        {
            if (enemiesField == null)
            {
                enemiesField = AccessTools.Field(typeof(GameController), "enemies");
                playersField = AccessTools.Field(typeof(GameController), "players");
            }
            if (enemiesField == null || playersField == null || enemiesField.FieldType != playersField.FieldType) return;
            object e = enemiesField.GetValue(null);
            enemiesField.SetValue(null, playersField.GetValue(null));
            playersField.SetValue(null, e);
        }

        internal static Enemy OwnerOf(Component c)
        {
            if (c == null) return null;
            ArcanaOwned tag = c.GetComponentInParent<ArcanaOwned>();
            return tag != null && tag.Owner != null ? tag.Owner : null;
        }

        internal static void Tag(GameObject go, Enemy owner)
        {
            if (go == null || owner == null) return;
            ArcanaOwned tag = go.GetComponent<ArcanaOwned>() ?? go.AddComponent<ArcanaOwned>();
            tag.Owner = owner;
        }

        // ---------- dynamic patches on every arcana prefab class ----------

        static readonly HashSet<string> UnityMessages = new HashSet<string>
        {
            "Awake", "Start", "OnEnable", "Update", "FixedUpdate", "LateUpdate",
            "OnTriggerEnter2D", "OnTriggerStay2D", "OnTriggerExit2D", "OnCollisionEnter2D", "OnCollisionStay2D",
        };

        static readonly HashSet<string> Shared = new HashSet<string> { "Projectile", "Wave", "ThrownItem" };

        internal static void PatchPrefabClasses(Harmony harmony)
        {
            Stopwatch sw = Stopwatch.StartNew();
            HashSet<MethodBase> done = new HashSet<MethodBase>();
            HarmonyMethod pre = new HarmonyMethod(typeof(OwnerContext), "Pre");
            HarmonyMethod fin = new HarmonyMethod(typeof(OwnerContext), "Fin");
            int classes = 0;
            List<string> names = new List<string>(ArcanaCatalog.AllPrefabClasses());
            names.AddRange(Shared);
            foreach (string cn in names)
            {
                Type t = AccessTools.TypeByName(cn);
                if (t == null || !typeof(MonoBehaviour).IsAssignableFrom(t)) continue;
                classes++;
                for (Type b = t; b != null && b != typeof(MonoBehaviour); b = b.BaseType)
                {
                    if (b.Assembly != t.Assembly) break;
                    foreach (MethodInfo m in b.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                        if (UnityMessages.Contains(m.Name) && !m.IsAbstract && !m.ContainsGenericParameters) TryPatch(harmony, m, pre, fin, done);
                    foreach (Type nt in b.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
                    {
                        if (!nt.Name.StartsWith("<")) continue; // compiler-generated coroutine iterators
                        MethodInfo mn = nt.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (mn != null) TryPatch(harmony, mn, pre, fin, done);
                    }
                }
            }
            Plugin.Log.LogInfo("ARCANA ownership patches: " + done.Count + " methods on " + classes + " prefab classes (" + sw.ElapsedMilliseconds + " ms)");
        }

        static void TryPatch(Harmony h, MethodBase m, HarmonyMethod pre, HarmonyMethod fin, HashSet<MethodBase> done)
        {
            if (!done.Add(m)) return;
            try { h.Patch(m, prefix: pre, finalizer: fin); }
            catch (Exception ex) { Plugin.Log.LogWarning("ARCANA could not patch " + m.DeclaringType + "." + m.Name + ": " + ex.Message); }
        }

        // Component behind a patched method: the instance itself, or the "this" captured by a coroutine iterator.
        static Component AsComponent(object instance)
        {
            Component c = instance as Component;
            if (c != null) return c;
            if (instance == null) return null;
            Traverse t = Traverse.Create(instance);
            foreach (string f in new[] { "$this", "<>f__this", "<>4__this" })
            {
                Traverse ft = t.Field(f);
                if (ft.FieldExists()) return ft.GetValue() as Component;
            }
            return null;
        }

        static void Pre(object __instance, MethodBase __originalMethod, out bool __state)
        {
            __state = false;
            try
            {
                Component c = AsComponent(__instance);
                if (c == null) return;
                Enemy owner = OwnerOf(c);
                if (owner == null && Current != null && (__originalMethod.Name == "Awake" || __originalMethod.Name == "OnEnable"))
                {
                    owner = Current; // spawned while an owned object's code (or our cast) was running
                    Tag(c.gameObject, owner);
                }
                if (owner == null) return;
                Enter(owner, true);
                __state = true;
            }
            catch (Exception) { }
        }

        static Exception Fin(Exception __exception, bool __state)
        {
            if (__state) Exit(true);
            return __exception;
        }
    }

    // Any Attack created inside an owned context belongs to that owner.
    [HarmonyPatch(typeof(Attack), "Awake")]
    static class AttackAwakeTagPatch
    {
        static void Prefix(Attack __instance)
        {
            Enemy owner = OwnerContext.Current;
            if (owner != null && OwnerContext.OwnerOf(__instance) == null) OwnerContext.Tag(__instance.gameObject, owner);
        }
    }

    // Follow-up hits often copy their parent's info: SetAttackInfo(AttackInfo, level) looks up stat data by the info's
    // category + skillID. Ours carry the enemy category with a player skill ID, so point the lookup back at "Player",
    // then re-own the result.
    [HarmonyPatch(typeof(Attack), "SetAttackInfo", new Type[] { typeof(AttackInfo), typeof(int) })]
    static class SetAttackInfoCopyPatch
    {
        static void Prefix(Attack __instance, AttackInfo __0, out Enemy __state)
        {
            __state = null;
            if (__0 == null) return;
            Enemy owner = __0.entity as Enemy;
            if (owner == null || !(Rivals.IsRival(owner) || OwnerContext.OwnerOf(__instance) != null || OwnerContext.Current != null)) return;
            if (EnemyArcana.WasReOwned(__0)) __0.skillCategory = "Player";
            __state = owner;
        }

        static void Postfix(Attack __instance, AttackInfo __0, Enemy __state)
        {
            if (__state == null) return;
            if (__instance.atkInfo != null) EnemyArcana.ReOwn(__instance.atkInfo, __state);
            if (__0 != null && __0 != __instance.atkInfo && EnemyArcana.WasReOwned(__0)) __0.skillCategory = __state.skillCategory;
        }
    }
}
