using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WolHybridBosses
{
    // v1.1.1: the Primordial Alchemist's true form. Once you own every Primordial robe (the six base elements, plus
    // Radiant with WoL Arcanos), the Alchemist has a third phase: at 25% health (after its Transmuted phase) it
    // freezes, speaks, heals back to 60% and starts cycling through its children's forms, one every 9 seconds: each
    // Archon's colours and aura, and two arcana of that element in place of the previous form's.
    internal class AlchemistTrueForm : MonoBehaviour
    {
        const float Threshold = 0.25f, HealTo = 0.6f, FormSeconds = 9f, FreezeSeconds = 3f;

        static readonly string[] BaseForms = { "Flame", "Frost", "Earth", "Wind", "Thunder", "Chaos" };
        static readonly Dictionary<string, string> Shouts = new Dictionary<string, string>
        {
            { "Flame", "Burn!" }, { "Frost", "Drown!" }, { "Earth", "Be buried!" }, { "Wind", "Be swept away!" },
            { "Thunder", "Be struck down!" }, { "Chaos", "Unravel!" }, { "Radiant", "Be blinded!" },
        };

        Enemy owner;
        RivalMarker mk;
        readonly Dictionary<SpriteRenderer, Texture> originals = new Dictionary<SpriteRenderer, Texture>();
        readonly List<ArcanaDef> formArcana = new List<ArcanaDef>();
        List<string> forms;
        bool started, seenHigh;
        int formIndex = -1;
        float nextForm;
        OverdriveEffects aura;

        internal static List<string> Forms()
        {
            List<string> l = new List<string>(BaseForms);
            if (ArcanosBridge.Present) l.Add("Radiant");
            return l;
        }

        // Every robe the true form asks for.
        internal static bool Earned
        {
            get
            {
                foreach (string f in Forms()) { PrimordialRobe r = PrimordialRobes.For(f); if (r == null || !r.Unlocked) return false; }
                return true;
            }
        }

        // From WhiteAlchemist.Setup, before its body turns white.
        internal static void RememberPalettes(Enemy e)
        {
            AlchemistTrueForm tf = e.GetComponent<AlchemistTrueForm>() ?? e.gameObject.AddComponent<AlchemistTrueForm>();
            tf.owner = e;
            foreach (SpriteRenderer sr in e.GetComponentsInChildren<SpriteRenderer>(true))
                if (sr != null && RecolorEngine.PaletteSprite(sr)) { Texture pal = sr.material.GetTexture("_Palette"); if (pal != null && !tf.originals.ContainsKey(sr)) tf.originals[sr] = pal; }
        }

        void Update()
        {
            if (owner == null) { owner = GetComponent<Enemy>(); return; }
            if (mk == null) mk = GetComponent<RivalMarker>();
            if (mk == null || !mk.Alchemist || owner.health == null) return;
            if (!started)
            {
                // v1.1.1: only once the fight is on and its Transmuted phase has run, and after its health has been seen
                // above the threshold: right after spawning, its health is still the base wizard's (a few % of its real
                // maximum), which set this off during its intro and un-froze it mid-speech.
                ChampionPhase cp = GetComponent<ChampionPhase>();
                if (cp == null || !cp.Shifted || !owner.enabled || owner.health.CurrentHealthValue <= 0) return;
                if (owner.health.HealthPercentage > Threshold + 0.02f) { seenHigh = true; return; }
                if (!seenHigh) return;
                if (!Earned) { enabled = false; return; } // not this time: the fight ends as usual
                started = true;
                StartCoroutine(Awaken());
                return;
            }
            if (formIndex >= 0 && owner.enabled && Time.time >= nextForm && owner.health.CurrentHealthValue > 0) NextForm(true);
        }

        IEnumerator Awaken()
        {
            owner.enabled = false;
            if (owner.movement != null) owner.movement.EndMovement();
            owner.health.SetInvulnerabilityDuration(FreezeSeconds + 1.5f, false);
            try { CameraController.ShakeCamera(3f, true); } catch (Exception) { }
            forms = Forms();
            RivalSpeech.Say(WhiteAlchemist.Name, "You have worn the skin of every one of my children. Then let me wear them too.", WhiteAlchemist.White, FreezeSeconds + 0.5f);
            List<string> names = new List<string>();
            foreach (string f in forms) names.Add(ElementalGiants.ElementColorName(f));
            IntroUI.Show(WhiteAlchemist.Name + ", True Form", "Third phase  ·  " + string.Join(", ", names.ToArray()), WhiteAlchemist.White, null, FreezeSeconds);
            try
            {
                int max = Mathf.RoundToInt(owner.health.healthStat.CurrentValue);
                owner.health.CurrentHealthValue = Mathf.Max(owner.health.CurrentHealthValue, Mathf.RoundToInt(max * HealTo));
            }
            catch (Exception) { }
            BossBar bar = owner.GetComponent<BossBar>();
            if (bar != null) bar.SetName(WhiteAlchemist.Name + ", True Form");
            mk.GapMultiplier = Mathf.Min(mk.GapMultiplier, 0.65f);
            Plugin.Log.LogInfo("ALCHEMIST true form: cycling " + string.Join(", ", forms.ToArray()));
            yield return new WaitForSeconds(FreezeSeconds);
            if (owner == null) yield break;
            NextForm(false);
            Rivals.DelayCasts(owner, 0.6f);
            owner.enabled = true;
        }

        void NextForm(bool shout)
        {
            formIndex = (formIndex + 1) % forms.Count;
            nextForm = Time.time + FormSeconds;
            string f = forms[formIndex];
            Color c = ElementalGiants.ElementColor(f);
            // Look: its original palettes onto this element's ramp, and an aura of its colour.
            string key = ArchonLook.RampForPrefix(f);
            if (key != null)
                foreach (KeyValuePair<SpriteRenderer, Texture> kv in originals)
                {
                    if (kv.Key == null) continue;
                    Texture2D twin = RecolorEngine.PaletteTwin(kv.Value, key);
                    if (twin != null) kv.Key.material.SetTexture("_Palette", twin);
                }
            foreach (Transform t in owner.transform) if (t.name == "HB_WhiteAura") Destroy(t.gameObject);
            if (aura != null) Destroy(aura.gameObject);
            aura = WhiteAura.Attach(owner.transform, owner.spriteRenderer != null ? owner.spriteRenderer.material : null, new Color(c.r * 1.5f, c.g * 1.5f, c.b * 1.5f, 0.5f));
            // Moveset: the previous form's arcana out, two of this element in.
            foreach (ArcanaDef d in formArcana)
            {
                mk.Arcana.RemoveAll(a => a.SkillID == d.SkillID);
                if (owner.availableEnemySkillsDict != null) owner.availableEnemySkillsDict.Remove("Arcana" + d.SkillID);
            }
            formArcana.Clear();
            List<ArcanaDef> pool = f == "Radiant" ? Variants.CastPool(StageVariant.Radiant) : ArcanaCatalog.Pool(f);
            pool.RemoveAll(d => d.Family == ArcanaFamily.Melee || EnemyArcana.Broken.Contains(d.SkillID) || mk.Arcana.Exists(a => a.SkillID == d.SkillID));
            List<ArcanaDef> picks = Rivals.PickVaried(pool, 2, false);
            EnemyArcana.Give(owner, picks, Rivals.TierCooldown(mk.Tier) * mk.Bonus.Cooldown * 0.85f, Rivals.TierWindup(mk.Tier));
            mk.Arcana.AddRange(picks);
            formArcana.AddRange(picks);
            string shoutLine;
            if (shout && Shouts.TryGetValue(f, out shoutLine)) RivalSpeech.Say(WhiteAlchemist.Name, shoutLine, c, 1.6f);
            Plugin.Log.LogInfo("ALCHEMIST form: " + f + " (" + Rivals.ArcanaList(picks) + ")");
        }
    }
}
