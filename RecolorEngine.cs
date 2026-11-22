using System;
using System.Collections.Generic;
using UnityEngine;

namespace WolHybridBosses
{
    // v0.6.5: the recolour engine (ROADMAP 6a), the base of arcana infusion, used first on variant enemies' attacks.
    // Vanilla paints each element into its arcana's animated sprite sheets, so an element's look is carried by
    // recolouring the art itself: every pixel's brightness is mapped onto that element's ramp (a gradient map), so
    // shadows, mid-tones and highlights land where the element puts them (brown earth, near-black chaos, white ice...),
    // animation and detail intact. Animated sprites get a recoloured twin of each frame, swapped in right after the
    // Animator sets it (RecolorSprites). Particles, trails, lines and lights are mapped onto the same ramp.
    internal static class ElementRamps
    {
        static Color C(float r, float g, float b) { return new Color(r, g, b); }

        static readonly Dictionary<string, Color[]> ramps = new Dictionary<string, Color[]>
        {
            { "Fire",    new[] { C(0.35f, 0.04f, 0.02f), C(0.85f, 0.22f, 0.04f), C(1f, 0.55f, 0.1f), C(1f, 0.85f, 0.35f), C(1f, 0.98f, 0.85f) } },
            { "Water",   new[] { C(0.03f, 0.08f, 0.3f), C(0.08f, 0.3f, 0.75f), C(0.25f, 0.6f, 1f), C(0.65f, 0.88f, 1f), C(0.92f, 0.98f, 1f) } },
            { "Ice",     new[] { C(0.25f, 0.4f, 0.6f), C(0.5f, 0.72f, 0.9f), C(0.75f, 0.9f, 1f), C(0.95f, 0.98f, 1f), C(1f, 1f, 1f) } },
            { "Earth",   new[] { C(0.18f, 0.1f, 0.04f), C(0.38f, 0.22f, 0.1f), C(0.58f, 0.4f, 0.2f), C(0.78f, 0.62f, 0.38f), C(0.93f, 0.85f, 0.66f) } },
            { "Thunder", new[] { C(0.25f, 0.22f, 0.03f), C(0.6f, 0.52f, 0.05f), C(0.98f, 0.85f, 0.15f), C(1f, 0.96f, 0.55f), C(1f, 1f, 0.92f) } },
            { "Wind",    new[] { C(0.05f, 0.25f, 0.22f), C(0.2f, 0.55f, 0.45f), C(0.5f, 0.85f, 0.7f), C(0.8f, 0.98f, 0.88f), C(0.97f, 1f, 0.97f) } },
            { "Chaos",   new[] { C(0.08f, 0.02f, 0.14f), C(0.3f, 0.08f, 0.48f), C(0.62f, 0.35f, 0.95f), C(0.85f, 0.72f, 1f) } },
            { "Radiant", new[] { C(0.55f, 0.38f, 0.12f), C(0.95f, 0.72f, 0.25f), C(1f, 0.93f, 0.65f), C(1f, 1f, 0.96f) } },
        };

        internal static bool Has(string key) { return key != null && ramps.ContainsKey(key); }

        // Brightness 0..1 -> the ramp's colour.
        internal static Color Sample(string key, float t)
        {
            Color[] r;
            if (!ramps.TryGetValue(key, out r)) return new Color(t, t, t);
            t = Mathf.Clamp01(t) * (r.Length - 1);
            int i = Mathf.Min((int)t, r.Length - 2);
            return Color.Lerp(r[i], r[i + 1], t - i);
        }

        // A colour mapped onto the ramp by its brightness (alpha kept). A little contrast stretch keeps detail.
        internal static Color Map(string key, Color c)
        {
            float lum = 0.3f * c.r + 0.59f * c.g + 0.11f * c.b;
            float mx = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            float t = Mathf.Clamp01((lum * 0.7f + mx * 0.3f - 0.04f) / 0.92f);
            Color o = Sample(key, t);
            o.a = c.a;
            return o;
        }

        internal static Color Mid(string key) { return Sample(key, 0.6f); }

        // Ramp key for a variant (Prism attacks use their cycle element's key instead).
        internal static string ForVariant(StageVariant v)
        {
            switch (v)
            {
                case StageVariant.Chaos: return "Chaos";
                case StageVariant.Radiant: return "Radiant";
                default: return null;
            }
        }

        // Ramp key for a game element (Water and Ice are separate looks).
        internal static string ForElement(ElementType e)
        {
            switch (e)
            {
                case ElementType.Fire: return "Fire";
                case ElementType.Water: return "Water";
                case ElementType.Ice: return "Ice";
                case ElementType.Earth: case ElementType.Plant: case ElementType.Poison: return "Earth";
                case ElementType.Lightning: return "Thunder";
                case ElementType.Air: return "Wind";
                case ElementType.Chaos: return "Chaos";
                default: return null;
            }
        }
    }

    internal static class RecolorEngine
    {
        // Recoloured twin of each (sprite, ramp), and the sprites we made (so a twin is never recoloured again).
        static readonly Dictionary<string, Sprite> twins = new Dictionary<string, Sprite>();
        static readonly HashSet<int> made = new HashSet<int>();
        static readonly HashSet<int> failed = new HashSet<int>();

        // The source atlas's pixels, read once (GPU blit + ReadPixels: sprite textures aren't readable). Only the last
        // couple are kept, they can be big.
        static readonly List<KeyValuePair<int, Color32[]>> atlasCache = new List<KeyValuePair<int, Color32[]>>();
        static int loggedTwins;

        static Color32[] AtlasPixels(Texture2D tex)
        {
            int id = tex.GetInstanceID();
            foreach (KeyValuePair<int, Color32[]> kv in atlasCache) if (kv.Key == id) return kv.Value;
            int w = tex.width, h = tex.height;
            RenderTexture rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
            Graphics.Blit(tex, rt);
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            t.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            Color32[] px = t.GetPixels32();
            UnityEngine.Object.Destroy(t);
            atlasCache.Add(new KeyValuePair<int, Color32[]>(id, px));
            if (atlasCache.Count > 2) atlasCache.RemoveAt(0);
            return px;
        }

        internal static bool IsTwin(Sprite s) { return s != null && made.Contains(s.GetInstanceID()); }

        // The ramp a twin was made with (null if it isn't one of ours).
        internal static string KeyOf(Sprite s)
        {
            if (!IsTwin(s)) return null;
            string n = s.name;
            int open = n.LastIndexOf(" ("), close = n.LastIndexOf(')');
            return open >= 0 && close > open ? n.Substring(open + 2, close - open - 2) : null;
        }

        // The recoloured twin of a sprite (null if it can't be made: tightly packed, unreadable...).
        internal static Sprite Twin(Sprite src, string key)
        {
            if (src == null || !ElementRamps.Has(key)) return null;
            int sid = src.GetInstanceID();
            if (made.Contains(sid)) return src;
            if (failed.Contains(sid)) return null;
            string k = sid + "|" + key;
            Sprite tw;
            if (twins.TryGetValue(k, out tw)) return tw;
            // v0.7.1: tightly packed atlas sprites (most effect animations) have no textureRect: twin them from a
            // recoloured copy of the whole atlas with the sprite's own mesh (OverrideGeometry).
            bool tight = false;
            try { Rect probe = src.textureRect; } catch (Exception) { tight = true; }
            if (tight)
            {
                tw = TightTwin(src, key);
                if (tw == null) { failed.Add(sid); return null; }
                made.Add(tw.GetInstanceID());
                twins[k] = tw;
                if (loggedTwins++ < 8) Plugin.Log.LogInfo("RECOLOR twin (tight) " + tw.name);
                return tw;
            }
            try
            {
                Texture2D tex = src.texture;
                if (tex == null) { failed.Add(sid); return null; }
                Rect r = src.textureRect;
                int x0 = Mathf.RoundToInt(r.x), y0 = Mathf.RoundToInt(r.y), rw = Mathf.RoundToInt(r.width), rh = Mathf.RoundToInt(r.height);
                if (rw <= 0 || rh <= 0) { failed.Add(sid); return null; }
                Color32[] atlas = AtlasPixels(tex);
                Color32[] px = new Color32[rw * rh];
                Dictionary<int, Color32> map = new Dictionary<int, Color32>();
                for (int y = 0; y < rh; y++)
                    for (int x = 0; x < rw; x++)
                    {
                        int ai = (y0 + y) * tex.width + (x0 + x);
                        if (ai < 0 || ai >= atlas.Length) continue;
                        Color32 c = atlas[ai];
                        if (c.a == 0) { px[y * rw + x] = c; continue; }
                        int ck = (c.r << 24) | (c.g << 16) | (c.b << 8) | c.a;
                        Color32 o;
                        if (!map.TryGetValue(ck, out o)) { o = ElementRamps.Map(key, c); map[ck] = o; }
                        px[y * rw + x] = o;
                    }
                Texture2D outTex = new Texture2D(rw, rh, TextureFormat.RGBA32, false);
                outTex.SetPixels32(px);
                outTex.filterMode = tex.filterMode;
                outTex.wrapMode = TextureWrapMode.Clamp;
                outTex.Apply(false);
                outTex.name = src.name + " (" + key + ")";
                Vector2 pivotPx = src.pivot - src.textureRectOffset;
                Vector2 pivot = new Vector2(pivotPx.x / rw, pivotPx.y / rh);
                tw = Sprite.Create(outTex, new Rect(0, 0, rw, rh), pivot, src.pixelsPerUnit, 0, SpriteMeshType.FullRect);
                tw.name = src.name + " (" + key + ")";
                made.Add(tw.GetInstanceID());
                twins[k] = tw;
                if (loggedTwins++ < 5) Plugin.Log.LogInfo("RECOLOR twin " + tw.name + " " + rw + "x" + rh);
                return tw;
            }
            catch (Exception)
            {
                failed.Add(sid);
                return null;
            }
        }

        // A whole atlas recoloured onto a ramp, cached per (atlas, ramp).
        static readonly Dictionary<string, Texture2D> atlasTwins = new Dictionary<string, Texture2D>();

        static Texture2D AtlasTwin(Texture2D tex, string key)
        {
            string k = tex.GetInstanceID() + "|" + key;
            Texture2D t;
            if (atlasTwins.TryGetValue(k, out t) && t != null) return t;
            Color32[] src = AtlasPixels(tex);
            Color32[] px = new Color32[src.Length];
            Dictionary<int, Color32> map = new Dictionary<int, Color32>();
            for (int i = 0; i < src.Length; i++)
            {
                Color32 c = src[i];
                if (c.a == 0) { px[i] = c; continue; }
                int ck = (c.r << 24) | (c.g << 16) | (c.b << 8) | c.a;
                Color32 o;
                if (!map.TryGetValue(ck, out o)) { o = ElementRamps.Map(key, c); map[ck] = o; }
                px[i] = o;
            }
            t = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
            t.SetPixels32(px);
            t.filterMode = tex.filterMode;
            t.wrapMode = TextureWrapMode.Clamp;
            t.Apply(false);
            t.name = tex.name + " (" + key + ")";
            atlasTwins[k] = t;
            Plugin.Log.LogInfo("RECOLOR atlas " + tex.name + " " + tex.width + "x" + tex.height + " -> " + key);
            return t;
        }

        // Tightly packed sprite: a sprite on the recoloured atlas covering the original's UV box, with the original
        // mesh (vertices moved into that box's pixel space), and the same pivot and pixels-per-unit.
        static Sprite TightTwin(Sprite src, string key)
        {
            try
            {
                Texture2D tex = src.texture;
                if (tex == null) return null;
                Vector2[] uv = src.uv, verts = src.vertices;
                ushort[] tris = src.triangles;
                if (uv == null || verts == null || uv.Length == 0 || uv.Length != verts.Length) return null;
                float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
                foreach (Vector2 u in uv) { minX = Mathf.Min(minX, u.x); minY = Mathf.Min(minY, u.y); maxX = Mathf.Max(maxX, u.x); maxY = Mathf.Max(maxY, u.y); }
                Rect box = new Rect(Mathf.Floor(minX * tex.width), Mathf.Floor(minY * tex.height), 0f, 0f);
                box.width = Mathf.Max(1f, Mathf.Ceil(maxX * tex.width) - box.x);
                box.height = Mathf.Max(1f, Mathf.Ceil(maxY * tex.height) - box.y);
                float ppu = src.pixelsPerUnit;
                // Each vertex in the box's pixel space; the pivot from vertex 0 (local units = (pixel - pivot) / ppu).
                Vector2[] px = new Vector2[uv.Length];
                for (int i = 0; i < uv.Length; i++)
                    px[i] = new Vector2(Mathf.Clamp(uv[i].x * tex.width - box.x, 0f, box.width), Mathf.Clamp(uv[i].y * tex.height - box.y, 0f, box.height));
                Vector2 pivotPx = px[0] - verts[0] * ppu;
                Vector2 pivot = new Vector2(pivotPx.x / box.width, pivotPx.y / box.height);
                Sprite tw = Sprite.Create(AtlasTwin(tex, key), box, pivot, ppu, 0, SpriteMeshType.Tight);
                tw.OverrideGeometry(px, tris);
                tw.name = src.name + " (" + key + ")";
                return tw;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning("RECOLOR tight twin failed for " + src.name + ": " + ex.Message);
                return null;
            }
        }

        // Sprites drawn by the game's palette shader carry colour indices, not colours: never recolour those.
        internal static bool PaletteSprite(SpriteRenderer sr)
        {
            Material m = sr != null ? sr.sharedMaterial : null;
            return m != null && m.HasProperty("_Palette");
        }

        // A particle colour setting mapped onto the ramp (constant, two colours, gradient, two gradients).
        internal static ParticleSystem.MinMaxGradient Map(ParticleSystem.MinMaxGradient g, string key)
        {
            switch (g.mode)
            {
                case ParticleSystemGradientMode.TwoColors:
                    return new ParticleSystem.MinMaxGradient(ElementRamps.Map(key, g.colorMin), ElementRamps.Map(key, g.colorMax));
                case ParticleSystemGradientMode.Gradient:
                    return new ParticleSystem.MinMaxGradient(Map(g.gradient, key));
                case ParticleSystemGradientMode.TwoGradients:
                    return new ParticleSystem.MinMaxGradient(Map(g.gradientMin, key), Map(g.gradientMax, key));
                default:
                    return new ParticleSystem.MinMaxGradient(ElementRamps.Map(key, g.color));
            }
        }

        static Gradient Map(Gradient src, string key)
        {
            if (src == null) return null;
            Gradient g = new Gradient();
            GradientColorKey[] ck = src.colorKeys;
            for (int i = 0; i < ck.Length; i++) ck[i].color = ElementRamps.Map(key, ck[i].color);
            g.SetKeys(ck, src.alphaKeys);
            return g;
        }
    }

    // On an attack's visual: swaps every frame the Animator sets for its recoloured twin (after the animation, in
    // LateUpdate). Sprites that can't be twinned are tinted toward the ramp instead. Removes itself (restoring) when
    // the attack is set up again for something else (pooled objects) or when the run's tints are restored.
    internal class RecolorSprites : MonoBehaviour
    {
        public string Key;
        public Attack Source;
        AttackInfo info;
        readonly Dictionary<SpriteRenderer, Sprite> lastSet = new Dictionary<SpriteRenderer, Sprite>();
        readonly Dictionary<SpriteRenderer, Color> tinted = new Dictionary<SpriteRenderer, Color>();
        readonly Dictionary<SpriteRenderer, Sprite> original = new Dictionary<SpriteRenderer, Sprite>();
        static readonly List<RecolorSprites> live = new List<RecolorSprites>();

        internal static RecolorSprites Attach(GameObject root, string key, Attack source)
        {
            if (root == null || !ElementRamps.Has(key)) return null;
            RecolorSprites rs = root.GetComponent<RecolorSprites>();
            if (rs != null && rs.Key != key) { rs.Restore(); Destroy(rs); rs = null; }
            if (rs == null) rs = root.AddComponent<RecolorSprites>();
            rs.Key = key;
            rs.Source = source;
            rs.info = source != null ? source.atkInfo : null;
            rs.Apply();
            return rs;
        }

        internal static void RestoreAll()
        {
            foreach (RecolorSprites rs in live.ToArray()) if (rs != null) { rs.Restore(); Destroy(rs); }
            live.Clear();
        }

        void OnEnable() { if (!live.Contains(this)) live.Add(this); }
        void OnDestroy() { live.Remove(this); }

        void LateUpdate()
        {
            // Pooled: set up again for another owner / cast -> not ours any more.
            if (Source != null && Source.atkInfo != info) { Restore(); Destroy(this); return; }
            Apply();
        }

        void Apply()
        {
            foreach (SpriteRenderer sr in GetComponentsInChildren<SpriteRenderer>(true))
            {
                // v0.7.1: only bodies *inside* this visual are skipped (an effect parented to the player is still an
                // effect: checking every parent up the scene skipped all of those).
                if (sr == null || sr.sprite == null || BodyWithin(sr.transform)) continue;
                Sprite cur = sr.sprite;
                Sprite done;
                if (lastSet.TryGetValue(sr, out done) && done == cur) continue; // our twin still on
                if (RecolorEngine.PaletteSprite(sr) || RecolorEngine.IsTwin(cur)) { lastSet[sr] = cur; continue; }
                Sprite twin = RecolorEngine.Twin(cur, Key);
                if (twin != null) { original[sr] = cur; sr.sprite = twin; lastSet[sr] = twin; }
                else if (!tinted.ContainsKey(sr))
                {
                    tinted[sr] = sr.color;
                    Color m = ElementRamps.Mid(Key);
                    sr.color = new Color(sr.color.r * m.r, sr.color.g * m.g, sr.color.b * m.b, sr.color.a);
                    lastSet[sr] = cur;
                }
                else lastSet[sr] = cur;
            }
        }

        bool BodyWithin(Transform t)
        {
            for (; t != null; t = t.parent)
            {
                if (t.GetComponent<Entity>() != null) return true;
                if (t == transform) return false;
            }
            return false;
        }

        void Restore()
        {
            foreach (KeyValuePair<SpriteRenderer, Color> kv in tinted) if (kv.Key != null) kv.Key.color = kv.Value;
            // Static sprites get their original back (animated ones get the Animator's next frame anyway).
            foreach (KeyValuePair<SpriteRenderer, Sprite> kv in original)
                if (kv.Key != null && RecolorEngine.IsTwin(kv.Key.sprite)) kv.Key.sprite = kv.Value;
            tinted.Clear();
            original.Clear();
            lastSet.Clear();
        }
    }
}
