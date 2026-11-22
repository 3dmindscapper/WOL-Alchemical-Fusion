using System;
using System.Collections.Generic;
using UnityEngine;

namespace WolHybridBosses
{
    // One icon + label shown in a boss/rival intro (an arcana, the robe, a relic...).
    internal class IntroEntry
    {
        public Sprite Icon;
        public string Label;
        public Color Tint = Color.white;
        public IntroEntry(Sprite icon, string label) { Icon = icon; Label = label; }
    }

    // Boss/rival intro: a title, then the entries appear left to right one by one, each with its name under it.
    internal class IntroUI : MonoBehaviour
    {
        string title, subtitle;
        Color titleColor = Color.white;
        List<IntroEntry> entries = new List<IntroEntry>();
        float start, duration, stepDelay;

        static IntroUI current;

        // v1.1.1: a card is on screen (so a less urgent one, like the bounty, can wait its turn).
        internal static bool Busy { get { return current != null && Time.time < current.start + current.duration; } }

        internal static float Show(string title, string subtitle, Color titleColor, List<IntroEntry> entries, float minDuration)
        {
            if (current != null) Destroy(current.gameObject);
            GameObject go = new GameObject("HybridBosses_IntroUI");
            current = go.AddComponent<IntroUI>();
            current.title = title;
            current.subtitle = subtitle;
            current.titleColor = titleColor;
            current.entries = entries ?? new List<IntroEntry>();
            current.stepDelay = 0.55f;
            current.start = Time.time;
            // Long enough to reveal every entry and read them.
            current.duration = Mathf.Max(minDuration, 0.8f + current.stepDelay * current.entries.Count + 1.6f);
            Destroy(go, current.duration + 0.1f);
            return current.duration;
        }

        // Sprites from the game's icon atlases -> the sub-rect of their texture.
        internal static void DrawSprite(Rect r, Sprite s, Color tint)
        {
            if (s == null || s.texture == null) return;
            Rect tr;
            try { tr = s.textureRect; } catch (Exception) { tr = s.rect; } // tightly packed sprites have no textureRect
            Texture2D tex = s.texture;
            Rect uv = new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height);
            Color old = GUI.color;
            GUI.color = new Color(tint.r, tint.g, tint.b, tint.a * old.a);
            GUI.DrawTextureWithTexCoords(r, tex, uv);
            GUI.color = old;
        }

        static GUIStyle titleStyle, subStyle, labelStyle;
        static Font styleFont;

        static void Label(Rect r, string text, GUIStyle style, Color color)
        {
            Color c = style.normal.textColor;
            style.normal.textColor = new Color(0f, 0f, 0f, color.a * 0.85f);
            GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), text, style);
            style.normal.textColor = color;
            GUI.Label(r, text, style);
            style.normal.textColor = c;
        }

        void OnGUI()
        {
            float t = Time.time - start;
            if (t > duration) return;
            if (titleStyle == null || GameFont.Changed(ref styleFont))
            {
                // v0.7.3: the game's own font (falls back to Unity's until it's found).
                titleStyle = GameFont.Style(GUI.skin.label, 32, TextAnchor.MiddleCenter, true);
                subStyle = GameFont.Style(GUI.skin.label, 18, TextAnchor.MiddleCenter);
                labelStyle = GameFont.Style(GUI.skin.label, 15, TextAnchor.UpperCenter, true, true);
            }

            float fadeOut = Mathf.Clamp01((duration - t) / 0.5f);
            float fadeIn = Mathf.Clamp01(t / 0.35f);
            float a = Mathf.Min(fadeIn, fadeOut);

            float y = Screen.height * 0.10f;
            Label(new Rect(0, y, Screen.width, 40), title, titleStyle, new Color(titleColor.r, titleColor.g, titleColor.b, a));
            if (!string.IsNullOrEmpty(subtitle))
                Label(new Rect(0, y + 38, Screen.width, 26), subtitle, subStyle, new Color(1f, 1f, 1f, a * 0.9f));

            if (entries.Count == 0) return;
            float icon = Mathf.Clamp(Screen.height * 0.075f, 40f, 80f);
            float slot = icon + 54f;
            float rowX = (Screen.width - slot * entries.Count) * 0.5f;
            float rowY = y + 76f;

            for (int i = 0; i < entries.Count; i++)
            {
                float appear = 0.8f + stepDelay * i;
                if (t < appear) break;
                float p = Mathf.Clamp01((t - appear) / 0.3f);          // pop-in
                float ea = Mathf.Min(p, fadeOut);
                float size = icon * Mathf.Lerp(1.35f, 1f, p);
                float cx = rowX + slot * i + slot * 0.5f;
                Rect r = new Rect(cx - size * 0.5f, rowY + (icon - size) * 0.5f + (1f - p) * -10f, size, size);
                IntroEntry e = entries[i];
                Color tint = new Color(e.Tint.r, e.Tint.g, e.Tint.b, ea);
                if (e.Icon != null) DrawSprite(r, e.Icon, tint);
                else
                {
                    Color old = GUI.color; GUI.color = new Color(e.Tint.r, e.Tint.g, e.Tint.b, ea * 0.6f);
                    GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = old;
                }
                Label(new Rect(cx - slot * 0.5f, rowY + icon + 6f, slot, 40f), e.Label, labelStyle, new Color(1f, 1f, 1f, ea));
            }
        }

        // ---------- lookups (all fail soft) ----------

        // null when the game has no icon for it (IconManager hands back its "?" placeholder for unknown IDs).
        internal static Sprite SkillIcon(string skillID)
        {
            try
            {
                if (string.IsNullOrEmpty(skillID)) return null;
                if (!placeholderKnown) { placeholderKnown = true; placeholder = IconManager.GetSkillIcon("HybridBossesNoSuchSkill"); }
                Sprite s = IconManager.GetSkillIcon(skillID);
                return s == null || (placeholder != null && s == placeholder) ? null : s;
            }
            catch (Exception) { return null; }
        }

        static Sprite placeholder;
        static bool placeholderKnown;

        // An enemy skill ("EarthKnightPoisonBolas") shown with the icon of the player arcana it mirrors
        // ("UsePoisonBolas"), matched by name; null if none fits (the entry then shows an element-coloured tile).
        // Enemy skills whose name doesn't match a player arcana, mapped by hand to the closest-looking one.
        static readonly Dictionary<string, string> EnemyIconMap = new Dictionary<string, string>
        {
            { "FlameKnightFlameShield", "FlameShieldDash" }, { "FlameMageIgnitionOrb", "UseFireOrbBasic" },
            { "FlameRogueFlameRush", "FireDash" }, { "FrostRogueIceDecoy", "IceDash" }, { "FrostArcherIceTrap", "UseFrostBlock" },
            { "FrostLancerIceBeam", "UseAquaBeam" }, { "FrostMageFrostBlast", "UseFrostFan" }, { "EarthArcherPoisonBomb", "PoisonDash" },
            { "EarthLancerDrillCharge", "UseEarthDrill" }, { "WindMageSummonTwister", "UseTwister" }, { "WindArcherWindTrap", "AirTrapDash" },
            { "WindRogueBlastDash", "UseAirWave" }, { "ThunderRogueShockBomb", "UseShockNova" }, { "ThunderLancerGroundShock", "UseShockSpear" },
            { "ThunderMageThunderBlast", "ThunderOrbBasic" },
        };

        internal static Sprite EnemySkillIcon(string enemySkill, string element, string kind)
        {
            Sprite own = SkillIcon(enemySkill);
            if (own != null) return own;
            string mapped;
            if (enemySkill != null && EnemyIconMap.TryGetValue(enemySkill, out mapped))
            {
                Sprite m = SkillIcon(mapped);
                if (m != null) return m;
            }
            string core = enemySkill ?? "";
            if (core.StartsWith(element)) core = core.Substring(element.Length);
            if (core.StartsWith(kind)) core = core.Substring(kind.Length);
            if (core.EndsWith("State")) core = core.Substring(0, core.Length - 5);
            if (core.Length < 4) return null;
            string best = null;
            foreach (GenArcana g in ArcanaCatalog.AllRows())
            {
                string id = g.SkillID;
                if (id == null || !id.EndsWith(core)) continue;
                if (best == null || id.Length < best.Length) best = id; // the closest name ("UseFireBlast" over "UseFireBlastXL")
            }
            return best != null ? SkillIcon(best) : null;
        }

        internal static Sprite ItemIcon(string itemID)
        {
            try { return IconManager.GetItemIcon(itemID); } catch (Exception) { return null; }
        }

        internal static string SkillName(string skillID)
        {
            try { string n = TextManager.GetSkillName(skillID); if (!string.IsNullOrEmpty(n)) return n; } catch (Exception) { }
            return EnemyArcana.DisplayName(skillID);
        }

        internal static string ItemName(string itemID)
        {
            try { string n = TextManager.GetItemName(itemID); if (!string.IsNullOrEmpty(n)) return n; } catch (Exception) { }
            return itemID;
        }

        // v1.1.1: a robe's icon in its colours. IconManager.GetWizardSprite takes a POSE name ("WizardDeadDown8"), not
        // a robe; the pose is drawn in palette indices (the game shows it through the palette shader with
        // _PaletteIndex = the robe's colour row), which drawn raw was a solid block. Here the indices are looked up
        // in the wizard palette (the player's material) once per row: red channel = column, row = robe colour.
        static readonly Dictionary<int, Sprite> robeIcons = new Dictionary<int, Sprite>();
        static readonly string[] Poses = { "WizardIdleDown0", "WizardIdleDown", "WizardRunDown0", "WizardJokePose0", "WizardDeadDown8" };
        static bool loggedRobe;

        internal static Sprite RobeIcon(int row)
        {
            Sprite cached;
            if (robeIcons.TryGetValue(row, out cached)) return cached;
            Sprite result = null;
            try
            {
                Player p = Elites.Player0;
                Material pm = p != null && p.spriteRenderer != null ? p.spriteRenderer.sharedMaterial : null;
                if (pm == null || !pm.HasProperty("_Palette") || !pm.HasProperty("_PaletteCount")) return null;
                Texture pal = pm.GetTexture("_Palette");
                int count = Mathf.Max(1, Mathf.RoundToInt(pm.GetFloat("_PaletteCount")));
                Sprite pose = null;
                string poseName = null;
                Dictionary<string, Sprite> all = HarmonyLib.Traverse.Create(typeof(IconManager)).Property("WizardSprites").GetValue() as Dictionary<string, Sprite>; // private
                if (all != null) foreach (string n in Poses) if (all.ContainsKey(n)) { pose = all[n]; poseName = n; break; }
                if (pose == null && all != null) // none of the expected names: any front-facing pose, else any
                    foreach (KeyValuePair<string, Sprite> kv in all) { if (pose == null || (kv.Key.Contains("Down") && !poseName.Contains("Down"))) { pose = kv.Value; poseName = kv.Key; } }
                if (pose == null || pal == null) return null;
                Rect tr;
                try { tr = pose.textureRect; } catch (Exception) { tr = pose.rect; }
                int pw, ph, sw = Mathf.RoundToInt(tr.width), sh = Mathf.RoundToInt(tr.height);
                Color32[] palPx = ReadAll(pal, out pw, out ph);
                int tw, th;
                Color32[] atlas = ReadAll(pose.texture, out tw, out th);
                if (palPx == null || atlas == null) return null;
                int r = Mathf.Clamp(row, 0, count - 1);
                int py = Mathf.Clamp(Mathf.FloorToInt((r + 0.5f) * ph / count), 0, ph - 1);
                Color32[] outPx = new Color32[sw * sh];
                int x0 = Mathf.RoundToInt(tr.x), y0 = Mathf.RoundToInt(tr.y);
                for (int y = 0; y < sh; y++)
                    for (int x = 0; x < sw; x++)
                    {
                        Color32 c = atlas[(y0 + y) * tw + (x0 + x)];
                        if (c.a == 0) { outPx[y * sw + x] = new Color32(0, 0, 0, 0); continue; }
                        int px = Mathf.Clamp(c.r * pw / 256, 0, pw - 1);
                        Color32 o = palPx[py * pw + px];
                        o.a = c.a;
                        outPx[y * sw + x] = o;
                    }
                Texture2D t = new Texture2D(sw, sh, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                t.SetPixels32(outPx);
                t.Apply(false);
                result = Sprite.Create(t, new Rect(0, 0, sw, sh), new Vector2(0.5f, 0.5f), 16f);
                result.name = "HB_Robe" + row;
                if (!loggedRobe) { loggedRobe = true; Plugin.Log.LogInfo("ROBE icon from " + poseName + " " + sw + "x" + sh + ", palette " + pw + "x" + ph + " (" + count + " rows)"); }
            }
            catch (Exception ex) { Plugin.Log.LogWarning("ROBE icon failed: " + ex.Message); result = null; }
            robeIcons[row] = result;
            return result;
        }

        static Color32[] ReadAll(Texture src, out int w, out int h)
        {
            w = src.width; h = src.height;
            RenderTexture rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            Graphics.Blit(src, rt);
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            t.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            t.Apply(false);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            Color32[] px = t.GetPixels32();
            Destroy(t);
            return px;
        }

        internal static string OutfitName(string outfitID)
        {
            try { string n = TextManager.GetOutfitName(outfitID); if (!string.IsNullOrEmpty(n)) return n; } catch (Exception) { }
            return outfitID;
        }
    }
}
