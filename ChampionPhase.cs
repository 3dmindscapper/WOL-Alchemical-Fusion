using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WolHybridBosses
{
    // Final champions' second phase, once, at half health. The champion freezes for a moment (invulnerable, camera
    // shake, the game's overdrive burst), an "awakens" card shows what it gained, the boss bar is renamed, then:
    //  - an Archon becomes a HYBRID: it keeps its 2 strongest arcana and takes 3 of a second element (a new signature
    //    arcana among them if one fits), and its spells come faster;
    //  - the Chaos Sovereign takes 2 more arcana from any element and casts faster still.
    internal class ChampionPhase : MonoBehaviour
    {
        const float Threshold = 0.5f, FreezeSeconds = 2.2f;
        Enemy owner;
        RivalMarker mk;
        string title;
        bool done;

        internal static void Attach(Enemy e, string title)
        {
            if (e == null || e.GetComponent<ChampionPhase>() != null) return;
            ChampionPhase cp = e.gameObject.AddComponent<ChampionPhase>();
            cp.owner = e;
            cp.mk = e.GetComponent<RivalMarker>();
            cp.title = title;
        }

        void Update()
        {
            if (done || owner == null || mk == null || !owner.enabled || owner.health == null) return;
            if (owner.health.CurrentHealthValue <= 0) { done = true; return; }
            if (owner.health.HealthPercentage > Threshold) return;
            done = true;
            StartCoroutine(Shift());
        }

        IEnumerator Shift()
        {
            // The cue: frozen, invulnerable, burst + shake.
            owner.enabled = false;
            if (owner.movement != null) owner.movement.EndMovement();
            if (owner.health != null) owner.health.SetInvulnerabilityDuration(FreezeSeconds + 1f, false);
            try { CameraController.ShakeCamera(2f, true); } catch (Exception) { }
            Burst(owner, ElementalGiants.ElementColor(mk.Prefix));

            string second = null;
            List<ArcanaDef> gained;
            string newTitle;
            if (mk.Prefix == "Chaos")
            {
                gained = Rivals.PickVaried(Others(ArcanaCatalog.Pool("Chaos")), 2, true);
                newTitle = title + ", Unbound";
                mk.GapMultiplier = 0.7f;
            }
            else
            {
                List<string> options = new List<string>(ElementalGiants.Prefixes);
                options.Remove(mk.Prefix);
                second = options[UnityEngine.Random.Range(0, options.Count)];
                // Keep its 2 strongest arcana, drop the rest from the AI's options.
                List<ArcanaDef> keep = new List<ArcanaDef>(mk.Arcana);
                keep.Sort((a, b) => b.Priority.CompareTo(a.Priority));
                foreach (ArcanaDef d in keep.GetRange(Mathf.Min(2, keep.Count), Mathf.Max(0, keep.Count - 2))) Drop(d);
                mk.Arcana = keep.GetRange(0, Mathf.Min(2, keep.Count));
                gained = Rivals.PickVaried(Others(ArcanaCatalog.Pool(second)), 3, false);
                newTitle = ElementalGiants.ElementColorName(mk.Prefix) + "–" + ElementalGiants.ElementColorName(second) + " Archon";
                mk.GapMultiplier = 0.85f;
                mk.SecondPrefix = second;
            }

            EnemyArcana.Give(owner, gained, Rivals.TierCooldown(mk.Tier) * mk.Bonus.Cooldown, Rivals.TierWindup(mk.Tier));
            mk.Arcana.AddRange(gained);

            // A new signature arcana if the old one was dropped (or a better fit is now there).
            if (mk.Signature == null || !mk.Arcana.Exists(a => a.SkillID == mk.Signature.SkillID))
            {
                ArcanaDef sig = null;
                foreach (ArcanaDef d in gained) if (Signature.Capable(d) && (sig == null || d.Priority > sig.Priority)) sig = d;
                if (sig != null)
                {
                    mk.Signature = sig;
                    SignatureMeter meter = owner.GetComponent<SignatureMeter>();
                    if (meter != null) meter.SkillID = sig.SkillID;
                }
            }

            BossBar bar = owner.GetComponent<BossBar>();
            if (bar != null) bar.SetName(newTitle);

            List<IntroEntry> entries = new List<IntroEntry>();
            Color col = ElementalGiants.ElementColor(second ?? mk.Prefix);
            foreach (ArcanaDef d in gained)
            {
                bool isSig = mk.Signature != null && mk.Signature.SkillID == d.SkillID;
                IntroEntry ie = new IntroEntry(IntroUI.SkillIcon(d.SkillID), IntroUI.SkillName(d.SkillID) + (isSig ? " (signature)" : ""));
                if (ie.Icon == null) ie.Tint = col;
                entries.Add(ie);
            }
            IntroUI.Show(newTitle + " awakens", second != null ? "Second phase  ·  now wields " + ElementalGiants.ElementColorName(second) : "Second phase  ·  unbound",
                col, entries, FreezeSeconds);
            if (second != null) Burst(owner, col);

            Plugin.Log.LogInfo("FINAL phase 2: " + newTitle + ", arcana now " + Rivals.ArcanaList(mk.Arcana)
                + ", signature " + (mk.Signature != null ? mk.Signature.SkillID : "none") + ", gap x" + mk.GapMultiplier);

            yield return new WaitForSeconds(FreezeSeconds);
            if (owner == null) yield break;
            Rivals.DelayCasts(owner, 0.6f);
            owner.enabled = true;
        }

        // Arcana it doesn't already have (and not basics).
        List<ArcanaDef> Others(List<ArcanaDef> pool)
        {
            pool.RemoveAll(d => d.Family == ArcanaFamily.Melee || EnemyArcana.Broken.Contains(d.SkillID)
                || mk.Arcana.Exists(a => a.SkillID == d.SkillID));
            return pool;
        }

        // Out of the AI's options (the FSM keeps the state, harmless).
        void Drop(ArcanaDef d)
        {
            if (owner.availableEnemySkillsDict == null) return;
            string key = "Arcana" + d.SkillID;
            if (owner.availableEnemySkillsDict.ContainsKey(key)) owner.availableEnemySkillsDict.Remove(key);
        }

        // The game's overdrive burst (sound + particles) on the champion.
        static void Burst(Enemy e, Color c)
        {
            try
            {
                GameObject prefab = OverdriveEffects.Prefab;
                if (prefab == null) return;
                GameObject go = UnityEngine.Object.Instantiate(prefab, e.transform) as GameObject;
                OverdriveEffects fx = go != null ? go.GetComponent<OverdriveEffects>() : null;
                if (fx == null) return;
                fx.Initialize(new Color(c.r * 1.6f, c.g * 1.6f, c.b * 1.6f, 0.5f), e.transform, e.spriteRenderer != null ? e.spriteRenderer.material : null);
                fx.Burst(40, false, true, true);
                UnityEngine.Object.Destroy(go, 3f);
            }
            catch (Exception) { }
        }
    }
}
