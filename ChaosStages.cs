using System;
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
                + armed + " enemies with chaos arcana, " + Mathf.RoundToInt((Time.realtimeSinceStartup - t0) * 1000f) + " ms");
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

    // Enemies on a Chaos stage cast Chaos-element player arcana (1 for regular enemies, 2 for giants).
    internal static class ChaosEnemies
    {
        static readonly HashSet<string> NonCombat = new HashSet<string> { "Dove", "Rat", "Eagle", "TrainingDummy", "Contestant" };

        internal static int ArmAll()
        {
            int n = 0;
            foreach (Enemy e in UnityEngine.Object.FindObjectsOfType<Enemy>()) if (Arm(e)) n++;
            return n;
        }

        internal static bool Arm(Enemy e)
        {
            if (e == null || ChaosStages.Active == null || Rivals.IsRival(e) || e is Boss) return false;
            if (NonCombat.Contains(e.GetType().Name) || e.fsm == null || e.GetComponent<ChaosArmed>() != null) return false;
            List<ArcanaDef> pool = ArcanaCatalog.PoolExact("Chaos");
            pool.RemoveAll(d => d.Family == ArcanaFamily.Minion || d.Family == ArcanaFamily.Seeker || d.Family == ArcanaFamily.Ward);
            if (pool.Count == 0) return false;
            int count = e is MiniBoss ? 2 : 1;
            int given = EnemyArcana.Give(e, Rivals.PickVaried(pool, count, false), e is MiniBoss ? 1f : 1.6f);
            e.gameObject.AddComponent<ChaosArmed>();
            return given > 0;
        }
    }

    internal class ChaosArmed : MonoBehaviour { }

    // Enemies spawned later on a Chaos stage (waves, summons, giants) get armed too.
    [HarmonyPatch(typeof(Enemy), "Start")]
    static class ChaosEnemyStartPatch
    {
        static void Postfix(Enemy __instance)
        {
            if (ChaosStages.Active == null) return;
            try { ChaosEnemies.Arm(__instance); } catch (Exception) { }
        }
    }

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
