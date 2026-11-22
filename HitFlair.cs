using System;
using HarmonyLib;
using UnityEngine;

namespace WolHybridBosses
{
    // v1.1.1: when an elemental ghoul, blob or golem (or an elemental Coffin Knight / Cyclops) hits you, a small burst
    // of its element flies off you: sparks for Thunder, rising embers for Fire, droplets for Water, shards for Ice,
    // dust for Earth, a gust for Wind, motes for Chaos (and Arcanos' elements in their colour). Built in code from
    // square particles, so no art is copied; at most one burst every 0.12s.
    internal static class HitFlair
    {
        static Material material;
        static float last;

        static bool Flairs(Entity e)
        {
            Enemy en = e as Enemy;
            if (en == null) return false;
            if (en.GetComponent<ElementalNeutral>() != null) return true;
            return ElementalNeutrals.IsBeast(en) || en is MovingStatue;
        }

        internal static void OnPlayerHit(AttackInfo info, Entity attacker)
        {
            if (info == null || Time.time - last < 0.12f || !Flairs(attacker)) return;
            ElementType el = info.elementType;
            if (el == ElementType.Neutral)
            {
                ElementalNeutral mark = attacker.GetComponent<ElementalNeutral>();
                if (mark != null) el = mark.Element;
                else if (attacker is Enemy) el = ((Enemy)attacker).element;
            }
            if (el == ElementType.Neutral) return;
            Player p = Elites.Player0;
            if (p == null) return;
            last = Time.time;
            Vector3 at = p.hurtBoxTransform != null ? p.hurtBoxTransform.position : p.transform.position + new Vector3(0f, 0.6f, 0f);
            Spawn(at, el, p.spriteRenderer);
        }

        internal static void Spawn(Vector3 at, ElementType el, SpriteRenderer over)
        {
            try
            {
                if (material == null)
                {
                    Shader sh = Shader.Find("Sprites/Default");
                    if (sh == null) return;
                    material = new Material(sh) { mainTexture = Texture2D.whiteTexture, name = "HB_HitFlair" };
                }
                GameObject go = new GameObject("HB_HitFlair");
                go.transform.position = at;
                ParticleSystem ps = go.AddComponent<ParticleSystem>();
                ps.Stop();
                ParticleSystem.MainModule main = ps.main;
                main.loop = false;
                main.duration = 0.6f;
                main.playOnAwake = false;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = 40;
                ParticleSystem.EmissionModule em = ps.emission;
                em.rateOverTime = 0f;
                ParticleSystem.ShapeModule shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = 0.15f;
                ParticleSystem.SizeOverLifetimeModule sol = ps.sizeOverLifetime;
                sol.enabled = true;
                sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

                Color a, b;
                int count;
                float sMin, sMax, lMin, lMax, size, gravity;
                string name = Infusion.Name(el);
                switch (name)
                {
                    case "Thunder": a = new Color(1f, 0.95f, 0.45f); b = Color.white; count = 16; sMin = 6f; sMax = 10f; lMin = 0.1f; lMax = 0.25f; size = 0.07f; gravity = 0f; break;
                    case "Fire": a = new Color(1f, 0.55f, 0.1f); b = new Color(1f, 0.85f, 0.3f); count = 12; sMin = 1.2f; sMax = 2.8f; lMin = 0.4f; lMax = 0.7f; size = 0.12f; gravity = -0.6f; break;
                    case "Water": a = new Color(0.25f, 0.6f, 1f); b = new Color(0.7f, 0.9f, 1f); count = 12; sMin = 2.5f; sMax = 4f; lMin = 0.3f; lMax = 0.5f; size = 0.1f; gravity = 1.6f; break;
                    case "Ice": a = new Color(0.8f, 0.93f, 1f); b = Color.white; count = 10; sMin = 3f; sMax = 5f; lMin = 0.25f; lMax = 0.4f; size = 0.11f; gravity = 0f; break;
                    case "Earth": a = new Color(0.6f, 0.45f, 0.25f); b = new Color(0.45f, 0.62f, 0.25f); count = 10; sMin = 1f; sMax = 2.5f; lMin = 0.4f; lMax = 0.6f; size = 0.16f; gravity = 1f; break;
                    case "Wind": a = new Color(0.6f, 1f, 0.85f); b = Color.white; count = 12; sMin = 3f; sMax = 4.5f; lMin = 0.3f; lMax = 0.5f; size = 0.08f; gravity = -0.2f; break;
                    case "Chaos": a = new Color(0.62f, 0.35f, 0.95f); b = new Color(0.25f, 0.08f, 0.4f); count = 12; sMin = 1.5f; sMax = 3.5f; lMin = 0.35f; lMax = 0.6f; size = 0.11f; gravity = -0.3f; break;
                    default: a = Infusion.ColorOf(el); b = Color.white; count = 12; sMin = 2f; sMax = 3.5f; lMin = 0.3f; lMax = 0.5f; size = 0.1f; gravity = 0f; break;
                }
                main.startColor = new ParticleSystem.MinMaxGradient(a, b);
                main.startSpeed = new ParticleSystem.MinMaxCurve(sMin, sMax);
                main.startLifetime = new ParticleSystem.MinMaxCurve(lMin, lMax);
                main.startSize = new ParticleSystem.MinMaxCurve(size * 0.7f, size * 1.3f);
                main.gravityModifier = gravity;
                em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

                ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
                r.material = material;
                if (over != null) { r.sortingLayerID = over.sortingLayerID; r.sortingOrder = over.sortingOrder + 20; }
                ps.Play();
                UnityEngine.Object.Destroy(go, 1.5f);
            }
            catch (Exception ex) { Plugin.Log.LogWarning("HITFLAIR failed: " + ex.Message); }
        }
    }

    // TakeDamage returns true only on a killing blow, so a landed hit is "health went down" (or that).
    [HarmonyPatch(typeof(Health), "TakeDamage")]
    static class HitFlairPatch
    {
        static void Prefix(Health __instance, ref int __state)
        {
            __state = -1;
            try { if (__instance != null && __instance.GetComponentInParent<Player>() != null) __state = __instance.CurrentHealthValue; } catch (Exception) { }
        }

        static void Postfix(Health __instance, AttackInfo __0, Entity __1, bool __result, int __state)
        {
            try
            {
                if (__state < 0 || __0 == null) return;
                if (!__result && __instance.CurrentHealthValue >= __state) return; // dodged, blocked, shielded
                HitFlair.OnPlayerHit(__0, __0.entity ?? __1);
            }
            catch (Exception) { }
        }
    }
}