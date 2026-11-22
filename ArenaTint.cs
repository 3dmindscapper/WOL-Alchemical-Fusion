using System;
using System.Collections.Generic;
using UnityEngine;

namespace WolHybridBosses
{
    // v1.1.1: on champion runs the whole final level (the castle walk-up, the throne room, the cosmic arena) takes
    // the champion's colours: pure white for the Primordial Alchemist, the element's ramp for an Archon (Prism: its
    // pink light only). Applied when the level loads (FinalChampion.Roll) and again when the champion appears, for
    // whatever turned on since (lit torches, the arena).
    //   Scenery sprites: palette-shader ones get a ramp-mapped palette, the rest a recoloured twin sprite (one pass:
    //     scenery doesn't animate, unlike bodies, so no per-frame component), or a tint if no twin can be made.
    //   Meshes (floors, walls): a ramp-mapped copy of their texture (strong colours follow the ramp, grey stone only
    //     a little).
    //   Lights: shifted to the champion's colour once; torch lights (reset by the game when lit) every pass.
    internal static class ArenaTint
    {
        static readonly HashSet<int> done = new HashSet<int>();
        static int sceneHandle = -1;

        static bool Skip(Component c)
        {
            if (c == null) return true;
            if (c.transform.root.name.StartsWith("HybridBosses")) return true;
            if (c.GetComponentInParent<Entity>() != null) return true;  // players, enemies, the champion, Sura
            if (c.GetComponentInParent<Canvas>() != null) return true;  // UI
            // v1.1.1: NPCs (merchants, Tomi, Nocturne...), their wares, pickups, chests and portals keep their colours;
            // only the castle itself is recoloured.
            for (Transform t = c.transform; t != null; t = t.parent)
            {
                bool known;
                if (keepCache.TryGetValue(t.GetInstanceID(), out known)) { if (known) return true; continue; }
                bool keep = false;
                foreach (MonoBehaviour mb in t.GetComponents<MonoBehaviour>())
                {
                    if (mb == null) continue;
                    string n = mb.GetType().Name;
                    if (mb is Npc || n.Contains("Npc") || n.Contains("NPC") || n.Contains("Store") || n.Contains("Shop") || n.Contains("Merchant")
                        || n.Contains("Item") || n.Contains("Pickup") || n.Contains("Loot") || n.Contains("Chest") || n.Contains("Portal")
                        || n.Contains("Pedestal") || n.Contains("Price") || n.Contains("Gold") || n.Contains("Gem") || n.Contains("Interact")
                        || n.Contains("Prompt") || n.Contains("Emote")) { keep = true; break; }
                }
                keepCache[t.GetInstanceID()] = keep;
                if (keep) return true;
            }
            return false;
        }

        static readonly Dictionary<int, bool> keepCache = new Dictionary<int, bool>();

        static bool Tagged(UnityEngine.Object t, string key) { return t != null && t.name.EndsWith("(" + key + ")"); }

        internal static void Apply(string key, Color colour, bool bright)
        {
            UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.GetHashCode() != sceneHandle) { sceneHandle = scene.GetHashCode(); done.Clear(); keepCache.Clear(); } // a new level: new objects
            int sprites = 0, palettes = 0, meshes = 0, lights = 0;
            float t0 = Time.realtimeSinceStartup;
            try
            {
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    if (root == null || root.name.StartsWith("HybridBosses")) continue;
                    if (key != null)
                    {
                        foreach (SpriteRenderer sr in root.GetComponentsInChildren<SpriteRenderer>(true))
                        {
                            if (sr == null || done.Contains(sr.GetInstanceID()) || Skip(sr)) continue;
                            done.Add(sr.GetInstanceID());
                            if (RecolorEngine.PaletteSprite(sr))
                            {
                                Material m = sr.material;
                                Texture pal = m.GetTexture("_Palette");
                                if (pal == null || Tagged(pal, key)) continue;
                                Texture2D tw = RecolorEngine.PaletteTwin(pal, key);
                                if (tw != null) { m.SetTexture("_Palette", tw); palettes++; }
                                continue;
                            }
                            if (sr.sprite == null || RecolorEngine.IsTwin(sr.sprite)) continue;
                            Sprite twin = RecolorEngine.Twin(sr.sprite, key);
                            if (twin != null) sr.sprite = twin;
                            else { Color mid = ElementRamps.Mid(key); sr.color = new Color(sr.color.r * mid.r, sr.color.g * mid.g, sr.color.b * mid.b, sr.color.a); }
                            sprites++;
                        }
                        foreach (MeshRenderer mr in root.GetComponentsInChildren<MeshRenderer>(true))
                        {
                            if (mr == null || done.Contains(mr.GetInstanceID()) || Skip(mr)) continue;
                            done.Add(mr.GetInstanceID());
                            foreach (Material m in mr.materials)
                            {
                                if (m == null || !m.HasProperty("_MainTex") || m.mainTexture == null || Tagged(m.mainTexture, key)) continue;
                                Texture2D tw = RecolorEngine.PaletteTwin(m.mainTexture, key);
                                if (tw != null) { m.mainTexture = tw; meshes++; }
                            }
                        }
                    }
                    foreach (Light l in root.GetComponentsInChildren<Light>(true))
                    {
                        if (l == null || Skip(l)) continue;
                        bool torch = l.GetComponentInParent<Torch>() != null;
                        if (!torch && done.Contains(l.GetInstanceID())) continue;
                        done.Add(l.GetInstanceID());
                        l.color = bright ? Color.Lerp(l.color, Color.white, 0.8f) : Color.Lerp(l.color, colour, 0.65f);
                        if (bright && !torch) l.intensity *= 1.15f;
                        lights++;
                    }
                }
                Camera cam = Camera.main;
                if (cam != null && !done.Contains(cam.GetInstanceID())) { done.Add(cam.GetInstanceID()); cam.backgroundColor = Color.Lerp(cam.backgroundColor, colour * 0.25f, 0.5f); }
            }
            catch (Exception ex) { Plugin.Log.LogWarning("ARENA tint failed: " + ex.Message); }
            Plugin.Log.LogInfo("ARENA tint " + (key ?? "light only") + ": " + sprites + " sprites, " + palettes + " palettes, " + meshes + " mesh materials, " + lights + " lights ("
                + Mathf.RoundToInt((Time.realtimeSinceStartup - t0) * 1000f) + " ms)");
        }

        internal static void ApplyFor(string prefix, bool alchemist)
        {
            Apply(alchemist ? "Pure" : ArchonLook.RampForPrefix(prefix), alchemist ? WhiteAlchemist.White : ElementalGiants.ElementColor(prefix), alchemist);
        }
    }
}
