using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace WolHybridBosses
{
    // Variant Council (v0.6.0; the Corrupted Council since v0.5.0): a Council member whose tier had a variant stage
    // takes that variant: Corrupted (Chaos), Radiant or Prismatic. Their sprites take the variant's tint and outline,
    // their attacks its damage type and colour, the boss bar reads "<Title> <name>", they get 2 echoes of that
    // element between their own attacks, custom lines, their room takes the variant's look, and their chest drops an
    // arcana of that element (Radiant / Prism: only one the player has already unlocked). Same seed as the stages, so
    // the board and the fight always agree.
    internal static class CouncilCorruption
    {
        internal static readonly Color Purple = new Color(0.62f, 0.32f, 1f);

        // The variant of the current fight (Chaos when none is running, for the old callers).
        internal static StageVariant Variant = StageVariant.Chaos;
        static VariantInfo Info { get { return Variants.Info(Variant); } }

        // Kept for older callers: the Chaos ramp.
        internal static Color ChaosRamp(Color c) { return Variants.Ramp(c, StageVariant.Chaos); }

        // ---------- custom voicelines ----------
        // Council dialogs have the member speak every line (2 per dialog; the player's portrait is shown but silent).
        // Each line is replaced by the variant's, in the game's upbeat mentor tone: the member has been dabbling in the
        // new element and wants to trade their own in for it, and comes back to it once beaten.
        static readonly Dictionary<string, string[][]> ChaosLines = new Dictionary<string, string[][]>
        {
            { "FireBoss", new[] {
                new[] { "Fire? Oh, that's old news! I've been dabbling in Chaos lately, and let me tell you, it's SO much hotter!",
                        "Don't hold back! Let's see if your flames can keep up with my brand new style!" },
                new[] { "Phew... maybe Chaos was a little too spicy, even for me.",
                        "Fine, fine, I'll stick to fire. Keep that spirit burning, alright?" } } },
            { "IceBoss", new[] {
                new[] { "Ice has served me well, but it's so... predictable. Chaos, on the other hand, keeps me on my toes.",
                        "Shall we find out which is colder? My old element, or this delightful void?" },
                new[] { "Hmm. It seems Chaos won't sit still the way ice does. How humbling.",
                        "Perhaps I'll return to my frost after all. Stay sharp out there, wizard." } } },
            { "EarthBoss", new[] {
                new[] { "I've stood firm on the earth for years. Figured it was time to try something with a little more... Chaos!",
                        "Brace yourself! I haven't quite figured out how to aim it yet!" },
                new[] { "Ha! Back on solid ground. Chaos is far too shaky for these old bones.",
                        "You've got steady footing, wizard. Don't let anything knock you off it!" } } },
            { "AirBoss", new[] {
                new[] { "The wind is nice and all, but Chaos? Chaos blows wherever it pleases! Just like me!",
                        "Try to keep up! Even I don't know what's coming next!" },
                new[] { "Whew, what a whirlwind. Maybe I'll let the breeze carry me again.",
                        "You read the winds well, wizard. Chaos too, apparently!" } } },
            { "LightningBoss", new[] {
                new[] { "Lightning was fast. Chaos is FASTER! We've traded up, and we're never going back!",
                        "Ready for a double dose of Chaos? Don't blink!" },
                new[] { "Okay, okay... maybe we were a little too quick to ditch the lightning.",
                        "You've got real spark, wizard! Back to the storm for us... for now." } } },
        };

        static readonly Dictionary<string, string[][]> RadiantLines = new Dictionary<string, string[][]>
        {
            { "FireBoss", new[] {
                new[] { "Why burn when you can SHINE? I've traded my flames for pure Radiance, and I look fabulous!",
                        "Shield your eyes, wizard! This light is hotter than any fire I ever threw!" },
                new[] { "Ow, ow... turns out glowing all day is exhausting.",
                        "Alright, back to good old fire. A little smoke never hurt anyone!" } } },
            { "IceBoss", new[] {
                new[] { "Ice merely reflects the light. I decided to become the light itself. Radiance suits me, don't you think?",
                        "Let us see whether you can bear to look straight at it." },
                new[] { "So bright, and yet so tiring to hold. Frost was always kinder to me.",
                        "I shall return to the cold. Do keep your own light burning, wizard." } } },
            { "EarthBoss", new[] {
                new[] { "Spent my whole life underground. Came up for air and found Radiance! Gold all the way down!",
                        "Hope you brought something to cover your eyes, kid!" },
                new[] { "Bah! Too much sun for these old bones. Give me a nice dark cave.",
                        "You shone brighter than I did, wizard. Hold on to that!" } } },
            { "AirBoss", new[] {
                new[] { "Wind? Invisible! Radiance? Everyone can see me now! Isn't it wonderful?",
                        "Catch me if you can! Just don't stare too long!" },
                new[] { "Hm, being seen all the time is kind of a lot. I miss sneaking around on the breeze.",
                        "Back to the wind it is! You've got a bright future, wizard!" } } },
            { "LightningBoss", new[] {
                new[] { "Lightning is just light that's in a hurry. So we skipped the hurry and kept the light!",
                        "Pure Radiance, double the shine! Try not to blink!" },
                new[] { "Okay, that glow didn't strike half as hard as our bolts.",
                        "Back to the storm! You really light up a fight, wizard!" } } },
        };

        static readonly Dictionary<string, string[][]> PrismLines = new Dictionary<string, string[][]>
        {
            { "FireBoss", new[] {
                new[] { "One element? How dull! With the Prism I get EVERY color, and they're all mine!",
                        "Red, blue, green, gold... let's see which one you can't handle!" },
                new[] { "Whoa, too many colors at once. I think I saw the inside of a rainbow.",
                        "Red is still my favorite. Keep that fire bright, alright?" } } },
            { "IceBoss", new[] {
                new[] { "A single crystal splits the light into every color. I've simply become that crystal.",
                        "Shall we see how you fare against all the elements at once?" },
                new[] { "Splitting myself so many ways left very little of me. How unwise.",
                        "Clear ice will do just fine. Stay sharp, wizard." } } },
            { "EarthBoss", new[] {
                new[] { "Found a shiny gem down in the deep, and now I've got every element rattling around in here!",
                        "Don't ask me which one's coming next. I'm as surprised as you are!" },
                new[] { "My head's spinning like a kaleidoscope. One element is plenty, thanks.",
                        "Steady as stone from now on. Nice work, kid!" } } },
            { "AirBoss", new[] {
                new[] { "Why pick one breeze when you can ride a rainbow? The Prism lets me be everything at once!",
                        "Every color, every element! Try to keep up!" },
                new[] { "Okay, rainbows are a lot heavier than they look.",
                        "I'll stick to the wind. You've got all the colors covered anyway!" } } },
            { "LightningBoss", new[] {
                new[] { "Why stop at lightning? We split it into every color there is! Double the wizards, every element!",
                        "Ready for a full spectrum shock? Here it comes!" },
                new[] { "Every color at once got a little messy, even for us.",
                        "Back to the bolts! You really outshone us there, wizard!" } } },
        };

        static Dictionary<string, string[][]> LinesFor(StageVariant v)
        {
            switch (v)
            {
                case StageVariant.Radiant: return RadiantLines;
                case StageVariant.Prism: return PrismLines;
                default: return ChaosLines;
            }
        }

        // "FireBoss-Intro", line 0 -> the variant's line (null = keep the original).
        internal static string CustomLine(string dialogId, int index)
        {
            if (string.IsNullOrEmpty(dialogId) || index < 0) return null;
            int dash = dialogId.IndexOf('-');
            string who = dash > 0 ? dialogId.Substring(0, dash) : dialogId;
            string[][] set;
            if (!LinesFor(Variant).TryGetValue(who, out set)) return null;
            string[] lines = dialogId == victoryId ? set[1] : set[0];
            return lines.Length == 0 ? null : lines[Mathf.Min(index, lines.Length - 1)];
        }

        // Messages we rewrote, put back when the fight ends (the dialog data is shared for the whole session).
        static readonly Dictionary<DialogMessage, string> rewritten = new Dictionary<DialogMessage, string>();

        internal static string OriginalOf(DialogMessage m)
        {
            string s;
            return m != null && rewritten.TryGetValue(m, out s) ? s : (m != null ? m.message : null);
        }

        internal static void Rewrite(DialogMessage m, string text)
        {
            if (m == null || text == null) return;
            if (!rewritten.ContainsKey(m)) rewritten[m] = m.message;
            m.message = text;
        }

        static void RestoreDialog()
        {
            foreach (KeyValuePair<DialogMessage, string> kv in rewritten) if (kv.Key != null) kv.Key.message = kv.Value;
            rewritten.Clear();
        }

        // The current variant fight (null when none).
        internal static BossRoomEventHandler Room;
        internal static Boss CurrentBoss;
        static string introId, victoryId;

        // Test keys. Shift+F12 cycles a forced variant for every Council fight: off -> Chaos -> Radiant -> Prism -> off
        // (Radiant / Prism with Arcanos). Ctrl+F12 jumps to this tier's Council for one fight, as the forced variant,
        // else the tier's own, else Chaos.
        internal static StageVariant Forced = StageVariant.None;
        internal static bool ForceOnce;

        internal static void JumpToCouncil()
        {
            string scene = Application.loadedLevelName;
            if (GameController.bossLevelNameDict == null || scene == null || !GameController.bossLevelNameDict.ContainsKey(scene))
            {
                Plugin.Log.LogWarning("COUNCIL jump: not on a regular stage of a run (scene " + scene + "); start a run first");
                return;
            }
            ForceOnce = true;
            GameController.stageCount = ChaosRun.RegularStages; // as if the tier's stages were done
            string boss = GameController.bossLevelNameDict[scene];
            Plugin.Log.LogInfo("COUNCIL jump: " + scene + " -> " + boss + " (tier " + GameController.tierCount + ", " + OnceVariant() + " for this fight)");
            GameController.LoadLevel(boss);
        }

        static StageVariant OnceVariant()
        {
            if (Forced != StageVariant.None) return Forced;
            StageVariant v = ChaosRun.TierVariant(GameController.tierCount);
            return v != StageVariant.None ? v : StageVariant.Chaos;
        }

        internal static void ToggleForce()
        {
            if (Forced == StageVariant.None) Forced = StageVariant.Chaos;
            else if (Forced == StageVariant.Chaos && ArcanosBridge.Present) Forced = StageVariant.Radiant;
            else if (Forced == StageVariant.Radiant) Forced = StageVariant.Prism;
            else Forced = StageVariant.None;
            Plugin.Log.LogInfo("COUNCIL variant forced: " + (Forced == StageVariant.None ? "OFF" : Forced + " (every Council fight)"));
            if (Forced == StageVariant.None) return;
            foreach (BossRoomEventHandler room in UnityEngine.Object.FindObjectsOfType<BossRoomEventHandler>())
            {
                if (room is FinalBossRoomEventHandler) continue;
                if (Room == room) End();
                Begin(room, Forced);
                if (room.GetComponent<CouncilCorruptionRunner>() == null) room.gameObject.AddComponent<CouncilCorruptionRunner>().Room = room;
            }
        }

        // A tier's Council member takes the variant of the tier's first variant stage.
        internal static StageVariant CouncilVariant(int tier)
        {
            if (Forced != StageVariant.None) return Forced;
            if (!Plugin.CorruptedCouncil.Value) return StageVariant.None;
            return ChaosRun.TierVariant(tier);
        }

        internal static bool IsCorrupted(int tier) { return CouncilVariant(tier) != StageVariant.None; }

        internal static void Begin(BossRoomEventHandler room, StageVariant v)
        {
            Room = room;
            CurrentBoss = null;
            Variant = v == StageVariant.None ? StageVariant.Chaos : v;
            Traverse tr = Traverse.Create(room);
            introId = tr.Field("bossIntroDialogID").GetValue<string>();
            victoryId = tr.Field("bossVictoryDialogID").GetValue<string>();
            Plugin.Log.LogInfo("COUNCIL " + Variant + " this fight (tier " + GameController.tierCount + "), dialogs " + introId + " / " + victoryId);
        }

        internal static void End()
        {
            RestoreDialog();
            RestoreTints();
            if (CurrentBoss != null) { PrismCycle pc = CurrentBoss.GetComponent<PrismCycle>(); if (pc != null) UnityEngine.Object.Destroy(pc); }
            if (roomTinted) { roomTinted = false; try { ChaosStages.Revert(); } catch (Exception) { } }
            Room = null; CurrentBoss = null; introId = victoryId = null;
        }

        // The Council room's tiles, decorations and lights, like a variant stage (visuals only: no rewards, no enemies).
        static bool roomTinted;

        internal static void TintRoom()
        {
            if (roomTinted) return;
            roomTinted = true;
            try { ChaosStages.Apply(true, Variant); } catch (Exception ex) { Plugin.Log.LogWarning("COUNCIL room tint failed: " + ex.Message); }
        }

        internal static bool DialogCorrupted(string dialogId)
        {
            return Room != null && !string.IsNullOrEmpty(dialogId) && (dialogId == introId || dialogId == victoryId);
        }

        // ---------- the boss ----------

        internal static void CorruptBoss(Boss b)
        {
            CurrentBoss = b;
            VariantInfo vi = Info;
            if (vi.Rainbow) { if (b.GetComponent<PrismCycle>() == null) b.gameObject.AddComponent<PrismCycle>(); }
            else
                foreach (SpriteRenderer sr in b.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (sr == null) continue;
                    sr.color = Multiply(sr.color, vi.BodyTint);
                    Material m = sr.material;
                    if (m != null && m.HasProperty("_OutlineColor")) m.SetColor("_OutlineColor", vi.Color);
                    if (m != null && m.HasProperty("_OutlineColor2")) m.SetColor("_OutlineColor2", vi.RampDark);
                }
            try
            {
                EnemyHealthBar bar = Traverse.Create(b).Field("healthBar").GetValue() as EnemyHealthBar;
                string name = b.BossDisplayName;
                if (bar != null) bar.SetName(vi.CouncilTitle + " " + (string.IsNullOrEmpty(name) ? b.GetType().Name : name));
            }
            catch (Exception) { }
            Plugin.Log.LogInfo("COUNCIL " + b.GetType().Name + " is " + vi.CouncilTitle + " (sprites, attacks, boss bar, dialog)");
        }

        // ---------- attacks ----------

        static readonly HashSet<int> tinted = new HashSet<int>();

        // Original colours of everything we tinted (pooled effects outlive the room): restored when the fight ends.
        static readonly Dictionary<SpriteRenderer, Color> origSprites = new Dictionary<SpriteRenderer, Color>();
        static readonly Dictionary<ParticleSystem, ParticleSystem.MinMaxGradient> origParticles = new Dictionary<ParticleSystem, ParticleSystem.MinMaxGradient>();
        static readonly Dictionary<Light, Color> origLights = new Dictionary<Light, Color>();
        static readonly Dictionary<TrailRenderer, Color[]> origTrails = new Dictionary<TrailRenderer, Color[]>();
        static readonly Dictionary<LineRenderer, Color[]> origLines = new Dictionary<LineRenderer, Color[]>();

        internal static void TintAttack(Attack atk) { TintAttack(atk, false, Variant); }
        internal static void TintAttack(Attack atk, bool stage) { TintAttack(atk, stage, stage ? ChaosStages.PlannedVariant : Variant); }

        // stage: an attack outside a Council fight (variant stage, marked caster); v: whose colours.
        internal static void TintAttack(Attack atk, bool stage, StageVariant v, Color? elementColor = null, string rampKey = null)
        {
            if (atk == null || v == StageVariant.None || (CurrentBoss == null && !stage)) return;
            if (atk.GetComponentInParent<Player>() != null || (atk.atkInfo != null && atk.atkInfo.entity is Player)) return;
            if (atk.name.StartsWith("HB_Arcanos")) return;
            GameObject root = VisualRoot(atk);
            if (root == null) return;
            // A pooled visual whose recolour removed itself (set up again for a new cast) can be recoloured again.
            if (!tinted.Add(root.GetInstanceID()) && root.GetComponent<RecolorSprites>() != null) return;
            TintTree(root, v, elementColor, rampKey, atk);
        }

        internal static void TintTree(GameObject root) { TintTree(root, Variant); }

        // A colour remapped onto one element's ramp by brightness (Prism attacks take their cycle element's colour).
        static Color ElementRamp(Color c, Color element)
        {
            float lum = 0.3f * c.r + 0.59f * c.g + 0.11f * c.b;
            Color r = Color.Lerp(element * 0.45f, Color.Lerp(element, Color.white, 0.55f), Mathf.Clamp01(0.2f + lum * 1.1f));
            r.a = c.a;
            return r;
        }

        // Everything visual under root, toward the variant's colours (not a boss's own body, not the player).
        internal static void TintTree(GameObject root, StageVariant v, Color? elementColor = null, string rampKey = null, Attack source = null)
        {
            VariantInfo vi = Variants.Info(v);
            // v0.6.5: the recolour engine (gradient map per element): recoloured sprite frames, particles, trails,
            // lines and lights on the element's ramp. Chaos / Radiant use their own ramp, Prism its cycle element's.
            string key = rampKey ?? ElementRamps.ForVariant(v);
            if (key != null && ElementRamps.Has(key)) { RecolorTree(root, key, source); return; }
            Func<Color, Color> ramp = elementColor.HasValue ? (Func<Color, Color>)(c => ElementRamp(c, elementColor.Value)) : (c => Variants.Ramp(c, v));
            foreach (SpriteRenderer sr in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sr == null || sr.GetComponentInParent<Boss>() != null || origSprites.ContainsKey(sr)) continue;
                origSprites[sr] = sr.color;
                // Radiant (v0.6.2) and Prism remap onto their ramp (a strong infusion); Chaos multiplies toward dark purple.
                sr.color = (elementColor.HasValue || vi.Rainbow || v == StageVariant.Radiant) ? ramp(sr.color) : Multiply(sr.color, vi.AttackTint);
            }
            foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (ps == null || origParticles.ContainsKey(ps)) continue;
                try
                {
                    ParticleSystem.MainModule main = ps.main;
                    origParticles[ps] = main.startColor;
                    main.startColor = (vi.Rainbow && !elementColor.HasValue)
                        ? new ParticleSystem.MinMaxGradient(Color.HSVToRGB(UnityEngine.Random.value, 0.7f, 1f), Color.HSVToRGB(UnityEngine.Random.value, 0.7f, 1f))
                        : new ParticleSystem.MinMaxGradient(ramp(main.startColor.color));
                }
                catch (Exception) { }
            }
            foreach (Light l in root.GetComponentsInChildren<Light>(true))
            {
                if (l == null || origLights.ContainsKey(l)) continue;
                origLights[l] = l.color;
                l.color = elementColor ?? vi.LightTint;
            }
            foreach (TrailRenderer t in root.GetComponentsInChildren<TrailRenderer>(true))
            {
                if (t == null || origTrails.ContainsKey(t)) continue;
                origTrails[t] = new[] { t.startColor, t.endColor };
                t.startColor = ramp(t.startColor); t.endColor = ramp(t.endColor);
            }
            foreach (LineRenderer lr in root.GetComponentsInChildren<LineRenderer>(true))
            {
                if (lr == null || origLines.ContainsKey(lr)) continue;
                origLines[lr] = new[] { lr.startColor, lr.endColor };
                lr.startColor = ramp(lr.startColor); lr.endColor = ramp(lr.endColor);
            }
        }

        // Ctrl+F10 preview: the player's own attack, recoloured (no player check, look only).
        internal static void PreviewRecolor(Attack atk, string key)
        {
            if (atk == null || !ElementRamps.Has(key)) return;
            GameObject root = VisualRoot(atk);
            if (root == null) return;
            tinted.Add(root.GetInstanceID());
            RecolorTree(root, key, atk);
        }

        static void RecolorTree(GameObject root, string key, Attack source)
        {
            if (root.GetComponentInParent<Boss>() == null) RecolorSprites.Attach(root, key, source);
            foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (ps == null || origParticles.ContainsKey(ps)) continue;
                try
                {
                    ParticleSystem.MainModule main = ps.main;
                    origParticles[ps] = main.startColor;
                    main.startColor = RecolorEngine.Map(main.startColor, key);
                }
                catch (Exception) { }
            }
            foreach (Light l in root.GetComponentsInChildren<Light>(true))
            {
                if (l == null || origLights.ContainsKey(l)) continue;
                origLights[l] = l.color;
                l.color = ElementRamps.Mid(key);
            }
            foreach (TrailRenderer t in root.GetComponentsInChildren<TrailRenderer>(true))
            {
                if (t == null || origTrails.ContainsKey(t)) continue;
                origTrails[t] = new[] { t.startColor, t.endColor };
                t.startColor = ElementRamps.Map(key, t.startColor); t.endColor = ElementRamps.Map(key, t.endColor);
            }
            foreach (LineRenderer lr in root.GetComponentsInChildren<LineRenderer>(true))
            {
                if (lr == null || origLines.ContainsKey(lr)) continue;
                origLines[lr] = new[] { lr.startColor, lr.endColor };
                lr.startColor = ElementRamps.Map(key, lr.startColor); lr.endColor = ElementRamps.Map(key, lr.endColor);
            }
        }

        // The boss's other effects (fire trails, bursts, pooled particles) carry no Attack: anything visual that shows up
        // after the fight starts and isn't the player's or part of the room gets the same treatment.
        static readonly HashSet<int> baseline = new HashSet<int>();

        internal static void SnapshotBaseline()
        {
            baseline.Clear();
            foreach (Renderer r in UnityEngine.Object.FindObjectsOfType<Renderer>()) if (r != null) baseline.Add(r.gameObject.GetInstanceID());
            foreach (ParticleSystem p in UnityEngine.Object.FindObjectsOfType<ParticleSystem>()) if (p != null) baseline.Add(p.gameObject.GetInstanceID());
        }

        internal static void TintNewEffects()
        {
            foreach (ParticleSystem p in UnityEngine.Object.FindObjectsOfType<ParticleSystem>()) TintIfNew(p != null ? p.gameObject : null);
            foreach (SpriteRenderer s in UnityEngine.Object.FindObjectsOfType<SpriteRenderer>()) TintIfNew(s != null ? s.gameObject : null);
        }

        static void TintIfNew(GameObject go)
        {
            if (go == null) return;
            int id = go.GetInstanceID();
            if (baseline.Contains(id) || tinted.Contains(id)) return;
            tinted.Add(id);
            if (go.layer == LayerMask.NameToLayer("UI") || go.GetComponentInParent<Canvas>() != null) return;
            if (go.GetComponentInParent<Entity>() != null) return; // bodies (boss, player, summons) keep their own look
            foreach (Attack a in go.transform.root.GetComponentsInChildren<Attack>(true))
                if (a.atkInfo != null && (a.atkInfo.skillCategory == "Player" || a.atkInfo.entity is Player)) return; // the player's arcana
            TintTree(go, Variant);
        }

        internal static void RestoreTints()
        {
            foreach (KeyValuePair<SpriteRenderer, Color> kv in origSprites) if (kv.Key != null) kv.Key.color = kv.Value;
            foreach (KeyValuePair<ParticleSystem, ParticleSystem.MinMaxGradient> kv in origParticles)
                if (kv.Key != null) { ParticleSystem.MainModule m = kv.Key.main; m.startColor = kv.Value; }
            foreach (KeyValuePair<Light, Color> kv in origLights) if (kv.Key != null) kv.Key.color = kv.Value;
            foreach (KeyValuePair<TrailRenderer, Color[]> kv in origTrails) if (kv.Key != null) { kv.Key.startColor = kv.Value[0]; kv.Key.endColor = kv.Value[1]; }
            foreach (KeyValuePair<LineRenderer, Color[]> kv in origLines) if (kv.Key != null) { kv.Key.startColor = kv.Value[0]; kv.Key.endColor = kv.Value[1]; }
            origSprites.Clear(); origParticles.Clear(); origLights.Clear(); origTrails.Clear(); origLines.Clear();
            RecolorSprites.RestoreAll();
            tinted.Clear(); baseline.Clear();
        }

        // The projectile / effect object the attack belongs to, never a whole room or the boss itself.
        static GameObject VisualRoot(Attack atk)
        {
            Projectile p = atk.GetComponentInParent<Projectile>();
            if (p != null) return p.gameObject;
            Transform t = atk.transform, parent = t.parent;
            if (parent != null && parent.GetComponent<Entity>() == null && parent.childCount <= 6) return parent.gameObject;
            return atk.gameObject;
        }

        internal static void ClearTinted() { tinted.Clear(); }

        // Where an arcana cast by this caster should go: its own target info when valid, else the nearest living player
        // (bosses track targets their own way, so closestTargetInfo can be empty for them).
        internal static Vector2? NearestPlayer(Vector2 from)
        {
            Vector2? best = null;
            float bestD = float.MaxValue;
            if (GameController.playerScripts == null) return null;
            foreach (Player p in GameController.playerScripts)
            {
                if (p == null || p.health == null || p.health.CurrentHealthValue <= 0) continue;
                float d = ((Vector2)p.transform.position - from).sqrMagnitude;
                if (d < bestD) { bestD = d; best = p.transform.position; }
            }
            return best;
        }

        // Damage type Chaos (kept for older callers): see Variants.ChaosifyInfo.
        internal static void Chaosify(AttackInfo info)
        {
            if (info == null || info.skillCategory == "Player") return;
            Variants.ChaosifyInfo(info);
        }

        // ---------- echoes ----------
        // The boss keeps its whole moveset; between its own attacks the variant bleeds out: a flash, then one of its
        // two arcana of that element is cast at the player.
        internal static List<ArcanaDef> Echoes = new List<ArcanaDef>();
        static float nextEcho;
        const float EchoCooldown = 5f, EchoChance = 0.45f, EchoTell = 0.6f;

        internal static void PickEchoes()
        {
            List<ArcanaDef> pool = Variants.CastPool(Variant);
            pool.RemoveAll(d => d.Family == ArcanaFamily.Melee || d.Family == ArcanaFamily.Dash || d.Family == ArcanaFamily.Minion
                || d.Family == ArcanaFamily.Seeker || d.Family == ArcanaFamily.Ward || EnemyArcana.Broken.Contains(d.SkillID));
            Echoes = Rivals.PickVaried(pool, 2, false);
            nextEcho = Time.time + 6f;
        }

        // Boss.TransitionToNextState: one attack just ended.
        internal static void OnBossTransition(Boss b)
        {
            if (b == null || b != CurrentBoss || Echoes.Count == 0 || Time.time < nextEcho) return;
            if (b.health == null || b.health.CurrentHealthValue <= 0) return;
            if (UnityEngine.Random.value > EchoChance) { nextEcho = Time.time + 1f; return; }
            nextEcho = Time.time + EchoCooldown;
            b.StartCoroutine(Echo(b, Echoes[UnityEngine.Random.Range(0, Echoes.Count)]));
        }

        static System.Collections.IEnumerator Echo(Boss b, ArcanaDef def)
        {
            SetOutline(b, Color.white);
            yield return new WaitForSeconds(EchoTell * 0.5f);
            SetOutline(b, Info.Color);
            yield return new WaitForSeconds(EchoTell * 0.5f);
            if (b == null || b.health == null || b.health.CurrentHealthValue <= 0 || b != CurrentBoss) yield break;
            Vector2 aim = Vector2.right;
            Vector2? target = ArcanaCaster.TargetOf(b);
            if (target.HasValue)
            {
                Vector2 to = target.Value - (Vector2)b.transform.position;
                if (to.sqrMagnitude > 0.01f) aim = to.normalized;
            }
            try { ArcanaCaster.CastAll(b, def, aim, null, UnityEngine.Random.value < Plugin.EnhancedArcanaChance.Value); }
            catch (Exception ex) { Plugin.Log.LogWarning("COUNCIL echo failed: " + ex.Message); }
        }

        static void SetOutline(Boss b, Color c)
        {
            if (b == null || Info.Rainbow) return; // Prism bosses cycle their own outline
            foreach (SpriteRenderer sr in b.GetComponentsInChildren<SpriteRenderer>())
            {
                Material m = sr != null ? sr.material : null;
                if (m != null && m.HasProperty("_OutlineColor")) m.SetColor("_OutlineColor", c);
            }
        }

        // ---------- reward ----------
        // The member's chest drops an arcana of the variant's element instead of its own element's. Radiant and Prism
        // arcana have their own unlock requirements: only arcana the player has already unlocked can drop (never a new
        // unlock); with none unlocked the chest keeps its normal drop.
        internal static bool CouncilChestOpening;

        internal static string PickVariantArcana()
        {
            List<string> list = new List<string>();
            if (Variant == StageVariant.Chaos) { if (LootManager.chaosSkillList != null) list.AddRange(LootManager.chaosSkillList); }
            else list.AddRange(ArcanosBridge.IdsOf(Variant));
            list.RemoveAll(id => !ArcanosBridge.Unlocked(id));
            List<string> owned = Traverse.Create(typeof(LootManager)).Field("currentPlayerSkills").GetValue() as List<string>;
            List<string> fresh = new List<string>(list);
            if (owned != null) fresh.RemoveAll(id => owned.Contains(id));
            if (fresh.Count > 0) list = fresh;
            return list.Count > 0 ? list[UnityEngine.Random.Range(0, list.Count)] : null;
        }

        internal static string PickChaosArcana() { return PickVariantArcana(); }

        internal static bool IsCouncilChest(TreasureChest c)
        {
            if (c == null || Room == null) return false;
            string id = Traverse.Create(typeof(BossRoomEventHandler)).Field("bossTreasureChestID").GetValue<string>();
            return !string.IsNullOrEmpty(id) && c.lootTableName == id;
        }

        static Color Multiply(Color a, Color b) { return new Color(a.r * b.r, a.g * b.g, a.b * b.b, a.a); }

        internal static bool IsPlayerSpeaker(string speakerId)
        {
            return string.IsNullOrEmpty(speakerId) || speakerId.IndexOf("Player", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    // Each boss room: decide, then watch for the boss to spawn (BossRoomEventHandler.BossIntro instantiates it).
    [HarmonyPatch(typeof(BossRoomEventHandler), "Start")]
    static class CouncilRoomStartPatch
    {
        static void Postfix(BossRoomEventHandler __instance)
        {
            try
            {
                // The final room derives from this handler: Sura (and the champions) are not Council members.
                if (__instance is FinalBossRoomEventHandler) return;
                bool once = CouncilCorruption.ForceOnce;
                CouncilCorruption.ForceOnce = false;
                StageVariant v = CouncilCorruption.CouncilVariant(GameController.tierCount);
                if (once && v == StageVariant.None) v = StageVariant.Chaos;
                if (Level.isBossRushMode || v == StageVariant.None) { CouncilCorruption.End(); return; }
                CouncilCorruption.Begin(__instance, v);
                __instance.gameObject.AddComponent<CouncilCorruptionRunner>().Room = __instance;
            }
            catch (Exception ex) { Plugin.Log.LogWarning("COUNCIL variant setup failed: " + ex.Message); }
        }
    }

    internal class CouncilCorruptionRunner : MonoBehaviour
    {
        public BossRoomEventHandler Room;
        Boss boss;
        float nextScan, startedAt = -1f;
        bool announced;

        void Update()
        {
            if (Room == null || CouncilCorruption.Room != Room) return;
            if (startedAt < 0f) startedAt = Time.time;
            // The room's tiles/lights, once the stage runner has settled on this scene (it reverts on scene change).
            if (Time.time - startedAt > 1.6f) CouncilCorruption.TintRoom();
            if (boss == null)
            {
                boss = Traverse.Create(Room).Field("boss").GetValue() as Boss;
                if (boss == null) return;
                CouncilCorruption.ClearTinted();
                CouncilCorruption.CorruptBoss(boss);
                CouncilCorruption.PickEchoes();
                CouncilCorruption.SnapshotBaseline(); // what exists now is the room and the bodies; later effects get tinted
            }
            // Once the intro dialog is over and the fight is on: show its echoes, like a giant's intro card.
            if (!announced && Traverse.Create(Room).Field("bossSpawned").GetValue<bool>() && !DialogOpen())
            {
                announced = true;
                Announce();
            }
            // Pooled attacks get their info before we see them: catch every active attack as it shows up.
            if (Time.time < nextScan) return;
            nextScan = Time.time + 0.2f;
            foreach (Attack a in FindObjectsOfType<Attack>())
            {
                if (a == null || a.GetComponentInParent<Player>() != null) continue;
                Variants.Apply(a, CouncilCorruption.Variant, false);
            }
            CouncilCorruption.TintNewEffects();
        }

        static bool DialogOpen()
        {
            try { DialogManager dm = GameUI.dialogManager; return dm != null && dm.IsActive; } catch (Exception) { return false; }
        }

        void Announce()
        {
            VariantInfo vi = Variants.Info(CouncilCorruption.Variant);
            string element = ElementalGiants.ElementColorName(vi.Prefix);
            List<IntroEntry> entries = new List<IntroEntry>();
            foreach (ArcanaDef d in CouncilCorruption.Echoes)
            {
                IntroEntry ie = new IntroEntry(IntroUI.SkillIcon(d.SkillID), Variants.Named(d, vi.Prefix));
                if (ie.Icon == null) ie.Tint = vi.Color;
                entries.Add(ie);
            }
            string name = boss != null && !string.IsNullOrEmpty(boss.BossDisplayName) ? boss.BossDisplayName : "Council";
            IntroUI.Show(vi.CouncilTitle + " " + name, element + " echoes  ·  " + element + " damage  ·  drops a " + element + " arcana", vi.Color, entries, 3f);
            Plugin.Log.LogInfo("COUNCIL echoes: " + Rivals.ArcanaList(CouncilCorruption.Echoes));
        }

        void OnDestroy() { if (CouncilCorruption.Room == Room) CouncilCorruption.End(); }
    }

    // Echoes between the boss's own attacks.
    [HarmonyPatch(typeof(Boss), "TransitionToNextState")]
    static class CouncilEchoPatch
    {
        static void Postfix(Boss __instance)
        {
            if (CouncilCorruption.CurrentBoss == null) return;
            try { CouncilCorruption.OnBossTransition(__instance); } catch (Exception) { }
        }
    }

    // Reward: while the member's chest opens, the arcana it drops becomes one of the variant's (unlocked) arcana.
    [HarmonyPatch(typeof(TreasureChest), "Break")]
    static class CouncilChestPatch
    {
        static void Prefix(TreasureChest __instance) { CouncilCorruption.CouncilChestOpening = CouncilCorruption.IsCouncilChest(__instance); }
        static void Finalizer() { CouncilCorruption.CouncilChestOpening = false; }
    }

    [HarmonyPatch(typeof(LootManager), "DropSkill")]
    static class CouncilChaosArcanaDropPatch
    {
        static void Prefix(ref int __1, ref string __2, ref float __3)
        {
            if (!CouncilCorruption.CouncilChestOpening) return;
            CouncilCorruption.CouncilChestOpening = false; // one per chest
            string id = CouncilCorruption.PickVariantArcana();
            if (id == null)
            {
                Plugin.Log.LogInfo("COUNCIL reward: no unlocked " + CouncilCorruption.Variant + " arcana, the chest keeps its normal drop");
                return;
            }
            if (__1 <= 0) __1 = 1;
            __2 = id;
            __3 = 0f; // never a locked arcana
            Plugin.Log.LogInfo("COUNCIL reward: " + CouncilCorruption.Variant + " arcana " + id);
        }
    }

    // The member's lines. The text that gets typed out comes straight from DialogMessage.message
    // (DialogManager.GhostWrite), so the message itself is swapped before it shows (and restored when the fight ends).
    // In Council dialogs the member (left portrait) speaks every line; the player's portrait is shown but silent.
    // Then the portrait and the text take the variant's colour.
    [HarmonyPatch(typeof(DialogManager), "Activate")]
    static class CouncilDialogPatch
    {
        static Color? originalText;

        static void Prefix(DialogManager __instance, DialogMessage __0)
        {
            try
            {
                string id = Traverse.Create(__instance).Field("currentDialogID").GetValue<string>();
                if (__0 == null || !CouncilCorruption.DialogCorrupted(id)) return;
                int index = -1;
                IDictionary dict = Traverse.Create(__instance).Field("dialogDict").GetValue() as IDictionary;
                DialogEntry entry = dict != null && dict.Contains(id) ? dict[id] as DialogEntry : null;
                if (entry != null && entry.messages != null) index = Array.IndexOf(entry.messages, __0);
                string line = CouncilCorruption.CustomLine(id, index);
                if (line != null) CouncilCorruption.Rewrite(__0, line);
            }
            catch (Exception ex) { Plugin.Log.LogWarning("COUNCIL dialog rewrite failed: " + ex.Message); }
        }

        static void Postfix(DialogManager __instance, DialogMessage __0)
        {
            try
            {
                Traverse tr = Traverse.Create(__instance);
                Text msg = tr.Field("messageText").GetValue() as Text;
                string id = tr.Field("currentDialogID").GetValue<string>();
                bool corrupted = __0 != null && CouncilCorruption.DialogCorrupted(id);
                if (msg != null && originalText == null) originalText = msg.color;
                if (!corrupted) { if (msg != null && originalText != null) msg.color = originalText.Value; return; }
                Color vc = Variants.Info(CouncilCorruption.Variant).Color;
                Image img = tr.Field("leftPortraitImage").GetValue() as Image;
                if (img != null) img.color = Color.Lerp(img.color, vc, 0.35f);
                if (msg != null && originalText != null) msg.color = Color.Lerp(originalText.Value, vc, 0.55f);
            }
            catch (Exception) { }
        }
    }
}
