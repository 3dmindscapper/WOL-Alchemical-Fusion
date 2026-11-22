using System;
using System.Collections.Generic;
using UnityEngine;

namespace WolHybridBosses
{
    // v1.1.1: the Rival League's board, at the giant wizard statue in the middle of the Hub. Walk up to it and press
    // Interact (or F2 anywhere in the Hub) to see your rank, points, your record against each named rival, the
    // Primordial Alchemist and the White Mantle, and the next run's bounty. Replaces the Hub's top-left text.
    //
    // The statue has no component of its own in the game's code, so it's found by its art: the largest sprite in the
    // Hub whose object or sprite name says "statue" (the candidates are logged once, to check in game).
    internal class LeagueMenu : MonoBehaviour
    {
        internal static bool IsOpen;
        static Transform statue;
        static Bounds statueBounds;
        static float searchedAt = -100f;
        static bool loggedSearch;
        float openedAt;
        static GUIStyle titleStyle, rowStyle, small, prompt;
        static Font styleFont;

        const float Range = 2.8f;

        static bool InHubScene { get { return UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Hub"; } }

        static void FindStatue()
        {
            searchedAt = Time.unscaledTime;
            statue = null;
            List<KeyValuePair<float, SpriteRenderer>> found = new List<KeyValuePair<float, SpriteRenderer>>();
            foreach (SpriteRenderer sr in FindObjectsOfType<SpriteRenderer>())
            {
                if (sr == null || !sr.enabled || sr.transform.root.name.StartsWith("HybridBosses")) continue;
                string n = (sr.name + " " + (sr.sprite != null ? sr.sprite.name : "") + " " + (sr.transform.parent != null ? sr.transform.parent.name : "")).ToLowerInvariant();
                if (!n.Contains("statue")) continue;
                if (sr.GetComponentInParent<Enemy>() != null) continue;
                Vector3 s = sr.bounds.size;
                found.Add(new KeyValuePair<float, SpriteRenderer>(s.x * s.y, sr));
            }
            found.Sort((a, b) => b.Key.CompareTo(a.Key));
            if (found.Count > 0) { statue = found[0].Value.transform; statueBounds = found[0].Value.bounds; }
            if (!loggedSearch)
            {
                loggedSearch = true;
                List<string> l = new List<string>();
                for (int i = 0; i < found.Count && i < 6; i++)
                {
                    SpriteRenderer sr = found[i].Value;
                    l.Add(sr.name + "/" + (sr.sprite != null ? sr.sprite.name : "-") + " @" + sr.bounds.center.ToString("0.0") + " size " + sr.bounds.size.ToString("0.0"));
                }
                Plugin.Log.LogInfo("LEAGUE statue: " + (statue != null ? "using " + statue.name : "none found (F2 opens the board)") + " | candidates: " + string.Join("; ", l.ToArray()));
            }
        }

        static bool Near(Player p)
        {
            if (statue == null || p == null) return false;
            Vector3 pos = p.transform.position;
            Vector3 c = statueBounds.ClosestPoint(new Vector3(pos.x, pos.y, statueBounds.center.z));
            return Vector2.Distance(pos, c) < Range;
        }

        void Update()
        {
            if (!InHubScene || TomiInfusion.InRun) { IsOpen = false; statue = null; return; }
            if (statue == null && Time.unscaledTime - searchedAt > 3f) FindStatue();
            Player p = Elites.Player0;
            ChaosInputDevice dev = p != null ? p.inputDevice : null;
            if (!IsOpen)
            {
                if (ModifiersMenu.IsOpen || NocturneMenu.IsOpen) return;
                if (Input.GetKeyDown(KeyCode.F2) || (Near(p) && Pad(dev, "Interact"))) { IsOpen = true; openedAt = Time.unscaledTime; }
                return;
            }
            if (Time.unscaledTime - openedAt < 0.25f) return;
            if (Input.GetKeyDown(KeyCode.F2) || Input.GetKeyDown(KeyCode.Escape) || Pad(dev, "Cancel") || Pad(dev, "Interact"))
                IsOpen = false;
        }

        static bool Pad(ChaosInputDevice dev, string action)
        {
            if (dev == null) return false;
            try { return dev.GetButtonDown(action); } catch (Exception) { return false; }
        }

        void OnGUI()
        {
            if (!InHubScene || TomiInfusion.InRun) return;
            if (rowStyle == null || GameFont.Changed(ref styleFont))
            {
                titleStyle = GameFont.Style(GUI.skin.label, 22, TextAnchor.MiddleCenter, true);
                rowStyle = GameFont.Style(GUI.skin.label, 16, TextAnchor.MiddleLeft);
                small = GameFont.Style(GUI.skin.label, 12, TextAnchor.MiddleCenter);
                prompt = GameFont.Style(GUI.skin.label, 15, TextAnchor.MiddleCenter, false, true);
            }
            Player p = Elites.Player0;
            if (!IsOpen)
            {
                // The prompt over the statue when you're close.
                if (ModifiersMenu.IsOpen || NocturneMenu.IsOpen || !Near(p)) return;
                Camera cam = Camera.main;
                if (cam == null) return;
                Vector3 sp = cam.WorldToScreenPoint(new Vector3(statueBounds.center.x, statueBounds.max.y + 0.4f, 0f));
                if (sp.z < 0f) return;
                GameFont.Shadowed(new Rect(sp.x - 200f, Screen.height - sp.y - 14f, 400f, 28f), "Rival League  ·  Interact", prompt, new Color(1f, 0.85f, 0.45f));
                return;
            }
            Draw();
        }

        static void Row(Rect box, ref float y, string left, string right, Color c)
        {
            Rect r = new Rect(box.x + 30f, y, box.width - 60f, 24f);
            GameFont.Shadowed(r, left, rowStyle, c);
            if (right != null) GameFont.Shadowed(r, "<color=#ffd27a>" + right + "</color>", Right(), c);
            y += 24f;
        }

        static GUIStyle right;
        static GUIStyle Right() { if (right == null || right.font != rowStyle.font) right = new GUIStyle(rowStyle) { alignment = TextAnchor.MiddleRight, richText = true }; return right; }

        static Rect Panel(float h)
        {
            float w = Mathf.Min(620f, Screen.width - 40f);
            Rect box = new Rect((Screen.width - w) / 2f, Mathf.Max(20f, (Screen.height - h) / 2f), w, h);
            Color old = GUI.color;
            GUI.color = new Color(0.04f, 0.03f, 0.07f, 0.92f);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = new Color(0.95f, 0.8f, 0.45f, 0.9f);
            GUI.DrawTexture(new Rect(box.x, box.y, box.width, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(box.x, box.yMax - 2f, box.width, 2f), Texture2D.whiteTexture);
            GUI.color = old;
            return box;
        }

        static void Draw()
        {
            List<string> prefixes = new List<string> { "Flame", "Frost", "Earth", "Wind", "Thunder", "Chaos" };
            if (ArcanosBridge.Present) { prefixes.Add("Radiant"); prefixes.Add("Prism"); }
            Rect box = Panel(254f + 24f * prefixes.Count);
            Color text = new Color(0.9f, 0.88f, 0.84f), dim = new Color(0.7f, 0.68f, 0.64f);
            GameFont.Shadowed(new Rect(box.x, box.y + 10f, box.width, 30f), "Rival League", titleStyle, new Color(1f, 0.9f, 0.6f));
            float y = box.y + 48f;
            if (!League.On) { Row(box, ref y, "The league is off (modifiers menu, F1)", null, dim); }
            else
            {
                Row(box, ref y, "Rank:  " + League.RankName, League.Points + " pts  ·  " + League.NextRankText, text);
                y += 6f;
                int tw = 0, tl = 0;
                foreach (string pf in prefixes)
                {
                    LeagueRival r = League.For(pf);
                    if (r == null) continue;
                    tw += r.Wins; tl += r.Losses;
                    Color ec = ElementalGiants.ElementColor(pf);
                    Row(box, ref y, r.Name + "  <color=#" + ColorUtility.ToHtmlStringRGB(ec) + ">(" + ElementalGiants.ElementColorName(pf) + ")</color>",
                        r.Wins + r.Losses == 0 ? "not met yet" : r.Wins + " – " + r.Losses, text);
                }
                Row(box, ref y, "All rivals", tw + " – " + tl, dim);
            }
            y += 6f;
            int aw = Progress.GetInt("alchemist.wins");
            Row(box, ref y, WhiteAlchemist.Name, aw > 0 ? "defeated " + aw + (aw == 1 ? " time" : " times") : "not yet defeated", text);
            Row(box, ref y, "White Mantle", !WhiteMantle.Unlocked ? "locked" : WhiteMantle.Worn ? "worn" : "in the wardrobe", text);
            Row(box, ref y, GrandmasterRobe.Name, GrandmasterRobe.Unlocked ? (GrandmasterRobe.Worn ? "worn" : "in the wardrobe") : "reach Grandmaster", text);
            Row(box, ref y, "Primordial robes (beat Archons)", PrimordialRobes.UnlockedCount + " / " + PrimordialRobes.All.Length + (PrimordialRobes.Worn != null ? "  ·  wearing " + PrimordialRobes.Worn.Element : ""), text);
            if (Bounties.Active && Plugin.BountiesEnabled.Value)
                Row(box, ref y, "Next run's bounty:  " + Bounties.Text, Bounties.Reward + "+ gold", text);
            if (Plugin.TestMode) Row(box, ref y, "Test hotkeys on: nothing is saved this session", null, new Color(1f, 0.55f, 0.45f));
            if (AlchemistTrueForm.Earned) Row(box, ref y, "Every Primordial robe: the Alchemist will show its true form", null, new Color(1f, 0.95f, 0.8f));
            GameFont.Shadowed(new Rect(box.x, box.yMax - 30f, box.width, 22f), "Interact / Esc / F2: close  ·  infusions: Tomix's journal", small, dim);
        }
    }
}
