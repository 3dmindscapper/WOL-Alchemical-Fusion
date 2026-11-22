using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace WolHybridBosses
{
    // How an arcana's prefab is delivered by an enemy.
    internal enum Delivery
    {
        Projectile, // spawned at the caster, flies toward the target (Fireball, Boulder, Twister...)
        Wave,       // a Wave prefab travelling from the caster toward the target (Combustion Wave, Air Wave...)
        AtTarget,   // spawned on the target's position (Boulder Drop, Tornado, Water Blast...)
        AtSelf,     // spawned on the caster (Fire Blast, Shock Nova...)
        Melee,      // spawned just in front of the caster, facing the target (basic arcana)
        Summon,     // minions / seekers, run in the game's own enemy mode (isEnemy) with Summoner enemy stats
        Ward,       // a ward placed under the caster; its hurtbox is made hostile so the player can break it
    }

    // One player arcana an enemy can cast. SkillID is the player arcana's staticID (its stat-data key).
    internal class ArcanaDef
    {
        public string SkillID, PrefabClass, PrefabPath, Element;
        public Delivery Delivery;
        public ArcanaFamily Family = ArcanaFamily.Spell;
        public float Windup, Cooldown, MinRange, MaxRange;
        public int Priority;
        public int Shots = 1;        // >1: a fan (Spread) or a chain (ShotDelay)
        public float Spread, ShotDelay;
        public string EnemyStatID;   // summons: enemy-side stats to use ("FlameSummonerMinion")
        public bool Castable = true;
        public bool Basic;           // a rival's basic attack: replaces its built-in "Attack", no spell gap, not AI-picked
        public ArcanosPort Port;     // v0.6.3: an Arcanos arcana rebuilt for enemies (ArcanosArts), not a prefab cast

        public ArcanaDef(string element, string skillID, string prefabClass, Delivery delivery,
            float windup = 0.6f, float cooldown = 7f, float minRange = 0f, float maxRange = 9f, int priority = 5)
        {
            Element = element; SkillID = skillID; PrefabClass = prefabClass; Delivery = delivery;
            Windup = windup; Cooldown = cooldown; MinRange = minRange; MaxRange = maxRange; Priority = priority;
        }

        public ArcanaDef Scaled(float cooldownMult, float windupMult)
        {
            ArcanaDef d = (ArcanaDef)MemberwiseClone();
            d.Cooldown *= cooldownMult;
            d.Windup *= windupMult;
            return d;
        }
    }

    internal static class EnemyArcana
    {
        // Hand-tuned overrides (timing, range, delivery) for arcana tested so far. Everything else comes from the
        // generated catalog with family defaults (ArcanaCatalog.FromRow). Element is always read from stat data.
        internal static readonly Dictionary<string, ArcanaDef> Tuned = new Dictionary<string, ArcanaDef>();

        static EnemyArcana()
        {
            ArcanaDef[] rows =
            {
                new ArcanaDef(null, "ShootFireball", "Fireball", Delivery.Projectile, 0.5f, 5f, 2f, 10f),
                new ArcanaDef(null, "UseFlameSlash", "FlameSlash", Delivery.Projectile, 0.6f, 7f, 0f, 7f),
                new ArcanaDef(null, "UseFireBlast", "FireBlast", Delivery.AtSelf, 0.8f, 9f, 0f, 4f, 7),
                new ArcanaDef(null, "UseFrostFan", "FrostFanProjectile", Delivery.Projectile, 0.5f, 6f, 2f, 9f) { Shots = 3, Spread = 30f },
                new ArcanaDef(null, "ThrowIceLance", "IceLance", Delivery.Projectile, 0.7f, 7f, 3f, 12f),
                new ArcanaDef(null, "UseWaterBlast", "WaterBlast", Delivery.AtTarget, 0.9f, 9f, 2f, 9f, 6),
                new ArcanaDef(null, "UseThrowBoulder", "Boulder", Delivery.Projectile, 0.7f, 6f, 2f, 10f),
                new ArcanaDef(null, "UseHammerThrow", "EarthHammer", Delivery.Projectile, 0.6f, 7f, 2f, 9f),
                new ArcanaDef(null, "UseBoulderDrop", "BoulderDrop", Delivery.AtTarget, 0.9f, 9f, 2f, 9f, 6),
                new ArcanaDef(null, "UseTwister", "Twister", Delivery.Projectile, 0.5f, 6f, 2f, 10f),
                new ArcanaDef(null, "SummonTornado", "Tornado", Delivery.AtTarget, 0.9f, 10f, 2f, 9f, 6),
                new ArcanaDef(null, "UseShockLine", "ShockLine", Delivery.Projectile, 0.5f, 6f, 2f, 10f),
                new ArcanaDef(null, "ThunderDash", "LightningStrike", Delivery.AtTarget, 0.8f, 8f, 0f, 9f, 6),
                new ArcanaDef(null, "SummonMeteorStrike", "MeteorRainCloud", Delivery.AtTarget, 1f, 11f, 2f, 10f, 6),
                new ArcanaDef(null, "SummonLightningStorm", "LightningStorm", Delivery.AtTarget, 1f, 11f, 2f, 10f, 6),
            };
            foreach (ArcanaDef d in rows) Tuned[d.SkillID] = d;
        }

        // Arcana whose cast threw an error once: never picked again this session.
        // Arcana enemies must not cast. v0.7.4: Chaos Ball is a player charge arcana (hold to grow a ball with orbiting
        // sub-balls and lightning, release to fire); cast by an enemy it was never released and crackled loudly while
        // trailing particles.
        internal static readonly HashSet<string> Broken = new HashSet<string> { "UseChaosBall" };

        internal static List<ArcanaDef> ForElement(string prefix) { return ArcanaCatalog.Pool(prefix); }

        internal static bool OwnedByEnemy(Attack atk, out Enemy owner)
        {
            owner = OwnerContext.OwnerOf(atk);
            return owner != null;
        }

        static readonly Dictionary<string, GameObject> prefabCache = new Dictionary<string, GameObject>();
        static MethodInfo bundleGet;

        internal static GameObject GetPrefab(ArcanaDef d)
        {
            string key = d.PrefabClass ?? d.PrefabPath;
            if (key == null) return null;
            GameObject go;
            if (prefabCache.TryGetValue(key, out go) && go != null) return go;
            go = null;
            try
            {
                if (d.PrefabClass != null)
                {
                    Type t = AccessTools.TypeByName(d.PrefabClass);
                    PropertyInfo p = t == null ? null : AccessTools.Property(t, "Prefab");
                    go = p == null ? null : p.GetValue(null, null) as GameObject;
                }
                else
                {
                    if (bundleGet == null)
                    {
                        bundleGet = AccessTools.Method(typeof(ChaosBundle), "Get", new[] { typeof(string) });
                        if (bundleGet != null && bundleGet.IsGenericMethodDefinition) bundleGet = bundleGet.MakeGenericMethod(typeof(GameObject));
                    }
                    if (bundleGet != null) go = bundleGet.Invoke(null, new object[] { d.PrefabPath }) as GameObject;
                }
            }
            catch (Exception ex) { Plugin.Log.LogWarning("ARCANA prefab load failed for " + key + ": " + ex.Message); }
            if (go == null) Plugin.Log.LogWarning("ARCANA prefab not found: " + key);
            prefabCache[key] = go;
            return go;
        }

        // "UseShockBoomerang" -> "Shock Boomerang"
        internal static string DisplayName(string skillID)
        {
            string s = skillID;
            foreach (string p in new[] { "Use", "Shoot", "Summon", "Throw" })
                if (s.StartsWith(p) && s.Length > p.Length && char.IsUpper(s[p.Length])) { s = s.Substring(p.Length); break; }
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                if (i > 0 && char.IsUpper(s[i])) sb.Append(' ');
                sb.Append(s[i]);
            }
            return sb.ToString();
        }

        // Adds the given arcana to an enemy as AI-usable skills. Returns how many were added.
        internal static int Give(Enemy e, IEnumerable<ArcanaDef> defs, float cooldownMultiplier = 1f, float windupMultiplier = 1f)
        {
            int added = 0;
            foreach (ArcanaDef d0 in defs)
            {
                ArcanaDef d = d0.Scaled(cooldownMultiplier, windupMultiplier);
                string name = "Arcana" + d.SkillID;
                if (e.availableEnemySkillsDict != null && e.availableEnemySkillsDict.ContainsKey(name)) continue;
                if (GetPrefab(d) == null) continue;
                try
                {
                    ArcanaState s = new ArcanaState(name, e.fsm, e, d);
                    e.fsm.AddState(s);
                    added++;
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning("ARCANA failed to add " + d.SkillID + " to " + e.GetType().Name + ": " + ex.Message);
                }
            }
            return added;
        }

        // Turns an attack loaded from player stat data into one owned by the enemy, so it hits players.
        // Empowered (signature) casts load the ultimate stats and hit harder.
        internal static void MakeEnemyAttack(Attack atk, Enemy owner, ArcanaDef d, bool empowered = false)
        {
            AttackInfo info = atk.SetAttackInfo("Player", d.SkillID, 1, ArcanaCaster.SignatureCast); // real arcana damage, element, status effects; signature stats only for a signature cast
            if (info == null) return;
            ReOwn(info, owner);
            // v0.6.2: the variant applies here, once the arcana belongs to its caster (at SetAttackInfo time it still
            // reads as the player's, so VariantAttackPatch skipped it and Radiant rivals' arcana kept their own look).
            try
            {
                StageVariant v = Variants.AttackVariant(atk);
                if (v != StageVariant.None) Variants.Apply(atk, v);
            }
            catch (Exception) { }
            if (empowered) info.damage = Mathf.RoundToInt(info.damage * 1.3f);
        }

        // Makes a player-side object hostile: Ally hurtbox/floor names and layers -> Enemy ones (same as a rival).
        internal static void MakeHostileObject(GameObject go)
        {
            int enemyLayer = LayerMask.NameToLayer("Enemy"), playerLayer = LayerMask.NameToLayer("Player");
            int enemyFloor = LayerMask.NameToLayer("EnemyFloorContact"), allyFloor = LayerMask.NameToLayer("AllyFloorContact");
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.StartsWith("Ally")) t.name = "Enemy" + t.name.Substring(4);
                if (t.gameObject.layer == playerLayer) t.gameObject.layer = enemyLayer;
                else if (t.gameObject.layer == allyFloor) t.gameObject.layer = enemyFloor;
            }
        }

        // Re-owns attack info loaded from "Player" stat data: enemy category, enemy attacker, enemy target list.
        // Idempotent: damage/crit adjustments are applied once per AttackInfo.
        static readonly HashSet<AttackInfo> reowned = new HashSet<AttackInfo>();

        internal static bool WasReOwned(AttackInfo info) { return info != null && reowned.Contains(info); }

        internal static void ReOwn(AttackInfo info, Enemy owner)
        {
            if (info == null || owner == null) return;
            AttackInfo enemyInfo = EnemyTemplate(owner);
            info.skillCategory = owner.skillCategory;
            info.entity = owner;
            info.gameObject = owner.gameObject;
            if (enemyInfo != null && enemyInfo.targetNames != null) info.targetNames = new List<string>(enemyInfo.targetNames);
            if (reowned.Add(info))
            {
                info.damage = Mathf.Max(1, Mathf.RoundToInt(info.damage * Plugin.ArcanaDamageMultiplier.Value * Rivals.DamageFor(owner)));
                float crit, critDmg;
                Rivals.CritFor(owner, out crit, out critDmg);
                info.critHitChance += crit;
                info.critDmgModifier += critDmg;
                if (reowned.Count > 8192) reowned.Clear();
            }
        }

        // An enemy attack's info, used as the template for who to hit. Falls back to a plain enemy attack
        // (e.g. the Contestant has no enemy-side basic attack data of its own).
        static readonly Dictionary<int, AttackInfo> templates = new Dictionary<int, AttackInfo>();

        static AttackInfo EnemyTemplate(Enemy owner)
        {
            AttackInfo t;
            if (templates.TryGetValue(owner.GetInstanceID(), out t)) return t;
            t = TryTemplate(owner, owner.skillCategory, owner.attackSkillID)
                ?? TryTemplate(owner, Enemy.globalEnemySkillCategory, "NeutralKnightAttack");
            templates[owner.GetInstanceID()] = t;
            Plugin.Log.LogInfo("ARCANA " + owner.GetType().Name + " targets: "
                + (t == null || t.targetNames == null ? "NONE" : string.Join(", ", t.targetNames.ToArray())));
            return t;
        }

        static AttackInfo TryTemplate(Enemy owner, string category, string id)
        {
            if (string.IsNullOrEmpty(category) || string.IsNullOrEmpty(id)) return null;
            try
            {
                StatData data = StatManager.GetSkillData(category, id);
                AttackInfo t = data == null ? null : AttackInfo.GetInfoFromData(owner, data, 1, false);
                return t != null && t.targetNames != null && t.targetNames.Count > 0 ? t : null;
            }
            catch (Exception) { return null; }
        }

        // Fills the fields a player arcana's state normally sets on its prefab (by name, only if the field exists
        // and has a compatible type): who cast it, which stats to load, where it goes and who it targets.
        internal static void InitPrefabFields(GameObject go, Enemy owner, ArcanaDef d, Vector2 origin, Vector2 dir, bool empowered = false)
        {
            // Enhanced = isEmpowered; the signature version (isUltimate) only on a rival's signature cast.
            bool signature = ArcanaCaster.SignatureCast;
            if (empowered || signature)
                foreach (MonoBehaviour mb in go.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb == null) continue;
                    Set(mb, mb.GetType(), "isEmpowered", true);
                    Set(mb, mb.GetType(), "empowered", true);
                    if (signature) Set(mb, mb.GetType(), "isUltimate", true);
                }
            foreach (MonoBehaviour mb in go.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || mb is Attack || mb is ArcanaOwned) continue;
                Type t = mb.GetType();
                // What the player's states point at themselves (boomerangs come back to it, swirls follow it)
                // and where they aim, when the prefab left them empty.
                SetIfEmpty(mb, t, "parentTrans", owner.transform);
                SetIfEmpty(mb, t, "followTrans", owner.transform);
                SetIfEmpty(mb, t, "orbitTrans", owner.transform);
                Set(mb, t, "targetVector", dir);
                Set(mb, t, "atkVector", dir);
                Set(mb, t, "attackVec", dir);
                Set(mb, t, "attackVector", dir);
                Set(mb, t, "spawnPos", (Vector3)origin);
                Set(mb, t, "skillCategory", "Player");
                Set(mb, t, "waveSkillCategory", "Player");
                Set(mb, t, "skillID", d.SkillID);
                Set(mb, t, "waveSkillID", d.SkillID);
                Set(mb, t, "skillLevel", 1);
                Set(mb, t, "waveSkillLevel", 1);
                Set(mb, t, "parentEntity", owner);
                Set(mb, t, "parentObject", owner.gameObject);
                Set(mb, t, "parentHealth", owner.health);
                Set(mb, t, "moveVector", dir);
                Set(mb, t, "origin", origin);
                Set(mb, t, "targetGroup", Traverse.Create(typeof(GameController)).Field("players").GetValue());
            }
        }

        static void SetIfEmpty(object target, Type t, string field, UnityEngine.Object value)
        {
            FieldInfo f = ArcanaFinisher.Field(t, field);
            if (f == null || value == null || !f.FieldType.IsAssignableFrom(value.GetType())) return;
            try { if ((f.GetValue(target) as UnityEngine.Object) == null) f.SetValue(target, value); } catch (Exception) { }
        }

        static void Set(object target, Type t, string field, object value)
        {
            if (value == null) return;
            FieldInfo f = ArcanaFinisher.Field(t, field);
            if (f == null || f.IsStatic || f.IsInitOnly || !f.FieldType.IsAssignableFrom(value.GetType())) return;
            try { f.SetValue(target, value); } catch (Exception) { }
        }
    }

    // Marks a spawned arcana object (and everything under it) as belonging to an enemy caster.
    internal class ArcanaOwned : MonoBehaviour
    {
        public Enemy Owner;
    }

    // Safety net: an enemy-cast arcana (or anything a rival casts) never damages the caster or any other enemy.
    [HarmonyPatch(typeof(Attack), "CheckCollision")]
    static class EnemyArcanaFriendlyFirePatch
    {
        static bool Prefix(Attack __instance, Collider2D __0, ref bool __result)
        {
            if (__0 == null) return true;
            Enemy owner;
            bool ours = EnemyArcana.OwnedByEnemy(__instance, out owner);
            if (!ours)
            {
                Entity src = __instance.atkInfo != null ? __instance.atkInfo.entity : null;
                owner = src as Enemy;
                ours = owner != null && Rivals.IsRival(owner);
            }
            if (!ours) return true;
            Entity target = __0.GetComponentInParent<Entity>();
            if (target is Enemy)
            {
                __result = false; // no self-hits, no friendly fire
                return false;
            }
            return true;
        }
    }


}
