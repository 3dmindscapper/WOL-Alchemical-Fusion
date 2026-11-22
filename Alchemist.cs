using System;
using System.Collections.Generic;
using UnityEngine;

namespace WolHybridBosses
{
    // v1.1.0: the White Alchemist (v1.1.1: the Primordial Alchemist, the essence every element flows from), a final champion (config FinalBoss.AlchemistChance of champion runs): a wizard
    // recoloured pure white with a white aura that never fades, every arcana dual-infused (two elements alternating),
    // and a "Transmuted" second phase that fuses two more. Beating it always teaches you one new infused version of an
    // arcana you're carrying; the first win also gives the White Mantle.
    //
    // The White Mantle (worn from the modifiers menu once earned): your wizard turns white with the same aura, and your
    // infused and dual arcana hit 15% harder and cool down 15% faster. It's a look and a bonus on top of whatever robe
    // you wear, so nothing is added to the game's wardrobe or save data.
    internal static class WhiteAlchemist
    {
        // v1.1.1: the Primordial Alchemist, a godlike manifestation of the essence every element flows from.
        internal const string Name = "The Primordial Alchemist";
        internal static readonly Color White = new Color(0.96f, 0.97f, 1f);

        // ---------- v1.1.1: its words ----------

        static string Pick(string[] o) { return o[UnityEngine.Random.Range(0, o.Length)]; }

        static readonly string[] Hello =
        {
            "Before the first spark, before the first drop, there was me. I am the Primordial Alchemist.",
            "Every element you have ever cast was borrowed from me. I am the Primordial Alchemist, and I have come to see what you did with it.",
            "Fire, water, earth, wind, lightning... only echoes. I am what they echo. Call me the Primordial Alchemist.",
        };

        static readonly string[] NoInfusion =
        {
            "No infusions? You still wield the elements one at a time, the way the Council taught you.",
            "Your arcana are pure, untouched. Let me show you what they could become.",
            "Not a single fusion? Visit Tomi, little wizard. The elements were never meant to stay apart.",
            "You have never fused an element. Learn to blend them, and one day you may stand where I stand.",
        };

        static readonly string[] Challenge =
        {
            "Come. Let us see which of us the elements answer to.",
            "Show me what you have made of my gifts.",
            "Every element is mine. Let us find out if any of them are yours.",
        };

        static readonly Dictionary<string, string[]> ElementWords = new Dictionary<string, string[]>
        {
            { "Fire", new[] { "Fire remembers me most. It still burns to come home.", "So much fire in you. It was always the most impatient of my children." } },
            { "Water", new[] { "Water was my first reflection. It still shows my face, if you look.", "You favour water. It takes every shape and keeps none, much like me." } },
            { "Ice", new[] { "Ice is only water that stopped to listen.", "You carry ice: water that chose stillness. Wise, or afraid?" } },
            { "Earth", new[] { "Earth is my patience, made solid.", "You lean on earth. Mountains are just my thoughts, given time." } },
            { "Wind", new[] { "Wind is my breath, still wandering.", "You ride the wind. It never forgot how to be free." } },
            { "Thunder", new[] { "Thunder is my voice, borrowed by the sky.", "Lightning in your hands. The loudest of my children, and the quickest to fade." } },
            { "Chaos", new[] { "Chaos... the part of me the Council fears to name.", "You touch Chaos. It is not the opposite of the elements. It is all of them, at once, unsorted." } },
            { "Radiant", new[] { "Radiance: my light, caught in glass.", "You carry radiance. It is what I looked like, the first morning." } },
            { "Prism", new[] { "A prism splits me into colours. Clever.", "Prismatic. You have learned I can be every colour at once." } },
        };

        static readonly string[] Hurt = { "You... unmake me? No. You refine me.", "Interesting. You transmute pain into progress.", "Half of everything is still everything." };
        static readonly string[] Defeat = { "Return me to the elements... I will be in every spark you cast.", "So the student fuses the master. Fitting.", "Go on, then. Carry my essence. You have earned it." };

        internal static string HurtLine() { return Pick(Hurt); }
        internal static string DefeatLine() { return Pick(Defeat); }

        // Its introduction: who it is, one comment (a dual-infused arcana, else an infused one, else the element you lean
        // on or a nudge toward fusion), and its challenge.
        internal static List<string> IntroLines()
        {
            List<string> lines = new List<string> { Pick(Hello) };
            Player p = Elites.Player0;
            string dualArc = null, singleArc = null;
            ElementType da = ElementType.Neutral, db = ElementType.Neutral, sa = ElementType.Neutral;
            Dictionary<string, int> counts = new Dictionary<string, int>();
            if (p != null && p.assignedSkills != null)
                foreach (Player.SkillState s in p.assignedSkills)
                {
                    if (s == null || string.IsNullOrEmpty(s.skillID)) continue;
                    ElementType a, b;
                    bool infused = Infusion.TryGet(s.skillID, out a);
                    bool dual = infused && Infusion.TryGetSecond(s.skillID, out b);
                    if (dual && dualArc == null) { Infusion.TryGetSecond(s.skillID, out b); dualArc = IntroUI.SkillName(s.skillID); da = a; db = b; }
                    else if (infused && !dual && singleArc == null) { singleArc = IntroUI.SkillName(s.skillID); sa = a; }
                    string n = Infusion.Name(Infusion.CurrentElement(s));
                    if (n == "Neutral") continue;
                    int c; counts.TryGetValue(n, out c); counts[n] = c + 1;
                }
            if (dualArc != null)
                lines.Add(Pick(new[]
                {
                    Infusion.Name(da) + " and " + Infusion.Name(db) + ", braided into your " + dualArc + ". You have glimpsed what I am made of.",
                    "Your " + dualArc + " carries " + Infusion.Name(da) + " and " + Infusion.Name(db) + " at once. Few wizards dare to fuse what the Council keeps apart.",
                }));
            else if (singleArc != null)
                lines.Add(Pick(new[]
                {
                    Infusion.Name(sa) + " poured into your " + singleArc + ". A small transmutation... but a true one.",
                    "I can feel " + Infusion.Name(sa) + " where it does not belong, in your " + singleArc + ". Good. Rules are for apprentices.",
                }));
            else
            {
                // v1.1.1: one comment only. Without infusions: half the time the element you lean on most, otherwise
                // a nudge toward fusion (also the fallback when no element stands out).
                string top = null;
                int best = 0;
                foreach (KeyValuePair<string, int> kv in counts) if (kv.Value > best) { best = kv.Value; top = kv.Key; }
                string[] words;
                if (top != null && ElementWords.TryGetValue(top, out words) && UnityEngine.Random.value < 0.5f) lines.Add(Pick(words));
                else lines.Add(Pick(NoInfusion));
            }
            lines.Add(Pick(Challenge));
            return lines;
        }

        // Before the fight: it speaks while frozen and untouchable, the camera on it.
        internal static System.Collections.IEnumerator Monologue(Enemy boss) { return ChampionSpeech.Monologue(boss, Name, IntroLines(), White); }

        // At the end of the champion's hostile setup.
        internal static void Setup(Enemy e)
        {
            if (e == null) return;
            try { RecolorEngine.RecolorBody(e, "Pure", false); } catch (Exception ex) { Plugin.Log.LogWarning("ALCHEMIST recolour failed: " + ex.Message); }
            WhiteAura.Attach(e.transform, e.spriteRenderer != null ? e.spriteRenderer.material : null);
            Plugin.Log.LogInfo("ALCHEMIST ready: white, aura, every arcana dual");
        }

        // The champion fell (FinalChampion.Finish).
        internal static void Rewards()
        {
            string learned = TeachInfusion();
            Progress.AddInt("alchemist.wins", 1);
            bool first = !Progress.GetBool("mantle.unlocked");
            if (first)
            {
                Progress.SetBool("mantle.unlocked", true);
                Progress.SetBool("mantle.on", true);
            }
            Plugin.Log.LogInfo("ALCHEMIST defeated (wins " + Progress.GetInt("alchemist.wins") + ")" + (learned != null ? ", learned " + learned : "") + (first ? ", White Mantle unlocked" : ""));
            if (first) IntroUI.Show("The White Mantle is yours", (MantleRobe.Registered ? "A new robe in the wardrobe" : "Wear it from the modifiers menu (F1 in the Hub)") + "  ·  infused arcana hit harder and cool down faster" + (learned != null ? "  ·  learned " + learned : ""), White, null, 6f);
            else if (learned != null) IntroUI.Show("Infusion learned", learned, White, null, 4f);
        }

        // One new infused version of an arcana you're carrying (unlocked for good).
        static string TeachInfusion()
        {
            Player p = Elites.Player0;
            if (p == null || p.assignedSkills == null) return null;
            List<ElementType> all = new List<ElementType> { ElementType.Fire, ElementType.Water, ElementType.Ice, ElementType.Earth, ElementType.Air, ElementType.Lightning, ElementType.Chaos };
            List<KeyValuePair<string, ElementType>> options = new List<KeyValuePair<string, ElementType>>();
            foreach (Player.SkillState s in p.assignedSkills)
            {
                if (s == null || string.IsNullOrEmpty(s.skillID) || s.isSignature) continue;
                HashSet<string> have = new HashSet<string>();
                foreach (ElementType v in InfusionUnlocks.Versions(s.skillID)) have.Add(Infusion.Name(v));
                ElementType? own = InfusionUnlocks.OwnElement(s.skillID);
                foreach (ElementType el in all)
                {
                    string n = Infusion.Name(el);
                    if (have.Contains(n) || (own.HasValue && Infusion.Name(own.Value) == n)) continue;
                    options.Add(new KeyValuePair<string, ElementType>(s.skillID, el));
                }
            }
            if (options.Count == 0) return null;
            KeyValuePair<string, ElementType> pick = options[UnityEngine.Random.Range(0, options.Count)];
            InfusionUnlocks.Unlock(pick.Key, pick.Value);
            return Infusion.Name(pick.Value) + "-infused " + IntroUI.SkillName(pick.Key);
        }
    }

    // A white overdrive aura that stays on (the game's own signature aura).
    internal static class WhiteAura
    {
        internal static OverdriveEffects Attach(Transform owner, Material mat) { return Attach(owner, mat, new Color(1.5f, 1.55f, 1.6f, 0.5f)); }

        // v1.1.1: any colour (Archons wear their element's).
        internal static OverdriveEffects Attach(Transform owner, Material mat, Color colour)
        {
            try
            {
                GameObject prefab = OverdriveEffects.Prefab;
                if (prefab == null || owner == null) return null;
                GameObject go = UnityEngine.Object.Instantiate(prefab, owner) as GameObject;
                if (go == null) return null;
                go.name = "HB_WhiteAura";
                OverdriveEffects fx = go.GetComponent<OverdriveEffects>();
                if (fx == null) { UnityEngine.Object.Destroy(go); return null; }
                fx.Initialize(colour, owner, mat);
                fx.ToggleAura(true);
                return fx;
            }
            catch (Exception ex) { Plugin.Log.LogWarning("WHITE aura failed: " + ex.Message); return null; }
        }
    }

    // The White Mantle on the player (lives on the persistent runner object).
    internal class WhiteMantle : MonoBehaviour
    {
        internal static bool Unlocked { get { return Progress.GetBool("mantle.unlocked"); } }
        // With LegendAPI the mantle is a wardrobe robe: worn = that robe equipped. Without it: the modifiers menu toggle.
        internal static bool Worn { get { return Unlocked && (MantleRobe.Registered ? MantleRobe.Wearing : Progress.GetBool("mantle.on")); } }
        internal static bool Active { get; private set; }

        internal const float DamageBonus = 1.15f, CooldownFactor = 0.85f;

        static readonly NumVarStatMod CooldownMod = new NumVarStatMod("HB_WhiteMantle", CooldownFactor, 10, VarStatModType.Multiplicative, false);
        readonly Dictionary<Material, Texture> originals = new Dictionary<Material, Texture>();
        readonly Dictionary<string, StatData> cooldowns = new Dictionary<string, StatData>();
        OverdriveEffects aura;
        Player owner;
        float next;

        internal static void Toggle()
        {
            if (!Unlocked || MantleRobe.Registered) return; // a wardrobe robe: worn from the wardrobe
            Progress.SetBool("mantle.on", !Progress.GetBool("mantle.on"));
        }

        void Update()
        {
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + 1f;
            Player p = Elites.Player0;
            bool want = Worn && p != null;
            if (owner != null && owner != p) Clear(); // a new player object (new scene)
            if (!want) { if (Active) Clear(); return; }
            owner = p;
            Active = true;
            Whiten(p);
            if (aura == null || aura.transform.parent != p.transform) aura = WhiteAura.Attach(p.transform, p.spriteRenderer != null ? p.spriteRenderer.material : null);
            UpdateCooldowns(p);
        }

        void Whiten(Player p)
        {
            foreach (SpriteRenderer sr in p.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sr == null || !RecolorEngine.PaletteSprite(sr)) continue;
                Material m = sr.material;
                Texture pal = m.GetTexture("_Palette");
                if (pal == null || pal.name.EndsWith("(Pure)")) continue;
                Texture2D twin = RecolorEngine.PaletteTwin(pal, "Pure");
                if (twin == null) continue;
                if (!originals.ContainsKey(m)) originals[m] = pal;
                m.SetTexture("_Palette", twin);
            }
        }

        // Infused (or dual) arcana you're carrying cool down faster; the bonus leaves an arcana once it isn't infused.
        void UpdateCooldowns(Player p)
        {
            HashSet<string> now = new HashSet<string>();
            if (p.assignedSkills != null)
                foreach (Player.SkillState s in p.assignedSkills)
                {
                    ElementType el;
                    if (s != null && !string.IsNullOrEmpty(s.skillID) && Infusion.TryGet(s.skillID, out el)) now.Add(s.skillID);
                }
            foreach (string id in now)
            {
                if (cooldowns.ContainsKey(id)) continue;
                try
                {
                    StatData d = StatManager.GetSkillData("Player", id);
                    if (d == null) continue;
                    d.ModifyMod(StatData.cdStr, CooldownMod, true);
                    cooldowns[id] = d;
                }
                catch (Exception) { }
            }
            foreach (string id in new List<string>(cooldowns.Keys))
                if (!now.Contains(id)) { try { cooldowns[id].ModifyMod(StatData.cdStr, CooldownMod, false); } catch (Exception) { } cooldowns.Remove(id); }
        }

        void Clear()
        {
            Active = false;
            foreach (KeyValuePair<Material, Texture> kv in originals) if (kv.Key != null) kv.Key.SetTexture("_Palette", kv.Value);
            originals.Clear();
            foreach (KeyValuePair<string, StatData> kv in cooldowns) { try { kv.Value.ModifyMod(StatData.cdStr, CooldownMod, false); } catch (Exception) { } }
            cooldowns.Clear();
            if (aura != null) Destroy(aura.gameObject);
            aura = null;
            owner = null;
        }

        void OnDestroy() { Clear(); }
    }
}
