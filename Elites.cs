using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace WolHybridBosses
{
    // v0.8.0: elite enemies (ROADMAP 7a). Now and then a regular enemy spawns with one trait that changes how you fight
    // it, a coloured outline and a label: Shielded (the game's own shield on top of its health), Hasted (faster, with
    // after-images), Splitting (two smaller copies on death), Draining (heals from the damage it deals), Blinking
    // (short teleports around you).
    // v0.9.7: seven more.
    //   Volatile    on death, a casting circle, then an explosion in its element (dash out of it).
    //   Vengeful    on death its soul flies to the nearest enemy: healed, faster and bigger (stacks up to 3).
    //   Haunting    on death a slow ghost of it drifts after you and bursts on contact (outrun it).
    //   Anchored    no knockback and no hurt stagger, and it pulls you gently toward it.
    //   Overcharged its next hit on you deals double; it recharges 8s after landing one (glowing = charged).
    //   Infused     its attacks carry a second element (damage type, status and colours). No arcana drop.
    //   Thief       its hits steal gold, then it blinks away; kill it to get double what it took back.
    // Every elite drops extra gold, more for the riskier traits (Elites.GoldFor), and, except Infused, sometimes
    // an infused arcana.
    internal enum EliteTrait { Shielded, Hasted, Splitting, Draining, Blinking, Volatile, Vengeful, Haunting, Anchored, Overcharged, Infused, Thief }

    internal static class Elites
    {
        internal const int TraitCount = 12;

        internal static float Chance
        {
            get
            {
                float c = 0.04f + 0.03f * Mathf.Max(0, GameController.tierCount) + (GameController.loopCount >= 1 ? 0.04f : 0f);
                return c * Mathf.Max(0f, Plugin.EliteChanceMultiplier.Value);
            }
        }

        internal static bool Eligible(Enemy e)
        {
            if (e == null || e.health == null || e is MiniBoss || e is Boss || Rivals.IsRival(e)) return false;
            if (e.GetComponent<EliteMark>() != null || e.GetComponent<EliteSplitChild>() != null || GiantCyclops.IsGiant(e) || e.GetComponent<BossEventEnemy>() != null) return false;
            if (OwnerContext.OwnerOf(e) != null || e.GetComponent<ArcanaOwned>() != null) return false; // summons of our casters
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (scene == "Hub" || scene == "Tutorial" || scene.Contains("BossLevel") || scene.Contains("PvP") || Level.isBossRushMode) return false;
            if (UnityEngine.Object.FindObjectOfType<BossRoomEventHandler>() != null) return false; // Council / final rooms
            return e.health.CurrentHealthValue > 0;
        }

        internal static void TryMake(Enemy e)
        {
            if (!Plugin.ElitesEnabled.Value || !Eligible(e)) return;
            if (UnityEngine.Random.value >= Chance) return;
            Make(e, (EliteTrait)UnityEngine.Random.Range(0, TraitCount));
        }

        internal static void Make(Enemy e, EliteTrait trait)
        {
            if (e == null || e.GetComponent<EliteMark>() != null) return;
            EliteMark m = e.gameObject.AddComponent<EliteMark>();
            m.Trait = trait;
            if (trait == EliteTrait.Infused) m.InfusedElement = InfusionFor(e);
            Plugin.Log.LogInfo("ELITE " + e.GetType().Name + " is " + trait + (trait == EliteTrait.Infused ? " (" + Infusion.Name(m.InfusedElement) + ")" : ""));
        }

        // Infused: a base element other than its own (Chaos now and then).
        static ElementType InfusionFor(Enemy e)
        {
            List<ElementType> options = new List<ElementType> { ElementType.Fire, ElementType.Water, ElementType.Ice, ElementType.Earth, ElementType.Air, ElementType.Lightning };
            if (UnityEngine.Random.value < 0.15f) options.Add(ElementType.Chaos);
            string own = Infusion.Name(e.element);
            options.RemoveAll(x => Infusion.Name(x) == own || (e.element == ElementType.Water && x == ElementType.Ice) || (e.element == ElementType.Ice && x == ElementType.Water));
            return options[UnityEngine.Random.Range(0, options.Count)];
        }

        internal static Color ColorOf(EliteTrait t)
        {
            switch (t)
            {
                case EliteTrait.Shielded: return new Color(1f, 0.85f, 0.3f);
                case EliteTrait.Hasted: return new Color(0.4f, 1f, 1f);
                case EliteTrait.Splitting: return new Color(0.7f, 1f, 0.55f);
                case EliteTrait.Draining: return new Color(1f, 0.25f, 0.25f);
                case EliteTrait.Volatile: return new Color(1f, 0.5f, 0.15f);
                case EliteTrait.Vengeful: return new Color(0.75f, 0.1f, 0.2f);
                case EliteTrait.Haunting: return new Color(0.8f, 0.95f, 1f);
                case EliteTrait.Anchored: return new Color(0.65f, 0.6f, 0.55f);
                case EliteTrait.Overcharged: return new Color(1f, 1f, 0.35f);
                case EliteTrait.Infused: return Color.white; // the element's colour (EliteMark.Colour)
                case EliteTrait.Thief: return new Color(1f, 0.78f, 0.2f);
                default: return new Color(0.75f, 0.5f, 1f);
            }
        }

        // Extra gold on death, on top of the enemy's own drop: base 15 + 10 per tier, more for riskier traits.
        internal static int GoldFor(EliteTrait t)
        {
            float mult;
            switch (t)
            {
                case EliteTrait.Overcharged: mult = 1.4f; break;
                case EliteTrait.Shielded: case EliteTrait.Vengeful: case EliteTrait.Infused: case EliteTrait.Anchored: mult = 1.25f; break;
                case EliteTrait.Hasted: case EliteTrait.Draining: case EliteTrait.Blinking: case EliteTrait.Volatile: case EliteTrait.Haunting: mult = 1.15f; break;
                default: mult = 1f; break; // Splitting (its copies drop too), Thief (pays back double what it stole)
            }
            return Mathf.RoundToInt((15 + 10 * Mathf.Max(0, GameController.tierCount)) * mult * (GameController.loopCount >= 1 ? 1.25f : 1f));
        }

        // A readable name: "FireKnight" -> "Knight".
        internal static string NameOf(Enemy e)
        {
            string n = e.GetType().Name;
            foreach (string p in new[] { "Flame", "Fire", "Frost", "Ice", "Water", "Earth", "Wind", "Air", "Thunder", "Lightning", "Chaos", "Neutral" })
                if (n.StartsWith(p) && n.Length > p.Length) return n.Substring(p.Length);
            return n;
        }

        internal static Player Player0 { get { return GameController.playerScripts != null && GameController.playerScripts.Length > 0 ? GameController.playerScripts[0] : null; } }

        internal static bool IsPlayers(Health h)
        {
            if (h == null || GameController.playerScripts == null) return false;
            foreach (Player p in GameController.playerScripts) if (p != null && p.health == h) return true;
            return false;
        }

        // Infused elites: their attacks take the element (damage type, status, colours). Called from VariantAttackPatch.
        internal static bool InfuseAttack(Attack atk)
        {
            if (atk == null || atk.atkInfo == null) return false;
            Entity owner = atk.atkInfo.entity;
            if (owner == null) owner = atk.GetComponentInParent<Entity>();
            if (owner == null || owner is Player) return false;
            EliteMark m = owner.GetComponent<EliteMark>();
            if (m == null || m.Trait != EliteTrait.Infused) return false;
            try
            {
                Infusion.ConvertEnemyInfo(atk.atkInfo, m.InfusedElement);
                string key = Infusion.RampKey(m.InfusedElement);
                if (key != null) CouncilCorruption.TintAttack(atk, true, StageVariant.Chaos, null, key);
            }
            catch (Exception) { }
            return true;
        }

        // Ctrl+F7 test key: the nearest enemy becomes the next elite trait.
        static int testIndex;
        internal static void TestNearest()
        {
            Player p = Player0;
            if (p == null) return;
            Enemy best = null;
            float bd = float.MaxValue;
            foreach (Enemy e in UnityEngine.Object.FindObjectsOfType<Enemy>())
            {
                if (e == null || e is MiniBoss || e is Boss || e.health == null || e.health.CurrentHealthValue <= 0 || e.GetComponent<EliteMark>() != null) continue;
                float d = Vector2.Distance(e.transform.position, p.transform.position);
                if (d < bd) { bd = d; best = e; }
            }
            if (best == null) { Plugin.Log.LogInfo("ELITE test: no enemy nearby"); return; }
            Make(best, (EliteTrait)(testIndex++ % TraitCount));
        }
    }

    // A Splitting elite's copy. v0.9.15: it keeps its parent's element / variant (ElementalNeutrals.TryApply reads
    // these instead of rolling again).
    internal class EliteSplitChild : MonoBehaviour
    {
        public string InheritPrefix;
        public bool InheritVariant;
    }

    internal class EliteMark : MonoBehaviour
    {
        public EliteTrait Trait;
        public ElementType InfusedElement = ElementType.Neutral;
        public bool Charged = true;      // Overcharged
        public int Stolen;               // Thief
        float rechargeAt, fleeAt = -1f;
        Enemy enemy;
        int maxHp;
        bool dead, applied;
        float nextBlink, nextImage, nextSpark;
        Vector2 lastPos;
        static GUIStyle style;
        static Font styleFont;

        internal Color Colour { get { return Trait == EliteTrait.Infused ? Infusion.ColorOf(InfusedElement) : Elites.ColorOf(Trait); } }

        string Label
        {
            get
            {
                string name = Elites.NameOf(enemy);
                if (Trait == EliteTrait.Infused) return Infusion.Name(InfusedElement) + "-infused " + name;
                if (Trait == EliteTrait.Thief && Stolen > 0) return "Thief " + name + " (" + Stolen + " gold)";
                if (Trait == EliteTrait.Overcharged && !Charged) return "Overcharged " + name + " (recharging)";
                return Trait + " " + name;
            }
        }

        void Start()
        {
            enemy = GetComponent<Enemy>();
            if (enemy == null || enemy.health == null) { Destroy(this); return; }
            maxHp = Mathf.Max(1, enemy.health.CurrentHealthValue);
            nextBlink = Time.time + UnityEngine.Random.Range(2.5f, 4f);
            lastPos = transform.position;
            Apply();
        }

        void Apply()
        {
            if (applied) return;
            applied = true;
            SetOutline(Colour);
            switch (Trait)
            {
                case EliteTrait.Shielded:
                    try { enemy.health.CurrentShieldValue = Mathf.Max(1, Mathf.RoundToInt(maxHp * 0.4f)); } catch (Exception) { }
                    break;
                case EliteTrait.Hasted:
                    try { enemy.movement.moveSpeedStat.AddMod(new NumVarStatMod("HB_EliteHaste", 1.35f, 10, VarStatModType.Multiplicative, false)); } catch (Exception) { }
                    break;
                case EliteTrait.Anchored:
                    // The game's own switches (Earth Knight's shield state uses the same ones).
                    try { enemy.health.ignoreKnockbackVarStat.AddMod(new BoolVarStatMod("HB_EliteAnchored", true, 10)); } catch (Exception) { }
                    try { enemy.health.ignoreHurtVarStat.AddMod(new BoolVarStatMod("HB_EliteAnchored", true, 10)); } catch (Exception) { }
                    transform.localScale = transform.localScale * 1.1f;
                    break;
            }
        }

        OutlineCache outline; // v0.9.15: cached materials (this runs every frame)

        void SetOutline(Color c)
        {
            if (outline == null) outline = new OutlineCache(this);
            outline.Set(c);
        }

        void Update()
        {
            if (enemy == null || dead) return;
            if (enemy.health == null || enemy.health.CurrentHealthValue <= 0) { dead = true; OnDeath(); return; }
            // A gentle pulse of the trait colour on the outline (Overcharged flickers fast while charged).
            Color c = Colour;
            bool flicker = Trait == EliteTrait.Overcharged && Charged;
            float pulse = flicker ? 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * 14f)) : 0.65f + 0.35f * Mathf.Sin(Time.time * 4f);
            if (Trait == EliteTrait.Overcharged && !Charged) pulse *= 0.5f;
            SetOutline(new Color(c.r * pulse, c.g * pulse, c.b * pulse, 1f));
            if (Trait == EliteTrait.Hasted)
            {
                if (enemy.anim != null && enemy.anim.speed < 1.3f) enemy.anim.speed = 1.3f;
                if (Time.time >= nextImage && ((Vector2)transform.position - lastPos).sqrMagnitude > 0.0004f) { nextImage = Time.time + 0.09f; AfterImage(new Color(0.4f, 1f, 1f, 0.45f)); }
            }
            if (Trait == EliteTrait.Blinking && Time.time >= nextBlink) Blink(true);
            if (Trait == EliteTrait.Anchored) Pull();
            if (Trait == EliteTrait.Overcharged)
            {
                if (!Charged && Time.time >= rechargeAt) Charged = true;
                if (Charged && Time.time >= nextSpark) { nextSpark = Time.time + 0.25f; AfterImage(new Color(1f, 1f, 0.5f, 0.35f)); }
            }
            if (Trait == EliteTrait.Thief && fleeAt > 0f && Time.time >= fleeAt) { fleeAt = -1f; Blink(false); }
            lastPos = transform.position;
        }

        // A fading copy of the current frame where it is (Hasted after-images, Overcharged sparks).
        void AfterImage(Color tint)
        {
            SpriteRenderer src = enemy.spriteRenderer;
            if (src == null || src.sprite == null) return;
            GameObject go = new GameObject("HB_EliteAfterImage");
            go.transform.position = src.transform.position;
            go.transform.rotation = src.transform.rotation;
            go.transform.localScale = src.transform.lossyScale;
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = src.sprite;
            sr.flipX = src.flipX;
            sr.sortingLayerID = src.sortingLayerID;
            sr.sortingOrder = src.sortingOrder - 1;
            sr.color = tint;
            go.AddComponent<FadeOut>().Seconds = 0.3f;
        }

        // Blinking: a short teleport to a free spot near the player, every few seconds while it's hunting.
        // Thief (toward = false): away from the player after stealing.
        void Blink(bool toward)
        {
            nextBlink = Time.time + UnityEngine.Random.Range(3.5f, 5f);
            Player p = Elites.Player0;
            if (p == null || p.IsDead || ArcanosBridge.Incapacitated(enemy)) return;
            string st = enemy.fsm != null ? enemy.fsm.currentStateName ?? "" : "";
            if (st.Contains("Dead") || st.Contains("Frozen") || st.Contains("Fall") || st.StartsWith("WolArcanos")) return;
            if (toward && st.Contains("Hurt")) return;
            float d = Vector2.Distance(p.transform.position, transform.position);
            Vector2 to;
            if (toward)
            {
                if (d > 12f || d < 2f) return;
                to = (Vector2)p.transform.position + ArcanaCaster.Rotate(Vector2.right, UnityEngine.Random.value * 360f) * 3.5f;
            }
            else
            {
                Vector2 away = (Vector2)transform.position - (Vector2)p.transform.position;
                if (away.sqrMagnitude < 0.01f) away = Vector2.right;
                to = (Vector2)transform.position + ArcanaCaster.Rotate(away.normalized, UnityEngine.Random.Range(-35f, 35f)) * 7f;
            }
            try { to = Entity.GetAvailableNearbyLocation(to, false, false); } catch (Exception) { return; }
            if (Physics2D.Linecast(transform.position, to, LayerMask.GetMask("Wall"))) return; // not through a wall
            ArcanosBridge.Fx("Teletransporte", transform.position);
            AfterImage(new Color(1f, 1f, 1f, 0.5f));
            transform.position = to;
            ArcanosBridge.Fx("Teletransporte", to);
        }

        // Anchored: the player drifts toward it while within reach (never through a wall).
        void Pull()
        {
            Player p = Elites.Player0;
            if (p == null || p.IsDead) return;
            Vector2 pp = p.transform.position;
            Vector2 toMe = (Vector2)transform.position - pp;
            float d = toMe.magnitude;
            if (d > 7f || d < 1.6f) return;
            Vector2 step = toMe / d * 1.3f * Time.deltaTime;
            if (Physics2D.Linecast(pp, pp + step * 6f, LayerMask.GetMask("Wall", "Pit", "Obstacle"))) return;
            p.transform.position = (Vector3)(pp + step) + new Vector3(0f, 0f, p.transform.position.z);
        }

        // Overcharged: a hit landed (EliteHitPatch).
        internal void Discharged() { Charged = false; rechargeAt = Time.time + 8f; }

        // Thief: a hit landed; take some gold, then blink away.
        internal void Steal()
        {
            int cap = 60 + 20 * Mathf.Max(0, GameController.tierCount);
            int bal = Player.goldWallet != null ? Player.goldWallet.balance : 0;
            int take = Mathf.Min(bal, 8 + 4 * Mathf.Max(0, GameController.tierCount), cap - Stolen);
            if (take <= 0) return;
            try { if (!Player.goldWallet.Withdraw(take)) return; } catch (Exception) { return; }
            Stolen += take;
            fleeAt = Time.time + 0.35f;
            Plugin.Log.LogInfo("ELITE Thief stole " + take + " gold (" + Stolen + " carried)");
        }

        void OnDeath()
        {
            Vector2 at = transform.position;
            int tier = Mathf.Max(0, GameController.tierCount);
            // Extra gold (Thief: plus double what it stole), and sometimes an infused arcana (not from Infused elites).
            try { LootManager.DropGold(at, Elites.GoldFor(Trait) + Stolen * 2); } catch (Exception) { }
            if (Trait != EliteTrait.Infused && UnityEngine.Random.value < Plugin.EliteInfusedDropChance.Value)
            {
                try
                {
                    InfusionDropPatch.ForceNext = true;
                    LootManager.DropSkill(at, 1, null, 0f, 0f, null, false, false);
                }
                catch (Exception ex) { Plugin.Log.LogWarning("ELITE arcana drop failed: " + ex.Message); }
                finally { InfusionDropPatch.ForceNext = false; }
            }
            switch (Trait)
            {
                case EliteTrait.Splitting: Split(at); break;
                case EliteTrait.Volatile:
                    EliteBlast.Spawn(at, 2.4f, 30 + 12 * tier, enemy.element, enemy, 0.9f);
                    Plugin.Log.LogInfo("ELITE Volatile " + enemy.GetType().Name + " is about to burst");
                    break;
                case EliteTrait.Vengeful: Vengeance(at); break;
                case EliteTrait.Haunting: EliteGhost.Spawn(enemy, at, 20 + 8 * tier); break;
            }
        }

        // Splitting: two smaller copies of the same enemy, at 40% of its health.
        void Split(Vector2 at)
        {
            string key = gameObject.name.Replace("(Clone)", "").Trim();
            for (int i = 0; i < 2; i++)
            {
                Vector2 pos = at + new Vector2(i == 0 ? -0.9f : 0.9f, 0f);
                try { pos = Entity.GetAvailableNearbyLocation(pos, false, false); } catch (Exception) { }
                Enemy copy = null;
                foreach (string k in new[] { key, enemy.GetType().Name })
                {
                    try { copy = Enemy.Spawn(k, pos, null, false, true, false, false, false); } catch (Exception) { copy = null; }
                    if (copy != null) break;
                }
                if (copy == null) { Plugin.Log.LogWarning("ELITE split: could not spawn a copy of " + key); return; }
                EliteSplitChild sc = copy.gameObject.AddComponent<EliteSplitChild>();
                ElementalNeutral en = GetComponent<ElementalNeutral>();
                sc.InheritPrefix = en != null ? en.Prefix : null;
                sc.InheritVariant = GetComponent<VariantMark>() != null;
                copy.transform.localScale = transform.localScale * 0.75f;
                copy.StartCoroutine(SetHealthLater(copy, Mathf.Max(1, Mathf.RoundToInt(maxHp * 0.4f))));
            }
            Plugin.Log.LogInfo("ELITE " + enemy.GetType().Name + " split in two");
        }

        // Vengeful: its soul flies to the nearest living enemy within 12 units.
        void Vengeance(Vector2 at)
        {
            Enemy best = null;
            float bd = 144f;
            foreach (GameObject go in GameController.enemies)
            {
                if (go == null || go == gameObject) continue;
                Enemy e = go.GetComponent<Enemy>();
                if (e == null || e.health == null || e.health.CurrentHealthValue <= 0 || e is Boss || Rivals.IsRival(e)) continue;
                // v0.9.15: never a mini-boss (giant, special mini-boss or one of its fight's enemies): stacked heals and
                // growth made those fights a slog.
                if (e is MiniBoss || GiantCyclops.IsGiant(e) || e.GetComponent<BossEventEnemy>() != null || e.GetComponent<BossArmour>() != null) continue;
                float d = ((Vector2)e.transform.position - at).sqrMagnitude;
                if (d < bd) { bd = d; best = e; }
            }
            if (best == null) return;
            EliteSoul.Spawn(enemy, at, best);
        }

        static IEnumerator SetHealthLater(Enemy e, int hp)
        {
            yield return null;
            yield return null;
            if (e != null && e.health != null && e.health.CurrentHealthValue > hp) e.health.CurrentHealthValue = hp;
        }

        void OnGUI()
        {
            if (enemy == null || dead) return;
            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 sp = cam.WorldToScreenPoint(transform.position + new Vector3(0f, 2.1f, 0f));
            if (sp.z < 0f || sp.x < 0 || sp.x > Screen.width || sp.y < 0 || sp.y > Screen.height) return;
            if (style == null || GameFont.Changed(ref styleFont)) style = GameFont.Style(GUI.skin.label, 13, TextAnchor.MiddleCenter, true);
            GameFont.Shadowed(new Rect(sp.x - 110f, Screen.height - sp.y - 10f, 220f, 20f), Label, style, Colour);
        }
    }

    // A delayed elemental burst: the game's casting circle as the warning, then its explosion, the meteor sound
    // and one frame of the circle hitbox (enemy-owned, so it only hits players).
    internal class EliteBlast : MonoBehaviour
    {
        Vector2 at;
        float radius, delay;
        int damage;
        ElementType element;
        Enemy owner;

        internal static void Spawn(Vector2 at, float radius, int damage, ElementType element, Enemy owner, float delay)
        {
            GameObject go = new GameObject("HB_EliteBlast");
            go.transform.position = at;
            EliteBlast b = go.AddComponent<EliteBlast>();
            b.at = at; b.radius = radius; b.damage = damage; b.element = element; b.owner = owner; b.delay = delay;
        }

        IEnumerator Start()
        {
            CastingCircle cc = null;
            if (delay > 0.05f)
            {
                try { cc = CastingCirclePool.Spawn("Draw", (Vector3)at, false, null, false, true, delay, radius * 2f, radius * 2f, (Color?)CastingCircle.enemyColor, 0f); }
                catch (Exception) { cc = null; }
                yield return new WaitForSeconds(delay);
            }
            try { if (cc != null) cc.Reset(false); } catch (Exception) { }
            try
            {
                GameObject fx = Explosion.Prefab;
                if (fx != null) { Explosion ex = Globals.ChaosInst<Explosion>(fx, null, (Vector3)at, null); if (ex != null) ex.Explode(Mathf.Max(1f, radius * 0.6f)); }
            }
            catch (Exception) { }
            try { SoundManager.PlayAudioWithDistance("MeteorExplosion", (Vector2?)at, null, 24f, -1f, 1f, false); } catch (Exception) { }
            try { CameraController.ShakeCamera(0.6f, false); } catch (Exception) { }
            try { Hit(); } catch (Exception ex) { Plugin.Log.LogWarning("ELITE blast failed: " + ex.Message); }
            Destroy(gameObject, 1f);
        }

        void Hit()
        {
            GameObject prefab = Attack.CirclePrefab;
            if (prefab == null) return;
            Attack atk = Globals.ChaosInst<Attack>(prefab, null, (Vector3)at, null);
            if (atk == null) return;
            atk.transform.localScale = Vector3.one;
            CircleCollider2D col = atk.GetComponent<CircleCollider2D>();
            if (col != null) { col.isTrigger = true; col.radius = radius; col.offset = Vector2.zero; }
            AttackInfo info = atk.SetAttackInfo(Enemy.globalEnemySkillCategory, "NeutralKnightAttack", 1, false);
            if (info != null)
            {
                if (owner != null) EnemyArcana.ReOwn(info, owner);
                else { info.entity = null; info.gameObject = null; }
                info.damage = damage;
                info.burnChance = info.slowChance = info.poisonChance = info.shockChance = info.freezeChance = info.chaosChance = 0f;
                if (element != ElementType.Neutral) Infusion.SetStatus(info, element, 0.5f, 1); else info.elementType = ElementType.Neutral;
            }
            atk.EnableForOneFrame(true, false);
            Destroy(atk.gameObject, 0.5f);
        }
    }

    // Haunting: a pale ghost of the elite that drifts after the player for a few seconds and bursts on contact.
    internal class EliteGhost : MonoBehaviour
    {
        SpriteRenderer sr;
        int damage;
        float born;
        ElementType element;

        internal static void Spawn(Enemy from, Vector2 at, int damage)
        {
            SpriteRenderer src = from != null ? from.spriteRenderer : null;
            if (src == null || src.sprite == null) return;
            GameObject go = new GameObject("HB_EliteGhost");
            go.transform.position = at;
            go.transform.localScale = src.transform.lossyScale;
            EliteGhost g = go.AddComponent<EliteGhost>();
            g.sr = go.AddComponent<SpriteRenderer>();
            g.sr.sprite = src.sprite;
            g.sr.flipX = src.flipX;
            g.sr.sortingLayerID = src.sortingLayerID;
            g.sr.sortingOrder = src.sortingOrder + 5;
            g.sr.color = new Color(0.8f, 0.95f, 1f, 0f);
            g.damage = damage;
            g.element = from.element;
            g.born = Time.time;
            Plugin.Log.LogInfo("ELITE Haunting ghost rises");
        }

        void Update()
        {
            float age = Time.time - born;
            if (age > 6.5f) { Destroy(gameObject); return; }
            float alpha = age < 0.6f ? age / 0.6f : age > 5.5f ? Mathf.Max(0f, 6.5f - age) : 1f;
            sr.color = new Color(0.8f, 0.95f, 1f, 0.55f * alpha + 0.1f * Mathf.Sin(Time.time * 8f));
            if (age < 0.6f) { transform.position += new Vector3(0f, 0.6f * Time.deltaTime, 0f); return; } // rising
            Player p = Elites.Player0;
            if (p == null || p.IsDead) return;
            Vector2 target = p.hurtBoxTransform != null ? (Vector2)p.hurtBoxTransform.position : (Vector2)p.transform.position;
            Vector2 to = target - (Vector2)transform.position;
            if (to.magnitude < 0.9f)
            {
                EliteBlast.Spawn(transform.position, 1.5f, damage, element, null, 0f);
                Destroy(gameObject);
                return;
            }
            transform.position += (Vector3)(to.normalized * 3.4f * Time.deltaTime); // through walls: it's a ghost
            sr.flipX = to.x < 0f;
        }
    }

    // Vengeful: the soul of the elite flies to another enemy and empowers it (healed, faster, bigger; up to 3 times).
    internal class EliteSoul : MonoBehaviour
    {
        Enemy target;
        Vector2 from;
        float born;

        internal static void Spawn(Enemy elite, Vector2 at, Enemy target)
        {
            SpriteRenderer src = elite != null ? elite.spriteRenderer : null;
            GameObject go = new GameObject("HB_EliteSoul");
            go.transform.position = at;
            if (src != null && src.sprite != null)
            {
                go.transform.localScale = src.transform.lossyScale * 0.5f;
                SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = src.sprite;
                sr.sortingLayerID = src.sortingLayerID;
                sr.sortingOrder = src.sortingOrder + 5;
                sr.color = new Color(0.9f, 0.15f, 0.25f, 0.8f);
            }
            EliteSoul s = go.AddComponent<EliteSoul>();
            s.target = target; s.from = at; s.born = Time.time;
        }

        void Update()
        {
            if (target == null || target.health == null || target.health.CurrentHealthValue <= 0) { Destroy(gameObject); return; }
            float t = (Time.time - born) / 0.55f;
            Vector2 to = target.transform.position;
            Vector2 pos = Vector2.Lerp(from, to, t) + new Vector2(0f, Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI) * 1.5f);
            transform.position = pos;
            if (t < 1f) return;
            Vengeance.Empower(target);
            Destroy(gameObject);
        }
    }

    internal class Vengeance : MonoBehaviour
    {
        public int Stacks;
        Enemy enemy;
        float glowUntil;

        internal static void Empower(Enemy e)
        {
            Vengeance v = e.GetComponent<Vengeance>() ?? e.gameObject.AddComponent<Vengeance>();
            v.enemy = e;
            if (v.Stacks >= 3) return;
            v.Stacks++;
            try { e.movement.moveSpeedStat.AddMod(new NumVarStatMod("HB_Vengeance" + v.Stacks, 1.2f, 10, VarStatModType.Multiplicative, false)); } catch (Exception) { }
            try { e.health.RestoreHealth(Mathf.Max(1, Mathf.RoundToInt(e.health.CurrentHealthValue * 0.5f)), false, false, false, false); } catch (Exception) { }
            e.transform.localScale = e.transform.localScale * 1.12f;
            v.glowUntil = Time.time + 1.2f;
            Plugin.Log.LogInfo("ELITE Vengeful soul empowered " + e.GetType().Name + " (x" + v.Stacks + ")");
        }

        OutlineCache outline; // v0.9.15: cached materials (this runs every frame)

        void Update()
        {
            if (enemy == null || GetComponent<EliteMark>() != null) return; // an elite keeps its own outline
            float pulse = Time.time < glowUntil ? 1f : 0.5f + 0.3f * Mathf.Sin(Time.time * 3f);
            if (outline == null) outline = new OutlineCache(this);
            outline.Set(new Color(0.75f * pulse, 0.1f * pulse, 0.2f * pulse, 1f));
        }
    }

    internal class FadeOut : MonoBehaviour
    {
        public float Seconds = 0.3f;
        SpriteRenderer sr;
        float start, a0;
        void Start() { sr = GetComponent<SpriteRenderer>(); start = Time.time; a0 = sr != null ? sr.color.a : 1f; }
        void Update()
        {
            float t = (Time.time - start) / Seconds;
            if (t >= 1f || sr == null) { Destroy(gameObject); return; }
            Color c = sr.color; c.a = a0 * (1f - t); sr.color = c;
        }
    }

    // Elites are rolled one frame after an enemy starts (palettes and stats are set by then).
    [HarmonyPatch(typeof(Enemy), "Start")]
    static class EliteSpawnPatch
    {
        static void Postfix(Enemy __instance)
        {
            if (!Plugin.ElitesEnabled.Value || __instance == null || __instance is MiniBoss || __instance is Boss) return;
            try { __instance.StartCoroutine(Later(__instance)); } catch (Exception) { }
        }

        static IEnumerator Later(Enemy e)
        {
            yield return null;
            yield return null;
            try { ElementalNeutrals.TryApply(e); } catch (Exception ex) { Plugin.Log.LogWarning("ELEMENTAL roll failed: " + ex.Message); } // v0.9.8
            try { Elites.TryMake(e); } catch (Exception ex) { Plugin.Log.LogWarning("ELITE roll failed: " + ex.Message); }
        }
    }

    // An elite's hit on a player: Draining heals for half the health it took, Overcharged deals double (then
    // recharges), Thief steals gold.
    [HarmonyPatch(typeof(Health), "TakeDamage")]
    static class EliteDrainPatch
    {
        struct State { public int Hp, Damage; public EliteMark Mark; }

        static void Prefix(Health __instance, AttackInfo __0, out State __state)
        {
            __state = new State { Hp = __instance != null ? __instance.CurrentHealthValue : 0, Damage = -1 };
            try
            {
                if (__instance == null || __0 == null || !Elites.IsPlayers(__instance)) return;
                Entity attacker = __0.entity;
                EliteMark m = attacker != null ? attacker.GetComponent<EliteMark>() : null;
                if (m == null) return;
                __state.Mark = m;
                if (m.Trait == EliteTrait.Overcharged && m.Charged) { __state.Damage = __0.damage; __0.damage = __0.damage * 2; }
            }
            catch (Exception) { }
        }

        static void Postfix(Health __instance, AttackInfo __0, State __state)
        {
            try
            {
                EliteMark m = __state.Mark;
                if (m == null || __0 == null) return;
                if (__state.Damage >= 0) __0.damage = __state.Damage; // attack infos are reused: undo the doubling
                int lost = __state.Hp - __instance.CurrentHealthValue;
                if (lost <= 0) return;
                switch (m.Trait)
                {
                    case EliteTrait.Draining:
                        Entity attacker = __0.entity;
                        if (attacker != null && attacker.health != null) attacker.health.RestoreHealth(Mathf.Max(1, lost / 2), false, false, false, false);
                        break;
                    case EliteTrait.Overcharged:
                        if (__state.Damage >= 0) { m.Discharged(); try { CameraController.ShakeCamera(0.8f, false); } catch (Exception) { } }
                        break;
                    case EliteTrait.Thief: m.Steal(); break;
                }
            }
            catch (Exception) { }
        }
    }
}
