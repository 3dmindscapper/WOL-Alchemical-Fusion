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

        // tier, stage = the regular stage's 0-based index within its tier (0 or 1: the game has stagesPerTier = 2 regular
        // stages, then the boss floor, which is its own scene and never Chaos). During a stage GameController.stageCount
        // is 1-based (LoadLevel adds 1 as the tier scene loads), so the stage itself is stageCount - 1.
        // (Before v0.5.3 the boss-floor guard used "stage >= stagesPerTier - 1", which rejected every in-game stage.)
        // 0.5.15: GameController.stagesPerTier is 1, not 2. The game goes to the boss once stageCount > stagesPerTier,
        // so a tier has stagesPerTier + 1 regular stages (stageCount 1 and 2). Treating it as the count made the board
        // give every square its own tier (marks on the wrong squares) and the 2nd stage of a tier could never be Chaos.
        internal static int RegularStages { get { return Mathf.Max(1, GameController.stagesPerTier + 1); } }

        internal static bool IsChaos(int tier, int stage)
        {
            float chance = Plugin.ChaosStageChance.Value;
            if (chance <= 0f || stage < 0 || tier < 0) return false;
            if (stage >= RegularStages) return false;
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
            if (scene == "Hub" || scene == "Tutorial" || scene.Contains("PvP") || scene.Contains("BossLevel")) return false;
            if (Level.isBossRushMode) return false;
            if (UnityEngine.Object.FindObjectOfType<BossRoomEventHandler>() != null) return false;
            FinalRun.Tick();
            return IsChaos(GameController.tierCount, GameController.stageCount - 1);
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
        internal static bool WasPlanned { get { return planned; } }
        internal static string PlannedKey { get { return plannedKey; } }
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

        // visualOnly: tiles, decorations and lights only (the Corrupted Council's room): no reward bonus, no enemies armed.
        internal static void Apply(bool visualOnly = false)
        {
            Revert();
            ChaosVariant v = Chaos;
            Active = v;
            float t0 = Time.realtimeSinceStartup;
            int meshes = 0, sprites = 0, lights = 0;

            // Hand-built boss floors may not keep their tiles under "Meshes": there (visualOnly) any non-body mesh counts.
            bool anyStageMesh = false;
            foreach (MeshRenderer mr in UnityEngine.Object.FindObjectsOfType<MeshRenderer>()) if (IsStageMesh(mr)) { anyStageMesh = true; break; }
            foreach (MeshRenderer mr in UnityEngine.Object.FindObjectsOfType<MeshRenderer>())
            {
                if (anyStageMesh ? !IsStageMesh(mr) : (!visualOnly || mr.GetComponentInParent<Entity>() != null || mr.GetComponentInParent<Canvas>() != null)) continue;
                Material[] mats = mr.sharedMaterials;
                Material[] swapped = new Material[mats.Length];
                bool any = false;
                for (int k = 0; k < mats.Length; k++) { swapped[k] = Recolored(mats[k], v); any |= swapped[k] != mats[k]; }
                if (!any) continue;
                origMaterials[mr] = mats;
                mr.sharedMaterials = swapped;
                meshes++;
            }

            // Generated stages keep their decorations under Level.levelContainer; hand-built boss floors don't have one,
            // so there every scene sprite that isn't a body or UI counts as decoration.
            Transform level = Level.levelContainer;
            IEnumerable<SpriteRenderer> decor = level != null
                ? (IEnumerable<SpriteRenderer>)level.GetComponentsInChildren<SpriteRenderer>(true)
                : (IEnumerable<SpriteRenderer>)UnityEngine.Object.FindObjectsOfType<SpriteRenderer>();
            foreach (SpriteRenderer sr in decor)
                {
                    if (sr == null || sr.GetComponentInParent<Entity>() != null || sr.GetComponentInParent<Canvas>() != null) continue;
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

            int armed = 0;
            if (!visualOnly)
            {
                AddRewards();
                armed = ChaosEnemies.ArmAll();
            }

            Plugin.Log.LogInfo("CHAOS stage on " + Level.tileSetName + ": " + meshes + " tile meshes, " + sprites + " decorations, " + lights + " lights, "
                + armed + " enemies turned Chaos, " + Mathf.RoundToInt((Time.realtimeSinceStartup - t0) * 1000f) + " ms");
        }

        internal static void Revert()
        {
            foreach (KeyValuePair<Renderer, Material[]> kv in origMaterials) if (kv.Key != null) kv.Key.sharedMaterials = kv.Value;
            foreach (KeyValuePair<SpriteRenderer, Color> kv in origSpriteColors) if (kv.Key != null) kv.Key.color = kv.Value;
            foreach (KeyValuePair<Light, Color> kv in origLights) if (kv.Key != null) kv.Key.color = kv.Value;
            origMaterials.Clear(); origSpriteColors.Clear(); origLights.Clear();
            if (CouncilCorruption.CurrentBoss == null) { try { CouncilCorruption.RestoreTints(); CouncilCorruption.ClearTinted(); } catch (Exception) { } }
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
            if (e == null || e.GetComponent<ChaosArmed>() != null) return false;
            if (e is MiniBoss) { ChaosGiants.Apply(e); return true; }
            return ArmRegular(e);
        }

        // The enemy types that have elemental versions on the other stages (and so a Chaos one here): Knight, Rogue
        // (thief), Archer, Mage (wizard), Summoner, Lancer, including their elemental subclasses. Like elemental stages,
        // where not every one of them is elemental, each rolls ChaosStages.EnemyChance: 1 Chaos arcana + purple row.
        static readonly HashSet<string> ChaosTypes = new HashSet<string> { "Knight", "Rogue", "Archer", "Mage", "Summoner", "Lancer" };

        internal static bool IsChaosType(Enemy e)
        {
            if (e == null || e is MiniBoss || Rivals.IsRival(e)) return false;
            for (Type t = e.GetType(); t != null && t != typeof(Enemy); t = t.BaseType)
                if (ChaosTypes.Contains(t.Name)) return true;
            return false;
        }

        internal static bool ArmRegular(Enemy e)
        {
            if (!IsChaosType(e) || e.fsm == null || e.GetComponent<ChaosArmed>() != null) return false;
            // 0.5.14: every humanoid with elemental versions turns Chaos (the 50% roll left half the stage's elemental
            // knights / lancers as they were). Their own attacks become Chaos via ChaosStageAttackPatch.
            e.gameObject.AddComponent<ChaosArmed>();
            int idx = Plugin.ChaosGiantPalette.Value >= 0 ? Plugin.ChaosGiantPalette.Value : ChaosGiants.PurpleRow(e);
            if (idx >= 0) ElementalGiants.SetPaletteIndexAll(e, idx);
            else ElementalGiants.Tint(e, ChaosRun.Color);
            List<ArcanaDef> pool = Pool();
            if (pool.Count > 0) EnemyArcana.Give(e, Rivals.PickVaried(pool, 1, false), 1.6f); // slower than a giant's
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

    // Chaos stage: enemy attacks that carry an element (an elemental knight's lightning slash, an archer's frost
    // arrow...) become Chaos damage, tinted Chaos purple. Restored on ChaosStages.Revert.
    [HarmonyPatch(typeof(Attack), "SetAttackInfo", new Type[] { typeof(string), typeof(string), typeof(int), typeof(bool) })]
    static class ChaosStageAttackPatch
    {
        static void Postfix(Attack __instance)
        {
            if (ChaosStages.Active == null || !ChaosStages.Planned || CouncilCorruption.CurrentBoss != null) return;
            try
            {
                AttackInfo info = __instance.atkInfo;
                if (info == null || info.skillCategory == "Player" || __instance.GetComponentInParent<Player>() != null) return;
                if (info.elementType == ElementType.Chaos || info.elementType == ElementType.Neutral) return;
                CouncilCorruption.Chaosify(info);
                CouncilCorruption.TintAttack(__instance, true);
            }
            catch (Exception) { }
        }
    }

    // Giants become Chaos giants through the giant Start hook (GiantStartPatch -> ElementalGiants.TryMakeElemental),
    // after MiniBoss.Start has set its gray palette. Regular Chaos-type enemies are armed one frame after their
    // Start (Enemy.Start runs InitElement, and some subclasses set palettes after it), so the purple row sticks.
    [HarmonyPatch(typeof(Enemy), "Start")]
    static class ChaosRegularEnemyPatch
    {
        static void Postfix(Enemy __instance)
        {
            if (!ChaosStages.Planned || !ChaosEnemies.IsChaosType(__instance)) return;
            try { __instance.StartCoroutine(NextFrame(__instance)); } catch (Exception) { }
        }

        static System.Collections.IEnumerator NextFrame(Enemy e)
        {
            yield return null;
            if (e != null && ChaosStages.Planned) ChaosEnemies.ArmRegular(e);
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

    // Loading-screen board: a Chaos stage's square turns dark purple with the Chaos symbol (the one on Sura's final
    // circle) in place of its element icon; a corrupted Council member's portrait is tinted purple. The board recolours
    // squares it has reached after Enable, so BoardMarks re-applies the marks every frame while the board shows.
    [HarmonyPatch(typeof(GameProgressBoard), "Enable")]
    static class ChaosBoardPatch
    {
        // 0.5.11: much darker (almost black purple) so it can't be mistaken for the Air tier's own lavender squares.
        static readonly Color SquareColor = new Color(0.17f, 0.06f, 0.26f), IconColor = new Color(0.8f, 0.55f, 1f);
        static bool loggedSquare;

        [HarmonyPriority(Priority.First)] // clears the marks, so it runs before the champion portrait patch adds its own
        static void Postfix(GameProgressBoard __instance)
        {
            try
            {
                BoardMarks marks = __instance.GetComponent<BoardMarks>() ?? __instance.gameObject.AddComponent<BoardMarks>();
                marks.Clear();
                FinalRun.Tick();
                if (Level.isBossRushMode) return;
                Traverse tr = Traverse.Create(__instance);
                RectTransform[] stages = tr.Field("stagesInOrder").GetValue() as RectTransform[];
                RectTransform[] bosses = tr.Field("bossesInOrder").GetValue() as RectTransform[];
                RectTransform finalCircle = tr.Field("finalBossCircle").GetValue() as RectTransform;
                RectTransform startCircle = tr.Field("startCircle").GetValue() as RectTransform;
                Image[] dimmers = tr.Field("dimmersInOrder").GetValue() as Image[];
                if (stages == null) return;
                // Regular squares per tier: the board's own count (curMaxStagesPerTier counts the boss too), which must
                // match ChaosRun.RegularStages; logged if they ever disagree.
                int boardPer = tr.Field("curMaxStagesPerTier").GetValue<int>() - 1;
                int regular = ChaosRun.RegularStages;
                if (boardPer > 0 && boardPer != regular) { Plugin.Log.LogWarning("CHAOS board has " + boardPer + " squares per tier, stages say " + regular + "; using the board's"); regular = boardPer; }
                HashSet<Image> skip = new HashSet<Image>(dimmers ?? new Image[0]);
                // stagesInOrder starts with the Start circle (logged layout) and may hold boss circles: skip all of them.
                HashSet<RectTransform> bossSet = new HashSet<RectTransform>(bosses ?? new RectTransform[0]);
                if (finalCircle != null) bossSet.Add(finalCircle);
                if (startCircle != null) bossSet.Add(startCircle);
                Sprite chaosIcon = null;
                if (finalCircle != null)
                {
                    Transform ci = finalCircle.Find("ChaosIcon");
                    Image cimg = ci != null ? ci.GetComponent<Image>() : null;
                    if (cimg != null) chaosIcon = cimg.sprite;
                }
                int chaosMarked = 0, councilMarked = 0, k = 0;
                // Regular stage squares in order (boss circles skipped, in case the list holds them): k-th square =
                // tier k / regular, stage k % regular, the same numbering the stage itself uses.
                foreach (RectTransform s in stages)
                {
                    if (s == null || bossSet.Contains(s)) continue;
                    int tier = k / regular, stage = k % regular;
                    k++;
                    if (!loggedSquare) { loggedSquare = true; LogLayout(s, skip); }
                    if (!ChaosRun.IsChaos(tier, stage)) continue;
                    foreach (Image img in s.GetComponentsInChildren<Image>(true))
                    {
                        if (skip.Contains(img)) continue;
                        if (img.transform == s) marks.Add(img, SquareColor, null);                     // the square
                        else marks.Add(img, IconColor, chaosIcon);                                      // its element icon
                    }
                    chaosMarked++;
                }
                // Council portraits of tiers whose member is corrupted. 0.5.12: only the portrait image itself (tinting
                // every image in the circle turned the whole Air tier lavender, squares included), and tiers counted
                // over the real Council circles in board order (no start / final / empty entries), like the squares.
                if (bosses != null)
                {
                    HashSet<Image> squareImages = new HashSet<Image>();
                    foreach (RectTransform s in stages)
                        if (s != null && !bossSet.Contains(s)) foreach (Image img in s.GetComponentsInChildren<Image>(true)) squareImages.Add(img);
                    int t = 0;
                    List<string> plan = new List<string>();
                    foreach (RectTransform circle in bosses)
                    {
                        if (circle == null || circle == finalCircle || circle == startCircle) continue;
                        bool corrupted = CouncilCorruption.IsCorrupted(t);
                        plan.Add(circle.name + "=" + (corrupted ? "corrupted" : "-"));
                        t++;
                        if (!corrupted) continue;
                        List<Image> portraits = new List<Image>();
                        foreach (Image img in circle.GetComponentsInChildren<Image>(true))
                            if (!skip.Contains(img) && !squareImages.Contains(img) && img.name.Contains("Portrait")) portraits.Add(img);
                        if (portraits.Count == 0)
                            foreach (Image img in circle.GetComponentsInChildren<Image>(true))
                                if (!skip.Contains(img) && !squareImages.Contains(img) && img.transform != circle && !img.name.Contains("Dimmer") && !img.name.Contains("Circle"))
                                    portraits.Add(img);
                        foreach (Image img in portraits) marks.Add(img, Color.Lerp(img.color, ChaosRun.Color, 0.6f), null);
                        plan[plan.Count - 1] += "(" + string.Join("/", portraits.ConvertAll(i => i.name).ToArray()) + ")";
                        councilMarked++;
                    }
                    List<string> squaresPlan = new List<string>();
                    for (int q = 0; q < k; q++) squaresPlan.Add(ChaosRun.IsChaos(q / regular, q % regular) ? "C" : ".");
                    Plugin.Log.LogInfo("CHAOS board plan: squares " + string.Join("", squaresPlan.ToArray()) + " | council " + string.Join(", ", plan.ToArray()));
                }
                if (chaosMarked + councilMarked > 0)
                    Plugin.Log.LogInfo("CHAOS board: " + chaosMarked + " chaos stage(s), " + councilMarked + " corrupted Council member(s) this run");
            }
            catch (Exception ex) { Plugin.Log.LogWarning("CHAOS board marker failed: " + ex.Message); }
        }

        static void LogLayout(RectTransform s, HashSet<Image> skip)
        {
            List<string> parts = new List<string>();
            foreach (Image img in s.GetComponentsInChildren<Image>(true))
                parts.Add(img.name + "[" + (img.sprite != null ? img.sprite.name : "-") + (skip.Contains(img) ? " dimmer" : "") + (img.transform == s ? " root" : "") + "]");
            Plugin.Log.LogInfo("CHAOS board square layout: " + string.Join(", ", parts.ToArray()));
        }
    }

    // Keeps the board's Chaos marks in place (the board recolours reached squares after Enable); restores the
    // originals when the marks are cleared for the next Enable.
    internal class BoardMarks : MonoBehaviour
    {
        sealed class Mark { public Image Img; public Color Color, OrigColor; public Sprite Sprite, OrigSprite; public bool Show, OrigEnabled; }
        readonly List<Mark> marks = new List<Mark>();

        // show: keep the image enabled too (Sura's portrait on champion runs).
        internal void Add(Image img, Color c, Sprite s, bool show = false)
        {
            if (img == null) return;
            marks.RemoveAll(m => m.Img == img && !m.Show && show); // a "show" mark wins over a plain one
            if (marks.Exists(m => m.Img == img)) return;
            marks.Add(new Mark { Img = img, Color = c, OrigColor = img.color, Sprite = s, OrigSprite = img.sprite, Show = show, OrigEnabled = img.enabled });
            Apply();
        }

        internal void Clear()
        {
            foreach (Mark m in marks) if (m.Img != null) { m.Img.color = m.OrigColor; m.Img.sprite = m.OrigSprite; if (m.Show) m.Img.enabled = m.OrigEnabled; }
            marks.Clear();
        }

        void LateUpdate() { Apply(); }

        void Apply()
        {
            foreach (Mark m in marks)
            {
                if (m.Img == null) continue;
                Color c = new Color(m.Color.r, m.Color.g, m.Color.b, m.Img.color.a); // keep the board's fades
                if (m.Img.color != c) m.Img.color = c;
                if (m.Sprite != null && m.Img.sprite != m.Sprite) m.Img.sprite = m.Sprite;
                if (m.Show && !m.Img.enabled) m.Img.enabled = true;
            }
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
            FinalRun.Tick(); // rolls the run (Chaos plan, champion) on arriving in the Hub
            string stage =UnityEngine.SceneManagement.SceneManager.GetActiveScene().name + "|" + Level.tileSetName + "|" + GameController.tierCount + "|" + GameController.stageCount;
            if (stage != lastStage)
            {
                lastStage = stage;
                if (ChaosStages.Active != null) ChaosStages.Revert();
                applyAt = Time.time + 1.5f; // let the stage finish building
            }
            // 0.5.14: no longer waits on Level.levelContainer (a planned stage was never tinted: the apply gave up
            // silently); keeps checking for a few seconds in case the plan's stage key settles late, and logs why not.
            if (applyAt > 0f && Time.time >= applyAt)
            {
                if (ChaosStages.Planned && ChaosStages.Active == null)
                {
                    applyAt = -1f;
                    ChaosStages.Apply();
                }
                else if (Time.time >= applyAt + 5f)
                {
                    applyAt = -1f;
                    if (ChaosStages.WasPlanned)
                        Plugin.Log.LogWarning("CHAOS stage was planned for " + ChaosStages.PlannedKey + " but this stage is " + ChaosStages.StageKey() + ": not applied");
                }
            }
        }
    }
}
