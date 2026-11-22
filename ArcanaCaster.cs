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
        // empowered = enhanced (isEmpowered, more shots, +30% damage); signature = the arcana's signature version
        // (isUltimate), only for a rival's signature arcana cast while its meter is ready (see Signature).
        internal static void CastAll(Enemy caster, ArcanaDef def, Vector2 aim, Vector2? originOverride, bool empowered, bool signature = false)
        {
            int shots = Mathf.Max(1, def.Shots);
            if (empowered && !signature) shots = shots > 1 ? shots * 2 - 1 : (def.Delivery == Delivery.Projectile ? 3 : 1);
            float spread = def.Spread > 0f ? def.Spread : (shots > 1 && def.ShotDelay <= 0f ? 30f : 0f);
            if (empowered && spread > 0f) spread *= 1.5f;

            if (def.ShotDelay > 0f && shots > 1) { caster.StartCoroutine(Chain(caster, def, aim, shots, empowered, signature)); return; }
            for (int i = 0; i < shots; i++)
            {
                float off = shots == 1 ? 0f : Mathf.Lerp(-spread * 0.5f, spread * 0.5f, i / (float)(shots - 1));
                SafeCast(caster, def, Rotate(aim, off), originOverride, empowered, i == 0, signature);
            }
        }

        // True while a signature-version cast is being spawned (read by MakeEnemyAttack / InitPrefabFields).
        internal static bool SignatureCast;

        // The caster's target: its own target info when it points at something, else the nearest living player.
        // (Bosses track targets their own way; with no target info a cast used to land beside the caster.)
        internal static Vector2? TargetOf(Enemy caster)
        {
            if (caster == null) return null;
            if (caster.closestTargetInfo != null && caster.closestTargetInfo.hurtBoxTransform != null) return caster.closestTargetInfo.position;
            return CouncilCorruption.NearestPlayer(caster.transform.position);
        }

        static IEnumerator Chain(Enemy caster, ArcanaDef def, Vector2 aim, int shots, bool empowered, bool signature)
        {
            for (int i = 0; i < shots; i++)
            {
                if (caster == null || caster.health == null || caster.health.CurrentHealthValue <= 0) yield break;
                Vector2 dir = aim;
                Vector2? tgt = TargetOf(caster);
                if (tgt.HasValue)
                {
                    Vector2 to = tgt.Value - (Vector2)caster.transform.position;
                    if (to.sqrMagnitude > 0.01f) dir = to.normalized; // re-aim each shot
                }
                SafeCast(caster, def, dir, null, empowered, i == 0, signature);
                yield return new WaitForSeconds(def.ShotDelay);
            }
        }

        internal static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        static void SafeCast(Enemy caster, ArcanaDef def, Vector2 dir, Vector2? originOverride, bool empowered, bool log, bool signature)
        {
            // Summons run in the game's native enemy mode: no ownership context (it would invert their targeting).
            bool owned = def.Delivery != Delivery.Summon;
            if (owned) OwnerContext.Enter(caster, true); // everything spawned now is tagged; its targeting aims at players
            bool prevSig = SignatureCast;
            SignatureCast = signature;
            try { Cast(caster, def, dir, originOverride, empowered, log); }
            catch (Exception ex)
            {
                EnemyArcana.Broken.Add(def.SkillID);
                Plugin.Log.LogWarning("ARCANA " + def.SkillID + " cast failed on " + caster.GetType().Name + " (disabled for this session): " + ex);
            }
            finally { SignatureCast = prevSig; if (owned) OwnerContext.Exit(true); }
        }

        static void Cast(Enemy caster, ArcanaDef def, Vector2 dir, Vector2? originOverride, bool empowered, bool log)
        {
            GameObject prefab = EnemyArcana.GetPrefab(def);
            if (prefab == null) return;

            Vector2 origin = originOverride ?? (caster.hurtBoxTransform != null ? (Vector2)caster.hurtBoxTransform.position : (Vector2)caster.transform.position);
            Vector2? tgt = TargetOf(caster);
            Vector2 target = tgt.HasValue ? tgt.Value : origin + dir * 4f;
            Quaternion rot = Globals.GetRotationQuaternion(dir);

            if (def.Delivery == Delivery.Summon) { Summons.Spawn(caster, def, prefab, origin, target, empowered, log); return; }

            GameObject spawned = null;
            switch (def.Delivery)
            {
                case Delivery.Projectile:
                    if (prefab.GetComponent<Projectile>() != null)
                    {
                        Projectile proj = Projectile.CreateProjectile(caster, prefab, origin, rot, null);
                        if (proj != null)
                        {
                            proj.parentObject = caster.gameObject;
                            proj.parentEntity = caster;
                            proj.moveVector = dir;
                            spawned = proj.gameObject;
                        }
                    }
                    else
                    {
                        // The projectile sits on a child (Water Arc): CreateProjectile can't take it, so spawn the whole
                        // prefab like the player's state does and wire the child projectile.
                        spawned = UnityEngine.Object.Instantiate(prefab, origin, rot) as GameObject;
                        Projectile child = spawned != null ? spawned.GetComponentInChildren<Projectile>(true) : null;
                        if (child != null)
                        {
                            child.parentObject = caster.gameObject;
                            child.parentEntity = caster;
                            child.moveVector = dir;
                        }
                    }
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
            ArcanaFinisher.Finish(spawned, caster, def, dir, target, empowered, SignatureCast); // the launch step the player's state would run
            if (log && Plugin.DebugKeys.Value)
                Plugin.Log.LogInfo("ARCANA " + caster.GetType().Name + " cast " + def.SkillID + " [" + def.Element + ", " + def.Family + "/" + def.Delivery
                    + (def.Shots > 1 ? " x" + def.Shots : "") + (SignatureCast ? ", SIGNATURE" : empowered ? ", enhanced" : "") + ", attacks " + attacks + "]");
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
                   Plugin.IgnoreSkillDifficulty.Value ? 0 : 1, d.Priority, d.MinRange, d.MaxRange, d.Cooldown, !d.Basic)
        {
            def = d;
            exitTime = 0.4f + (d.Shots > 1 ? d.ShotDelay * d.Shots : 0f);
            spellCastColor = ElementalGiants.ElementColor(d.Element);
        }

        internal ArcanaDef Def { get { return def; } }

        // Shared spell rhythm: no arcana while the enemy's cast gap is running (see Rivals.MarkCast).
        public override bool IsReady()
        {
            if (EnemyArcana.Broken.Contains(def.SkillID)) return false;
            // A ready signature goes out at the next opening, whatever the shared cast gap says.
            if (Signature.IsReady(parent) && Signature.IsSignatureArcana(parent, def)) return base.IsReady();
            return def.Basic ? base.IsReady() : base.IsReady() && Rivals.CanCast(parent);
        }

        public override void OnEnter()
        {
            if (!def.Basic) Rivals.MarkCast(parent);
            base.OnEnter();
            if (parent.movement != null) parent.movement.EndMovement();
            PlayCastAnimation();
            windupStopwatchID = ChaosStopwatch.Begin(def.Windup, false, 0f, 0, 0);
        }

        public override void ExecuteSkill()
        {
            base.ExecuteSkill(); // marks windup done and queues the exit
            DeactivatePowerUpEffect(false);
            Vector2 aim = targetVector.sqrMagnitude > 0.001f ? targetVector.normalized : Vector2.right;
            // Its signature arcana cast with a full meter = the signature version (spends the meter, with the cue).
            // Any cast by anyone can roll an enhanced version.
            bool signature = !def.Basic && Signature.Consume(parent, def);
            bool empowered = signature || UnityEngine.Random.value < Plugin.EnhancedArcanaChance.Value;
            ArcanaCaster.CastAll(parent, def, aim, null, empowered, signature);
        }

        // Without a cast animation the caster stands still with its run cycle looping ("walking in place").
        // Like the game's own enemy states: basics play "Attack"+facing, spells "Cast"+facing (Contestant: CastLeft,
        // AttackRight...), falling back to the plain names and the skill's castingAnimName. Only states the
        // animator actually has are played.
        void PlayCastAnimation()
        {
            string dir = parent.facingDirection.ToString();
            string[] names = def.Basic
                ? new[] { "Attack" + dir, "Attack", "Cast" + dir, "Cast", castingAnimName }
                : new[] { "Cast" + dir, "Cast", castingAnimName, "Attack" + dir, "Attack" };
            string played = null;
            foreach (string n in names) if (TryPlay(n)) { played = n; break; }
            if (Plugin.DebugKeys.Value && animLogged.Add(parent.GetType().Name + "|" + def.SkillID))
                Plugin.Log.LogInfo("ARCANA " + parent.GetType().Name + " " + def.SkillID + " animation: " + (played ?? "none found (keeps its current animation)"));
        }

        static readonly HashSet<string> animLogged = new HashSet<string>();

        bool TryPlay(string anim)
        {
            Animator a = parent.anim;
            if (a == null || string.IsNullOrEmpty(anim)) return false;
            int hash = Animator.StringToHash(anim);
            if (!a.HasState(0, hash)) return false;
            a.Play(hash, 0, 0f);
            return true;
        }
    }
}
