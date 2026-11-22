using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace WolHybridBosses
{
    // v0.8.0: elite enemies (ROADMAP 7a). Now and then a regular enemy spawns with one trait that changes how you fight
    // it, a coloured outline and an "Elite" label: Shielded (the game's own shield on top of its health), Hasted
    // (faster, with after-images), Splitting (two smaller copies on death), Draining (heals from the damage it deals),
    // Blinking (short teleports around you). Elites drop more gold and sometimes an infused arcana.
    internal enum EliteTrait { Shielded, Hasted, Splitting, Draining, Blinking }

    internal static class Elites
    {
        internal static float Chance
        {
            get
            {
                float c = 0.04f + 0.03f * Mathf.Max(0, GameController.tierCount) + (GameController.loopCount >= 1 ? 0.04f : 0f);
                return c * Mathf.Max(0f, Plugin.EliteChanceMultiplier.Value);
            }
        }

        internal static bool Eligible(Enemy e)
        {
            if (e == null || e.health == null || e is MiniBoss || e is Boss || Rivals.IsRival(e)) return false;
            if (e.GetComponent<EliteMark>() != null || e.GetComponent<EliteSplitChild>() != null) return false;
            if (OwnerContext.OwnerOf(e) != null || e.GetComponent<ArcanaOwned>() != null) return false; // summons of our casters
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (scene == "Hub" || scene == "Tutorial" || scene.Contains("BossLevel") || scene.Contains("PvP") || Level.isBossRushMode) return false;
            if (UnityEngine.Object.FindObjectOfType<BossRoomEventHandler>() != null) return false; // Council / final rooms
            return e.health.CurrentHealthValue > 0;
        }

        internal static void TryMake(Enemy e)
        {
            if (!Plugin.ElitesEnabled.Value || !Eligible(e)) return;
            if (UnityEngine.Random.value >= Chance) return;
            Make(e, (EliteTrait)UnityEngine.Random.Range(0, 5));
        }

        internal static void Make(Enemy e, EliteTrait trait)
        {
            if (e == null || e.GetComponent<EliteMark>() != null) return;
            EliteMark m = e.gameObject.AddComponent<EliteMark>();
            m.Trait = trait;
            Plugin.Log.LogInfo("ELITE " + e.GetType().Name + " is " + trait);
        }

        internal static Color ColorOf(EliteTrait t)
        {
            switch (t)
            {
                case EliteTrait.Shielded: return new Color(1f, 0.85f, 0.3f);
                case EliteTrait.Hasted: return new Color(0.4f, 1f, 1f);
                case EliteTrait.Splitting: return new Color(0.7f, 1f, 0.55f);
                case EliteTrait.Draining: return new Color(1f, 0.25f, 0.25f);
                default: return new Color(0.75f, 0.5f, 1f);
            }
        }

        // A readable name: "FireKnight" -> "Knight".
        internal static string NameOf(Enemy e)
        {
            string n = e.GetType().Name;
            foreach (string p in new[] { "Flame", "Fire", "Frost", "Ice", "Water", "Earth", "Wind", "Air", "Thunder", "Lightning", "Chaos", "Neutral" })
                if (n.StartsWith(p) && n.Length > p.Length) return n.Substring(p.Length);
            return n;
        }

        // Ctrl+F7 test key: the nearest enemy becomes the next elite trait.
        static int testIndex;
        internal static void TestNearest()
        {
            Player p = GameController.playerScripts != null && GameController.playerScripts.Length > 0 ? GameController.playerScripts[0] : null;
            if (p == null) return;
            Enemy best = null;
            float bd = float.MaxValue;
            foreach (Enemy e in UnityEngine.Object.FindObjectsOfType<Enemy>())
            {
                if (e == null || e is MiniBoss || e is Boss || e.health == null || e.health.CurrentHealthValue <= 0 || e.GetComponent<EliteMark>() != null) continue;
                float d = Vector2.Distance(e.transform.position, p.transform.position);
                if (d < bd) { bd = d; best = e; }
            }
            if (best == null) { Plugin.Log.LogInfo("ELITE test: no enemy nearby"); return; }
            Make(best, (EliteTrait)(testIndex++ % 5));
        }
    }

    internal class EliteSplitChild : MonoBehaviour { }

    internal class EliteMark : MonoBehaviour
    {
        public EliteTrait Trait;
        Enemy enemy;
        int maxHp;
        bool dead, applied;
        float nextBlink, nextImage;
        Vector2 lastPos;
        static GUIStyle style;
        static Font styleFont;

        void Start()
        {
            enemy = GetComponent<Enemy>();
            if (enemy == null || enemy.health == null) { Destroy(this); return; }
            maxHp = Mathf.Max(1, enemy.health.CurrentHealthValue);
            nextBlink = Time.time + UnityEngine.Random.Range(2.5f, 4f);
            lastPos = transform.position;
            Apply();
        }

        void Apply()
        {
            if (applied) return;
            applied = true;
            SetOutline(Elites.ColorOf(Trait));
            switch (Trait)
            {
                case EliteTrait.Shielded:
                    try { enemy.health.CurrentShieldValue = Mathf.Max(1, Mathf.RoundToInt(maxHp * 0.4f)); } catch (Exception) { }
                    break;
                case EliteTrait.Hasted:
                    try { enemy.movement.moveSpeedStat.AddMod(new NumVarStatMod("HB_EliteHaste", 1.35f, 10, VarStatModType.Multiplicative, false)); } catch (Exception) { }
                    break;
            }
        }

        void SetOutline(Color c)
        {
            foreach (SpriteRenderer sr in GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sr == null) continue;
                Material m = sr.material;
                if (m != null && m.HasProperty("_OutlineColor")) m.SetColor("_OutlineColor", c);
            }
        }

        void Update()
        {
            if (enemy == null || dead) return;
            if (enemy.health == null || enemy.health.CurrentHealthValue <= 0) { dead = true; OnDeath(); return; }
            // A gentle pulse of the trait colour on the outline.
            Color c = Elites.ColorOf(Trait);
            float pulse = 0.65f + 0.35f * Mathf.Sin(Time.time * 4f);
            SetOutline(new Color(c.r * pulse, c.g * pulse, c.b * pulse, 1f));
            if (Trait == EliteTrait.Hasted)
            {
                if (enemy.anim != null && enemy.anim.speed < 1.3f) enemy.anim.speed = 1.3f;
                if (Time.time >= nextImage && ((Vector2)transform.position - lastPos).sqrMagnitude > 0.0004f) { nextImage = Time.time + 0.09f; AfterImage(); }
            }
            if (Trait == EliteTrait.Blinking && Time.time >= nextBlink) Blink();
            lastPos = transform.position;
        }

        // Hasted: a fading copy of the current frame where it just was.
        void AfterImage()
        {
            SpriteRenderer src = enemy.spriteRenderer;
            if (src == null || src.sprite == null) return;
            GameObject go = new GameObject("HB_EliteAfterImage");
            go.transform.position = src.transform.position;
            go.transform.rotation = src.transform.rotation;
            go.transform.localScale = src.transform.lossyScale;
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = src.sprite;
            sr.flipX = src.flipX;
            sr.sortingLayerID = src.sortingLayerID;
            sr.sortingOrder = src.sortingOrder - 1;
            sr.color = new Color(0.4f, 1f, 1f, 0.45f);
            go.AddComponent<FadeOut>().Seconds = 0.3f;
        }

        // Blinking: a short teleport to a free spot near the player, every few seconds while it's hunting.
        void Blink()
        {
            nextBlink = Time.time + UnityEngine.Random.Range(3.5f, 5f);
            Player p = GameController.playerScripts != null && GameController.playerScripts.Length > 0 ? GameController.playerScripts[0] : null;
            if (p == null || p.IsDead || ArcanosBridge.Incapacitated(enemy)) return;
            string st = enemy.fsm != null ? enemy.fsm.currentStateName ?? "" : "";
            if (st.Contains("Hurt") || st.Contains("Dead") || st.Contains("Frozen") || st.Contains("Fall") || st.StartsWith("WolArcanos")) return;
            float d = Vector2.Distance(p.transform.position, transform.position);
            if (d > 12f || d < 2f) return;
            Vector2 to = (Vector2)p.transform.position + ArcanaCaster.Rotate(Vector2.right, UnityEngine.Random.value * 360f) * 3.5f;
            try { to = Entity.GetAvailableNearbyLocation(to, false, false); } catch (Exception) { return; }
            if (Physics2D.Linecast(p.transform.position, to, LayerMask.GetMask("Wall"))) return; // not through a wall
            ArcanosBridge.Fx("Teletransporte", transform.position);
            transform.position = to;
            ArcanosBridge.Fx("Teletransporte", to);
        }

        void OnDeath()
        {
            Vector2 at = transform.position;
            // More gold, and sometimes an infused arcana.
            try { LootManager.DropGold(at, 15 + 10 * Mathf.Max(0, GameController.tierCount)); } catch (Exception) { }
            if (UnityEngine.Random.value < Plugin.EliteInfusedDropChance.Value)
            {
                try
                {
                    InfusionDropPatch.ForceNext = true;
                    LootManager.DropSkill(at, 1, null, 0f, 0f, null, false, false);
                }
                catch (Exception ex) { Plugin.Log.LogWarning("ELITE arcana drop failed: " + ex.Message); }
                finally { InfusionDropPatch.ForceNext = false; }
            }
            if (Trait == EliteTrait.Splitting) Split(at);
        }

        // Splitting: two smaller copies of the same enemy, at 40% of its health.
        void Split(Vector2 at)
        {
            string key = gameObject.name.Replace("(Clone)", "").Trim();
            for (int i = 0; i < 2; i++)
            {
                Vector2 pos = at + new Vector2(i == 0 ? -0.9f : 0.9f, 0f);
                try { pos = Entity.GetAvailableNearbyLocation(pos, false, false); } catch (Exception) { }
                Enemy copy = null;
                foreach (string k in new[] { key, enemy.GetType().Name })
                {
                    try { copy = Enemy.Spawn(k, pos, null, false, true, false, false, false); } catch (Exception) { copy = null; }
                    if (copy != null) break;
                }
                if (copy == null) { Plugin.Log.LogWarning("ELITE split: could not spawn a copy of " + key); return; }
                copy.gameObject.AddComponent<EliteSplitChild>();
                copy.transform.localScale = transform.localScale * 0.75f;
                copy.StartCoroutine(SetHealthLater(copy, Mathf.Max(1, Mathf.RoundToInt(maxHp * 0.4f))));
            }
            Plugin.Log.LogInfo("ELITE " + enemy.GetType().Name + " split in two");
        }

        static IEnumerator SetHealthLater(Enemy e, int hp)
        {
            yield return null;
            yield return null;
            if (e != null && e.health != null && e.health.CurrentHealthValue > hp) e.health.CurrentHealthValue = hp;
        }

        void OnGUI()
        {
            if (enemy == null || dead) return;
            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 sp = cam.WorldToScreenPoint(transform.position + new Vector3(0f, 2.1f, 0f));
            if (sp.z < 0f || sp.x < 0 || sp.x > Screen.width || sp.y < 0 || sp.y > Screen.height) return;
            if (style == null || GameFont.Changed(ref styleFont)) style = GameFont.Style(GUI.skin.label, 13, TextAnchor.MiddleCenter, true);
            GameFont.Shadowed(new Rect(sp.x - 90f, Screen.height - sp.y - 10f, 180f, 20f), Trait + " " + Elites.NameOf(enemy), style, Elites.ColorOf(Trait));
        }
    }

    internal class FadeOut : MonoBehaviour
    {
        public float Seconds = 0.3f;
        SpriteRenderer sr;
        float start, a0;
        void Start() { sr = GetComponent<SpriteRenderer>(); start = Time.time; a0 = sr != null ? sr.color.a : 1f; }
        void Update()
        {
            float t = (Time.time - start) / Seconds;
            if (t >= 1f || sr == null) { Destroy(gameObject); return; }
            Color c = sr.color; c.a = a0 * (1f - t); sr.color = c;
        }
    }

    // Elites are rolled one frame after an enemy starts (palettes and stats are set by then).
    [HarmonyPatch(typeof(Enemy), "Start")]
    static class EliteSpawnPatch
    {
        static void Postfix(Enemy __instance)
        {
            if (!Plugin.ElitesEnabled.Value || __instance == null || __instance is MiniBoss || __instance is Boss) return;
            try { __instance.StartCoroutine(Later(__instance)); } catch (Exception) { }
        }

        static IEnumerator Later(Enemy e)
        {
            yield return null;
            yield return null;
            try { Elites.TryMake(e); } catch (Exception ex) { Plugin.Log.LogWarning("ELITE roll failed: " + ex.Message); }
        }
    }

    // Draining: an elite that damages a player heals for half the health it took.
    [HarmonyPatch(typeof(Health), "TakeDamage")]
    static class EliteDrainPatch
    {
        static bool IsPlayers(Health h)
        {
            if (GameController.playerScripts == null) return false;
            foreach (Player p in GameController.playerScripts) if (p != null && p.health == h) return true;
            return false;
        }

        static void Prefix(Health __instance, out int __state) { __state = __instance != null ? __instance.CurrentHealthValue : 0; }

        static void Postfix(Health __instance, AttackInfo __0, int __state)
        {
            try
            {
                if (__instance == null || __0 == null || !IsPlayers(__instance)) return;
                Entity attacker = __0.entity;
                EliteMark m = attacker != null ? attacker.GetComponent<EliteMark>() : null;
                if (m == null || m.Trait != EliteTrait.Draining || attacker.health == null) return;
                int lost = __state - __instance.CurrentHealthValue;
                if (lost <= 0) return;
                attacker.health.RestoreHealth(Mathf.Max(1, lost / 2), false, false, false, false);
            }
            catch (Exception) { }
        }
    }
}
