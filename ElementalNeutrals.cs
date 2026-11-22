using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace WolHybridBosses
{
    // v0.9.8: elemental Coffin Knights and Cyclops, like the elemental knights.
    // The game only has neutral versions of these two. On an elemental stage each one rolls
    // (config Enemies.ElementalCoffinCyclopsChance) to take the stage's element:
    //   - element on its body, an element tint and outline (their sheets have no usable elemental palette rows: the
    //     Super Coffin's rows render pink), so they read like the stage's other elemental enemies;
    //   - their own attacks deal that element with its status, in its colours (the cyclops' beam too);
    //   - one borrowed player arcana of the element (a knight-style one for the Coffin, a ranged one for the Cyclops),
    //     cast slower than a giant does.
    // On Chaos / Radiant / Prism stages they're converted with the other humanoids (ChaosEnemies.ChaosTypes).
    internal class ElementalNeutral : MonoBehaviour
    {
        public ElementType Element;
        public string Prefix;
    }

    internal static class ElementalNeutrals
    {
        internal static bool IsKind(Enemy e) { return e != null && (e.GetType() == typeof(Coffin) || e.GetType() == typeof(Cyclops) || IsBeast(e)); }

        // v0.9.14: blobs (Blob, mini Blob, Roller Blob, Blob Barrager, Super Blob) and ghouls (Ghoul, Macho Ghoul):
        // elemental look and damage only, no arcana.
        internal static bool IsBeast(Enemy e) { return e is Blob || e is BlobSpitter || e is Ghoul || e is GhoulLarge; }

        internal static ElementType ElementOf(string prefix)
        {
            switch (prefix)
            {
                case "Flame": return ElementType.Fire;
                case "Frost": return ElementType.Water;
                case "Earth": return ElementType.Earth;
                case "Wind": return ElementType.Air;
                default: return ElementType.Lightning;
            }
        }

        // Regular spawns (two frames after Start, from EliteSpawnPatch). The Giant Cyclops applies its own.
        internal static void TryApply(Enemy e)
        {
            if (!IsKind(e) || e.GetComponent<GiantCyclops>() != null || e.GetComponent<ElementalNeutral>() != null || e.GetComponent<BossEventEnemy>() != null) return;
            if (e.GetComponent<ChaosArmed>() != null || e.GetComponent<VariantMark>() != null || Level.isBossRushMode) return;
            if (Rivals.IsRival(e) || OwnerContext.OwnerOf(e) != null) return;
            bool beast = IsBeast(e);
            // v0.9.15: a Splitting elite's copies keep their parent's element / variant instead of rolling again.
            EliteSplitChild split = e.GetComponent<EliteSplitChild>();
            if (split != null)
            {
                if (split.InheritPrefix != null) Apply(e, split.InheritPrefix, beast ? 0 : 1, 1.6f);
                else if (split.InheritVariant && ChaosStages.Planned) MinibossFights.MatchStage(e, false, 0);
                return;
            }
            if (ChaosStages.Planned)
            {
                // Coffins / Cyclops are converted with the stage's humanoids (ChaosEnemies); blobs and ghouls take the
                // variant's look and attacks here, without arcana.
                if (beast && UnityEngine.Random.value < Plugin.ElementalNeutralChance.Value) MinibossFights.MatchStage(e, false, 0);
                return;
            }
            string prefix = ElementalGiants.StagePrefix();
            if (prefix == null) return; // neutral stage: neutral, like the knights there
            if (UnityEngine.Random.value >= Plugin.ElementalNeutralChance.Value) return;
            Apply(e, prefix, beast ? 0 : 1, 1.6f);
        }

        internal static void Apply(Enemy e, string prefix, int arcanaCount, float cooldown)
        {
            if (e == null || e.GetComponent<ElementalNeutral>() != null) return;
            ElementType el = ElementOf(prefix);
            ElementalNeutral mark = e.gameObject.AddComponent<ElementalNeutral>();
            mark.Element = el;
            mark.Prefix = prefix;
            e.element = el;
            ElementalGiants.RecolorOrTint(e, Infusion.RampKey(el), ElementalGiants.ElementColor(prefix)); // v0.9.9
            Cyclops cy = e as Cyclops;
            if (cy != null) TintLaser(cy, Infusion.RampKey(el));
            ConvertExisting(e, el, StageVariant.None);
            int n = 0;
            if (arcanaCount > 0)
            {
                List<ArcanaDef> picks = ElementalGiants.GapArcana(e is Coffin ? "Knight" : "Mage", prefix, arcanaCount);
                n = EnemyArcana.Give(e, picks, cooldown);
            }
            Plugin.Log.LogInfo("ELEMENTAL " + e.GetType().Name + (e.GetComponent<GiantCyclops>() != null ? " (giant)" : "") + " -> " + prefix + " (" + el + "), arcana " + n);
        }

        // v0.9.14: hitboxes set up before the element was applied (a blob's or ghoul's contact / body attack is often
        // set once in its own Start): their damage type and status change now; later attacks go through
        // VariantAttackPatch as usual. v = None: the element; otherwise the stage variant's conversion.
        internal static void ConvertExisting(Enemy e, ElementType el, StageVariant v)
        {
            if (e == null) return;
            int n = 0;
            foreach (Attack a in e.GetComponentsInChildren<Attack>(true))
            {
                if (a == null || a.atkInfo == null) continue;
                try { if (v == StageVariant.None) Infusion.ConvertEnemyInfo(a.atkInfo, el); else Variants.Convert(a, v); n++; } catch (Exception) { }
            }
            if (n > 0) Plugin.Log.LogInfo("ELEMENTAL " + e.GetType().Name + ": " + n + " existing hitbox(es) converted");
        }

        // The beam's colours are fields the laser re-applies every time it charges and fires, so they're set there.
        internal static void TintLaser(Cyclops cy, string key)
        {
            if (cy == null || key == null || !ElementRamps.Has(key)) return;
            try
            {
                Laser l = Traverse.Create(cy).Field("currentLaser").GetValue<Laser>();
                if (l == null) return;
                Traverse t = Traverse.Create(l);
                foreach (string f in new[] { "chargeColor0", "chargeColor1", "chargeColor2", "shootColor0", "shootColor1", "shootColor2" })
                    t.Field(f).SetValue(ElementRamps.Map(key, t.Field(f).GetValue<Color>()));
            }
            catch (Exception) { }
        }

        // Their attacks: the element's damage type, status and colours (from VariantAttackPatch).
        internal static bool ConvertAttack(Attack atk)
        {
            if (atk == null || atk.atkInfo == null) return false;
            Entity owner = atk.atkInfo.entity;
            if (owner == null) owner = atk.GetComponentInParent<Entity>();
            if (owner == null || owner is Player) return false;
            ElementalNeutral m = owner.GetComponent<ElementalNeutral>();
            if (m == null) return false;
            try
            {
                Infusion.ConvertEnemyInfo(atk.atkInfo, m.Element);
                string key = Infusion.RampKey(m.Element);
                if (key != null) CouncilCorruption.TintAttack(atk, true, StageVariant.Chaos, null, key);
            }
            catch (Exception) { }
            return true;
        }
    }

    // ---------- the Giant Cyclops ----------
    //
    // A mini-boss made from the Cyclops (the game has no Super Cyclops): replaces a giant spawn now and then
    // (config Enemies.GiantCyclopsChance; not in boss / Council rooms).
    //   - 1.9x size, mini-boss health, the mini-boss chest, the giant intro card and boss bar.
    //   - Its beam is about 2.4x thicker (the laser's hitbox height follows its line thickness), reaches farther and
    //     hits 1.5x harder.
    //   - Stun phases like the other giants: it's armoured (no hurt stagger, no knockback: the same switches
    //     MiniBoss uses) until it has taken 20% of its health since the armour went up; then the armour breaks and
    //     it's stunned for 3.5s (held in its Hurt state, beam off), open to combos; then the armour comes back.
    //   - Elemental like the others: the stage's element (or a random one by the giant element chance), 2 arcana;
    //     on Chaos / Radiant / Prism stages a giant of that variant.
    internal class GiantCyclops : MonoBehaviour
    {
        Enemy enemy;
        int maxHp, hpAtArmour;
        bool armoured, started;
        float stunUntil, nextHold;
        static readonly BoolVarStatMod ArmourMod = new BoolVarStatMod("HB_GiantCyclopsArmour", true, 10);
        static readonly Color ArmourColour = new Color(0.75f, 0.85f, 1f);

        internal static bool Pending; // set while a giant spawn is being swapped for a Cyclops

        internal static Enemy SpawnAt(Vector3 pos)
        {
            Pending = true; // the spawn patch's postfix marks it
            Enemy e = null;
            try { e = Enemy.Spawn("Cyclops", pos, null, true, true, false, false, false); }
            finally { Pending = false; }
            if (e != null) Mark(e);
            return e;
        }

        internal static void Mark(Enemy e)
        {
            if (e == null || !(e is Cyclops) || e.GetComponent<GiantCyclops>() != null) return;
            e.gameObject.AddComponent<GiantCyclops>();
            e.transform.localScale = e.transform.localScale * 1.9f;
            e.dropLootChest = true;
            e.chestLootTableID = "TreasureChestMiniBoss";
        }

        internal static bool IsGiant(Enemy e) { return e != null && e.GetComponent<GiantCyclops>() != null; }

        IEnumerator Start()
        {
            enemy = GetComponent<Enemy>();
            yield return null;
            yield return null;
            if (enemy == null || enemy.health == null) yield break;
            int tier = Mathf.Max(0, GameController.tierCount);
            int hp = Mathf.RoundToInt((900 + 350 * tier) * (GameController.loopCount >= 1 ? 1.5f : 1f));
            try { enemy.health.healthStat.AddMod(new NumVarStatMod("HB_GiantCyclopsHP", hp, 10, VarStatModType.Override, true)); } catch (Exception) { }
            try { enemy.health.CurrentHealthValue = hp; } catch (Exception) { }
            maxHp = Mathf.Max(1, enemy.health.CurrentHealthValue);
            enemy.dropLootChest = true;
            enemy.chestLootTableID = "TreasureChestMiniBoss";

            // Element, like the other giants.
            string prefix = "Neutral";
            if (ChaosStages.Planned) { if (enemy.GetComponent<ChaosArmed>() == null) ChaosGiants.Apply(enemy); prefix = Variants.Info(ChaosStages.PlannedVariant).Prefix ?? "Chaos"; }
            else
            {
                string stage = ElementalGiants.StagePrefix();
                if (stage != null) prefix = stage; // v0.9.9: always the stage's element there
                else if (UnityEngine.Random.value < Plugin.ElementalGiantChance.Value) prefix = ElementalGiants.RandomPrefix();
                if (prefix != "Neutral") ElementalNeutrals.Apply(enemy, prefix, 2, 1f);
            }
            if (Plugin.GiantIntros.Value && GetComponent<BossEventEnemy>() == null) GiantIntro.Attach(enemy, prefix, "Cyclops"); // the Eye Court has its own card
            SetArmour(true);
            started = true;
            Plugin.Log.LogInfo("GIANT Cyclops ready: HP " + maxHp + ", " + prefix);
        }

        void SetArmour(bool on)
        {
            armoured = on;
            try { enemy.health.ignoreHurtVarStat.Modify(ArmourMod, on); } catch (Exception) { }
            try { enemy.health.ignoreKnockbackVarStat.Modify(ArmourMod, on); } catch (Exception) { }
            if (on) hpAtArmour = enemy.health.CurrentHealthValue;
            // v0.9.15: while stunned, the outline it had before its armour first went up (its element / variant
            // outline; the old "element of its prefix" showed grey on Chaos stages).
            if (outlineCache == null) { outlineCache = new OutlineCache(this); originalOutline = outlineCache.Current ?? ElementalGiants.ElementColor(Prefix()); }
            outlineCache.Set(on ? ArmourColour : originalOutline);
        }

        OutlineCache outlineCache;
        Color originalOutline;

        string Prefix() { ElementalNeutral m = GetComponent<ElementalNeutral>(); return m != null ? m.Prefix : "Neutral"; }

        void Outline(Color c)
        {
            foreach (SpriteRenderer sr in GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sr == null) continue;
                Material sm = sr.sharedMaterial;
                if (sm == null || !sm.HasProperty("_OutlineColor")) continue;
                sr.material.SetColor("_OutlineColor", c);
            }
        }

        void Update()
        {
            if (!started || enemy == null || enemy.health == null || enemy.health.CurrentHealthValue <= 0) return;
            if (armoured)
            {
                if (hpAtArmour - enemy.health.CurrentHealthValue >= maxHp * 0.2f) Break();
                return;
            }
            if (Time.time >= stunUntil) { SetArmour(true); Plugin.Log.LogInfo("GIANT Cyclops armour back"); return; }
            // Stunned: held in its hurt state, beam off.
            if (Time.time >= nextHold)
            {
                nextHold = Time.time + 0.25f;
                try
                {
                    string st = enemy.fsm != null ? enemy.fsm.currentStateName ?? "" : "";
                    if (!st.Contains("Hurt") && !st.Contains("Dead") && !st.Contains("Frozen") && !st.StartsWith("WolArcanos") && enemy.fsm.HasState("Hurt"))
                        enemy.fsm.ChangeState("Hurt", true);
                }
                catch (Exception) { }
                try { Laser l = Traverse.Create(enemy).Field("currentLaser").GetValue<Laser>(); if (l != null) l.Deactivate(); } catch (Exception) { }
            }
        }

        void Break()
        {
            SetArmour(false);
            stunUntil = Time.time + 3.5f;
            nextHold = 0f;
            try { CameraController.ShakeCamera(0.8f, false); } catch (Exception) { }
            try
            {
                ParticleEffect fx = PoolManager.GetPoolItem<ParticleEffect>("ArmorDissipateEffect");
                if (fx != null) fx.Emit(15, (Vector3?)transform.position, null, null, 0f, null, null);
            }
            catch (Exception) { }
            Plugin.Log.LogInfo("GIANT Cyclops armour broken: stunned 3.5s");
            try { MinibossFights.OnCyclopsBreak(enemy); } catch (Exception) { } // v0.9.11: the Eye Court
        }
    }

    // The giant's beam: thicker, longer. InitLaser runs in Cyclops.Start, after the giant mark is on.
    [HarmonyPatch(typeof(Cyclops), "InitLaser")]
    static class GiantCyclopsLaserPatch
    {
        static void Postfix(Cyclops __instance)
        {
            try
            {
                if (!GiantCyclops.IsGiant(__instance)) return;
                Laser l = Traverse.Create(__instance).Field("currentLaser").GetValue<Laser>();
                if (l == null) return;
                Traverse t = Traverse.Create(l);
                foreach (string f in new[] { "shootThickness0", "shootThickness1", "shootThickness2" }) t.Field(f).SetValue(t.Field(f).GetValue<float>() * 2.4f);
                foreach (string f in new[] { "chargeThickness0", "chargeThickness1", "chargeThickness2" }) t.Field(f).SetValue(t.Field(f).GetValue<float>() * 2f);
                foreach (string f in new[] { "randomSizeRange0", "randomSizeRange1", "randomSizeRange2" }) t.Field(f).SetValue(t.Field(f).GetValue<Vector2>() * 2.4f);
                l.maxRange = Mathf.Max(l.maxRange, 26f);
                // The giant's 1.9x body scale would also stretch the beam's hitbox if the laser is parented to it: undo it.
                Transform parent = l.transform.parent;
                if (parent != null)
                {
                    Vector3 ps = parent.lossyScale;
                    l.transform.localScale = new Vector3(ps.x != 0f ? 1f / Mathf.Abs(ps.x) : 1f, ps.y != 0f ? 1f / Mathf.Abs(ps.y) : 1f, 1f);
                }
            }
            catch (Exception ex) { Plugin.Log.LogWarning("GIANT Cyclops laser setup failed: " + ex.Message); }
        }
    }

    // v0.9.11: the special mini-boss pool. Now and then a giant spawn (Super Knight / Mage / Archer / Lancer /
    // Summoner / Coffin; never the Super Rogue, whose clones use Spawn too; never boss rooms) becomes one of the
    // special mini-bosses (MinibossFights): Giant Cyclops, Mimic King, Cyclops Eye Court or Elemental Conclave.
    static class MinibossSwap
    {
        static readonly HashSet<string> Giants = new HashSet<string> { "SuperKnight", "SuperMage", "SuperArcher", "SuperLancer", "SuperSummoner", "SuperCoffin" };
        internal static MinibossFight? Pending; // set while a giant spawn is being swapped (cleared by the postfix)

        // The replacement enemy name, or null to keep the giant.
        internal static string Roll(string key)
        {
            if (Pending.HasValue || GiantCyclops.Pending || key == null || !Giants.Contains(key)) return null;
            if (!Plugin.BossFightsEnabled.Value || Plugin.MinibossFightChance.Value <= 0f || Level.isBossRushMode) return null;
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name ?? "";
            if (scene == "Hub" || scene == "Tutorial" || scene.Contains("BossLevel") || scene.Contains("PvP")) return null;
            if (UnityEngine.Object.FindObjectOfType<BossRoomEventHandler>() != null) return null;
            if (UnityEngine.Random.value >= Plugin.MinibossFightChance.Value) return null;
            MinibossFight f = (MinibossFight)UnityEngine.Random.Range(0, MinibossFights.Count);
            Pending = f;
            string to = MinibossFights.KeyFor(f);
            Plugin.Log.LogInfo("MINIBOSS spawn " + key + " becomes " + MinibossFights.Title(f) + " (" + to + ")");
            return to;
        }

        internal static void Done(Enemy result)
        {
            if (!Pending.HasValue) return;
            MinibossFight f = Pending.Value;
            Pending = null; // also when the spawn failed, so the next spawn isn't taken for one
            if (result != null) MinibossFights.Begin(result, f);
        }
    }

    [HarmonyPatch(typeof(Enemy), "Spawn", new Type[] { typeof(string), typeof(Vector3), typeof(Transform), typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(bool) })]
    static class MinibossSpawnByName
    {
        static void Prefix(ref string __0) { try { string to = MinibossSwap.Roll(__0); if (to != null) __0 = to; } catch (Exception) { } }
        static void Postfix(Enemy __result) { try { MinibossSwap.Done(__result); } catch (Exception) { } }
    }

    [HarmonyPatch(typeof(Enemy), "Spawn", new Type[] { typeof(Enemy.EName), typeof(Vector3), typeof(Transform), typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(bool) })]
    static class MinibossSpawnByEnum
    {
        static void Prefix(ref Enemy.EName __0)
        {
            try { string to = MinibossSwap.Roll(__0.ToString()); if (to != null) __0 = (Enemy.EName)Enum.Parse(typeof(Enemy.EName), to); } catch (Exception) { }
        }
        static void Postfix(Enemy __result) { try { MinibossSwap.Done(__result); } catch (Exception) { } }
    }

    // Its beam hits harder than a regular cyclops'.
    [HarmonyPatch(typeof(Health), "TakeDamage")]
    static class GiantCyclopsDamagePatch
    {
        static void Prefix(Health __instance, AttackInfo __0, out int __state)
        {
            __state = -1;
            try
            {
                if (__0 == null || !Elites.IsPlayers(__instance)) return;
                Enemy e = __0.entity as Enemy;
                if (!GiantCyclops.IsGiant(e)) return;
                __state = __0.damage;
                __0.damage = Mathf.RoundToInt(__0.damage * 1.5f);
            }
            catch (Exception) { }
        }

        static void Postfix(AttackInfo __0, int __state) { if (__state >= 0 && __0 != null) __0.damage = __state; }
    }
}
