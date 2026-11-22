using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WolHybridBosses
{
    // v0.7.3: the game's own UI font for every on-screen text the mod draws (announcement cards, hints, labels), so
    // they match the game's look. It's the font most of the game's UI Text components use, found at runtime.
    internal static class GameFont
    {
        static Font font;
        static float nextTry;

        internal static Font Get()
        {
            if (font != null) return font;
            if (Time.unscaledTime < nextTry) return null;
            nextTry = Time.unscaledTime + 2f;
            try
            {
                Dictionary<Font, int> uses = new Dictionary<Font, int>();
                foreach (Text t in Resources.FindObjectsOfTypeAll<Text>())
                {
                    if (t == null || t.font == null || t.font.name == "Arial") continue;
                    int n;
                    uses.TryGetValue(t.font, out n);
                    uses[t.font] = n + 1;
                }
                int best = 0;
                foreach (KeyValuePair<Font, int> kv in uses) if (kv.Value > best) { best = kv.Value; font = kv.Key; }
                if (font != null) Plugin.Log.LogInfo("UI using the game's font: " + font.name + " (" + best + " texts" + (font.dynamic ? ", dynamic" : "") + ")");
            }
            catch (Exception) { }
            return font;
        }

        // A style in the game's font (pixel fonts have no bold face, so bold is dropped when the font is found).
        internal static GUIStyle Style(GUIStyle baseStyle, int size, TextAnchor align, bool bold = false, bool wrap = false)
        {
            GUIStyle s = new GUIStyle(baseStyle) { fontSize = size, alignment = align, wordWrap = wrap, richText = true };
            Font f = Get();
            if (f != null) s.font = f;
            s.fontStyle = bold && f == null ? FontStyle.Bold : FontStyle.Normal;
            return s;
        }

        // True when the game font turned up since the caller built its styles (so it should rebuild them).
        internal static bool Changed(ref Font seen)
        {
            Font f = Get();
            if (f == seen) return false;
            seen = f;
            return true;
        }

        // Text with the game's dark drop shadow.
        internal static void Shadowed(Rect r, string text, GUIStyle style, Color color)
        {
            Color c = style.normal.textColor;
            style.normal.textColor = new Color(0f, 0f, 0f, color.a * 0.85f);
            // v0.9.1: the shadow pass drops <color> tags, or coloured words drew a second coloured copy offset by 2px.
            string plain = System.Text.RegularExpressions.Regex.Replace(text, "</?color[^>]*>", "");
            GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), plain, style);
            style.normal.textColor = color;
            GUI.Label(r, text, style);
            style.normal.textColor = c;
        }
    }
}
