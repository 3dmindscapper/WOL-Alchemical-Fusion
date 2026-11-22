using System;
using HarmonyLib;
using UnityEngine;

namespace WolHybridBosses
{
    // v0.9.4: Mortal Coil cast by enemies, wired the way the player's own cast does it (Player+UseMortalCoil.InitMortalCoil):
    //   Initialize(start, aim, "Player", skillID, signature, caster's transform, range 8 / 14 for the signature),
    //   delayStopwatchTime 0.125.
    // The generic finisher used to call Initialize with guessed arguments: the target as the start point, range 1,
    // and the enemy skill category as both category and skill ID. SetAttackInfo then found no stats (atkInfo null),
    // so MortalCoil.Update threw in Projectile.UpdateCalculatedDamage every frame before marking the attack created.
    // The coil respawned its projectile, trail and "ChaosFormLong"/"ChaosBeamStart" sounds each frame: the loud
    // trailing Chaos noise. With no parent transform the returning projectile also logged "has no parent, but is set
    // to boomerang". Now the coil returns to its caster, and the attacks it creates later (its line in InitAttackBox,
    // the returning projectile in CreateMortalCoilProj) are turned into the enemy's attacks as they're made.
    internal class EnemyMortalCoil : MonoBehaviour
    {
        public Enemy Owner;
        public ArcanaDef Def;
        public bool Signature;

        internal static bool Launch(MonoBehaviour mb, Enemy caster, ArcanaDef def, Vector2 dir, bool signature)
        {
            MortalCoil mc = mb as MortalCoil;
            if (mc == null || caster == null || def == null) return false;
            EnemyMortalCoil tag = mc.GetComponent<EnemyMortalCoil>() ?? mc.gameObject.AddComponent<EnemyMortalCoil>();
            tag.Owner = caster;
            tag.Def = def;
            tag.Signature = signature;
            Vector2 aim = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;
            try
            {
                Traverse.Create(mc).Method("Initialize", new[] { typeof(Vector3), typeof(Vector2), typeof(string), typeof(string), typeof(bool), typeof(Transform), typeof(float) })
                    .GetValue(mc.transform.position, aim, "Player", def.SkillID, signature, caster.transform, signature ? 14f : 8f);
                Traverse.Create(mc).Field("delayStopwatchTime").SetValue(0.125f);
            }
            catch (Exception ex) { Plugin.Log.LogWarning("ARCANA Mortal Coil launch failed: " + ex.Message); }
            return true;
        }

        internal static void ReOwn(MortalCoil mc, Attack atk)
        {
            EnemyMortalCoil tag = mc != null ? mc.GetComponent<EnemyMortalCoil>() : null;
            if (tag == null || atk == null) return;
            if (tag.Owner == null) { Destroy(mc.gameObject); return; } // the caster died mid-cast
            EnemyArcana.MakeEnemyAttack(atk, tag.Owner, tag.Def, tag.Signature);
        }
    }

    [HarmonyPatch(typeof(MortalCoil), "InitAttackBox")]
    static class EnemyMortalCoilLinePatch
    {
        static void Postfix(MortalCoil __instance)
        {
            try { EnemyMortalCoil.ReOwn(__instance, Traverse.Create(__instance).Field("attackBox").GetValue<Attack>()); }
            catch (Exception) { }
        }
    }

    [HarmonyPatch(typeof(MortalCoil), "CreateMortalCoilProj")]
    static class EnemyMortalCoilProjPatch
    {
        static void Postfix(MortalCoil __instance)
        {
            try
            {
                Projectile p = Traverse.Create(__instance).Field("mortalCoilProj").GetValue<Projectile>();
                if (p == null || __instance.GetComponent<EnemyMortalCoil>() == null) return;
                EnemyMortalCoil.ReOwn(__instance, p.attackBox);
                p.UpdateCalculatedDamage(true);
            }
            catch (Exception) { }
        }
    }
}
