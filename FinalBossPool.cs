using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

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

        internal static FinalChampion Roll(FinalBossRoomEventHandler room)
        {
            bool force = ForceNext;
            ForceNext = false;
            if (!force && (!Plugin.FinalChampions.Value || UnityEngine.Random.value >= Plugin.FinalChampionChance.Value)) return null;

            FinalChampion fc = room.gameObject.GetComponent<FinalChampion>() ?? room.gameObject.AddComponent<FinalChampion>();
            fc.room = room;
            fc.prefix = UnityEngine.Random.value < Plugin.FinalChaosChance.Value ? "Chaos" : ElementalGiants.RandomPrefix();
            fc.title = fc.prefix == "Chaos" ? "Chaos Sovereign" : ElementalGiants.ElementColorName(fc.prefix) + " Archon";
            FinalChampionBadge.Show(fc);
            Plugin.Log.LogInfo("FINAL boss this run: " + fc.title + " (not Sura)");
            return fc;
        }

        void Update()
        {
            if (room == null) return;
            Traverse tr = Traverse.Create(room);
            if (!swapped)
            {
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
            champion = Rivals.Spawn(pos, prefix, RivalTier.Final);
            if (champion == null)
            {
                Plugin.Log.LogWarning("FINAL champion spawn failed, Sura returns");
                ShowSura();
                ended = true;
                yield break;
            }
            championSpawned = true;
            yield return null; // its Start + hostile setup
            yield return null;
            yield return StartCoroutine(Rivals.Intro(champion, title, "Final Champion"));
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
            if (sura == null) yield break;
            Traverse ts = Traverse.Create(sura);
            ts.Field("chaosMode").SetValue(true);      // her death state takes the final ("DeadHoodless") branch
            ts.Field("superMode").SetValue(false);
            tr.Field("secondFormStarted").SetValue(true); // the room skips the Chaos-form intro...
            tr.Field("thirdFormStarted").SetValue(true);  // ...and the super-form intro
            try { sura.fsm.ChangeState("Dead", false); }
            catch (Exception ex) { Plugin.Log.LogError("FINAL could not finish Sura: " + ex); }
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
                GUIStyle q = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                q.normal.textColor = el;
                GUI.Label(frame, "?", q);
            }
            GUIStyle st = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperRight };
            st.normal.textColor = el;
            GUI.Label(new Rect(Screen.width - 260f, frame.yMax + 4f, 236f, 20f), "NOT SURA", st);
            st.normal.textColor = Color.white;
            st.fontStyle = FontStyle.Normal;
            GUI.Label(new Rect(Screen.width - 260f, frame.yMax + 20f, 236f, 20f), fc.Title + " awaits", st);
            GUI.color = old;
        }
    }
}
