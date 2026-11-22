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
        internal static bool IsKind(Enemy e) { return e != null && (e.GetType() == typeof(Coffin) || e.GetType() == typeof(Cyclops)); }

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
            if (!IsKind(e) || e.GetComponent<GiantCyclops>() != null || e.GetComponent<ElementalNeutral>() != null) return;
            if (e.GetComponent<ChaosArmed>() != null || ChaosStages.Planned || Level.isBossRushMode) return;
            if (Rivals.IsRival(e) || OwnerContext.OwnerOf(e) != null) return;
            string prefix = ElementalGiants.StagePrefix();
            if (prefix == null) return; // neutral stage: neutral, like the knights there
            if (UnityEngine.Random.value >= Plugin.ElementalNeutralChance.Value) return;
            Apply(e, prefix, 1, 1.6f);
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
            int n = 0;
            if (arcanaCount > 0)
            {
                List<ArcanaDef> picks = ElementalGiants.GapArcana(e is Coffin ? "Knight" : "Mage", prefix, arcanaCount);
                n = EnemyArcana.Give(e, picks, cooldown);
            }
            Plugin.Log.LogInfo("ELEMENTAL " + e.GetType().Name + (e.GetComponent<GiantCyclops>() != null ? " (giant)" : "") + " -> " + prefix + " (" + el + "), arcana " + n);
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
            if (Plugin.GiantIntros.Value) GiantIntro.Attach(enemy, prefix, "Cyclops");
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
            Outline(on ? ArmourColour : ElementalGiants.ElementColor(Prefix()));
        }

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

    // Now and then a giant spawn becomes a Giant Cyclops (never the Super Rogue's clones, never boss rooms).
    static class GiantCyclopsSwap
    {
        static readonly HashSet<string> Giants = new HashSet<string> { "SuperKnight", "SuperMage", "SuperArcher", "SuperLancer", "SuperSummoner", "SuperCoffin" };

        internal static bool Roll(string key)
        {
            if (GiantCyclops.Pending || key == null || !Giants.Contains(key)) return false;
            if (Plugin.GiantCyclopsChance.Value <= 0f || Level.isBossRushMode) return false;
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name ?? "";
            if (scene == "Hub" || scene == "Tutorial" || scene.Contains("BossLevel") || scene.Contains("PvP")) return false;
            if (UnityEngine.Object.FindObjectOfType<BossRoomEventHandler>() != null) return false;
            if (UnityEngine.Random.value >= Plugin.GiantCyclopsChance.Value) return false;
            Plugin.Log.LogInfo("GIANT spawn " + key + " becomes a Giant Cyclops");
            GiantCyclops.Pending = true;
            return true;
        }

        internal static void Done(Enemy result)
        {
            if (!GiantCyclops.Pending) return;
            GiantCyclops.Pending = false; // also when the spawn failed, so the next spawn isn't taken for a giant
            if (result != null) GiantCyclops.Mark(result);
        }
    }

    [HarmonyPatch(typeof(Enemy), "Spawn", new Type[] { typeof(string), typeof(Vector3), typeof(Transform), typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(bool) })]
    static class GiantCyclopsSpawnByName
    {
        static void Prefix(ref string __0) { try { if (GiantCyclopsSwap.Roll(__0)) __0 = "Cyclops"; } catch (Exception) { } }
        static void Postfix(Enemy __result) { try { GiantCyclopsSwap.Done(__result); } catch (Exception) { } }
    }

    [HarmonyPatch(typeof(Enemy), "Spawn", new Type[] { typeof(Enemy.EName), typeof(Vector3), typeof(Transform), typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(bool) })]
    static class GiantCyclopsSpawnByEnum
    {
        static void Prefix(ref Enemy.EName __0) { try { if (GiantCyclopsSwap.Roll(__0.ToString())) __0 = Enemy.EName.Cyclops; } catch (Exception) { } }
        static void Postfix(Enemy __result) { try { GiantCyclopsSwap.Done(__result); } catch (Exception) { } }
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
