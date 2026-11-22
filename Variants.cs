using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace WolHybridBosses
{
    // Stage variants (v0.6.0): a regular stage that rolls a variant (ChaosStages.StageChance) is Chaos, Radiant or Prism.
    // Radiant and Prism are WoL Arcanos' "Fulgor" and "Prisma" elements: without Arcanos every variant is Chaos.
    // Everything that used to be "Chaos stage" reads its look, names, element and arcana from the variant here.
    internal enum StageVariant { None, Chaos, Radiant, Prism }

    internal sealed class VariantInfo
    {
        public StageVariant Variant;
        public string Prefix;        // catalog element / rival prefix: "Chaos", "Radiant", "Prism"
        public string StageTitle;    // "Chaos Forest", "Radiant Forest", "Prismatic Forest"
        public string CouncilTitle;  // "Corrupted", "Radiant", "Prismatic"
        public string ArcanosElement; // Arcanos' own name: "Chaos", "Fulgor", "Prisma"
        public Color Color;          // UI / text / outlines
        public Color RampDark, RampBright; // attack effects remapped by brightness
        public Color BodyTint;       // enemy / boss sprites
        public Color AttackTint;     // attack sprites (multiplied)
        public Color LightTint;
        public Color BoardSquare, BoardIcon;
        public bool Rainbow;         // Prism: colours cycle / spread over the hue wheel
        public ChaosVariant Tiles;
    }

    internal static class Variants
    {
        static readonly VariantInfo chaos = new VariantInfo
        {
            Variant = StageVariant.Chaos, Prefix = "Chaos", StageTitle = "Chaos", CouncilTitle = "Corrupted", ArcanosElement = "Chaos",
            Color = new Color(0.62f, 0.32f, 1f), RampDark = new Color(0.16f, 0.03f, 0.28f), RampBright = new Color(0.72f, 0.32f, 1f),
            BodyTint = new Color(0.62f, 0.42f, 0.85f), AttackTint = new Color(0.46f, 0.2f, 0.72f), LightTint = new Color(0.55f, 0.35f, 1f),
            BoardSquare = new Color(0.17f, 0.06f, 0.26f), BoardIcon = new Color(0.8f, 0.55f, 1f),
            // Dark purple: every colour pulled to violet, keeping a little of its variation.
            Tiles = new ChaosVariant("Chaos", new Color(0.75f, 0.60f, 1.00f), new Color(0.55f, 0.35f, 1.00f)) { TargetHue = 0.77f, HueSpread = 0.12f, Saturation = 0.6f, SatFloor = 0.25f, Value = 0.62f },
        };
        static readonly VariantInfo radiant = new VariantInfo
        {
            Variant = StageVariant.Radiant, Prefix = "Radiant", StageTitle = "Radiant", CouncilTitle = "Radiant", ArcanosElement = "Fulgor",
            // v0.6.2: attacks infused much more strongly (bright gold core to pearl white, see TintTree); the stage
            // itself more gently.
            Color = new Color(1f, 0.88f, 0.5f), RampDark = new Color(0.95f, 0.7f, 0.22f), RampBright = new Color(1f, 1f, 0.95f),
            BodyTint = new Color(1f, 0.95f, 0.78f), AttackTint = new Color(1f, 0.93f, 0.7f), LightTint = new Color(1f, 0.92f, 0.65f),
            BoardSquare = new Color(0.62f, 0.52f, 0.28f), BoardIcon = new Color(1f, 0.96f, 0.82f),
            // A warm golden wash that keeps much more of the stage's own colours.
            Tiles = new ChaosVariant("Radiant", new Color(1f, 0.97f, 0.9f), new Color(1f, 0.92f, 0.7f)) { TargetHue = 0.12f, HueSpread = 0.35f, Saturation = 0.55f, SatFloor = 0.04f, Value = 0.8f, ValueLift = 0.18f },
        };
        static readonly VariantInfo prism = new VariantInfo
        {
            Variant = StageVariant.Prism, Prefix = "Prism", StageTitle = "Prismatic", CouncilTitle = "Prismatic", ArcanosElement = "Prisma",
            Color = new Color(1f, 0.55f, 0.9f), RampDark = new Color(0.3f, 0.2f, 0.5f), RampBright = new Color(1f, 0.85f, 1f),
            BodyTint = Color.white, AttackTint = Color.white, LightTint = new Color(0.85f, 0.65f, 1f),
            BoardSquare = new Color(0.45f, 0.25f, 0.5f), BoardIcon = Color.white, Rainbow = true,
            // Every shade lands somewhere else on the hue wheel: a rainbow tileset.
            Tiles = new ChaosVariant("Prism", new Color(0.95f, 0.88f, 1f), new Color(0.85f, 0.65f, 1f)) { Rainbow = true, Saturation = 1.1f, SatFloor = 0.25f, Value = 0.95f },
        };

        internal static VariantInfo Info(StageVariant v)
        {
            switch (v)
            {
                case StageVariant.Radiant: return radiant;
                case StageVariant.Prism: return prism;
                default: return chaos;
            }
        }

        internal static StageVariant FromPrefix(string prefix)
        {
            switch (prefix)
            {
                case "Chaos": return StageVariant.Chaos;
                case "Radiant": return StageVariant.Radiant;
                case "Prism": return StageVariant.Prism;
                default: return StageVariant.None;
            }
        }

        // Which variant a variant roll becomes: 50 / 35 / 15 by default (config), Chaos only without Arcanos.
        internal static StageVariant Split(float r)
        {
            if (!ArcanosBridge.Present) return StageVariant.Chaos;
            float c = Mathf.Max(0f, Plugin.VariantChaosShare.Value), ra = Mathf.Max(0f, Plugin.VariantRadiantShare.Value), p = Mathf.Max(0f, Plugin.VariantPrismShare.Value);
            float total = c + ra + p;
            if (total <= 0f) return StageVariant.Chaos;
            r *= total;
            if (r < c) return StageVariant.Chaos;
            if (r < c + ra) return StageVariant.Radiant;
            return StageVariant.Prism;
        }

        internal static StageVariant RandomVariant() { return Split(UnityEngine.Random.value); }

        // A colour remapped onto the variant: Chaos and Radiant by brightness along their ramp, Prism around the wheel.
        internal static Color Ramp(Color c, StageVariant v)
        {
            VariantInfo vi = Info(v);
            float lum = 0.3f * c.r + 0.59f * c.g + 0.11f * c.b;
            Color r;
            if (vi.Rainbow)
            {
                float h, s, val;
                Color.RGBToHSV(c, out h, out s, out val);
                r = Color.HSVToRGB(Mathf.Repeat(h * 0.3f + lum * 1.7f + 0.05f, 1f), 0.7f, Mathf.Clamp01(0.55f + lum * 0.5f));
            }
            else if (v == StageVariant.Radiant) r = Color.Lerp(vi.RampDark, vi.RampBright, Mathf.Clamp01(0.25f + lum * 1.2f)); // never dim
            else r = Color.Lerp(vi.RampDark, vi.RampBright, Mathf.Clamp01(lum * 1.1f));
            r.a = c.a;
            return r;
        }

        // ---------- arcana ----------

        // What variant enemies, giants, echoes and rivals cast. Radiant / Prism: Arcanos' arcana of that element; if
        // there are few castable ones, topped up with base-game arcana (Radiant: Thunder and Wind light; Prism: every
        // element), which become that element when cast (VariantMark on the caster).
        internal static List<ArcanaDef> CastPool(StageVariant v, bool noSummons = true)
        {
            VariantInfo vi = Info(v);
            List<ArcanaDef> pool = ArcanaCatalog.PoolExact(vi.Prefix);
            if (v != StageVariant.Chaos && pool.Count < 4)
            {
                if (v == StageVariant.Radiant) { pool.AddRange(ArcanaCatalog.Pool("Thunder")); pool.AddRange(ArcanaCatalog.Pool("Wind")); }
                else foreach (string p in new[] { "Flame", "Frost", "Earth", "Wind", "Thunder" }) pool.AddRange(ArcanaCatalog.Pool(p));
            }
            if (noSummons) pool.RemoveAll(d => d.Family == ArcanaFamily.Minion || d.Family == ArcanaFamily.Seeker || d.Family == ArcanaFamily.Ward);
            return pool;
        }

        // How an arcana is shown in intros for a Radiant / Prism caster: base arcana infused with the element read
        // "Radiant Tele Strike" / "Prismatic Tele Strike" (Arcanos' own arcana of the element keep their name).
        internal static string Named(ArcanaDef d, string prefix)
        {
            string name = IntroUI.SkillName(d.SkillID);
            StageVariant v = FromPrefix(prefix);
            if ((v != StageVariant.Radiant && v != StageVariant.Prism) || d.Element == prefix) return name;
            return Info(v).StageTitle + " " + name;
        }

        internal static List<ArcanaDef> DashPool(StageVariant v)
        {
            List<ArcanaDef> pool = ArcanaCatalog.DashPool(Info(v).Prefix);
            if (pool.Count == 0 && v == StageVariant.Radiant) { pool.AddRange(ArcanaCatalog.DashPool("Thunder")); pool.AddRange(ArcanaCatalog.DashPool("Wind")); }
            if (pool.Count == 0 && v == StageVariant.Prism) pool.AddRange(ArcanaCatalog.DashPool("Chaos"));
            return pool;
        }

        // ---------- attacks ----------

        // Damage type of the variant. Chaos: element Chaos and the elemental status becomes Chaos status. Radiant /
        // Prism: Arcanos sets its own element on the attack (Prism attacks cycle elements its own way).
        internal static void Convert(Attack atk, StageVariant v)
        {
            if (atk == null || v == StageVariant.None) return;
            AttackInfo info = atk.atkInfo;
            if (info == null || info.entity is Player || atk.GetComponentInParent<Player>() != null) return;
            if (v == StageVariant.Chaos) { ChaosifyInfo(info); return; }
            // v0.6.1: Arcanos' element values are huge numbers the game's damage code doesn't expect (a Prism giant
            // became unhittable once its element was set to one), so they're never written onto enemy attacks/bodies.
            // Radiant enemy attacks keep their own damage element and take the Radiant look.
            // v0.6.3: Prism attacks do what Arcanos' Prism does: each is the next of the five base elements (per
            // caster), with that element's status. Converted once per attack info (pooled attacks are re-set up).
            if (v == StageVariant.Prism)
            {
                PrismElement el;
                if (!prismDone.TryGetValue(info, out el))
                {
                    el = Prism.Next(info.entity != null ? (Component)info.entity : atk);
                    prismDone[info] = el;
                    if (prismDone.Count > 4096) prismDone.Clear();
                }
                info.elementType = el.Type;
                info.burnChance = info.slowChance = info.poisonChance = info.shockChance = info.freezeChance = info.chaosChance = 0f;
                el.ApplyStatus(info);
            }
        }

        static readonly Dictionary<AttackInfo, PrismElement> prismDone = new Dictionary<AttackInfo, PrismElement>();

        // The colour a Prism attack takes: its cycle element's (null for other variants / not yet converted).
        internal static Color? PrismColor(Attack atk)
        {
            PrismElement el;
            return atk != null && atk.atkInfo != null && prismDone.TryGetValue(atk.atkInfo, out el) ? (Color?)el.Color : null;
        }

        // Damage type + look, the one entry point for every variant attack.
        internal static void Apply(Attack atk, StageVariant v, bool stage = true)
        {
            if (atk == null || v == StageVariant.None) return;
            // v0.7.4: an attack that already is Chaos (a Chaos arcana) keeps its own look: recolouring it onto the
            // Chaos ramp only turned it into a murky blob, and accents on it left a sparking, noisy trail.
            if (v == StageVariant.Chaos && atk.atkInfo != null && atk.atkInfo.elementType == ElementType.Chaos) return;
            Convert(atk, v);
            PrismElement el = null;
            if (v == StageVariant.Prism && atk.atkInfo != null) prismDone.TryGetValue(atk.atkInfo, out el);
            string key = el != null ? ElementRamps.ForElement(el.Type) : ElementRamps.ForVariant(v);
            CouncilCorruption.TintAttack(atk, stage, v, PrismColor(atk), key);
            Accent(atk, v, el);
        }

        // v0.6.5: a few element particles where the attack starts, to sell the element (Arcanos' effects when
        // installed). At most one per caster every 0.35s, so busy fights don't fill with sparks.
        static readonly Dictionary<int, float> lastAccent = new Dictionary<int, float>();
        static readonly HashSet<int> accented = new HashSet<int>();

        // v0.7.4: once per attack object (attacks that re-set their info while travelling used to leave a trail of
        // sparks and repeated sounds), at most once per caster every 1.5s.
        static void Accent(Attack atk, StageVariant v, PrismElement el)
        {
            if (!ArcanosBridge.Present || atk == null || atk.atkInfo == null) return;
            if (!accented.Add(atk.GetInstanceID())) return;
            if (accented.Count > 4096) accented.Clear();
            Entity owner = atk.atkInfo.entity;
            int id = owner != null ? owner.GetInstanceID() : 0;
            float last;
            if (lastAccent.TryGetValue(id, out last) && Time.time - last < 1.5f) return;
            lastAccent[id] = Time.time;
            if (lastAccent.Count > 256) lastAccent.Clear();
            Vector2 p = atk.transform.position;
            if (v == StageVariant.Chaos) ArcanosBridge.Fx("ChispasCaos", p, 4);
            else if (v == StageVariant.Radiant) ArcanosBridge.Fx("Destellos", p, 3);
            else if (el != null)
            {
                switch (el.Type)
                {
                    case ElementType.Fire: ArcanosBridge.Fx("Chispas", p, Vector2.up, 4); break;
                    case ElementType.Water: ArcanosBridge.Fx("Salpicon", p, 4); break;
                    case ElementType.Earth: ArcanosBridge.Fx("Polvo", p, 4, 0.8f); break;
                    case ElementType.Air: ArcanosBridge.Fx("Rafaga", p, 3); break;
                    default: ArcanosBridge.Fx("ImpactoRayo", p, 3); break;
                }
            }
        }

        internal static void ChaosifyInfo(AttackInfo info)
        {
            if (info == null || info.elementType == ElementType.Chaos) return;
            info.elementType = ElementType.Chaos;
            float chance = Mathf.Max(Mathf.Max(info.burnChance, info.slowChance), Mathf.Max(Mathf.Max(info.poisonChance, info.shockChance), info.freezeChance));
            int level = Mathf.Max(1, Mathf.Max(Mathf.Max(info.burnLevel, info.slowLevel), Mathf.Max(info.poisonLevel, info.shockLevel)));
            if (chance > info.chaosChance) { info.chaosChance = chance; info.chaosLevel = Mathf.Max(info.chaosLevel, level); }
            info.burnChance = info.slowChance = info.poisonChance = info.shockChance = info.freezeChance = 0f;
        }

        // The variant whose attacks these are, if any: the Council fight's, the caster's mark, or the stage's.
        internal static StageVariant AttackVariant(Attack atk)
        {
            if (atk == null || atk.atkInfo == null) return StageVariant.None;
            if (CouncilCorruption.CurrentBoss != null) return CouncilCorruption.Variant;
            Entity owner = atk.atkInfo.entity;
            if (owner == null) owner = atk.GetComponentInParent<Entity>();
            if (owner is Player) return StageVariant.None;
            if (owner != null)
            {
                VariantMark mark = owner.GetComponent<VariantMark>();
                if (mark != null) return mark.Variant;
                if (owner.GetComponent<RivalMarker>() != null) return StageVariant.None; // rivals carry their own mark
            }
            if (ChaosStages.Active != null && ChaosStages.Planned)
            {
                ElementType el = atk.atkInfo.elementType;
                if (el != ElementType.Neutral && el != ElementType.Chaos) return ChaosStages.PlannedVariant;
            }
            return StageVariant.None;
        }

        // ---------- the look of bodies ----------

        internal static void TintBody(Entity e, StageVariant v)
        {
            if (e == null) return;
            VariantInfo vi = Info(v);
            if (vi.Rainbow) { if (e.GetComponent<PrismCycle>() == null) e.gameObject.AddComponent<PrismCycle>(); return; }
            Enemy en = e as Enemy;
            if (en != null) ElementalGiants.Tint(en, vi.Color);
        }
    }

    // Marks a caster (stage enemy, giant, rival, champion) as belonging to a variant: its attacks become that element.
    // Radiant enemies sometimes leave a health pickup behind (the Radiant reward twist).
    internal class VariantMark : MonoBehaviour
    {
        public StageVariant Variant;
        Enemy enemy;
        bool dropped;

        internal static VariantMark Set(Component c, StageVariant v)
        {
            if (c == null || v == StageVariant.None) return null;
            VariantMark m = c.GetComponent<VariantMark>() ?? c.gameObject.AddComponent<VariantMark>();
            m.Variant = v;
            return m;
        }

        void Start() { enemy = GetComponent<Enemy>(); }

        void Update()
        {
            if (dropped || enemy == null || enemy.health == null || Variant != StageVariant.Radiant) return;
            if (enemy.health.CurrentHealthValue > 0) return;
            dropped = true;
            if (!ChaosStages.Planned || Rivals.IsRival(enemy) || enemy is MiniBoss) return;
            if (UnityEngine.Random.value >= Plugin.RadiantHealthDropChance.Value) return;
            try { LootManager.DropHealth(transform.position, 1); } catch (Exception) { }
        }
    }

    // Prism bodies slowly cycle through the hues (the outline too, where the material has one).
    internal class PrismCycle : MonoBehaviour
    {
        SpriteRenderer[] renderers;
        Color[] originals;
        float phase;

        void Start()
        {
            renderers = GetComponentsInChildren<SpriteRenderer>(true);
            originals = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++) originals[i] = renderers[i] != null ? renderers[i].color : Color.white;
            phase = UnityEngine.Random.value;
        }

        void LateUpdate()
        {
            if (renderers == null) return;
            Color hue = Color.HSVToRGB(Mathf.Repeat(phase + Time.time * 0.25f, 1f), 0.45f, 1f);
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer sr = renderers[i];
                if (sr == null) continue;
                Color o = originals[i];
                sr.color = new Color(o.r * hue.r, o.g * hue.g, o.b * hue.b, sr.color.a);
                Material m = sr.sharedMaterial;
                if (m != null && m.HasProperty("_OutlineColor")) sr.material.SetColor("_OutlineColor", Color.HSVToRGB(Mathf.Repeat(phase + Time.time * 0.25f, 1f), 0.8f, 1f));
            }
        }

        void OnDestroy()
        {
            if (renderers == null) return;
            for (int i = 0; i < renderers.Length; i++) if (renderers[i] != null) renderers[i].color = originals[i];
        }
    }

    // Ctrl+F10 test key: the recolour engine on the player's own arcana, to judge each element's look (look only:
    // no damage or element change; infusion proper comes later).
    internal static class InfusionPreview
    {
        static readonly string[] Order = { "Fire", "Water", "Ice", "Earth", "Thunder", "Wind", "Chaos", "Radiant" };
        static int index = -1;
        internal static string Key { get { return index >= 0 ? Order[index] : null; } }

        internal static void Cycle()
        {
            index++;
            if (index >= Order.Length) index = -1;
            if (index < 0) CouncilCorruption.RestoreTints();
            string label = Key == null ? "off" : Key;
            Plugin.Log.LogInfo("RECOLOR infusion preview: " + label);
            IntroUI.Show("Infusion preview: " + label, Key == null ? "Your arcana look normal again" : "Your arcana take the " + label + " look (no damage change)",
                Key == null ? Color.white : ElementRamps.Mid(Key), null, 2f);
        }
    }

    // A Radiant / Prism rival's (or arena Archon's) chest: the arcana it drops is one of that element the player has
    // already unlocked, never a new unlock (those elements have their own unlock requirements in Arcanos). With none
    // unlocked, the chest keeps its normal drop but can't hand out a locked arcana either.
    internal class VariantChest : MonoBehaviour { public StageVariant Variant; }

    internal static class VariantRewards
    {
        internal static StageVariant Opening = StageVariant.None;

        internal static string PickUnlocked(StageVariant v)
        {
            List<string> ids = ArcanosBridge.IdsOf(v);
            ids.RemoveAll(id => !ArcanosBridge.Unlocked(id));
            List<string> owned = Traverse.Create(typeof(LootManager)).Field("currentPlayerSkills").GetValue() as List<string>;
            List<string> fresh = new List<string>(ids);
            if (owned != null) fresh.RemoveAll(id => owned.Contains(id));
            if (fresh.Count > 0) ids = fresh;
            return ids.Count > 0 ? ids[UnityEngine.Random.Range(0, ids.Count)] : null;
        }
    }

    [HarmonyPatch(typeof(TreasureChest), "Break")]
    static class VariantChestPatch
    {
        static void Prefix(TreasureChest __instance)
        {
            VariantChest vc = __instance != null ? __instance.GetComponent<VariantChest>() : null;
            VariantRewards.Opening = vc != null ? vc.Variant : StageVariant.None;
        }
        static void Finalizer() { VariantRewards.Opening = StageVariant.None; }
    }

    [HarmonyPatch(typeof(LootManager), "DropSkill")]
    static class VariantChestDropPatch
    {
        static void Prefix(ref int __1, ref string __2, ref float __3)
        {
            StageVariant v = VariantRewards.Opening;
            if (v == StageVariant.None) return;
            VariantRewards.Opening = StageVariant.None; // one per chest
            __3 = 0f; // never a locked arcana
            string id = VariantRewards.PickUnlocked(v);
            if (id == null) { Plugin.Log.LogInfo("RIVAL reward: no unlocked " + v + " arcana, normal (unlocked-only) drop"); return; }
            if (__1 <= 0) __1 = 1;
            __2 = id;
            Plugin.Log.LogInfo("RIVAL reward: " + v + " arcana " + id);
        }
    }

    // Radiant and Prism Archons (final champion and arena Archon), with Arcanos.
    internal static class Archons
    {
        internal static string MaybeVariant(string prefix)
        {
            if (!ArcanosBridge.Present || Variants.FromPrefix(prefix) != StageVariant.None) return prefix;
            if (UnityEngine.Random.value >= Plugin.VariantArchonChance.Value) return prefix;
            float ra = Mathf.Max(0f, Plugin.VariantRadiantShare.Value), p = Mathf.Max(0f, Plugin.VariantPrismShare.Value);
            if (ra + p <= 0f) return prefix;
            return UnityEngine.Random.value * (ra + p) < ra ? "Radiant" : "Prism";
        }
    }

    // Every non-player attack that belongs to a variant (Council fight, marked caster, variant stage) takes its damage
    // type and its tint as soon as its info is set.
    [HarmonyPatch(typeof(Attack), "SetAttackInfo", new Type[] { typeof(string), typeof(string), typeof(int), typeof(bool) })]
    static class VariantAttackPatch
    {
        static void Postfix(Attack __instance)
        {
            try
            {
                if (__instance == null) return;
                if (Infusion.ApplyToAttack(__instance)) return; // the player's infused arcana (v0.7.0)
                if (InfusionPreview.Key != null && (__instance.GetComponentInParent<Player>() != null || (__instance.atkInfo != null && __instance.atkInfo.entity is Player)))
                {
                    CouncilCorruption.PreviewRecolor(__instance, InfusionPreview.Key);
                    return;
                }
                if (__instance.GetComponentInParent<Player>() != null) return;
                StageVariant v = Variants.AttackVariant(__instance);
                if (v == StageVariant.None) return;
                Variants.Apply(__instance, v);
            }
            catch (Exception) { }
        }
    }

    // WoL Arcanos, read by reflection (no build reference): its two extra elements, their icons and colours, which
    // arcana belong to them and whether the player has unlocked each one.
    internal static class ArcanosBridge
    {
        static bool checkedOnce, present;
        static MethodInfo elementoDe, desbloqueado, idsDe, icono, elemento, colorDe;
        static ElementType? fulgor, prisma;
        static Type usoType;

        internal static bool Present { get { Init(); return present; } }

        static void Init()
        {
            if (checkedOnce) return;
            checkedOnce = true;
            try
            {
                Type des = AccessTools.TypeByName("WolArcanos.Desbloqueo");
                Type nuevos = AccessTools.TypeByName("WolArcanos.ElementosNuevos");
                if (des == null || nuevos == null) { Plugin.Log.LogInfo("VARIANTS: WoL Arcanos not installed: every variant stage is Chaos"); return; }
                elementoDe = AccessTools.Method(des, "ElementoDe", new[] { typeof(string) });
                desbloqueado = AccessTools.Method(des, "Desbloqueado", new[] { typeof(string) });
                idsDe = AccessTools.Method(des, "IdsDe", new[] { typeof(string) });
                usoType = AccessTools.TypeByName("WolArcanos.ElementosNuevos+Uso") ?? nuevos.GetNestedType("Uso", BindingFlags.Public | BindingFlags.NonPublic);
                if (usoType != null) icono = AccessTools.Method(nuevos, "Icono", new[] { typeof(ElementType), usoType });
                elemento = AccessTools.Method(nuevos, "Elemento", new[] { typeof(Attack), typeof(ElementType) });
                Type prismatico = AccessTools.TypeByName("WolArcanos.Prismatico");
                if (prismatico != null) colorDe = AccessTools.Method(prismatico, "ColorDe", new[] { typeof(ElementType) });
                PropertyInfo pf = AccessTools.Property(nuevos, "Fulgor"), pp = AccessTools.Property(nuevos, "Prisma");
                if (pf != null) fulgor = (ElementType)pf.GetValue(null, null);
                if (pp != null) prisma = (ElementType)pp.GetValue(null, null);
                present = elementoDe != null && fulgor.HasValue && prisma.HasValue;
                Plugin.Log.LogInfo("VARIANTS: WoL Arcanos " + (present ? "found (Radiant = element " + (int)fulgor.Value + ", Prism = element " + (int)prisma.Value + ")" : "found but its element API is missing: Chaos only"));
            }
            catch (Exception ex) { present = false; Plugin.Log.LogWarning("VARIANTS: could not read WoL Arcanos: " + ex.Message); }
        }

        internal static ElementType? ElementFor(StageVariant v)
        {
            Init();
            if (!present) return null;
            if (v == StageVariant.Radiant) return fulgor;
            if (v == StageVariant.Prism) return prisma;
            return v == StageVariant.Chaos ? (ElementType?)ElementType.Chaos : null;
        }

        // This mod's prefix for an arcana Arcanos files under Fulgor / Prisma (null otherwise).
        internal static string PrefixOf(string skillID, ElementType et)
        {
            Init();
            if (!present) return null;
            if (fulgor.HasValue && et == fulgor.Value) return "Radiant";
            if (prisma.HasValue && et == prisma.Value) return "Prism";
            try
            {
                string el = elementoDe.Invoke(null, new object[] { skillID }) as string;
                if (el == "Fulgor") return "Radiant";
                if (el == "Prisma") return "Prism";
            }
            catch (Exception) { }
            return null;
        }

        // Unlocked for the player (the game's own locked list, and Arcanos' own unlock state for its arcana).
        internal static bool Unlocked(string skillID)
        {
            if (string.IsNullOrEmpty(skillID)) return false;
            List<string> locked = null;
            try { locked = Traverse.Create(typeof(LootManager)).Field("lockedSkillList").GetValue() as List<string>; } catch (Exception) { }
            if (locked != null && locked.Contains(skillID)) return false;
            Init();
            if (present && desbloqueado != null)
            {
                try { return (bool)desbloqueado.Invoke(null, new object[] { skillID }); } catch (Exception) { }
            }
            return true;
        }

        // Every arcana Arcanos files under this variant's element (castable or not), for rewards.
        internal static List<string> IdsOf(StageVariant v)
        {
            Init();
            List<string> ids = new List<string>();
            if (!present || idsDe == null) return ids;
            try
            {
                List<string> got = idsDe.Invoke(null, new object[] { Variants.Info(v).ArcanosElement }) as List<string>;
                if (got != null) ids.AddRange(got);
            }
            catch (Exception) { }
            return ids;
        }

        internal static bool SetElement(Attack atk, ElementType et)
        {
            Init();
            if (!present || elemento == null) return false;
            try { elemento.Invoke(null, new object[] { atk, et }); return true; } catch (Exception) { return false; }
        }

        // ---------- v0.6.3: Arcanos' own art, effects and ailment checks ----------

        static Type recursos, efectos, ceguera, parcheEstados;
        static MethodInfo spriteM, ciegoM, sinControlM;
        static readonly Dictionary<string, MethodInfo> fx = new Dictionary<string, MethodInfo>();
        static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();

        static void InitArt()
        {
            Init();
            if (!present || recursos != null) return;
            recursos = AccessTools.TypeByName("WolArcanos.Recursos");
            efectos = AccessTools.TypeByName("WolArcanos.Efectos");
            ceguera = AccessTools.TypeByName("WolArcanos.Ceguera");
            parcheEstados = AccessTools.TypeByName("WolArcanos.ParcheEstadosEnemigos");
            if (recursos != null) spriteM = AccessTools.Method(recursos, "Sprite", new[] { typeof(string), typeof(float) });
            if (ceguera != null) ciegoM = AccessTools.Method(ceguera, "Ciego", new[] { typeof(Entity) });
            if (parcheEstados != null) sinControlM = AccessTools.Method(parcheEstados, "SinControl", new[] { typeof(Enemy) });
        }

        // One of Arcanos' embedded sprites (it loads them at 16 pixels per unit).
        internal static Sprite Sprite(string name)
        {
            InitArt();
            if (spriteM == null || string.IsNullOrEmpty(name)) return null;
            Sprite s;
            if (sprites.TryGetValue(name, out s)) return s;
            try { s = spriteM.Invoke(null, new object[] { name, 16f }) as Sprite; } catch (Exception) { s = null; }
            sprites[name] = s;
            if (s == null) Plugin.Log.LogWarning("ARCANOS sprite not found: " + name);
            return s;
        }

        // One of Arcanos' effects (Efectos.<name>(position, ...)); the args are converted to its parameter types.
        internal static void Fx(string name, Vector2 at, params object[] rest)
        {
            InitArt();
            if (efectos == null) return;
            string key = name + "/" + (rest.Length + 1);
            MethodInfo m;
            if (!fx.TryGetValue(key, out m))
            {
                m = null;
                foreach (MethodInfo cand in efectos.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                    if (cand.Name == name && cand.GetParameters().Length == rest.Length + 1) { m = cand; break; }
                fx[key] = m;
            }
            if (m == null) return;
            try
            {
                ParameterInfo[] ps = m.GetParameters();
                object[] args = new object[ps.Length];
                args[0] = at;
                for (int i = 1; i < ps.Length; i++) args[i] = System.Convert.ChangeType(rest[i - 1], ps[i].ParameterType);
                m.Invoke(null, args);
            }
            catch (Exception) { }
        }

        // A game sound through Arcanos' own player (Efectos.Sonido(id, position, pitch, volume)).
        internal static void Sound(string id, Vector2 at)
        {
            InitArt();
            if (efectos == null) return;
            MethodInfo m;
            if (!fx.TryGetValue("Sonido", out m)) { m = AccessTools.Method(efectos, "Sonido", new[] { typeof(string), typeof(Vector2), typeof(float), typeof(float) }); fx["Sonido"] = m; }
            if (m == null) return;
            try { m.Invoke(null, new object[] { id, at, 1f, 1f }); } catch (Exception) { }
        }

        // Blinded or out of control by one of Arcanos' ailments (Ceguera, its pulse stun and the like): such an enemy
        // shouldn't cast, and the giant watchdog shouldn't "unstick" it.
        internal static bool Incapacitated(Enemy e)
        {
            if (e == null) return false;
            InitArt();
            if (!present) return false;
            try
            {
                if (ciegoM != null && (bool)ciegoM.Invoke(null, new object[] { e })) return true;
                if (sinControlM != null && (bool)sinControlM.Invoke(null, new object[] { e })) return true;
            }
            catch (Exception) { }
            return false;
        }

        internal static Sprite Icon(StageVariant v)
        {
            ElementType? et = ElementFor(v);
            if (!et.HasValue || icono == null || usoType == null) return null;
            foreach (string uso in new[] { "Color", "Lista", "Mini" })
            {
                try
                {
                    Sprite s = icono.Invoke(null, new object[] { et.Value, Enum.Parse(usoType, uso) }) as Sprite;
                    if (s != null) return s;
                }
                catch (Exception) { }
            }
            return null;
        }
    }
}
