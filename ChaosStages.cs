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
    // How a variant recolours the tileset (v0.6.0): every colour pulled to TargetHue keeping HueSpread of its own
    // variation (Chaos violet, Radiant gold), or spread around the hue wheel by brightness (Prism, Rainbow).
    // (The old relative hue shift turned grass red instead of purple.)
    internal class ChaosVariant
    {
        public string Name;
        public float TargetHue = -1f, HueSpread = 0.1f, Saturation = 1f, SatFloor, Value = 1f, ValueLift;
        public bool Rainbow;
        public Color SpriteTint, LightTint;

        public ChaosVariant(string name, Color sprite, Color light)
        {
            Name = name; SpriteTint = sprite; LightTint = light;
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

        // v0.6.0: which variant a variant stage is (Chaos / Radiant / Prism), from a second hash of the same seed, so
        // the board, the stage, its rival and the tier's Council always agree. (IsChaos above = "is a variant stage".)
        internal static StageVariant VariantOf(int tier, int stage)
        {
            if (!IsChaos(tier, stage)) return StageVariant.None;
            unchecked
            {
                int h = FinalRun.Seed ^ 0x2f6b9a1;
                h = h * 37 + tier * 6151;
                h = h * 37 + stage * 92821;
                h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
                return Variants.Split(((h & 0x7fffffff) % 10000) / 10000f);
            }
        }

        // A tier's Council takes the variant of its first variant stage (None when its stages were all normal).
        internal static StageVariant TierVariant(int tier)
        {
            for (int s = 0; s < RegularStages; s++)
            {
                StageVariant v = VariantOf(tier, s);
                if (v != StageVariant.None) return v;
            }
            return StageVariant.None;
        }

        internal static StageVariant CurrentStageVariant()
        {
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name ?? "";
            if (scene == "Hub" || scene == "Tutorial" || scene.Contains("PvP") || scene.Contains("BossLevel")) return StageVariant.None;
            if (Level.isBossRushMode) return StageVariant.None;
            if (UnityEngine.Object.FindObjectOfType<BossRoomEventHandler>() != null) return StageVariant.None;
            FinalRun.Tick();
            return VariantOf(GameController.tierCount, GameController.stageCount - 1);
        }
    }

    internal static class ChaosStages
    {
        // Decided when the stage is generated (before the rival portal is set up), and only valid for that stage (so
        // the Hub or a boss room after a variant stage isn't one). v0.6.0: which variant too.
        static bool planned;
        static string plannedKey;
        static StageVariant plannedVariant = StageVariant.Chaos;
        internal static bool Planned { get { return planned && plannedKey == StageKey(); } }
        internal static StageVariant PlannedVariant { get { return Planned ? plannedVariant : StageVariant.None; } }
        internal static VariantInfo PlannedInfo { get { return Variants.Info(plannedVariant); } }
        static StageVariant activeVariant = StageVariant.None;
        internal static StageVariant ActiveVariant { get { return Active != null ? activeVariant : StageVariant.None; } }
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
            StageVariant v = ChaosRun.CurrentStageVariant();
            planned = v != StageVariant.None;
            if (planned) plannedVariant = v;
            plannedKey = StageKey();
            if (planned) Plugin.Log.LogInfo("CHAOS stage planned: " + v + ", tier " + GameController.tierCount + " stage " + GameController.stageCount + " (" + Level.tileSetName + ")");
        }

        // Shift+F11 test key: cycles this stage through normal -> Chaos -> Radiant -> Prism (the last two with Arcanos).
        internal static void ToggleOnCurrentStage()
        {
            StageVariant next;
            if (Active == null) next = StageVariant.Chaos;
            else if (activeVariant == StageVariant.Chaos && ArcanosBridge.Present) next = StageVariant.Radiant;
            else if (activeVariant == StageVariant.Radiant) next = StageVariant.Prism;
            else next = StageVariant.None;
            Revert();
            if (next == StageVariant.None) { planned = false; Plugin.Log.LogInfo("CHAOS stage: off (test)"); return; }
            planned = true; plannedKey = StageKey(); plannedVariant = next;
            Apply();
        }

        // visualOnly: tiles, decorations and lights only (the Corrupted Council's room): no reward bonus, no enemies armed.
        internal static void Apply(bool visualOnly = false, StageVariant which = StageVariant.None, ChaosVariant tilesOverride = null)
        {
            Revert();
            if (which == StageVariant.None) which = plannedVariant;
            ChaosVariant v = tilesOverride ?? Variants.Info(which).Tiles; // v0.8.0: an infused Council room passes its element's tiles
            activeVariant = which;
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
                AddRewards(which);
                armed = ChaosEnemies.ArmAll();
            }

            Plugin.Log.LogInfo("CHAOS stage on (" + which + ") " + Level.tileSetName + ": " + meshes + " tile meshes, " + sprites + " decorations, " + lights + " lights, "
                + armed + " enemies turned " + which + ", " + Mathf.RoundToInt((Time.realtimeSinceStartup - t0) * 1000f) + " ms");
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
            activeVariant = StageVariant.None;
        }

        // ---------- rewards: more gold and item drops while on a Chaos stage ----------

        // Per variant (v0.6.0): Chaos more gold and item drops; Radiant a bit more gold, and its enemies may leave a
        // health pickup (VariantMark); Prism more item drops and more Chaos gems.
        static void AddRewards(StageVariant which)
        {
            if (rewardMods) return;
            rewardMods = true;
            float m = Plugin.ChaosStageRewards.Value;
            float gold = which == StageVariant.Prism ? 1f : which == StageVariant.Radiant ? 1f + (m - 1f) * 0.5f : m;
            float drops = which == StageVariant.Radiant ? 1f : m;
            float plat = which == StageVariant.Prism ? m : 1f;
            try
            {
                if (gold != 1f) LootManager.globalGoldModifier.AddMod(new NumVarStatMod("HB_ChaosGold", gold, 10, VarStatModType.Multiplicative, false));
                if (drops != 1f) LootManager.itemDropModifier.AddMod(new NumVarStatMod("HB_ChaosDrops", drops, 10, VarStatModType.Multiplicative, false));
                if (plat != 1f) LootManager.globalPlatModifier.AddMod(new NumVarStatMod("HB_ChaosPlat", plat, 10, VarStatModType.Multiplicative, false));
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
                LootManager.globalPlatModifier.RemoveMod("HB_ChaosPlat");
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
            if (v.Rainbow) h = Mathf.Repeat(h * 0.3f + val * 1.7f, 1f);
            else if (v.TargetHue >= 0f) h = Mathf.Repeat(v.TargetHue + (h - 0.5f) * v.HueSpread, 1f);
            s = Mathf.Clamp01(s * v.Saturation + v.SatFloor);
            val = Mathf.Clamp01(val * v.Value + v.ValueLift);
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
            // v0.6.0: the stage's variant. Chaos: their purple palette row; Radiant: pale gold tint; Prism: cycling hues.
            StageVariant v = ChaosStages.ActiveVariant != StageVariant.None ? ChaosStages.ActiveVariant : ChaosStages.PlannedVariant;
            if (v == StageVariant.None) v = StageVariant.Chaos;
            VariantMark.Set(e, v);
            if (v == StageVariant.Chaos)
            {
                int idx = Plugin.ChaosGiantPalette.Value >= 0 ? Plugin.ChaosGiantPalette.Value : ChaosGiants.PurpleRow(e);
                if (idx >= 0) ElementalGiants.SetPaletteIndexAll(e, idx);
                else ElementalGiants.Tint(e, ChaosRun.Color);
            }
            else Variants.TintBody(e, v);
            List<ArcanaDef> pool = Variants.CastPool(v);
            if (pool.Count > 0) EnemyArcana.Give(e, Rivals.PickVaried(pool, 1, false), 1.6f); // slower than a giant's
            return true;
        }

        internal static List<ArcanaDef> Pool() { return Variants.CastPool(StageVariant.Chaos); }
    }

    // Chaos giants: Chaos element, the giant's own purple palette row (found by reading its palette texture), or a
    // purple tint when its sheet has none (e.g. the Coffin), and 2 Chaos arcana. No borrowed elemental skills.
    internal static class ChaosGiants
    {
        internal static void Apply(Enemy e)
        {
            if (e == null || e.GetComponent<ChaosArmed>() != null) return;
            e.gameObject.AddComponent<ChaosArmed>();
            // v0.6.0: a Radiant / Prism giant on those stages (tint / cycling hues, 2 arcana of that element).
            StageVariant v = ChaosStages.ActiveVariant != StageVariant.None ? ChaosStages.ActiveVariant : ChaosStages.PlannedVariant;
            if (v == StageVariant.None) v = StageVariant.Chaos;
            VariantInfo vi = Variants.Info(v);
            VariantMark.Set(e, v);
            // v0.6.1: only Chaos changes the body's element. Arcanos' Radiant / Prism element values are huge numbers
            // (e.g. -1896111776) the game's damage lookup can't handle: a Prism Coffin took no damage at all.
            if (v == StageVariant.Chaos) e.element = ElementType.Chaos;

            int idx = -1;
            if (v == StageVariant.Chaos)
            {
                idx = Plugin.ChaosGiantPalette.Value >= 0 ? Plugin.ChaosGiantPalette.Value : PurpleRow(e);
                if (idx >= 0 && !ElementalGiants.NoElementPalettes(e)) ElementalGiants.SetPaletteIndexAll(e, idx);
                else { ElementalGiants.Tint(e, ChaosRun.Color); idx = -1; }
            }
            else Variants.TintBody(e, v);

            int arcana = 0;
            List<ArcanaDef> pool = Variants.CastPool(v);
            if (pool.Count > 0) arcana = EnemyArcana.Give(e, Rivals.PickVaried(pool, 2, false));
            if (Plugin.GiantIntros.Value) GiantIntro.Attach(e, vi.Prefix, "");
            Plugin.Log.LogInfo("GIANT " + e.GetType().Name + " -> " + v + " giant, palette " + (idx < 0 ? "tint" : idx.ToString()) + ", arcana " + arcana);
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
            if (!ChaosStages.Planned || string.IsNullOrEmpty(__result)) return;
            string title = ChaosStages.PlannedInfo.StageTitle + " ";
            if (!__result.StartsWith(title)) __result = title + __result;
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
                    StageVariant sv = ChaosRun.VariantOf(tier, stage);
                    if (sv == StageVariant.None) continue;
                    // v0.6.0: the variant's square colour and icon (Chaos symbol; Arcanos' Radiant / Prism icons).
                    VariantInfo vi = Variants.Info(sv);
                    Sprite icon = sv == StageVariant.Chaos ? chaosIcon : (ArcanosBridge.Icon(sv) ?? chaosIcon);
                    foreach (Image img in s.GetComponentsInChildren<Image>(true))
                    {
                        if (skip.Contains(img)) continue;
                        if (img.transform == s) marks.Add(img, sv == StageVariant.Chaos ? SquareColor : vi.BoardSquare, null); // the square
                        else marks.Add(img, sv == StageVariant.Chaos ? IconColor : vi.BoardIcon, icon);                       // its element icon
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
                        StageVariant cv = CouncilCorruption.CouncilVariant(t);
                        ElementType? infusedEl = cv == StageVariant.None ? CouncilCorruption.CouncilInfusion(t, circle.name) : null; // v0.8.0
                        bool corrupted = cv != StageVariant.None || infusedEl.HasValue;
                        plan.Add(circle.name + "=" + (cv != StageVariant.None ? cv.ToString() : infusedEl.HasValue ? Infusion.Name(infusedEl.Value) + "-infused" : "-"));
                        t++;
                        if (!corrupted) continue;
                        List<Image> portraits = new List<Image>();
                        foreach (Image img in circle.GetComponentsInChildren<Image>(true))
                            if (!skip.Contains(img) && !squareImages.Contains(img) && img.name.Contains("Portrait")) portraits.Add(img);
                        if (portraits.Count == 0)
                            foreach (Image img in circle.GetComponentsInChildren<Image>(true))
                                if (!skip.Contains(img) && !squareImages.Contains(img) && img.transform != circle && !img.name.Contains("Dimmer") && !img.name.Contains("Circle"))
                                    portraits.Add(img);
                        Color markColor = infusedEl.HasValue ? Infusion.ColorOf(infusedEl.Value) : Variants.Info(cv).Color;
                        foreach (Image img in portraits) marks.Add(img, Color.Lerp(img.color, markColor, 0.6f), null);
                        plan[plan.Count - 1] += "(" + string.Join("/", portraits.ConvertAll(i => i.name).ToArray()) + ")";
                        councilMarked++;
                    }
                    List<string> squaresPlan = new List<string>();
                    for (int q = 0; q < k; q++)
                    {
                        StageVariant qv = ChaosRun.VariantOf(q / regular, q % regular);
                        squaresPlan.Add(qv == StageVariant.None ? "." : qv == StageVariant.Chaos ? "C" : qv == StageVariant.Radiant ? "R" : "P");
                    }
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
            EchoRecorder.Ensure(go); // v0.9.0: notes the run's loadout for the Echo
        }

        void Update()
        {
            FinalRun.Tick(); // rolls the run (Chaos plan, champion) on arriving in the Hub
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (sceneName == "Hub" || sceneName == "Tutorial") Infusion.Clear(); // infusions last one run
            string stage =UnityEngine.SceneManagement.SceneManager.GetActiveScene().name + "|" + Level.tileSetName + "|" + GameController.tierCount + "|" + GameController.stageCount;
            if (stage != lastStage)
            {
                lastStage = stage;
                if (ChaosStages.Active != null) ChaosStages.Revert();
                applyAt = Time.time + 1.5f; // let the stage finish building
            }
            // 0.6.4: no time limit. The plan is made when the stage finishes generating, which on big stages (an Ice
            // stage) came after the old 6.5s window, so a planned Prism stage was never tinted. Now: whenever this
            // stage is planned and not tinted yet, tint it (1.5s after the scene changed at the earliest).
            if (applyAt > 0f && Time.time >= applyAt && ChaosStages.Planned && ChaosStages.Active == null)
            {
                applyAt = -1f;
                try { ChaosStages.Apply(); }
                catch (Exception ex) { Plugin.Log.LogWarning("CHAOS stage apply failed: " + ex); }
            }
        }
    }
}
