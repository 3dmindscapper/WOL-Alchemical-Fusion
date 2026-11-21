using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace WolHybridBosses
{
    // Chaos stages: Chaos versions of the normal stages.
    //   - Look: purple-tinted tiles (the tileset atlas recoloured into a cloned material, code only), decorations and lights.
    //   - Rules: enemies cast Chaos-element player arcana; the stage's rival arena (if any) features a Chaos rival.
    //   - Rewards: more gold and more item drops while on the stage.
    //   - Shown: "Chaos <stage>" title, and Chaos stages are marked on the loading-screen board.
    // Which stages are Chaos is derived from the run seed + tier + stage (ChaosRun), so the board and the stage agree.
    // Shift+F11 toggles Chaos on the current stage for testing.
    internal class ChaosVariant
    {
        public string Name;
        public float HueShift, Saturation, Value;
        public Color SpriteTint, LightTint;

        public ChaosVariant(string name, float hue, float sat, float val, Color sprite, Color light)
        {
            Name = name; HueShift = hue; Saturation = sat; Value = val; SpriteTint = sprite; LightTint = light;
        }
    }

    internal static class ChaosRun
    {
        static readonly Color Purple = new Color(0.62f, 0.32f, 1f);
        internal static Color Color { get { return Purple; } }

        // Regular floors only: the last stage of each tier (the boss floor) and non-run scenes stay normal.
        internal static bool IsChaos(int tier, int stage)
        {
            float chance = Plugin.ChaosStageChance.Value;
            if (chance <= 0f) return false;
            int per = GameController.stagesPerTier;
            if (per > 0 && stage >= per - 1) return false;
            unchecked
            {
                int h = FinalRun.Seed;
                h = h * 31 + tier * 7919;
                h = h * 31 + stage * 104729;
                h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
                return (h & 0x7fffffff) % 10000 < chance * 10000f;
            }
        }

        internal static bool IsCurrentStageChaos()
        {
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name ?? "";
            if (scene == "Hub" || scene == "Tutorial" || scene.Contains("PvP")) return false;
            if (Level.isBossRushMode) return false;
            if (UnityEngine.Object.FindObjectOfType<FinalBossRoomEventHandler>() != null) return false;
            FinalRun.Tick();
            return IsChaos(GameController.tierCount, GameController.stageCount);
        }
    }

    internal static class ChaosStages
    {
        internal static readonly ChaosVariant Chaos =
            new ChaosVariant("Chaos", 0.72f, 0.85f, 0.80f, new Color(0.75f, 0.60f, 1.00f), new Color(0.55f, 0.35f, 1.00f));

        // Decided when the stage is generated (before the misc room / rival arena is set up), and only valid for
        // that stage (so the Hub or a boss room after a Chaos stage isn't Chaos).
        static bool planned;
        static string plannedKey;
        internal static bool Planned { get { return planned && plannedKey == StageKey(); } }
        internal static ChaosVariant Active { get; private set; }

        internal static string StageKey() { return Level.tileSetName + "|" + GameController.tierCount + "|" + GameController.stageCount; }

        static readonly Dictionary<Renderer, Material[]> origMaterials = new Dictionary<Renderer, Material[]>();
        static readonly Dictionary<SpriteRenderer, Color> origSpriteColors = new Dictionary<SpriteRenderer, Color>();
        static readonly Dictionary<Light, Color> origLights = new Dictionary<Light, Color>();
        static readonly Dictionary<string, Material> materialCache = new Dictionary<string, Material>();
        static readonly Dictionary<string, Texture2D> textureCache = new Dictionary<string, Texture2D>();
        static bool rewardMods;

        internal static void PlanCurrentStage()
        {
            planned = ChaosRun.IsCurrentStageChaos();
            plannedKey = StageKey();
            if (planned) Plugin.Log.LogInfo("CHAOS stage planned: tier " + GameController.tierCount + " stage " + GameController.stageCount + " (" + Level.tileSetName + ")");
        }

        // Shift+F11 test toggle.
        internal static void ToggleOnCurrentStage()
        {
            if (Active != null) { Revert(); planned = false; Plugin.Log.LogInfo("CHAOS stage: off (test)"); }
            else { planned = true; plannedKey = StageKey(); Apply(); }
        }

        internal static void Apply()
        {
            Revert();
            ChaosVariant v = Chaos;
            Active = v;
            float t0 = Time.realtimeSinceStartup;
            int meshes = 0, sprites = 0, lights = 0;

            foreach (MeshRenderer mr in UnityEngine.Object.FindObjectsOfType<MeshRenderer>())
            {
                if (!IsStageMesh(mr)) continue;
                Material[] mats = mr.sharedMaterials;
                Material[] swapped = new Material[mats.Length];
                bool any = false;
                for (int k = 0; k < mats.Length; k++) { swapped[k] = Recolored(mats[k], v); any |= swapped[k] != mats[k]; }
                if (!any) continue;
                origMaterials[mr] = mats;
                mr.sharedMaterials = swapped;
                meshes++;
            }

            Transform level = Level.levelContainer;
            if (level != null)
                foreach (SpriteRenderer sr in level.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (sr == null || sr.GetComponentInParent<Entity>() != null) continue;
                    origSpriteColors[sr] = sr.color;
                    sr.color = new Color(sr.color.r * v.SpriteTint.r, sr.color.g * v.SpriteTint.g, sr.color.b * v.SpriteTint.b, sr.color.a);
                    sprites++;
                }

            foreach (Light l in UnityEngine.Object.FindObjectsOfType<Light>())
            {
                origLights[l] = l.color;
                l.color = Color.Lerp(l.color, v.LightTint, 0.6f);
                lights++;
            }

            AddRewards();
            int armed = ChaosEnemies.ArmAll();

            Plugin.Log.LogInfo("CHAOS stage on " + Level.tileSetName + ": " + meshes + " tile meshes, " + sprites + " decorations, " + lights + " lights, "
                + armed + " giants turned Chaos, " + Mathf.RoundToInt((Time.realtimeSinceStartup - t0) * 1000f) + " ms");
        }

        internal static void Revert()
        {
            foreach (KeyValuePair<Renderer, Material[]> kv in origMaterials) if (kv.Key != null) kv.Key.sharedMaterials = kv.Value;
            foreach (KeyValuePair<SpriteRenderer, Color> kv in origSpriteColors) if (kv.Key != null) kv.Key.color = kv.Value;
            foreach (KeyValuePair<Light, Color> kv in origLights) if (kv.Key != null) kv.Key.color = kv.Value;
            origMaterials.Clear(); origSpriteColors.Clear(); origLights.Clear();
            RemoveRewards();
            Active = null;
        }

        // ---------- rewards: more gold and item drops while on a Chaos stage ----------

        static void AddRewards()
        {
            if (rewardMods) return;
            rewardMods = true;
            float m = Plugin.ChaosStageRewards.Value;
            try
            {
                LootManager.globalGoldModifier.AddMod(new NumVarStatMod("HB_ChaosGold", m, 10, VarStatModType.Multiplicative, false));
                LootManager.itemDropModifier.AddMod(new NumVarStatMod("HB_ChaosDrops", m, 10, VarStatModType.Multiplicative, false));
            }
            catch (Exception ex) { Plugin.Log.LogWarning("CHAOS rewards failed: " + ex.Message); }
        }

        static void RemoveRewards()
        {
            if (!rewardMods) return;
            rewardMods = false;
            try
            {
                LootManager.globalGoldModifier.RemoveMod("HB_ChaosGold");
                LootManager.itemDropModifier.RemoveMod("HB_ChaosDrops");
            }
            catch (Exception) { }
        }

        // ---------- tile recolour ----------

        static bool IsStageMesh(MeshRenderer mr)
        {
            for (Transform t = mr.transform; t != null; t = t.parent) if (t.name == "Meshes") return true;
            return false;
        }

        static Material Recolored(Material m, ChaosVariant v)
        {
            if (m == null || m.mainTexture == null) return m;
            string key = m.GetInstanceID() + "|" + v.Name;
            Material cached;
            if (materialCache.TryGetValue(key, out cached) && cached != null) return cached;
            Texture2D tex = RecolorTexture(m.mainTexture, v);
            if (tex == null) return m;
            Material copy = new Material(m) { name = m.name + " (Chaos)", mainTexture = tex };
            materialCache[key] = copy;
            return copy;
        }

        static Texture2D RecolorTexture(Texture src, ChaosVariant v)
        {
            string key = src.GetInstanceID() + "|" + v.Name;
            Texture2D done;
            if (textureCache.TryGetValue(key, out done) && done != null) return done;
            try
            {
                int w = src.width, h = src.height;
                RenderTexture rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
                Graphics.Blit(src, rt);
                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = rt;
                Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, false);
                t.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);

                Color32[] px = t.GetPixels32();
                Dictionary<int, Color32> map = new Dictionary<int, Color32>();
                for (int i = 0; i < px.Length; i++)
                {
                    Color32 c = px[i];
                    if (c.a == 0) continue;
                    int k = (c.r << 24) | (c.g << 16) | (c.b << 8) | c.a;
                    Color32 r;
                    if (!map.TryGetValue(k, out r)) { r = Shift(c, v); map[k] = r; }
                    px[i] = r;
                }
                t.SetPixels32(px);
                t.filterMode = src.filterMode;
                t.wrapMode = src.wrapMode;
                t.Apply(false);
                t.name = src.name + " (Chaos)";
                textureCache[key] = t;
                return t;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning("CHAOS could not recolour " + src.name + ": " + ex.Message);
                return null;
            }
        }

        static Color32 Shift(Color32 c, ChaosVariant v)
        {
            float h, s, val;
            Color.RGBToHSV(c, out h, out s, out val);
            h = Mathf.Repeat(h + v.HueShift, 1f);
            s = Mathf.Clamp01(s * v.Saturation);
            val = Mathf.Clamp01(val * v.Value);
            Color o = Color.HSVToRGB(h, s, val);
            return new Color32((byte)(o.r * 255f), (byte)(o.g * 255f), (byte)(o.b * 255f), c.a);
        }
    }

    // On a Chaos stage only giants cast Chaos arcana (regular enemies keep their normal kit). Giants normally become
    // Chaos giants through ElementalGiants (ChaosGiants.Apply); this catches giants spawned once the stage is active
    // or when elemental giants are turned off.
    internal static class ChaosEnemies
    {
        internal static int ArmAll()
        {
            int n = 0;
            foreach (Enemy e in UnityEngine.Object.FindObjectsOfType<Enemy>()) if (Arm(e)) n++;
            return n;
        }

        internal static bool Arm(Enemy e)
        {
            if (e == null || ChaosStages.Active == null || !(e is MiniBoss) || e.GetComponent<ChaosArmed>() != null) return false;
            ChaosGiants.Apply(e);
            return true;
        }

        internal static List<ArcanaDef> Pool()
        {
            List<ArcanaDef> pool = ArcanaCatalog.PoolExact("Chaos");
            pool.RemoveAll(d => d.Family == ArcanaFamily.Minion || d.Family == ArcanaFamily.Seeker || d.Family == ArcanaFamily.Ward);
            return pool;
        }
    }

    // Chaos giants: Chaos element, the giant's own purple palette row (found by reading its palette texture), or a
    // purple tint when its sheet has none (e.g. the Coffin), and 2 Chaos arcana. No borrowed elemental skills.
    internal static class ChaosGiants
    {
        internal static void Apply(Enemy e)
        {
            if (e == null || e.GetComponent<ChaosArmed>() != null) return;
            e.gameObject.AddComponent<ChaosArmed>();
            e.element = ElementType.Chaos;

            int idx = Plugin.ChaosGiantPalette.Value >= 0 ? Plugin.ChaosGiantPalette.Value : PurpleRow(e);
            if (idx >= 0 && !ElementalGiants.NoElementPalettes(e)) ElementalGiants.SetPaletteIndexAll(e, idx);
            else { ElementalGiants.Tint(e, ChaosRun.Color); idx = -1; }

            int arcana = 0;
            List<ArcanaDef> pool = ChaosEnemies.Pool();
            if (pool.Count > 0) arcana = EnemyArcana.Give(e, Rivals.PickVaried(pool, 2, false));
            if (Plugin.GiantIntros.Value) GiantIntro.Attach(e, "Chaos", "");
            Plugin.Log.LogInfo("GIANT " + e.GetType().Name + " -> Chaos giant, palette " + (idx < 0 ? "purple tint" : idx.ToString()) + ", chaos arcana " + arcana);
        }

        // Palette texture per material: which row is purple, cached by texture.
        static readonly Dictionary<int, int> purpleByTexture = new Dictionary<int, int>();

        // Reads the giant's palette texture (_Palette, one row per _PaletteIndex), works out the row order from rows
        // whose colour is known (Water bluish, Fire reddish), and returns the most purple row, or -1 if none is purple.
        internal static int PurpleRow(Enemy e)
        {
            SpriteRenderer sr = e.spriteRenderer;
            Material m = sr != null ? sr.sharedMaterial : null;
            if (m == null || !m.HasProperty("_Palette") || !m.HasProperty("_PaletteCount")) return -1;
            Texture pal = m.GetTexture("_Palette");
            if (pal == null) return -1;
            int cached;
            if (purpleByTexture.TryGetValue(pal.GetInstanceID(), out cached)) return cached;

            int result = -1;
            try
            {
                int count = Mathf.RoundToInt(m.GetFloat("_PaletteCount"));
                Color32[] px; int w, h;
                Read(pal, out px, out w, out h);
                bool rowsAlongY = h == count, colsAlongX = w == count && !rowsAlongY;
                if (!rowsAlongY && !colsAlongX) { purpleByTexture[pal.GetInstanceID()] = -1; return -1; }

                // Average hue/saturation per row (raw texture order).
                float[] hue = new float[count], sat = new float[count];
                for (int r = 0; r < count; r++) RowStats(px, w, h, r, rowsAlongY, out hue[r], out sat[r]);

                // Map palette index -> raw row: try both orders, keep the one where Water is bluish and Fire reddish.
                IDictionary table = e.elementToPalette as IDictionary;
                int water = table != null && table.Contains(ElementType.Water) ? (int)table[ElementType.Water] : 4;
                int fire = table != null && table.Contains(ElementType.Fire) ? (int)table[ElementType.Fire] : 5;
                bool flipped = Score(hue, sat, count, water, fire, true) > Score(hue, sat, count, water, fire, false);

                float best = 0f;
                HashSet<int> elemental = new HashSet<int>();
                if (table != null) foreach (object v in table.Values) elemental.Add((int)v);
                for (int i = 0; i < count; i++)
                {
                    if (elemental.Contains(i)) continue; // keep element rows for elements
                    int raw = flipped ? count - 1 - i : i;
                    float p = Purpleness(hue[raw], sat[raw]);
                    if (p > best) { best = p; result = i; }
                }
                if (best < 0.25f) result = -1;
                Plugin.Log.LogInfo("CHAOS palette for " + e.GetType().Name + ": " + (result < 0 ? "no purple row, tint instead" : "row " + result)
                    + " (" + w + "x" + h + ", " + (flipped ? "flipped" : "direct") + " order, score " + best.ToString("0.00") + ")");
            }
            catch (Exception ex) { Plugin.Log.LogWarning("CHAOS palette read failed: " + ex.Message); result = -1; }
            purpleByTexture[pal.GetInstanceID()] = result;
            return result;
        }

        static void Read(Texture src, out Color32[] px, out int w, out int h)
        {
            w = src.width; h = src.height;
            RenderTexture rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
            Graphics.Blit(src, rt);
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            t.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            px = t.GetPixels32();
            UnityEngine.Object.Destroy(t);
        }

        // Circular mean hue (weighted by saturation) and mean saturation of one palette row.
        static void RowStats(Color32[] px, int w, int h, int row, bool alongY, out float hue, out float sat)
        {
            float x = 0f, y = 0f, s = 0f; int n = 0;
            int len = alongY ? w : h;
            for (int k = 0; k < len; k++)
            {
                Color32 c = alongY ? px[row * w + k] : px[k * w + row];
                if (c.a == 0) continue;
                float hh, ss, vv;
                Color.RGBToHSV(c, out hh, out ss, out vv);
                if (vv < 0.15f) continue;
                x += Mathf.Cos(hh * 2f * Mathf.PI) * ss; y += Mathf.Sin(hh * 2f * Mathf.PI) * ss; s += ss; n++;
            }
            hue = Mathf.Repeat(Mathf.Atan2(y, x) / (2f * Mathf.PI), 1f);
            sat = n > 0 ? s / n : 0f;
        }

        static float HueDistance(float a, float b) { float d = Mathf.Abs(a - b); return Mathf.Min(d, 1f - d); }

        static float Purpleness(float hue, float sat)
        {
            float d = HueDistance(hue, 0.78f); // violet/purple
            return Mathf.Clamp01(1f - d / 0.12f) * Mathf.Clamp01(sat / 0.35f);
        }

        // How well an order fits: Water row near blue (0.58), Fire row near red/orange (0.03).
        static float Score(float[] hue, float[] sat, int count, int water, int fire, bool flipped)
        {
            int rw = flipped ? count - 1 - water : water, rf = flipped ? count - 1 - fire : fire;
            if (rw < 0 || rw >= count || rf < 0 || rf >= count) return 0f;
            return (1f - HueDistance(hue[rw], 0.58f)) + (1f - HueDistance(hue[rf], 0.03f));
        }
    }

    internal class ChaosArmed : MonoBehaviour { }

    // Giants spawned later on a Chaos stage become Chaos giants through the giant Start hook (GiantStartPatch ->
    // ElementalGiants.TryMakeElemental), which runs after MiniBoss.Start has set its gray palette. An Enemy.Start
    // hook would run too early (inside MiniBoss.Start) and be overwritten, so there is none.

    // "Chaos <stage>" title on Chaos stages.
    [HarmonyPatch(typeof(GameController), "get_StageTitle")]
    static class ChaosStageTitlePatch
    {
        static void Postfix(ref string __result)
        {
            if (ChaosStages.Planned && !string.IsNullOrEmpty(__result) && !__result.StartsWith("Chaos ")) __result = "Chaos " + __result;
        }
    }

    // Loading-screen board: Chaos stages' markers are tinted purple.
    [HarmonyPatch(typeof(GameProgressBoard), "Enable")]
    static class ChaosBoardPatch
    {
        static readonly Dictionary<Image, Color> tinted = new Dictionary<Image, Color>();

        static void Postfix(GameProgressBoard __instance)
        {
            try
            {
                foreach (KeyValuePair<Image, Color> kv in tinted) if (kv.Key != null) kv.Key.color = kv.Value;
                tinted.Clear();
                FinalRun.Tick();
                Traverse tr = Traverse.Create(__instance);
                RectTransform[] stages = tr.Field("stagesInOrder").GetValue() as RectTransform[];
                Image[] dimmers = tr.Field("dimmersInOrder").GetValue() as Image[];
                int per = tr.Field("curMaxStagesPerTier").GetValue<int>();
                int final = tr.Field("curFinalStageIndex").GetValue<int>();
                if (stages == null || per <= 0) return;
                HashSet<Image> skip = new HashSet<Image>(dimmers ?? new Image[0]);
                int marked = 0;
                for (int i = 0; i < stages.Length && i < final; i++)
                {
                    if (stages[i] == null || !ChaosRun.IsChaos(i / per, i % per)) continue;
                    foreach (Image img in stages[i].GetComponentsInChildren<Image>(true))
                    {
                        if (skip.Contains(img)) continue;
                        tinted[img] = img.color;
                        img.color = Color.Lerp(img.color, ChaosRun.Color, 0.75f);
                    }
                    marked++;
                }
                if (marked > 0) Plugin.Log.LogInfo("CHAOS board: " + marked + " chaos stage(s) marked this run");
            }
            catch (Exception ex) { Plugin.Log.LogWarning("CHAOS board marker failed: " + ex.Message); }
        }
    }

    // Per stage: forget the old stage, and apply Chaos once the new (planned) stage has finished building.
    internal class ChaosStageRunner : MonoBehaviour
    {
        string lastStage;
        float applyAt = -1f;

        internal static void Ensure()
        {
            if (FindObjectOfType<ChaosStageRunner>() != null) return;
            GameObject go = new GameObject("HybridBosses_ChaosStages");
            DontDestroyOnLoad(go);
            go.AddComponent<ChaosStageRunner>();
        }

        void Update()
        {
            string stage = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name + "|" + Level.tileSetName + "|" + GameController.tierCount + "|" + GameController.stageCount;
            if (stage != lastStage)
            {
                lastStage = stage;
                if (ChaosStages.Active != null) ChaosStages.Revert();
                applyAt = Time.time + 1.5f; // let the stage finish building
            }
            if (applyAt > 0f && Time.time >= applyAt)
            {
                applyAt = -1f;
                if (ChaosStages.Planned && Level.levelContainer != null) ChaosStages.Apply();
            }
        }
    }
}
