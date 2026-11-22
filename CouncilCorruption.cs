using System;
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
        static readonly Color SpriteTint = new Color(0.78f, 0.55f, 1f);
        static readonly Color AttackTint = new Color(0.66f, 0.36f, 1f);

        // The current corrupted fight (null when none).
        internal static BossRoomEventHandler Room;
        internal static Boss CurrentBoss;
        static string introId, victoryId;

        // Test key (Shift+F12): every Council member counts as corrupted until toggled off; applies right away if
        // you're already in a Council room whose boss has spawned or not.
        internal static bool Forced;

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
            Forced = true;
            GameController.stageCount = GameController.stagesPerTier; // as if the tier's stages were done
            string boss = GameController.bossLevelNameDict[scene];
            Plugin.Log.LogInfo("COUNCIL jump: " + scene + " -> " + boss + " (tier " + GameController.tierCount + ", corruption forced ON)");
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
            int per = GameController.stagesPerTier;
            if (per <= 1) return false;
            for (int s = 0; s < per - 1; s++) if (ChaosRun.IsChaos(tier, s)) return true;
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

        internal static void End() { Room = null; CurrentBoss = null; introId = victoryId = null; }

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

        // A non-player attack in the corrupted fight: tint its visual object (the projectile/effect it belongs to).
        internal static void TintAttack(Attack atk)
        {
            if (atk == null || CurrentBoss == null) return;
            if (atk.atkInfo != null && atk.atkInfo.skillCategory == "Player") return;
            if (atk.GetComponentInParent<Player>() != null) return;
            GameObject root = VisualRoot(atk);
            if (root == null || !tinted.Add(root.GetInstanceID())) return;
            foreach (SpriteRenderer sr in root.GetComponentsInChildren<SpriteRenderer>(true))
                if (sr != null && sr.GetComponentInParent<Boss>() == null) sr.color = Multiply(sr.color, AttackTint);
            foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (ps == null) continue;
                try { ParticleSystem.MainModule main = ps.main; main.startColor = new ParticleSystem.MinMaxGradient(Multiply(main.startColor.color, AttackTint)); }
                catch (Exception) { }
            }
            foreach (Light l in root.GetComponentsInChildren<Light>(true)) if (l != null) l.color = Purple;
            foreach (TrailRenderer t in root.GetComponentsInChildren<TrailRenderer>(true))
                if (t != null) { t.startColor = Multiply(t.startColor, AttackTint); t.endColor = Multiply(t.endColor, AttackTint); }
            foreach (LineRenderer lr in root.GetComponentsInChildren<LineRenderer>(true))
                if (lr != null) { lr.startColor = Multiply(lr.startColor, AttackTint); lr.endColor = Multiply(lr.endColor, AttackTint); }
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
            if (b.closestTargetInfo != null)
            {
                Vector2 to = (Vector2)b.closestTargetInfo.position - (Vector2)b.transform.position;
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
                if (Level.isBossRushMode || !CouncilCorruption.IsCorrupted(GameController.tierCount)) { CouncilCorruption.End(); return; }
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
        float nextScan;
        bool announced;

        void Update()
        {
            if (Room == null || CouncilCorruption.Room != Room) return;
            if (boss == null)
            {
                boss = Traverse.Create(Room).Field("boss").GetValue() as Boss;
                if (boss == null) return;
                CouncilCorruption.ClearTinted();
                CouncilCorruption.CorruptBoss(boss);
                CouncilCorruption.PickEchoes();
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

    // The corrupted member's lines: glitched text and whispers.
    [HarmonyPatch(typeof(DialogManager), "ParseMessage")]
    static class CouncilDialogTextPatch
    {
        static bool loggedSpeakers;

        static void Postfix(DialogManager __instance, DialogMessage __0, ref string __result)
        {
            try
            {
                string id = Traverse.Create(__instance).Field("currentDialogID").GetValue<string>();
                if (__0 == null || !CouncilCorruption.DialogCorrupted(id)) return;
                string speaker = __0.leftActive && !__0.rightActive ? __0.leftSpeakerID : __0.rightActive && !__0.leftActive ? __0.rightSpeakerID : null;
                if (!loggedSpeakers) { loggedSpeakers = true; Plugin.Log.LogInfo("COUNCIL dialog speakers: left " + __0.leftSpeakerID + " (" + __0.leftActive + "), right " + __0.rightSpeakerID + " (" + __0.rightActive + ")"); }
                if (speaker == null || CouncilCorruption.IsPlayerSpeaker(speaker)) return;
                __result = CouncilCorruption.Corrupt(__result);
            }
            catch (Exception) { }
        }
    }

    // ...and their portrait and message text tinted purple while they speak.
    [HarmonyPatch(typeof(DialogManager), "Activate")]
    static class CouncilDialogLookPatch
    {
        static Color? originalText;

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

                bool bossSpeaking = false;
                if (__0.leftActive && !CouncilCorruption.IsPlayerSpeaker(__0.leftSpeakerID)) { Tint(tr, "leftPortraitImage"); bossSpeaking = true; }
                if (__0.rightActive && !CouncilCorruption.IsPlayerSpeaker(__0.rightSpeakerID)) { Tint(tr, "rightPortraitImage"); bossSpeaking = true; }
                if (msg != null && originalText != null)
                    msg.color = bossSpeaking ? Color.Lerp(originalText.Value, CouncilCorruption.Purple, 0.55f) : originalText.Value;
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
