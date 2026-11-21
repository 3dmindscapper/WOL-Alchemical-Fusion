using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace WolHybridBosses
{
    // Casts one player arcana for an enemy. Used by ArcanaState (AI-picked casts) and DashArcana (casts from dashes).
    internal static class ArcanaCaster
    {
        // One cast, honouring the def's shots: a fan goes out at once, a chain runs as a coroutine on the caster.
        internal static void CastAll(Enemy caster, ArcanaDef def, Vector2 aim, Vector2? originOverride, bool empowered)
        {
            int shots = Mathf.Max(1, def.Shots);
            if (empowered) shots = shots > 1 ? shots * 2 - 1 : (def.Delivery == Delivery.Projectile ? 3 : 1);
            float spread = def.Spread > 0f ? def.Spread : (shots > 1 && def.ShotDelay <= 0f ? 30f : 0f);
            if (empowered && spread > 0f) spread *= 1.5f;

            if (def.ShotDelay > 0f && shots > 1) { caster.StartCoroutine(Chain(caster, def, aim, shots, empowered)); return; }
            for (int i = 0; i < shots; i++)
            {
                float off = shots == 1 ? 0f : Mathf.Lerp(-spread * 0.5f, spread * 0.5f, i / (float)(shots - 1));
                SafeCast(caster, def, Rotate(aim, off), originOverride, empowered, i == 0);
            }
        }

        static IEnumerator Chain(Enemy caster, ArcanaDef def, Vector2 aim, int shots, bool empowered)
        {
            for (int i = 0; i < shots; i++)
            {
                if (caster == null || caster.health == null || caster.health.CurrentHealthValue <= 0) yield break;
                Vector2 dir = aim;
                if (caster.closestTargetInfo != null)
                {
                    Vector2 to = (Vector2)caster.closestTargetInfo.position - (Vector2)caster.transform.position;
                    if (to.sqrMagnitude > 0.01f) dir = to.normalized; // re-aim each shot
                }
                SafeCast(caster, def, dir, null, empowered, i == 0);
                yield return new WaitForSeconds(def.ShotDelay);
            }
        }

        internal static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        static void SafeCast(Enemy caster, ArcanaDef def, Vector2 dir, Vector2? originOverride, bool empowered, bool log)
        {
            // Summons run in the game's native enemy mode: no ownership context (it would invert their targeting).
            bool owned = def.Delivery != Delivery.Summon;
            if (owned) OwnerContext.Enter(caster, true); // everything spawned now is tagged; its targeting aims at players
            try { Cast(caster, def, dir, originOverride, empowered, log); }
            catch (Exception ex)
            {
                EnemyArcana.Broken.Add(def.SkillID);
                Plugin.Log.LogWarning("ARCANA " + def.SkillID + " cast failed on " + caster.GetType().Name + " (disabled for this session): " + ex);
            }
            finally { if (owned) OwnerContext.Exit(true); }
        }

        static void Cast(Enemy caster, ArcanaDef def, Vector2 dir, Vector2? originOverride, bool empowered, bool log)
        {
            GameObject prefab = EnemyArcana.GetPrefab(def);
            if (prefab == null) return;

            Vector2 origin = originOverride ?? (caster.hurtBoxTransform != null ? (Vector2)caster.hurtBoxTransform.position : (Vector2)caster.transform.position);
            Vector2 target = caster.closestTargetInfo != null ? (Vector2)caster.closestTargetInfo.position : origin + dir * 4f;
            Quaternion rot = Globals.GetRotationQuaternion(dir);

            if (def.Delivery == Delivery.Summon) { Summons.Spawn(caster, def, prefab, origin, target, empowered, log); return; }

            GameObject spawned = null;
            switch (def.Delivery)
            {
                case Delivery.Projectile:
                    Projectile proj = Projectile.CreateProjectile(caster, prefab, origin, rot, null);
                    if (proj != null)
                    {
                        proj.parentObject = caster.gameObject;
                        proj.parentEntity = caster;
                        proj.moveVector = dir;
                        spawned = proj.gameObject;
                    }
                    else spawned = UnityEngine.Object.Instantiate(prefab, origin, rot) as GameObject; // not a Projectile after all
                    break;
                case Delivery.Wave:
                    spawned = UnityEngine.Object.Instantiate(prefab, origin, rot) as GameObject;
                    break;
                case Delivery.AtTarget:
                    spawned = UnityEngine.Object.Instantiate(prefab, target, Quaternion.identity) as GameObject;
                    break;
                case Delivery.AtSelf:
                case Delivery.Ward:
                    spawned = UnityEngine.Object.Instantiate(prefab, origin, Quaternion.identity) as GameObject;
                    break;
                case Delivery.Melee:
                    spawned = UnityEngine.Object.Instantiate(prefab, origin + dir * 1.2f, rot) as GameObject;
                    break;
            }
            if (spawned == null) return;

            OwnerContext.Tag(spawned, caster);
            EnemyArcana.InitPrefabFields(spawned, caster, def, origin, dir, empowered);
            if (def.Delivery == Delivery.Ward)
            {
                EnemyArcana.MakeHostileObject(spawned); // the player can break it, like the player's wards for enemies
                Traverse.Create(spawned.GetComponent<Ward>()).Field("spawnPosition").SetValue(origin);
            }
            if (empowered) spawned.transform.localScale *= 1.2f;

            int attacks = 0;
            foreach (Attack atk in spawned.GetComponentsInChildren<Attack>(true))
            {
                EnemyArcana.MakeEnemyAttack(atk, caster, def, empowered);
                attacks++;
            }
            if (log && Plugin.DebugKeys.Value)
                Plugin.Log.LogInfo("ARCANA " + caster.GetType().Name + " cast " + def.SkillID + " [" + def.Element + ", " + def.Family + "/" + def.Delivery
                    + (def.Shots > 1 ? " x" + def.Shots : "") + (empowered ? ", SIGNATURE" : "") + ", attacks " + attacks + "]");
        }
    }

    // Minion and seeker arcana as the game's own elemental Summoners do it (Summoner+SummonerMinionState /
    // SummonerSeekerState): spawn the player's prefab, set isEnemy, give it the element's enemy Summoner stats.
    internal static class Summons
    {
        const int MaxMinions = 3, MaxSeekers = 6;

        internal static void Spawn(Enemy caster, ArcanaDef def, GameObject prefab, Vector2 origin, Vector2 target, bool empowered, bool log)
        {
            SummonLeash.Prune(caster);
            string cat = Enemy.globalEnemySkillCategory;
            string id = def.EnemyStatID;
            GameObject go;

            if (def.Family == ArcanaFamily.Minion)
            {
                if (SummonLeash.Count(caster, false) >= MaxMinions) return;
                Vector2 pos = origin + UnityEngine.Random.insideUnitCircle * 1.5f;
                go = UnityEngine.Object.Instantiate(prefab, pos, Quaternion.identity) as GameObject;
                BasicMinion bm = go == null ? null : go.GetComponentInChildren<BasicMinion>();
                if (bm == null) { if (go != null) UnityEngine.Object.Destroy(go); return; }
                go.name = prefab.name + caster.skillCategory;
                bm.parentTrans = caster.transform;
                bm.isEnemy = true;
                bm.useSummonDuration = false;
                bm.useBurst = empowered || GameController.CurrentDifficulty > 0;
                bm.startingHealth = empowered ? 70 : 35;
                bm.transform.localScale *= empowered ? 1.5f : 1.25f;
                bm.SetSkillInfo(cat, id, 1, empowered);
                SummonLeash.Attach(go, caster, false);
            }
            else
            {
                if (SummonLeash.Count(caster, true) >= MaxSeekers) return;
                go = UnityEngine.Object.Instantiate(prefab, caster.transform.position, Quaternion.identity) as GameObject;
                Seeker sk = go == null ? null : go.GetComponentInChildren<Seeker>();
                if (sk == null) { if (go != null) UnityEngine.Object.Destroy(go); return; }
                go.name = prefab.name + caster.skillCategory;
                sk.SetTransforms(caster.transform, caster.transform, null);
                sk.SetThrowPosition(target + UnityEngine.Random.insideUnitCircle * 2f);
                sk.transform.localScale *= empowered ? 1.5f : 1.25f;
                sk.isEnemy = true;
                if (sk.attack != null) sk.attack.SetAttackInfo(cat, id, 1, empowered);
                sk.startingHealth = empowered ? 10 : 5;
                SummonLeash.Attach(go, caster, true);
            }
            if (log && Plugin.DebugKeys.Value)
                Plugin.Log.LogInfo("ARCANA " + caster.GetType().Name + " summoned " + def.SkillID + " (" + id + (empowered ? ", SIGNATURE" : "") + ")");
        }
    }

    // Ties a summon to its caster: counted for the cap, and removed when the caster dies.
    internal class SummonLeash : MonoBehaviour
    {
        Enemy owner;
        bool seeker;
        static readonly List<SummonLeash> all = new List<SummonLeash>();

        internal static void Attach(GameObject go, Enemy owner, bool seeker)
        {
            SummonLeash l = go.AddComponent<SummonLeash>();
            l.owner = owner;
            l.seeker = seeker;
            all.Add(l);
        }

        internal static void Prune(Enemy owner) { all.RemoveAll(l => l == null); }

        internal static int Count(Enemy owner, bool seeker)
        {
            int n = 0;
            foreach (SummonLeash l in all) if (l != null && l.owner == owner && l.seeker == seeker) n++;
            return n;
        }

        void Update()
        {
            if (owner != null && owner.health != null && owner.health.CurrentHealthValue > 0) return;
            BasicMinion bm = GetComponentInChildren<BasicMinion>();
            if (bm != null) { try { bm.DestroyMinion(); return; } catch (Exception) { } }
            Destroy(gameObject);
        }

        void OnDestroy() { all.Remove(this); }
    }

    // A data-driven enemy skill that casts a player arcana. Uses Enemy.SkillState's normal lifecycle:
    // OnEnter (face target, power-up glow) -> windup -> ExecuteSkill -> exit timer -> HandleExitTransitions.
    internal class ArcanaState : Enemy.SkillState
    {
        readonly ArcanaDef def;

        public ArcanaState(string name, FSM fsm, Enemy ent, ArcanaDef d)
            : base(name, fsm, ent,
                   Plugin.IgnoreSkillDifficulty.Value ? 0 : 1, d.Priority, d.MinRange, d.MaxRange, d.Cooldown, true)
        {
            def = d;
            exitTime = 0.4f + (d.Shots > 1 ? d.ShotDelay * d.Shots : 0f);
            spellCastColor = ElementalGiants.ElementColor(d.Element);
        }

        internal ArcanaDef Def { get { return def; } }

        // Shared spell rhythm: no arcana while the enemy's cast gap is running (see Rivals.MarkCast).
        public override bool IsReady()
        {
            return base.IsReady() && Rivals.CanCast(parent) && !EnemyArcana.Broken.Contains(def.SkillID);
        }

        public override void OnEnter()
        {
            Rivals.MarkCast(parent);
            base.OnEnter();
            if (parent.movement != null) parent.movement.EndMovement();
            if (!string.IsNullOrEmpty(castingAnimName)) TryPlay(castingAnimName);
            float windup = def.Windup;
            if (Signature.Ready(parent)) windup *= 1.4f; // a longer, readable tell before a signature cast
            windupStopwatchID = ChaosStopwatch.Begin(windup, false, 0f, 0, 0);
        }

        public override void ExecuteSkill()
        {
            base.ExecuteSkill(); // marks windup done and queues the exit
            DeactivatePowerUpEffect(false);
            Vector2 aim = targetVector.sqrMagnitude > 0.001f ? targetVector.normalized : Vector2.right;
            // Signature casters (rivals) empower from their meter; everyone else can roll an enhanced cast.
            bool empowered = Signature.HasMeter(parent)
                ? Signature.Consume(parent)
                : UnityEngine.Random.value < Plugin.EnhancedArcanaChance.Value;
            ArcanaCaster.CastAll(parent, def, aim, null, empowered);
        }

        void TryPlay(string anim)
        {
            Animator a = parent.anim;
            if (a == null) return;
            int hash = Animator.StringToHash(anim);
            if (a.HasState(0, hash)) a.Play(hash, 0, 0f);
        }
    }
}
