using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace WolHybridBosses
{
    // Corrupted Council: a Council member whose tier had a Chaos stage is "touched by Chaos". Their sprites take a dark
    // purple tint and outline, every attack in their fight is tinted like Chaos arcana, the boss bar reads
    // "Corrupted <name>", their intro/victory lines come out glitched with Chaos whispers (portrait and text tinted),
    // and their boss circle on the loading-screen board is purple. Same seed as the Chaos stages, so the board and
    // the fight always agree. Visual/dialogue only: their moves and stats are unchanged.
    internal static class CouncilCorruption
    {
        internal static readonly Color Purple = new Color(0.62f, 0.32f, 1f);
        // Chaos is a dark, almost black purple: attacks are pushed hard toward it, the boss a bit less (stays readable).
        static readonly Color SpriteTint = new Color(0.62f, 0.42f, 0.85f);
        static readonly Color AttackTint = new Color(0.46f, 0.2f, 0.72f);
        static readonly Color ChaosDark = new Color(0.16f, 0.03f, 0.28f), ChaosBright = new Color(0.72f, 0.32f, 1f);

        // A colour remapped onto the Chaos ramp by its brightness (dark -> near-black purple, bright -> violet).
        internal static Color ChaosRamp(Color c)
        {
            float lum = 0.3f * c.r + 0.59f * c.g + 0.11f * c.b;
            Color r = Color.Lerp(ChaosDark, ChaosBright, Mathf.Clamp01(lum * 1.1f));
            r.a = c.a;
            return r;
        }

        // ---------- custom voicelines ----------
        // Council dialogs have the member speak every line (2 per dialog; the player's portrait is shown but silent).
        // Corrupted, each line is replaced by its Chaos counterpart, in the game's upbeat mentor tone: the member has
        // been dabbling in Chaos and wants to trade their element in for it, and loses the taste for it when beaten.
        static readonly Dictionary<string, string[][]> Lines = new Dictionary<string, string[][]>
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

        // "FireBoss-Intro", line 0 -> the corrupted line (null = keep the original, just glitched).
        internal static string CustomLine(string dialogId, int index)
        {
            if (string.IsNullOrEmpty(dialogId) || index < 0) return null;
            int dash = dialogId.IndexOf('-');
            string who = dash > 0 ? dialogId.Substring(0, dash) : dialogId;
            string[][] set;
            if (!Lines.TryGetValue(who, out set)) return null;
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

        // The current corrupted fight (null when none).
        internal static BossRoomEventHandler Room;
        internal static Boss CurrentBoss;
        static string introId, victoryId;

        // Test key (Shift+F12): every Council member counts as corrupted until toggled off; applies right away if
        // you're already in a Council room whose boss has spawned or not.
        internal static bool Forced;
        internal static bool ForceOnce; // Ctrl+F12: the next Council fight only

        // Test key (Ctrl+F12): straight to this tier's Council floor, corrupted. The same scene the game loads after a
        // tier's last stage (NextLevelLoader: bossLevelNameDict[current tier scene]), via GameController.LoadLevel.
        internal static void JumpToCouncil()
        {
            string scene = Application.loadedLevelName;
            if (GameController.bossLevelNameDict == null || scene == null || !GameController.bossLevelNameDict.ContainsKey(scene))
            {
                Plugin.Log.LogWarning("COUNCIL jump: not on a regular stage of a run (scene " + scene + "); start a run first");
                return;
            }
            ForceOnce = true; // just this fight (Shift+F12 is the lasting toggle)
            GameController.stageCount = GameController.stagesPerTier; // as if the tier's stages were done
            string boss = GameController.bossLevelNameDict[scene];
            Plugin.Log.LogInfo("COUNCIL jump: " + scene + " -> " + boss + " (tier " + GameController.tierCount + ", corrupted for this fight)");
            GameController.LoadLevel(boss);
        }

        internal static void ToggleForce()
        {
            Forced = !Forced;
            Plugin.Log.LogInfo("COUNCIL corruption forced " + (Forced ? "ON (every Council fight)" : "OFF"));
            if (!Forced) return;
            foreach (BossRoomEventHandler room in UnityEngine.Object.FindObjectsOfType<BossRoomEventHandler>())
            {
                if (room is FinalBossRoomEventHandler || Room == room) continue;
                Begin(room);
                if (room.GetComponent<CouncilCorruptionRunner>() == null) room.gameObject.AddComponent<CouncilCorruptionRunner>().Room = room;
            }
        }

        // A tier's Council member is corrupted when any regular stage of that tier is a Chaos stage.
        internal static bool IsCorrupted(int tier)
        {
            if (Forced) return true;
            if (!Plugin.CorruptedCouncil.Value) return false;
            for (int s = 0; s < GameController.stagesPerTier; s++) if (ChaosRun.IsChaos(tier, s)) return true; // its regular stages
            return false;
        }

        internal static void Begin(BossRoomEventHandler room)
        {
            Room = room;
            CurrentBoss = null;
            Traverse tr = Traverse.Create(room);
            introId = tr.Field("bossIntroDialogID").GetValue<string>();
            victoryId = tr.Field("bossVictoryDialogID").GetValue<string>();
            Plugin.Log.LogInfo("COUNCIL corrupted this fight (tier " + GameController.tierCount + " had a Chaos stage), dialogs " + introId + " / " + victoryId);
        }

        internal static void End()
        {
            RestoreDialog();
            RestoreTints();
            if (roomTinted) { roomTinted = false; try { ChaosStages.Revert(); } catch (Exception) { } }
            Room = null; CurrentBoss = null; introId = victoryId = null;
        }

        // The Council room's tiles, decorations and lights, like a Chaos stage (visuals only: no rewards, no enemies).
        static bool roomTinted;

        internal static void TintRoom()
        {
            if (roomTinted) return;
            roomTinted = true;
            try { ChaosStages.Apply(true); } catch (Exception ex) { Plugin.Log.LogWarning("COUNCIL room tint failed: " + ex.Message); }
        }

        internal static bool DialogCorrupted(string dialogId)
        {
            return Room != null && !string.IsNullOrEmpty(dialogId) && (dialogId == introId || dialogId == victoryId);
        }

        // ---------- the boss ----------

        internal static void CorruptBoss(Boss b)
        {
            CurrentBoss = b;
            foreach (SpriteRenderer sr in b.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sr == null) continue;
                sr.color = Multiply(sr.color, SpriteTint);
                Material m = sr.material;
                if (m != null && m.HasProperty("_OutlineColor")) m.SetColor("_OutlineColor", Purple);
                if (m != null && m.HasProperty("_OutlineColor2")) m.SetColor("_OutlineColor2", new Color(0.3f, 0.1f, 0.5f));
            }
            try
            {
                EnemyHealthBar bar = Traverse.Create(b).Field("healthBar").GetValue() as EnemyHealthBar;
                string name = b.BossDisplayName;
                if (bar != null) bar.SetName("Corrupted " + (string.IsNullOrEmpty(name) ? b.GetType().Name : name));
            }
            catch (Exception) { }
            Plugin.Log.LogInfo("COUNCIL " + b.GetType().Name + " corrupted (sprites, attacks, boss bar, dialog)");
        }

        // ---------- attacks ----------

        static readonly HashSet<int> tinted = new HashSet<int>();

        // Original colours of everything we tinted (pooled effects outlive the room): restored when the fight ends.
        static readonly Dictionary<SpriteRenderer, Color> origSprites = new Dictionary<SpriteRenderer, Color>();
        static readonly Dictionary<ParticleSystem, ParticleSystem.MinMaxGradient> origParticles = new Dictionary<ParticleSystem, ParticleSystem.MinMaxGradient>();
        static readonly Dictionary<Light, Color> origLights = new Dictionary<Light, Color>();
        static readonly Dictionary<TrailRenderer, Color[]> origTrails = new Dictionary<TrailRenderer, Color[]>();
        static readonly Dictionary<LineRenderer, Color[]> origLines = new Dictionary<LineRenderer, Color[]>();

        // A non-player attack in the corrupted fight: tint its visual object (the projectile/effect it belongs to).
        internal static void TintAttack(Attack atk)
        {
            if (atk == null || CurrentBoss == null) return;
            if (atk.atkInfo != null && atk.atkInfo.skillCategory == "Player") return;
            if (atk.GetComponentInParent<Player>() != null) return;
            GameObject root = VisualRoot(atk);
            if (root == null || !tinted.Add(root.GetInstanceID())) return;
            TintTree(root);
        }

        // Everything visual under root, toward Chaos's dark purple (not the boss's own body, not the player).
        internal static void TintTree(GameObject root)
        {
            foreach (SpriteRenderer sr in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sr == null || sr.GetComponentInParent<Boss>() != null || origSprites.ContainsKey(sr)) continue;
                origSprites[sr] = sr.color;
                sr.color = Multiply(sr.color, AttackTint);
            }
            foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (ps == null || origParticles.ContainsKey(ps)) continue;
                try
                {
                    ParticleSystem.MainModule main = ps.main;
                    origParticles[ps] = main.startColor;
                    main.startColor = new ParticleSystem.MinMaxGradient(ChaosRamp(main.startColor.color));
                }
                catch (Exception) { }
            }
            foreach (Light l in root.GetComponentsInChildren<Light>(true))
            {
                if (l == null || origLights.ContainsKey(l)) continue;
                origLights[l] = l.color;
                l.color = new Color(0.45f, 0.15f, 0.8f);
            }
            foreach (TrailRenderer t in root.GetComponentsInChildren<TrailRenderer>(true))
            {
                if (t == null || origTrails.ContainsKey(t)) continue;
                origTrails[t] = new[] { t.startColor, t.endColor };
                t.startColor = ChaosRamp(t.startColor); t.endColor = ChaosRamp(t.endColor);
            }
            foreach (LineRenderer lr in root.GetComponentsInChildren<LineRenderer>(true))
            {
                if (lr == null || origLines.ContainsKey(lr)) continue;
                origLines[lr] = new[] { lr.startColor, lr.endColor };
                lr.startColor = ChaosRamp(lr.startColor); lr.endColor = ChaosRamp(lr.endColor);
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
            TintTree(go);
        }

        static void RestoreTints()
        {
            foreach (KeyValuePair<SpriteRenderer, Color> kv in origSprites) if (kv.Key != null) kv.Key.color = kv.Value;
            foreach (KeyValuePair<ParticleSystem, ParticleSystem.MinMaxGradient> kv in origParticles)
                if (kv.Key != null) { ParticleSystem.MainModule m = kv.Key.main; m.startColor = kv.Value; }
            foreach (KeyValuePair<Light, Color> kv in origLights) if (kv.Key != null) kv.Key.color = kv.Value;
            foreach (KeyValuePair<TrailRenderer, Color[]> kv in origTrails) if (kv.Key != null) { kv.Key.startColor = kv.Value[0]; kv.Key.endColor = kv.Value[1]; }
            foreach (KeyValuePair<LineRenderer, Color[]> kv in origLines) if (kv.Key != null) { kv.Key.startColor = kv.Value[0]; kv.Key.endColor = kv.Value[1]; }
            origSprites.Clear(); origParticles.Clear(); origLights.Clear(); origTrails.Clear(); origLines.Clear();
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

        // Damage type Chaos: element Chaos, and its elemental status (burn, freeze, shock...) becomes the Chaos status at
        // the same chance. Idempotent (attacks may regenerate their info every use).
        internal static void Chaosify(AttackInfo info)
        {
            if (info == null || info.skillCategory == "Player" || info.elementType == ElementType.Chaos) return;
            info.elementType = ElementType.Chaos;
            float chance = Mathf.Max(Mathf.Max(info.burnChance, info.slowChance), Mathf.Max(Mathf.Max(info.poisonChance, info.shockChance), info.freezeChance));
            int level = Mathf.Max(1, Mathf.Max(Mathf.Max(info.burnLevel, info.slowLevel), Mathf.Max(info.poisonLevel, info.shockLevel)));
            if (chance > info.chaosChance) { info.chaosChance = chance; info.chaosLevel = Mathf.Max(info.chaosLevel, level); }
            info.burnChance = info.slowChance = info.poisonChance = info.shockChance = info.freezeChance = 0f;
        }

        // ---------- Chaos echoes ----------
        // The boss keeps its whole moveset; between its own attacks Chaos bleeds out: a purple flash, then one of its
        // two Chaos arcana is cast at the player.
        internal static List<ArcanaDef> Echoes = new List<ArcanaDef>();
        static float nextEcho;
        const float EchoCooldown = 5f, EchoChance = 0.45f, EchoTell = 0.6f;

        internal static void PickEchoes()
        {
            List<ArcanaDef> pool = ArcanaCatalog.PoolExact("Chaos");
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
            SetOutline(b, Purple);
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
            if (b == null) return;
            foreach (SpriteRenderer sr in b.GetComponentsInChildren<SpriteRenderer>())
            {
                Material m = sr != null ? sr.material : null;
                if (m != null && m.HasProperty("_OutlineColor")) m.SetColor("_OutlineColor", c);
            }
        }

        // ---------- reward ----------
        // The corrupted member's chest drops a Chaos arcana instead of its element's arcana.
        internal static bool CouncilChestOpening;

        internal static string PickChaosArcana()
        {
            List<string> list = LootManager.chaosSkillList != null ? new List<string>(LootManager.chaosSkillList) : new List<string>();
            List<string> owned = Traverse.Create(typeof(LootManager)).Field("currentPlayerSkills").GetValue() as List<string>;
            if (owned != null) list.RemoveAll(id => owned.Contains(id));
            if (list.Count == 0 && LootManager.chaosSkillList != null) list.AddRange(LootManager.chaosSkillList);
            return list.Count > 0 ? list[UnityEngine.Random.Range(0, list.Count)] : null;
        }

        internal static bool IsCouncilChest(TreasureChest c)
        {
            if (c == null || Room == null) return false;
            string id = Traverse.Create(typeof(BossRoomEventHandler)).Field("bossTreasureChestID").GetValue<string>();
            return !string.IsNullOrEmpty(id) && c.lootTableName == id;
        }

        static Color Multiply(Color a, Color b) { return new Color(a.r * b.r, a.g * b.g, a.b * b.b, a.a); }

        // ---------- dialog ----------

        static readonly string[] Whispers =
        {
            "...the Chaos sees you...", "...it whispers...", "...so loud... the void...", "...not my words...",
            "...it hungers...", "...we are many...",
        };
        const string Glitch = "#%&*";

        // Glitched letters and stutters in plain text (tags left alone), sometimes a Chaos whisper at the end.
        // Seeded by the line, so a line reads the same if it's redrawn.
        internal static string Corrupt(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            System.Random rng = new System.Random(text.GetHashCode());
            StringBuilder sb = new StringBuilder(text.Length + 32);
            bool inTag = false, wordStart = true;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '<') inTag = true;
                if (inTag) { sb.Append(c); if (c == '>') inTag = false; continue; }
                if (char.IsLetter(c))
                {
                    if (wordStart && rng.NextDouble() < 0.06) { sb.Append(c).Append('-'); } // st-stutter
                    if (!wordStart && rng.NextDouble() < 0.05) { sb.Append(Glitch[rng.Next(Glitch.Length)]); wordStart = false; continue; }
                    wordStart = false;
                }
                else wordStart = true;
                sb.Append(c);
            }
            if (rng.NextDouble() < 0.5) sb.Append("  ").Append(Whispers[rng.Next(Whispers.Length)]);
            return sb.ToString();
        }

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
                if (Level.isBossRushMode || !(once || CouncilCorruption.IsCorrupted(GameController.tierCount))) { CouncilCorruption.End(); return; }
                CouncilCorruption.Begin(__instance);
                __instance.gameObject.AddComponent<CouncilCorruptionRunner>().Room = __instance;
            }
            catch (Exception ex) { Plugin.Log.LogWarning("COUNCIL corruption setup failed: " + ex.Message); }
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
            // The room's tiles/lights, once the Chaos stage runner has settled on this scene (it reverts on scene change).
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
            // Once the intro dialog is over and the fight is on: show its Chaos echoes, like a giant's intro card.
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
                CouncilCorruption.TintAttack(a);
                CouncilCorruption.Chaosify(a.atkInfo);
            }
            CouncilCorruption.TintNewEffects();
        }

        static bool DialogOpen()
        {
            try { DialogManager dm = GameUI.dialogManager; return dm != null && dm.IsActive; } catch (Exception) { return false; }
        }

        void Announce()
        {
            List<IntroEntry> entries = new List<IntroEntry>();
            foreach (ArcanaDef d in CouncilCorruption.Echoes)
            {
                IntroEntry ie = new IntroEntry(IntroUI.SkillIcon(d.SkillID), IntroUI.SkillName(d.SkillID));
                if (ie.Icon == null) ie.Tint = CouncilCorruption.Purple;
                entries.Add(ie);
            }
            string name = boss != null && !string.IsNullOrEmpty(boss.BossDisplayName) ? boss.BossDisplayName : "Council";
            IntroUI.Show("Corrupted " + name, "Chaos echoes  ·  Chaos damage  ·  drops a Chaos arcana", CouncilCorruption.Purple, entries, 3f);
            Plugin.Log.LogInfo("COUNCIL echoes: " + Rivals.ArcanaList(CouncilCorruption.Echoes));
        }

        void OnDestroy() { if (CouncilCorruption.Room == Room) CouncilCorruption.End(); }
    }

    // New attacks in the corrupted fight are tinted as soon as their info is set.
    [HarmonyPatch(typeof(Attack), "SetAttackInfo", new Type[] { typeof(string), typeof(string), typeof(int), typeof(bool) })]
    static class CouncilAttackTintPatch
    {
        static void Postfix(Attack __instance)
        {
            if (CouncilCorruption.CurrentBoss == null) return;
            try
            {
                if (__instance.GetComponentInParent<Player>() != null) return;
                CouncilCorruption.TintAttack(__instance);
                CouncilCorruption.Chaosify(__instance.atkInfo);
            }
            catch (Exception) { }
        }
    }

    // Chaos echoes between the boss's own attacks.
    [HarmonyPatch(typeof(Boss), "TransitionToNextState")]
    static class CouncilEchoPatch
    {
        static void Postfix(Boss __instance)
        {
            if (CouncilCorruption.CurrentBoss == null) return;
            try { CouncilCorruption.OnBossTransition(__instance); } catch (Exception) { }
        }
    }

    // Reward: while the corrupted member's chest opens, the arcana it drops becomes a Chaos arcana.
    [HarmonyPatch(typeof(TreasureChest), "Break")]
    static class CouncilChestPatch
    {
        static void Prefix(TreasureChest __instance) { CouncilCorruption.CouncilChestOpening = CouncilCorruption.IsCouncilChest(__instance); }
        static void Finalizer() { CouncilCorruption.CouncilChestOpening = false; }
    }

    [HarmonyPatch(typeof(LootManager), "DropSkill")]
    static class CouncilChaosArcanaDropPatch
    {
        static void Prefix(ref int __1, ref string __2)
        {
            if (!CouncilCorruption.CouncilChestOpening) return;
            string id = CouncilCorruption.PickChaosArcana();
            if (id == null) return;
            if (__1 <= 0) __1 = 1;
            __2 = id;
            CouncilCorruption.CouncilChestOpening = false; // one Chaos arcana per chest
            Plugin.Log.LogInfo("COUNCIL reward: Chaos arcana " + id);
        }
    }

    // The corrupted member's lines. The text that gets typed out comes straight from DialogMessage.message
    // (DialogManager.GhostWrite), so the message itself is swapped before it shows (and restored when the fight ends).
    // In Council dialogs the member (left portrait) speaks every line; the player's portrait is shown but silent.
    // Then the portrait and the text are tinted purple.
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
                if (line != null) CouncilCorruption.Rewrite(__0, line); // plain text: the game's tone, no glitching
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
                Tint(tr, "leftPortraitImage");
                if (msg != null && originalText != null) msg.color = Color.Lerp(originalText.Value, CouncilCorruption.Purple, 0.55f);
            }
            catch (Exception) { }
        }

        static void Tint(Traverse tr, string field)
        {
            Image img = tr.Field(field).GetValue() as Image;
            if (img != null) img.color = new Color(img.color.r * 0.8f, img.color.g * 0.55f, img.color.b, img.color.a);
        }
    }
}
