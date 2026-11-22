using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace WolHybridBosses
{
    // Final-stage boss pool: some runs, the final boss room holds a champion instead of Sura.
    //
    // Sura is not spawned: she is placed in the scene, and FinalBossRoomEventHandler drives the whole ending from her
    // ("boss" = GameObject "FinalBoss"). When her FSM state is "Dead" and her Chaos form is done, it announces victory,
    // runs BossOutro (which unlocks the locked Chaos arcana and the achievement) and ActivateExitPortal (the ending).
    // So Sura stays the room's official boss: when the fight starts she is hidden and frozen and the champion appears
    // in her place; when the champion dies she is put straight into her *final* death (chaos form done), and the
    // room's own code runs the normal victory, Chaos arcana unlock and ending.
    internal class FinalChampion : MonoBehaviour
    {
        internal static bool ForceNext;

        FinalBossRoomEventHandler room;
        Boss sura;
        Enemy champion;
        string prefix;
        string title;
        bool swapped, ended;

        internal string Prefix { get { return prefix; } }
        internal string Title { get { return title; } }
        internal bool Fighting { get { return swapped && !ended; } }

        // Uses the run's decision (FinalRun, rolled when the run starts, also shown on the loading-screen board).
        internal static FinalChampion Roll(FinalBossRoomEventHandler room)
        {
            if (ForceNext) { ForceNext = false; FinalRun.ForceChampion(); }
            FinalRun.Tick();
            if (FinalRun.ChampionPrefix == null) return null;

            FinalChampion fc = room.gameObject.GetComponent<FinalChampion>() ?? room.gameObject.AddComponent<FinalChampion>();
            fc.room = room;
            fc.prefix = FinalRun.ChampionPrefix;
            fc.title = FinalRun.ChampionTitle;
            // v1.1.1: Sura doesn't speak or walk up (the room's own intro skips that whole part); our cutscene replaces it.
            try { Traverse.Create(room).Field("playBossDialog").SetValue(false); } catch (Exception ex) { Plugin.Log.LogWarning("FINAL could not silence Sura: " + ex.Message); }
            fc.StartCoroutine(fc.TintSoon()); // v1.1.1: the whole castle in the champion's colours
            if (Plugin.FinalRoomBadge.Value) FinalChampionBadge.Show(fc);
            Plugin.Log.LogInfo("FINAL boss this run: " + fc.title + " (not Sura)");
            return fc;
        }

        void Update()
        {
            if (room == null) return;
            Traverse tr = Traverse.Create(room);
            if (!swapped)
            {
                // v1.1.1: Sura is never seen on champion runs (her throne stays empty until the champion arrives).
                // Every frame until the swap: her intro states and the health bar's FadeIn turn things back on, which
                // showed her and her bar for a moment.
                HideSuraEarly(tr);
                if (!tr.Field("bossSpawned").GetValue<bool>()) return; // Sura's intro finished, fight begins now
                sura = tr.Field("boss").GetValue() as Boss;
                if (sura == null) return;
                swapped = true;
                StartCoroutine(Swap());
                return;
            }
            if (ended || !championSpawned) return;
            if (ChampionDown()) { ended = true; StartCoroutine(Finish(tr)); }
        }

        bool championSpawned;
        FinalBoss placedSura;

        // A frame after the level's Start, so every scenery object has set itself up.
        internal IEnumerator TintSoon()
        {
            yield return null;
            yield return null;
            ArenaTint.ApplyFor(prefix, FinalRun.Alchemist);
        }

        void HideSuraEarly(Traverse tr)
        {
            Boss b = tr.Field("boss").GetValue() as Boss;
            if (b == null) b = tr.Field("finalBoss").GetValue() as FinalBoss;
            if (b == null) { if (placedSura == null) placedSura = FindObjectOfType<FinalBoss>(); b = placedSura; }
            if (b == null) return;
            foreach (Renderer r in b.GetComponentsInChildren<Renderer>(true)) if (r.enabled) r.enabled = false;
            try
            {
                if (b.healthBar != null && b.healthBar.gameObject.activeSelf) b.healthBar.gameObject.SetActive(false);
                if (b.health != null) b.health.showDamageNumbers = false;
            }
            catch (Exception) { }
        }

        // v1.1.1: the torches the skipped intro would have lit, one after another.
        IEnumerator LightTorches()
        {
            List<Torch> torches = new List<Torch>();
            try { Torch[] own = Traverse.Create(room).Field("regularModeTorches").GetValue() as Torch[]; if (own != null) torches.AddRange(own); } catch (Exception) { }
            foreach (Torch t in FindObjectsOfType<Torch>()) if (t != null && !torches.Contains(t)) torches.Add(t);
            foreach (Torch t in torches)
            {
                if (t == null) continue;
                try { t.Toggle(true); } catch (Exception) { }
                try { SoundManager.PlayAudio("MageFireballRelease", 1f, false, -1f, -1f); } catch (Exception) { }
                yield return new WaitForSeconds(0.12f);
            }
        }

        bool ChampionDown()
        {
            if (champion == null) return true;
            if (champion.health != null && champion.health.CurrentHealthValue <= 0) return true;
            return champion.fsm != null && champion.fsm.currentStateName != null && champion.fsm.currentStateName.Contains("Dead");
        }

        IEnumerator Swap()
        {
            Vector3 pos = sura.transform.position;
            HideSura();
            // v1.1.1: the champion's own entrance: the torches light, a flash on the empty throne, then it appears.
            yield return StartCoroutine(LightTorches());
            // v1.1.1: the room takes the champion's colours.
            ArenaTint.ApplyFor(prefix, FinalRun.Alchemist); // second pass: lit torches, anything turned on since
            try { CameraController cam = CameraController.Instance; if (cam != null) cam.OverrideAndMoveCameraToLocation(sura.transform, null, 2f, true, false, false, false); } catch (Exception) { }
            yield return new WaitForSeconds(0.6f);
            ArcanosBridge.Fx("Teletransporte", pos);
            champion = Rivals.Spawn(pos, prefix, RivalTier.Final);
            if (champion == null)
            {
                Plugin.Log.LogWarning("FINAL champion spawn failed, Sura returns");
                ShowSura();
                ended = true;
                yield break;
            }
            championSpawned = true;
            bool alchemist = FinalRun.Alchemist;
            RivalMarker cm = champion.GetComponent<RivalMarker>();
            if (cm != null) cm.Alchemist = alchemist; // v1.1.0: set before its Start (the hostile setup reads it)
            yield return null; // its Start + hostile setup
            yield return null;
            if (alchemist) yield return StartCoroutine(WhiteAlchemist.Monologue(champion)); // v1.1.1: it introduces itself
            else if (champion != null) yield return StartCoroutine(ChampionSpeech.Monologue(champion, title, ArchonLines.IntroLines(prefix), ElementalGiants.ElementColor(prefix))); // v1.1.1: so does an Archon
            if (champion == null) yield break;
            yield return StartCoroutine(Rivals.Intro(champion, title, alchemist ? "Final Champion  ·  the essence of every element" : "Final Champion"));
            if (alchemist && champion != null)
            {
                RivalBanter b = champion.gameObject.AddComponent<RivalBanter>(); // half-health and defeat lines
                b.Voice = RivalVoice.Primordial;
                b.Speaker = WhiteAlchemist.Name;
                b.Colour = WhiteAlchemist.White;
            }
            else if (champion != null && champion.GetComponent<RivalBanter>() == null)
            {
                RivalBanter b = champion.gameObject.AddComponent<RivalBanter>(); // v1.1.1: an Archon's half-health and defeat lines
                b.Voice = RivalVoice.Archon;
                b.Prefix = prefix;
                b.Speaker = title;
                b.Colour = ElementalGiants.ElementColor(prefix);
            }
        }

        // Sura stays in the scene as the room's boss, but out of the fight: no AI, no sprites, no hitboxes, no bar.
        void HideSura()
        {
            sura.enabled = false;
            if (sura.health != null) sura.health.invulnerable = true;
            foreach (Renderer r in sura.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            foreach (Collider2D c in sura.GetComponentsInChildren<Collider2D>(true)) c.enabled = false;
            Type bar = AccessTools.TypeByName("EnemyHealthBar");
            if (bar != null)
                foreach (UnityEngine.Object o in FindObjectsOfType(bar))
                {
                    Component c = o as Component;
                    if (c != null) c.gameObject.SetActive(false);
                }
        }

        void ShowSura()
        {
            if (sura == null) return;
            sura.enabled = true;
            if (sura.health != null) sura.health.invulnerable = false;
            foreach (Renderer r in sura.GetComponentsInChildren<Renderer>(true)) r.enabled = true;
            foreach (Collider2D c in sura.GetComponentsInChildren<Collider2D>(true)) c.enabled = true;
        }

        // The champion is down: Sura takes her final death (as after her Chaos form), and the room's own Update does
        // the rest: AnnounceVictory -> BossOutro (Chaos arcana unlock, achievement) -> ActivateExitPortal (ending).
        IEnumerator Finish(Traverse tr)
        {
            Plugin.Log.LogInfo("FINAL champion defeated: handing the victory to the room (normal ending, Chaos arcana unlock)");
            yield return new WaitForSeconds(1.5f); // let the champion's death play
            FinalChampionBadge.Hide();
            if (FinalRun.Alchemist) try { WhiteAlchemist.Rewards(); } catch (Exception ex) { Plugin.Log.LogWarning("ALCHEMIST rewards failed: " + ex.Message); }
            if (sura == null) yield break;
            Traverse ts = Traverse.Create(sura);
            ts.Field("chaosMode").SetValue(true);      // her death state takes the final ("DeadHoodless") branch
            ts.Field("superMode").SetValue(false);
            tr.Field("secondFormStarted").SetValue(true); // the room skips the Chaos-form intro...
            tr.Field("thirdFormStarted").SetValue(true);  // ...and the super-form intro
            try { sura.fsm.ChangeState("Dead", false); }
            catch (Exception ex) { Plugin.Log.LogError("FINAL could not finish Sura: " + ex); }
            // v1.1.1: the room's Update only declares victory when chaosMode is off (with it on, it waits for her Chaos
            // form; she's frozen and hidden, so it waited forever: the Alchemist died and nothing happened). Her Dead
            // state has read it by now; turn it off, and no super form either.
            yield return null;
            if (sura != null) ts.Field("chaosMode").SetValue(false);
            tr.Field("superModeAvailable").SetValue(false);
            yield return new WaitForSeconds(2f);
            if (sura != null && !tr.Field("bossDefeated").GetValue<bool>())
                Plugin.Log.LogWarning("FINAL victory not declared yet (Sura state " + (sura.fsm != null ? sura.fsm.currentStateName : "?") + ")");
            else Plugin.Log.LogInfo("FINAL victory declared");
        }
    }

    // The run's final-boss decision. Rolled once per run: when you leave the Hub for a new run, or when the run's
    // loop count / stage order changes. Read by the loading-screen board (icon) and the final room (the fight).
    internal static class FinalRun
    {
        static string runKey;
        static bool needRoll = true, wasInHub;

        internal static string ChampionPrefix { get; private set; }
        internal static bool Alchemist { get; private set; } // v1.1.0: the White Alchemist (prefix "Chaos": every element)

        // Per-run seed: other per-run decisions (which stages are Chaos) are derived from it, so the loading-screen
        // board and the stage generator always agree.
        internal static int Seed { get; private set; }

        internal static string ChampionTitle
        {
            get
            {
                if (ChampionPrefix == null) return null;
                if (Alchemist) return WhiteAlchemist.Name;
                return ChampionPrefix == "Chaos" ? "Chaos Sovereign" : ElementalGiants.ElementColorName(ChampionPrefix) + " Archon";
            }
        }

        // A run = from leaving the Hub until back in it; a new loop (after beating the final boss) counts as a new run.
        // (The level order used to be part of the key, but the game reshuffles it during a run, which re-rolled the
        // seed mid-run: the Chaos stages and the champion changed between stages.)
        static string Key() { return GameController.loopCount.ToString(); }

        internal static void Tick()
        {
            string level = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name ?? "";
            // 0.5.15: roll once on arriving in the Hub, not on leaving it. The loading board shown when leaving the Hub
            // still has the Hub as active scene, so it used to draw the previous run's Chaos plan / champion, which
            // then changed at the next board.
            bool inHub = level == "Hub" || level == "Tutorial";
            if (inHub) { if (wasInHub) return; wasInHub = true; }
            else
            {
                wasInHub = false;
                if (!needRoll && Key() == runKey) return;
            }
            string key = Key();
            needRoll = false;
            runKey = key;
            Seed = UnityEngine.Random.Range(1, int.MaxValue);
            if (Plugin.FinalChampions.Value && UnityEngine.Random.value < Plugin.FinalChampionChance.Value) Pick();
            else { ChampionPrefix = null; Alchemist = false; }
            Plugin.Log.LogInfo("FINAL run roll (" + key + "): " + (ChampionPrefix == null ? "Sura" : ChampionTitle));
        }

        // v1.1.0 test key (Alt+F12): the White Alchemist in the next final room.
        internal static void ForceAlchemist()
        {
            Tick();
            Alchemist = true;
            ChampionPrefix = "Chaos";
        }

        // v1.1.1 test key (Alt+4): an Archon (or the Chaos Sovereign) in the next final room.
        internal static void ForceArchon()
        {
            Tick();
            Alchemist = false;
            ChampionPrefix = UnityEngine.Random.value < 0.2f ? "Chaos" : Archons.MaybeVariant(ElementalGiants.RandomPrefix());
        }

        internal static void ForceChampion()
        {
            Tick();
            if (ChampionPrefix == null) Pick();
        }

        static void Pick()
        {
            Alchemist = UnityEngine.Random.value < Plugin.AlchemistChance.Value;
            if (Alchemist) { ChampionPrefix = "Chaos"; return; }
            ChampionPrefix = UnityEngine.Random.value < Plugin.FinalChaosChance.Value ? "Chaos" : Archons.MaybeVariant(ElementalGiants.RandomPrefix());
        }
    }

    // Loading-screen board: the final circle (Sura's Chaos icon after the three stage bosses) shows the champion
    // instead on champion runs: the circle tinted in its element colour, the Chaos symbol hidden, one of the element's
    // arcana icons in its place (placeholder art). It stays under the board's dimmer, so it greys out like the others.
    [HarmonyPatch(typeof(GameProgressBoard), "Enable")]
    static class ProgressBoardPatch
    {
        const string OverlayName = "HB_ChampionIcon";
        static bool loggedLayout;
        static readonly Dictionary<Image, KeyValuePair<Color, bool>> original = new Dictionary<Image, KeyValuePair<Color, bool>>();
        static readonly Dictionary<Image, Sprite> originalSprite = new Dictionary<Image, Sprite>(); // v1.1.1

        static Color Multiply(Color a, Color b) { return new Color(a.r * b.r, a.g * b.g, a.b * b.b, a.a); }

        static BoardMarks BoardMarksFor(GameProgressBoard b) { return b.GetComponent<BoardMarks>() ?? b.gameObject.AddComponent<BoardMarks>(); }

        static void Postfix(GameProgressBoard __instance)
        {
            try
            {
                FinalRun.Tick();
                RectTransform circle = Traverse.Create(__instance).Field("finalBossCircle").GetValue() as RectTransform;
                Image dimmer = Traverse.Create(__instance).Field("finalBossDimmer").GetValue() as Image;
                if (circle == null) return;
                if (!loggedLayout) { loggedLayout = true; LogLayout(circle, dimmer); }

                // Layout (logged): FinalBossCircle (ring), BossCircleInner, BossPortrait (Sura), ChaosIcon, dimmers.
                // On champion runs Sura's own portrait and circle take the champion's element colour; the Chaos symbol
                // stays only for the Chaos Sovereign. (The old placeholder arcana icon is removed.)
                Transform existing = circle.Find(OverlayName);
                if (existing != null) UnityEngine.Object.Destroy(existing.gameObject);
                string prefix = FinalRun.ChampionPrefix;
                bool champion = prefix != null;
                Color el = champion ? (FinalRun.Alchemist ? WhiteAlchemist.White : ElementalGiants.ElementColor(prefix)) : Color.white;
                foreach (Image img in circle.GetComponentsInChildren<Image>(true))
                {
                    if (img == dimmer || img.name.Contains("Dimmer")) continue;
                    if (!original.ContainsKey(img)) original[img] = new KeyValuePair<Color, bool>(img.color, img.enabled);
                    KeyValuePair<Color, bool> o = original[img];
                    img.color = o.Key;
                    img.enabled = o.Value;
                    if (!originalSprite.ContainsKey(img)) originalSprite[img] = img.sprite;
                    img.sprite = originalSprite[img];
                    if (!champion) continue;
                    switch (img.name)
                    {
                        case "BossPortrait":                                    // Sura in the element's colour (the game hides
                            img.enabled = true;                                 // her portrait behind the Chaos symbol by default)
                            // v1.1.1: a recoloured Sura in the champion's palette (pure white for the Primordial
                            // Alchemist, the element's ramp for an Archon); the old tint if the twin can't be made.
                            string ramp = FinalRun.Alchemist ? "Pure" : ArchonLook.RampForPrefix(prefix);
                            Sprite twin = ramp != null ? RecolorEngine.Twin(originalSprite[img], ramp) : null;
                            if (twin != null) { img.sprite = twin; img.color = o.Key; }
                            else img.color = Multiply(o.Key, Color.Lerp(Color.white, el, 0.7f));
                            BoardMarksFor(__instance).Add(img, img.color, null, true); // keep it shown if the board toggles it
                            break;
                        case "BossCircleInner": img.color = Multiply(o.Key, Color.Lerp(el, Color.black, 0.35f)); break;
                        case "ChaosIcon": img.enabled = prefix == "Chaos" && !FinalRun.Alchemist; break;
                        default: if (img.transform == circle) img.color = Multiply(o.Key, el); break;              // the ring
                    }
                }
            }
            catch (Exception ex) { Plugin.Log.LogWarning("FINAL board icon failed: " + ex.Message); }
        }

        static void LogLayout(RectTransform circle, Image dimmer)
        {
            List<string> parts = new List<string>();
            foreach (Transform t in circle.GetComponentsInChildren<Transform>(true))
            {
                Image img = t.GetComponent<Image>();
                parts.Add(t.name + (img != null ? "[Image " + (img.sprite != null ? img.sprite.name : "-") + (img == dimmer ? " dimmer" : "") + "]" : ""));
            }
            Plugin.Log.LogInfo("FINAL board circle layout: " + string.Join(", ", parts.ToArray())
                + (dimmer != null ? " | dimmer parent: " + (dimmer.transform.parent != null ? dimmer.transform.parent.name : "none") : ""));
        }
    }

    [HarmonyPatch(typeof(FinalBossRoomEventHandler), "Start")]
    static class FinalRoomStartPatch
    {
        static void Postfix(FinalBossRoomEventHandler __instance)
        {
            try { FinalChampion.Roll(__instance); }
            catch (Exception ex) { Plugin.Log.LogError("FINAL boss roll failed: " + ex); }
        }
    }

    // Placeholder "not Sura" badge, top right, from arriving in the final room until the champion falls.
    internal class FinalChampionBadge : MonoBehaviour
    {
        static FinalChampionBadge current;
        FinalChampion fc;
        Sprite icon;
        float shownAt;

        internal static void Show(FinalChampion fc)
        {
            Hide();
            GameObject go = new GameObject("HybridBosses_FinalBadge");
            current = go.AddComponent<FinalChampionBadge>();
            current.fc = fc;
            current.shownAt = Time.time;
        }

        internal static void Hide()
        {
            if (current != null) Destroy(current.gameObject);
            current = null;
        }

        void OnGUI()
        {
            if (fc == null) { Destroy(gameObject); return; }
            if (icon == null)
            {
                // Placeholder art: the first arcana of the champion's pool (any player arcana icon of that element).
                List<ArcanaDef> pool = ArcanaCatalog.Pool(fc.Prefix);
                if (pool.Count > 0) icon = IntroUI.SkillIcon(pool[0].SkillID);
            }
            Color el = ElementalGiants.ElementColor(fc.Prefix);
            float pulse = 0.75f + 0.25f * Mathf.Sin((Time.time - shownAt) * 3f);
            float size = 56f;
            Rect frame = new Rect(Screen.width - size - 24f, 24f, size, size);
            Color old = GUI.color;
            GUI.color = new Color(el.r, el.g, el.b, pulse);
            GUI.DrawTexture(frame, Texture2D.whiteTexture);
            GUI.color = new Color(0.08f, 0.05f, 0.12f, 0.95f);
            GUI.DrawTexture(new Rect(frame.x + 3, frame.y + 3, size - 6, size - 6), Texture2D.whiteTexture);
            GUI.color = old;
            if (icon != null) IntroUI.DrawSprite(new Rect(frame.x + 6, frame.y + 6, size - 12, size - 12), icon, Color.white);
            else
            {
                GUIStyle q = GameFont.Style(GUI.skin.label, 30, TextAnchor.MiddleCenter, true);
                q.normal.textColor = el;
                GUI.Label(frame, "?", q);
            }
            GUIStyle st = GameFont.Style(GUI.skin.label, 13, TextAnchor.UpperRight, true);
            st.normal.textColor = el;
            GUI.Label(new Rect(Screen.width - 260f, frame.yMax + 4f, 236f, 20f), "NOT SURA", st);
            st.normal.textColor = Color.white;
            GUI.Label(new Rect(Screen.width - 260f, frame.yMax + 20f, 236f, 20f), fc.Title + " awaits", st);
            GUI.color = old;
        }
    }
}
