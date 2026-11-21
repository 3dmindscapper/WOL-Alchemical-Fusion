using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace WolHybridBosses
{
    // ---------------------------------------------------------------------------------------------
    // Dash arcana: like the player, an enemy with a dash arcana triggers it from its own dashes
    // (Contestant's DashState for rivals, Enemy.DashState for giants). Wave/self/trap-type effects are left at the
    // dash start along the dash direction; projectiles fire from the dash end; ground spells land on the target.
    // ---------------------------------------------------------------------------------------------
    internal class DashArcana : MonoBehaviour
    {
        public ArcanaDef Def;
        Vector2 start;
        bool dashing;
        float next;

        internal static DashArcana Give(Enemy e, ArcanaDef def)
        {
            if (def == null || EnemyArcana.GetPrefab(def) == null) return null;
            DashArcana d = e.GetComponent<DashArcana>() ?? e.gameObject.AddComponent<DashArcana>();
            d.Def = def;
            return d;
        }

        internal void Begin(Enemy e)
        {
            start = e.transform.position;
            dashing = true;
        }

        internal void End(Enemy e)
        {
            if (!dashing || Def == null || EnemyArcana.Broken.Contains(Def.SkillID)) return;
            dashing = false;
            if (Time.time < next) return;
            next = Time.time + Mathf.Max(0.8f, Def.Cooldown);

            Vector2 end = e.transform.position;
            Vector2 dir = end - start;
            if (dir.sqrMagnitude < 0.04f && e.closestTargetInfo != null) dir = (Vector2)e.closestTargetInfo.position - end;
            dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;

            Vector2? origin;
            switch (Def.Delivery)
            {
                case Delivery.Projectile:
                    origin = end;
                    if (e.closestTargetInfo != null)
                    {
                        Vector2 to = (Vector2)e.closestTargetInfo.position - end;
                        if (to.sqrMagnitude > 0.01f) dir = to.normalized; // dash shots fly at the player
                    }
                    break;
                case Delivery.AtTarget: origin = null; break;
                default: origin = start; break; // leave-behind: walls, traps, decoys, mines, waves along the dash
            }
            ArcanaCaster.CastAll(e, Def, dir, origin, false);
        }
    }

    [HarmonyPatch]
    static class DashArcanaPatch
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (string n in new[] { "Contestant+DashState", "Enemy+DashState" })
            {
                Type t = AccessTools.TypeByName(n);
                if (t == null) continue;
                foreach (string mn in new[] { "OnEnter", "OnExit" })
                {
                    MethodInfo m = AccessTools.DeclaredMethod(t, mn);
                    if (m != null) yield return m;
                }
            }
        }

        static void Postfix(object __instance, MethodBase __originalMethod)
        {
            try
            {
                Enemy e = Traverse.Create(__instance).Field("parent").GetValue() as Enemy;
                DashArcana d = e == null ? null : e.GetComponent<DashArcana>();
                if (d == null) return;
                if (__originalMethod.Name == "OnEnter") d.Begin(e); else d.End(e);
            }
            catch (Exception ex) { Plugin.Log.LogWarning("DASH arcana hook failed: " + ex.Message); }
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Signature: like the player's, a meter fills as the caster's attacks land on the player. When full, the
    // caster glows and its next arcana is empowered (ultimate stats, more shots, bigger, +30% damage).
    // ---------------------------------------------------------------------------------------------
    internal static class Signature
    {
        internal static void Enable(Enemy e, int hitsNeeded, string elementPrefix)
        {
            SignatureMeter m = e.GetComponent<SignatureMeter>() ?? e.gameObject.AddComponent<SignatureMeter>();
            m.Owner = e;
            m.Need = Mathf.Max(1, hitsNeeded);
            m.Color = ElementalGiants.ElementColor(elementPrefix);
        }

        internal static bool HasMeter(Enemy e) { return e != null && e.GetComponent<SignatureMeter>() != null; }

        internal static bool Ready(Enemy e)
        {
            SignatureMeter m = e == null ? null : e.GetComponent<SignatureMeter>();
            return m != null && m.IsReady;
        }

        internal static bool Consume(Enemy e)
        {
            SignatureMeter m = e == null ? null : e.GetComponent<SignatureMeter>();
            if (m == null || !m.IsReady) return false;
            m.Reset();
            Plugin.Log.LogInfo("SIGNATURE " + e.GetType().Name + " unleashed");
            return true;
        }

        internal static void AddHit(Enemy e)
        {
            SignatureMeter m = e == null ? null : e.GetComponent<SignatureMeter>();
            if (m != null) m.AddHit();
        }
    }

    internal class SignatureMeter : MonoBehaviour
    {
        public Enemy Owner;
        public int Need = 8;
        public Color Color = Color.white;
        int hits;
        float readySince;

        internal bool IsReady { get { return hits >= Need; } }

        internal void AddHit()
        {
            if (IsReady) return;
            hits++;
            if (IsReady)
            {
                readySince = Time.time;
                SetOutline(Color);
                Plugin.Log.LogInfo("SIGNATURE " + (Owner != null ? Owner.GetType().Name : "?") + " ready");
            }
        }

        internal void Reset()
        {
            hits = 0;
            SetOutline(Color.clear);
        }

        void SetOutline(Color c)
        {
            if (Owner == null || Owner.spriteRenderer == null) return;
            Material m = Owner.spriteRenderer.material;
            if (m == null) return;
            if (m.HasProperty("_OutlineColor")) m.SetColor("_OutlineColor", c);
            if (m.HasProperty("_OutlineColor2")) m.SetColor("_OutlineColor2", c);
        }

        void OnGUI()
        {
            if (Owner == null || !Owner.enabled || Owner.health == null || Owner.health.CurrentHealthValue <= 0) return;
            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 sp = cam.WorldToScreenPoint(Owner.transform.position + new Vector3(0f, -0.35f, 0f));
            if (sp.z < 0f) return;
            float w = 64f, h = 6f;
            Rect back = new Rect(sp.x - w * 0.5f, Screen.height - sp.y, w, h);
            Color old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.DrawTexture(back, Texture2D.whiteTexture);
            float fill = Mathf.Clamp01(hits / (float)Need);
            Color c = Color;
            if (IsReady) c = Color.Lerp(Color, Color.white, 0.5f + 0.5f * Mathf.Sin((Time.time - readySince) * 10f));
            GUI.color = c;
            GUI.DrawTexture(new Rect(back.x + 1, back.y + 1, (w - 2) * fill, h - 2), Texture2D.whiteTexture);
            if (IsReady)
            {
                GUI.color = c;
                GUIStyle st = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperCenter };
                st.normal.textColor = c;
                GUI.Label(new Rect(back.x - 30, back.y + h, w + 60, 18), "SIGNATURE", st);
            }
            GUI.color = old;
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Fall protection for rivals, as the player has it: falling into a pit costs some health (the player's own pit
    // damage, at least 5% of the rival's max HP, never lethal), then the rival comes back at the nearest free spot
    // to where it last stood on solid ground (Fall.preFallPos), briefly invulnerable. The Contestant's own fall
    // code teleports it to myOrigin when the fall finishes, so we point myOrigin at the safe spot for that moment.
    // ---------------------------------------------------------------------------------------------
    [HarmonyPatch(typeof(Entity.FallState), "Update")]
    static class RivalFallPatch
    {
        static readonly HashSet<object> handled = new HashSet<object>();

        static void Postfix(Entity.FallState __instance)
        {
            if (!__instance.finishedFalling) { handled.Remove(__instance); return; }
            Enemy e = Traverse.Create(__instance).Field("parent").GetValue() as Enemy;
            if (e == null || !Rivals.IsRival(e) || !handled.Add(__instance)) return;
            try
            {
                Vector2 last = e.fall != null ? (Vector2)e.fall.preFallPos : (Vector2)e.myOrigin;
                Vector2 safe = Entity.GetAvailableNearbyLocation(last, false, false);
                Vector3 home = e.myOrigin;
                e.myOrigin = safe;                       // Contestant's fall state teleports here this frame
                e.StartCoroutine(RestoreOrigin(e, home));

                int dmg = FallDamage(e);
                if (e.health != null && dmg > 0)
                {
                    e.health.CurrentHealthValue = Mathf.Max(1, e.health.CurrentHealthValue - dmg);
                    e.health.SetInvulnerabilityDuration(1f, false);
                }
                Plugin.Log.LogInfo("RIVAL fell: -" + dmg + " HP, back at " + safe);
            }
            catch (Exception ex) { Plugin.Log.LogWarning("RIVAL fall recovery failed: " + ex.Message); }
        }

        static System.Collections.IEnumerator RestoreOrigin(Enemy e, Vector3 home)
        {
            yield return null;
            if (e != null) e.myOrigin = home;
        }

        static int FallDamage(Enemy e)
        {
            float playerPit = 0f;
            try
            {
                Player p = GameController.playerScripts != null && GameController.playerScripts.Length > 0 ? GameController.playerScripts[0] : null;
                if (p != null) playerPit = p.FallDamage.CurrentValue;
            }
            catch (Exception) { }
            float max = e.health != null ? e.health.healthStat.CurrentValue : 0f;
            return Mathf.RoundToInt(Mathf.Max(playerPit, max * 0.05f));
        }
    }

    // A hit lands: if it came from an enemy with a signature meter (directly, or via something it cast/summoned)
    // and the target is a player, the meter fills.
    [HarmonyPatch(typeof(Attack), "ExecuteAttack")]
    static class SignatureHitPatch
    {
        static void Postfix(Attack __instance, Collider2D __0)
        {
            try
            {
                if (__0 == null || __0.GetComponentInParent<Player>() == null) return;
                Enemy owner = OwnerContext.OwnerOf(__instance);
                if (owner == null && __instance.atkInfo != null) owner = __instance.atkInfo.entity as Enemy;
                if (owner != null) Signature.AddHit(owner);
            }
            catch (Exception) { }
        }
    }
}
