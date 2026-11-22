using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace WolHybridBosses
{
    // A player arcana's state often keeps driving the object it spawned: it calls Init()/InitThrow()/Release()
    // or flips "released" on a charge shot. Enemies never run that state, so without this the object just sits
    // there (the Hammer Throw floated in place). tools/driven.ps1 lists those calls; this runs the launch step.
    internal static class ArcanaFinisher
    {
        sealed class Step
        {
            public string Method;
            public bool Late;   // the player calls it after a hold/spin, i.e. after the object's Start
            public int Level;   // int argument (skill level) when the method takes one; 0 = 1 / 2 when empowered
            public Step(string m, bool late = false, int level = 0) { Method = m; Late = late; Level = level; }
        }

        // Prefab class -> what the player's state calls to set it going.
        static readonly Dictionary<string, Step[]> Steps = new Dictionary<string, Step[]>
        {
            { "EarthHammer",           new[] { new Step("InitThrow", true, 4) } },
            { "EarthAxeBasic",         new[] { new Step("InitThrow") } },   // (dir, swingFore): spins out, starts its life timer
            { "WhirlingAxe",           new[] { new Step("ThrowAxe", true) } },
            { "PoisonBolasProjectile", new[] { new Step("ReleaseBolas", true) } },
            { "FrostWingProjectile",   new[] { new Step("Release", true) } },
            { "EarthCascade",          new[] { new Step("SetReleaseVars", true) } },
            { "IceBoomerang",          new[] { new Step("Init") } },
            { "ShockBoomerang",        new[] { new Step("Init") } },
            { "ShockDragon",           new[] { new Step("Init") } },
            { "AquaDragon",            new[] { new Step("Initialize") } },
            { "AeroRing",              new[] { new Step("Init") } },
            { "AirSentry",             new[] { new Step("Init") } },
            { "FrostSwing",            new[] { new Step("Init") } },
            { "ElectricAura",          new[] { new Step("Init") } },
            { "IceChargeSword",        new[] { new Step("Init") } },
            { "FrostBlock",            new[] { new Step("Init") } },
            { "DragonGrade",           new[] { new Step("Init") } },
            { "ChaosWard",             new[] { new Step("Init") } },
            { "LightningXStrike",      new[] { new Step("InitStrike") } },
            { "ShockDart",             new[] { new Step("InitBounce") } },
            { "ThunderJavelin",        new[] { new Step("InitJavelin") } },
            { "PlantBarrage",          new[] { new Step("Initialize") } },
            { "MortalCoil",            new[] { new Step("Initialize") } },
            { "MagmaBomb",             new[] { new Step("Initialize") } },
            { "LightningShadow",       new[] { new Step("Initialize") } },
            { "CreepingTendril",       new[] { new Step("Initialize") } },
            { "ThrownFireball",        new[] { new Step("Initialize") } },
            { "HomingMissile",         new[] { new Step("InitOrbitVars") } },
            { "AquaBlitzBall",         new[] { new Step("Activate", true) } },
            { "LightningFist",         new[] { new Step("Activate") } },
            { "EarthDrill",            new[] { new Step("Activate") } },
        };

        const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        static readonly HashSet<string> logged = new HashSet<string>();

        // Fields the player's state sets to fixed values (Player+WaterArcBasic.CreateWaterArc: flyTime 0.25, arcRange 5),
        // applied when the prefab left them at zero.
        static readonly Dictionary<string, KeyValuePair<string, float>[]> Defaults = new Dictionary<string, KeyValuePair<string, float>[]>
        {
            { "WaterArcProjectile", new[] { new KeyValuePair<string, float>("arcRange", 5f), new KeyValuePair<string, float>("flyTime", 0.25f) } },
        };

        internal static void Finish(GameObject go, Enemy caster, ArcanaDef def, Vector2 dir, Vector2 target, bool empowered, bool signature)
        {
            if (go == null) return;
            bool late = false;
            foreach (MonoBehaviour mb in go.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                ApplyDefaults(mb);
                if (ReleaseCharge(mb)) late = true; // again after its Start, in case Start resets the charge
                Step[] steps;
                if (Steps.TryGetValue(mb.GetType().Name, out steps))
                    foreach (Step s in steps)
                    {
                        if (s.Late) { late = true; continue; }
                        Run(mb, s, caster, def, dir, target, signature);
                    }
                EnsureLifetime(mb);
            }
            if (late && caster != null) caster.StartCoroutine(FinishLate(go, caster, def, dir, target, signature));
        }

        static void ApplyDefaults(MonoBehaviour mb)
        {
            KeyValuePair<string, float>[] list;
            if (!Defaults.TryGetValue(mb.GetType().Name, out list)) return;
            foreach (KeyValuePair<string, float> kv in list)
            {
                FieldInfo f = Field(mb.GetType(), kv.Key);
                if (f == null || f.FieldType != typeof(float)) continue;
                try { if ((float)f.GetValue(mb) <= 0f) f.SetValue(mb, kv.Value); } catch (Exception) { }
            }
        }

        // A melee object only goes away through its animation end or life timer, which the player's state starts.
        // If neither is running after the launch step, give it a short life so nothing lingers on the floor.
        static void EnsureLifetime(MonoBehaviour mb)
        {
            MeleeAttackObject mao = mb as MeleeAttackObject;
            if (mao == null || mao.useAnimTime || mao.useLifeTime) return;
            mao.useLifeTime = true;
            mao.lifeStopwatchID = ChaosStopwatch.Begin(mao.lifeTime > 0f ? mao.lifeTime : 1.2f, false, 0f, 0, 0);
            Note(mb.GetType().Name + ".lifetime", "no timer after launch, gave it " + (mao.lifeTime > 0f ? mao.lifeTime : 1.2f) + "s");
        }

        // After the object's Start (the player's spin/hold): aim it again and run the throw/release steps.
        static IEnumerator FinishLate(GameObject go, Enemy caster, ArcanaDef def, Vector2 dir, Vector2 target, bool signature)
        {
            yield return null;
            yield return new WaitForSeconds(0.1f);
            if (go == null || caster == null) yield break;
            Vector2? tgt = ArcanaCaster.TargetOf(caster);
            if (tgt.HasValue)
            {
                Vector2 d = tgt.Value - (Vector2)go.transform.position;
                if (d.sqrMagnitude > 0.01f) { dir = d.normalized; target = tgt.Value; }
            }
            foreach (MonoBehaviour mb in go.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                ReleaseCharge(mb);
                Step[] steps;
                if (!Steps.TryGetValue(mb.GetType().Name, out steps)) continue;
                bool hasLate = false;
                foreach (Step s in steps) hasLate |= s.Late;
                if (!hasLate) continue;
                Projectile p = mb as Projectile;
                if (p != null)
                {
                    p.moveVector = dir;
                    p.transform.localEulerAngles = Globals.GetRotationVector(dir);
                    p.goThroughWalls = false;
                }
                foreach (Step s in steps) if (s.Late) Run(mb, s, caster, def, dir, target, signature);
                if (p != null)
                {
                    p.updateVelocity = true;
                    try { p.RefreshFlightTime(); } catch (Exception) { }
                }
            }
        }

        // Charge shots (Ball Lightning, Charged Arrow, Wind Ball, Water Ball, ...) wait for the button release.
        static bool ReleaseCharge(MonoBehaviour mb)
        {
            Type t = mb.GetType();
            FieldInfo released = Field(t, "released");
            if (released == null || released.FieldType != typeof(bool)) return false;
            FieldInfo cur = Field(t, "currentCharge"), max = Field(t, "maxCharge");
            try
            {
                if (cur != null && max != null && cur.FieldType == max.FieldType) cur.SetValue(mb, max.GetValue(mb));
                released.SetValue(mb, true);
                Note(t.Name + ".released", "charge released");
            }
            catch (Exception) { }
            return true;
        }

        // signature: the player's states pass the higher level / true for the signature version (InitThrow 5 vs 4).
        static void Run(MonoBehaviour mb, Step s, Enemy caster, ArcanaDef def, Vector2 dir, Vector2 target, bool signature)
        {
            bool empowered = signature;
            MethodInfo m = null;
            for (Type t = mb.GetType(); t != null && m == null; t = t.BaseType)
                foreach (MethodInfo cand in t.GetMethods(All | BindingFlags.DeclaredOnly))
                    if (cand.Name == s.Method && (m == null || cand.GetParameters().Length < m.GetParameters().Length)) m = cand;
            string key = mb.GetType().Name + "." + s.Method;
            if (m == null) { Note(key, "not found"); return; }
            ParameterInfo[] ps = m.GetParameters();
            object[] args = new object[ps.Length];
            for (int i = 0; i < ps.Length; i++) args[i] = Arg(ps[i].ParameterType, s, caster, dir, target, empowered);
            try
            {
                OwnerContext.Enter(caster, true);
                try { m.Invoke(mb, args); } finally { OwnerContext.Exit(true); }
                Note(key, "ran (" + ps.Length + " args)");
            }
            catch (Exception ex) { Note(key, "failed: " + (ex.InnerException ?? ex).Message); }
        }

        static object Arg(Type pt, Step s, Enemy caster, Vector2 dir, Vector2 target, bool empowered)
        {
            if (pt == typeof(int)) return s.Level > 0 ? s.Level + (empowered ? 1 : 0) : (empowered ? 2 : 1);
            if (pt == typeof(bool)) return empowered;
            if (pt == typeof(float)) return 1f;
            if (pt == typeof(Vector2)) return dir;
            if (pt == typeof(Vector3)) return (Vector3)target;
            if (pt == typeof(Transform)) return caster != null ? caster.transform : null;
            if (pt == typeof(GameObject)) return caster != null ? caster.gameObject : null;
            if (caster != null && pt.IsAssignableFrom(caster.GetType())) return caster;
            if (caster != null && caster.health != null && pt.IsAssignableFrom(caster.health.GetType())) return caster.health;
            if (pt == typeof(string)) return Enemy.globalEnemySkillCategory;
            return pt.IsValueType ? Activator.CreateInstance(pt) : null;
        }

        static void Note(string key, string what)
        {
            if (Plugin.DebugKeys.Value && logged.Add(key)) Plugin.Log.LogInfo("ARCANA finisher " + key + ": " + what);
        }

        // Quiet field lookup through base classes (AccessTools.Field logs a warning for every miss).
        internal static FieldInfo Field(Type t, string name)
        {
            for (; t != null; t = t.BaseType)
            {
                FieldInfo f = t.GetField(name, All | BindingFlags.DeclaredOnly);
                if (f != null) return f;
            }
            return null;
        }
    }
}
