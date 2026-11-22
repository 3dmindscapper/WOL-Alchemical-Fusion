using System;
using UnityEngine;

namespace WolHybridBosses
{
    // The game's own boss health bar (EnemyHealthBar, "Assets/Prefabs/UI/EnemyHealthBar.prefab", the one Boss.InitComponents
    // creates for the Council and Sura) for our rivals, champions and giants. Like a Boss: instantiate it with no parent
    // (the prefab has its own canvas), Claim(enemy), SetName, keep it hidden, FadeIn when the fight starts. The bar
    // follows health/shield by itself. Only one is shown at a time; it fades out a moment after the enemy dies.
    internal class BossBar : MonoBehaviour
    {
        static BossBar shown;
        EnemyHealthBar bar;
        Enemy owner;
        float deadSince = -1f;

        // Creates the bar for this enemy (hidden until Show), or returns the one it already has.
        internal static BossBar Attach(Enemy e, string name)
        {
            if (e == null || !Plugin.BossBars.Value) return null;
            BossBar bb = e.GetComponent<BossBar>();
            if (bb != null && bb.bar != null) { bb.SetName(name); return bb; }
            if (bb == null) bb = e.gameObject.AddComponent<BossBar>();
            bb.owner = e;
            try
            {
                bb.bar = Globals.ChaosInst<EnemyHealthBar>(EnemyHealthBar.Prefab, null, null, null);
                if (bb.bar == null) { Plugin.Log.LogWarning("BOSSBAR prefab missing"); return null; }
                EnsureCanvas(bb.bar);
                bb.bar.Claim(e);
                bb.bar.gameObject.SetActive(false);
                bb.SetName(name);
            }
            catch (Exception ex) { Plugin.Log.LogWarning("BOSSBAR create failed: " + ex.Message); bb.bar = null; return null; }
            return bb;
        }

        // Attach + show right away (giants, whose intro doesn't freeze anything).
        internal static void ShowFor(Enemy e, string name)
        {
            BossBar bb = Attach(e, name);
            if (bb != null) bb.Show();
        }

        internal void SetName(string name)
        {
            if (bar == null) return;
            try { bar.SetName(name ?? ""); } catch (Exception) { }
        }

        internal void Show()
        {
            if (bar == null) return;
            if (shown != null && shown != this) shown.Hide(); // one boss bar at a time: the newest fight wins
            shown = this;
            try { bar.FadeIn(); } catch (Exception ex) { Plugin.Log.LogWarning("BOSSBAR fade-in failed: " + ex.Message); bar.gameObject.SetActive(true); }
            Plugin.Log.LogInfo("BOSSBAR shown for " + (owner != null ? owner.GetType().Name : "?"));
        }

        void Hide()
        {
            if (bar != null) bar.gameObject.SetActive(false);
            if (shown == this) shown = null;
        }

        void Update()
        {
            if (bar == null) return;
            bool dead = owner == null || owner.health == null || owner.health.CurrentHealthValue <= 0;
            if (!dead) { deadSince = -1f; return; }
            if (deadSince < 0f) deadSince = Time.time;
            if (Time.time - deadSince > 1.5f) Remove(); // let the bar show the last hit drain first
        }

        void Remove()
        {
            if (shown == this) shown = null;
            if (bar != null)
            {
                Destroy(bar.gameObject);
                bar = null;
            }
        }

        void OnDestroy() { Remove(); }

        // The boss bar prefab normally brings its own canvas; if it doesn't, put it under the game's screen canvas.
        static void EnsureCanvas(EnemyHealthBar b)
        {
            if (b.GetComponentInParent<Canvas>() != null) return;
            foreach (Canvas c in FindObjectsOfType<Canvas>())
            {
                if (c == null || !c.isRootCanvas || c.renderMode == RenderMode.WorldSpace) continue;
                b.transform.SetParent(c.transform, false);
                Plugin.Log.LogInfo("BOSSBAR parented to canvas " + c.name);
                return;
            }
            Plugin.Log.LogWarning("BOSSBAR no screen canvas found; the bar may not be visible");
        }
    }
}
