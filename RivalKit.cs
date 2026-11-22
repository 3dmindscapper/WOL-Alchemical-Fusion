using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace WolHybridBosses
{
    // ---------------------------------------------------------------------------------------------
    // Dash arcana: like the player, an enemy with a dash arcana triggers it from its own dashes
    // (Contestant's DashState for rivals, Enemy.DashState for giants). Wave/self/trap-type effects are left at the
    // dash start along the dash direction; projectiles fire from the dash end; ground spells land on the target.
    // ---------------------------------------------------------------------------------------------
    internal class DashArcana : MonoBehaviour
    {
        public ArcanaDef Def;
        Vector2 start;
        bool dashing;
        float next;

        internal static DashArcana Give(Enemy e, ArcanaDef def)
        {
            if (def == null || EnemyArcana.GetPrefab(def) == null) return null;
            DashArcana d = e.GetComponent<DashArcana>() ?? e.gameObject.AddComponent<DashArcana>();
            d.Def = def;
            return d;
        }

        internal void Begin(Enemy e)
        {
            start = e.transform.position;
            dashing = true;
        }

        internal void End(Enemy e)
        {
            if (!dashing || Def == null || EnemyArcana.Broken.Contains(Def.SkillID)) return;
            dashing = false;
            if (Time.time < next) return;
            next = Time.time + Mathf.Max(0.8f, Def.Cooldown);

            Vector2 end = e.transform.position;
            Vector2 dir = end - start;
            if (dir.sqrMagnitude < 0.04f && e.closestTargetInfo != null) dir = (Vector2)e.closestTargetInfo.position - end;
            dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;

            Vector2? origin;
            switch (Def.Delivery)
            {
                case Delivery.Projectile:
                    origin = end;
                    if (e.closestTargetInfo != null)
                    {
                        Vector2 to = (Vector2)e.closestTargetInfo.position - end;
                        if (to.sqrMagnitude > 0.01f) dir = to.normalized; // dash shots fly at the player
                    }
                    break;
                case Delivery.AtTarget: origin = null; break;
                default: origin = start; break; // leave-behind: walls, traps, decoys, mines, waves along the dash
            }
            ArcanaCaster.CastAll(e, Def, dir, origin, false);
        }
    }

    [HarmonyPatch]
    static class DashArcanaPatch
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (string n in new[] { "Contestant+DashState", "Enemy+DashState" })
            {
                Type t = AccessTools.TypeByName(n);
                if (t == null) continue;
                foreach (string mn in new[] { "OnEnter", "OnExit" })
                {
                    MethodInfo m = AccessTools.DeclaredMethod(t, mn);
                    if (m != null) yield return m;
                }
            }
        }

        static void Postfix(object __instance, MethodBase __originalMethod)
        {
            try
            {
                Enemy e = Traverse.Create(__instance).Field("parent").GetValue() as Enemy;
                DashArcana d = e == null ? null : e.GetComponent<DashArcana>();
                if (d == null) return;
                if (__originalMethod.Name == "OnEnter") d.Begin(e); else d.End(e);
            }
            catch (Exception ex) { Plugin.Log.LogWarning("DASH arcana hook failed: " + ex.Message); }
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Signature, as the player's works (Player.HandleOverdrive): hits on the player fill a meter. Full = READY:
    // the game's overdrive aura + glowing outline + the "PlayerOverdrive" activation sound, and the signature
    // arcana's cooldown resets. Ready slowly runs out (expire sound) if unused. Only the rival's ONE signature
    // arcana (an arcana the game gives a signature variant, Player+SkillState.hasSignatureVariant) can spend it:
    // cast while ready it fires its signature version (isUltimate), with the player's cue (burst, hit-stop freeze,
    // camera shake, brief invulnerability). Any other cast leaves the meter alone. Giants never get this.
    // ---------------------------------------------------------------------------------------------
    internal static class Signature
    {
        // Player skill states whose constructor sets hasSignatureVariant = true (tools/fieldwriters.ps1).
        static readonly HashSet<string> SignatureStates = new HashSet<string>
        {
            "FreezingLungeState", "MultiShotState", "ShootArrowState", "ShootBallLightningState", "ShootFireArc", "ShootFireball",
            "ShootVacuumShot", "SummonHomingMissilesState", "SummonIceWaveState", "SummonMeteorStrikeState", "SummonSkeletonMinionState",
            "SummonTornadoState", "SummonTumbleFistState", "SwordThrowState", "TeleStrikeState", "ThrowIceLance", "UseAeroRipple",
            "UseAquaBeam", "UseAquaDragon", "UseBlazingBlitz", "UseChaosBall", "UseChaosBeam", "UseChaosCluster", "UseChaosDrop",
            "UseChaosEraser", "UseChaosLunge", "UseChaosMinion", "UseChaosSwordStorm", "UseChaosSwordSummon", "UseCurrentBurst",
            "UseEarthAxe", "UseEarthBoomerang", "UseEarthCascade", "UseEarthDrill", "UseEarthJump", "UseEarthWheel", "UseFireBeam",
            "UseFireBlastState", "UseFlameReturner", "UseFlameSlash", "UseFlameSwirl", "UseFrostFan", "UseFrostSwing", "UseHammerThrow",
            "UseIceBoomerang", "UseIceChakram", "UseLightningFist", "UseLightningSword", "UseLightningXStrike", "UseMortalCoil",
            "UsePhantomKnights", "UsePiercingShock", "UsePlantBarrage", "UsePoisonBolas", "UseRisingDragon", "UseShatterStone",
            "UseShockDragon", "UseShockNova", "UseSlicingBarrage", "UseSonicBoom", "UseSpikeRing", "UseThunderDrop", "UseThunderFan",
            "UseVacuumFlame", "UseVoidShift", "UseVoltSplinter", "UseWaterBall", "UseWaterBlast", "UseWhirlingAxe", "UseWhirlpool",
            "UseWhirlwind", "UseWindFalcon", "UseWindSlam",
        };
        static HashSet<string> capableIds;

        // Does this arcana have a signature version in the game?
        internal static bool Capable(ArcanaDef d)
        {
            if (d == null) return false;
            if (capableIds == null)
            {
                capableIds = new HashSet<string>();
                foreach (GenArcana g in ArcanaCatalog.AllRows())
                    if (g.ClassName != null && SignatureStates.Contains(g.ClassName)) capableIds.Add(g.SkillID);
            }
            return capableIds.Contains(d.SkillID);
        }

        internal static void Enable(Enemy e, int hitsNeeded, string elementPrefix, float readySeconds, ArcanaDef signatureArcana)
        {
            SignatureMeter m = e.GetComponent<SignatureMeter>() ?? e.gameObject.AddComponent<SignatureMeter>();
            m.Owner = e;
            m.Need = Mathf.Max(1, hitsNeeded);
            m.ReadySeconds = Mathf.Max(4f, readySeconds);
            m.Color = ElementalGiants.ElementColor(elementPrefix);
            m.SkillID = signatureArcana != null ? signatureArcana.SkillID : null;
        }

        static SignatureMeter Meter(Enemy e) { return e == null ? null : e.GetComponent<SignatureMeter>(); }

        internal static bool HasMeter(Enemy e) { return Meter(e) != null; }

        // The meter is full and waiting for the signature arcana.
        internal static bool IsReady(Enemy e) { SignatureMeter m = Meter(e); return m != null && m.Ready; }

        internal static bool IsSignatureArcana(Enemy e, ArcanaDef d)
        {
            SignatureMeter m = Meter(e);
            return m != null && d != null && m.SkillID != null && m.SkillID == d.SkillID;
        }

        // Called as an arcana fires: true (and the meter is spent, with the player's cue) only for the signature
        // arcana while ready. The cast then uses its signature version.
        internal static bool Consume(Enemy e, ArcanaDef d)
        {
            SignatureMeter m = Meter(e);
            if (m == null || !m.Ready || !IsSignatureArcana(e, d)) return false;
            m.Fire(d);
            return true;
        }

        internal static void AddHit(Enemy e)
        {
            SignatureMeter m = Meter(e);
            if (m != null) m.AddHit();
        }
    }

    internal class SignatureMeter : MonoBehaviour
    {
        public Enemy Owner;
        public int Need = 8;
        public float ReadySeconds = 10f;
        public Color Color = Color.white;
        public string SkillID;
        int hits;
        float readySince, firedAt = -10f, nextForce;
        string firedName;
        OverdriveEffects aura;

        internal bool Ready { get; private set; }

        internal void AddHit()
        {
            if (Ready || SkillID == null) return;
            hits++;
            if (hits >= Need) BecomeReady();
        }

        // Full meter: the player's overdrive look and sound, and the signature arcana is ready to go.
        void BecomeReady()
        {
            Ready = true;
            readySince = Time.time;
            nextForce = Time.time + 0.3f;
            EnsureAura();
            try { if (aura != null) { aura.ToggleAura(true); aura.Burst(10, true, false, true); } } catch (Exception) { }
            SetOutline(Color);
            ArcanaState s = SignatureState();
            if (s != null && s.cooldownRef != null) { try { s.cooldownRef.Reset(); } catch (Exception) { } }
            Plugin.Log.LogInfo("SIGNATURE " + Owner.GetType().Name + " ready (" + SkillID + ")");
        }

        // The signature arcana was cast while ready: the player's activation cue, then the meter starts over.
        internal void Fire(ArcanaDef d)
        {
            Ready = false;
            hits = 0;
            firedAt = Time.time;
            firedName = IntroUI.SkillName(d.SkillID);
            try { if (aura != null) { aura.Burst(40, false, true, true); aura.ToggleAura(false); } } catch (Exception) { }
            try { TimeScaleController.StandardFreezeIntoEaseOutTimeScale(); } catch (Exception) { }
            try { CameraController.ShakeCamera(1f, false); } catch (Exception) { }
            try { if (Owner.health != null) Owner.health.SetInvulnerabilityDuration(0.6f, false); } catch (Exception) { }
            SetOutline(Color.clear);
            Plugin.Log.LogInfo("SIGNATURE " + Owner.GetType().Name + " cast: " + d.SkillID + " (signature version)");
        }

        void Expire()
        {
            Ready = false;
            hits = 0;
            try { if (aura != null) aura.ToggleAura(false); } catch (Exception) { }
            try { SoundManager.PlayAudio("PlayerOverdriveExpire", 1f, false, -1f, -1f); } catch (Exception) { }
            SetOutline(Color.clear);
            Plugin.Log.LogInfo("SIGNATURE " + Owner.GetType().Name + " expired unused");
        }

        void EnsureAura()
        {
            if (aura != null || Owner == null) return;
            try
            {
                GameObject prefab = OverdriveEffects.Prefab;
                if (prefab == null) return;
                GameObject go = UnityEngine.Object.Instantiate(prefab, Owner.transform) as GameObject;
                aura = go != null ? go.GetComponent<OverdriveEffects>() : null;
                Material mat = Owner.spriteRenderer != null ? Owner.spriteRenderer.material : null;
                if (aura != null) aura.Initialize(new Color(Color.r * 1.6f, Color.g * 1.6f, Color.b * 1.6f, 0.5f), Owner.transform, mat);
            }
            catch (Exception ex) { Plugin.Log.LogWarning("SIGNATURE aura failed: " + ex.Message); aura = null; }
        }

        ArcanaState SignatureState()
        {
            if (Owner == null || Owner.availableEnemySkillsDict == null) return null;
            foreach (Enemy.SkillState s in Owner.availableEnemySkillsDict.Values)
            {
                ArcanaState a = s as ArcanaState;
                if (a != null && a.Def.SkillID == SkillID) return a;
            }
            return null;
        }

        void Update()
        {
            if (Owner == null || !Ready) return;
            if (Owner.health == null || Owner.health.CurrentHealthValue <= 0) { Expire(); return; }
            if (Time.time - readySince > ReadySeconds) { Expire(); return; }
            if (ArcanosBridge.Incapacitated(Owner)) return; // blinded / stunned by an Arcanos ailment: no forced cast
            // Like a player who has their signature up: use it at the next opening (out of Chase/Idle).
            if (Time.time < nextForce || !Owner.enabled || Owner.fsm == null || Owner.enemySkillUseInProgress) return;
            string state = Owner.fsm.currentStateName ?? "";
            if (!state.Contains("Chase") && !state.Contains("Idle")) return;
            nextForce = Time.time + 0.5f;
            ArcanaState s = SignatureState();
            if (s == null || EnemyArcana.Broken.Contains(SkillID)) { Expire(); return; }
            if (!s.IsReady()) return;
            try { s.Transition(false); } catch (Exception ex) { Plugin.Log.LogWarning("SIGNATURE cast failed: " + ex.Message); }
        }

        void OnDestroy() { if (aura != null) Destroy(aura.gameObject); }

        void SetOutline(Color c)
        {
            if (Owner == null || Owner.spriteRenderer == null) return;
            Material m = Owner.spriteRenderer.material;
            if (m == null) return;
            if (m.HasProperty("_OutlineColor")) m.SetColor("_OutlineColor", c);
            if (m.HasProperty("_OutlineColor2")) m.SetColor("_OutlineColor2", c);
        }

        static GUIStyle burstStyle, labelStyle;

        void OnGUI()
        {
            if (Owner == null || !Owner.enabled || Owner.health == null || Owner.health.CurrentHealthValue <= 0) return;
            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 sp = cam.WorldToScreenPoint(Owner.transform.position + new Vector3(0f, -0.35f, 0f));
            if (sp.z < 0f) return;
            if (burstStyle == null)
            {
                burstStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperCenter };
            }
            float w = 64f, h = 6f;
            Rect back = new Rect(sp.x - w * 0.5f, Screen.height - sp.y, w, h);
            Color old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.DrawTexture(back, Texture2D.whiteTexture);

            float fill;
            Color c = Color;
            if (Ready)
            {
                fill = Mathf.Clamp01(1f - (Time.time - readySince) / ReadySeconds); // runs out if unused
                c = Color.Lerp(Color, Color.white, 0.5f + 0.5f * Mathf.Sin((Time.time - readySince) * 10f));
            }
            else fill = Mathf.Clamp01(hits / (float)Need);
            GUI.color = c;
            GUI.DrawTexture(new Rect(back.x + 1, back.y + 1, (w - 2) * fill, h - 2), Texture2D.whiteTexture);
            GUI.color = old;

            if (Ready)
            {
                labelStyle.normal.textColor = c;
                GUI.Label(new Rect(back.x - 40, back.y + h, w + 80, 18), "SIGNATURE READY", labelStyle);
            }
            // The cast: big text over the rival, rising and fading.
            float t = Time.time - firedAt;
            if (t >= 0f && t < 1.4f)
            {
                Vector3 top = cam.WorldToScreenPoint(Owner.transform.position + new Vector3(0f, 1.6f, 0f));
                float a = Mathf.Clamp01(1.4f - t);
                string text = "SIGNATURE: " + (firedName ?? "");
                Rect r = new Rect(top.x - 160f, Screen.height - top.y - 20f - t * 30f, 320f, 40f);
                burstStyle.normal.textColor = new Color(0f, 0f, 0f, a * 0.8f);
                GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), text, burstStyle);
                burstStyle.normal.textColor = new Color(Color.r, Color.g, Color.b, a);
                GUI.Label(r, text, burstStyle);
            }
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Fall protection for rivals, as the player has it: falling into a pit costs some health (the player's own pit
    // damage, at least 5% of the rival's max HP, never lethal), then the rival comes back at the nearest free spot
    // to where it last stood on solid ground (Fall.preFallPos), briefly invulnerable. The Contestant's own fall
    // code teleports it to myOrigin when the fall finishes, so we point myOrigin at the safe spot for that moment.
    // ---------------------------------------------------------------------------------------------
    [HarmonyPatch(typeof(Entity.FallState), "Update")]
    static class RivalFallPatch
    {
        static readonly HashSet<object> handled = new HashSet<object>();

        static void Postfix(Entity.FallState __instance)
        {
            if (!__instance.finishedFalling) { handled.Remove(__instance); return; }
            Enemy e = Traverse.Create(__instance).Field("parent").GetValue() as Enemy;
            if (e == null || !Rivals.IsRival(e) || !handled.Add(__instance)) return;
            try
            {
                Vector2 last = e.fall != null ? (Vector2)e.fall.preFallPos : (Vector2)e.myOrigin;
                Vector2 safe = Entity.GetAvailableNearbyLocation(last, false, false);
                Vector3 home = e.myOrigin;
                e.myOrigin = safe;                       // Contestant's fall state teleports here this frame
                e.StartCoroutine(RestoreOrigin(e, home));

                int dmg = FallDamage(e);
                if (e.health != null && dmg > 0)
                {
                    e.health.CurrentHealthValue = Mathf.Max(1, e.health.CurrentHealthValue - dmg);
                    e.health.SetInvulnerabilityDuration(1f, false);
                }
                Plugin.Log.LogInfo("RIVAL fell: -" + dmg + " HP, back at " + safe);
            }
            catch (Exception ex) { Plugin.Log.LogWarning("RIVAL fall recovery failed: " + ex.Message); }
        }

        static System.Collections.IEnumerator RestoreOrigin(Enemy e, Vector3 home)
        {
            yield return null;
            if (e != null) e.myOrigin = home;
        }

        static int FallDamage(Enemy e)
        {
            float playerPit = 0f;
            try
            {
                Player p = GameController.playerScripts != null && GameController.playerScripts.Length > 0 ? GameController.playerScripts[0] : null;
                if (p != null) playerPit = p.FallDamage.CurrentValue;
            }
            catch (Exception) { }
            float max = e.health != null ? e.health.healthStat.CurrentValue : 0f;
            return Mathf.RoundToInt(Mathf.Max(playerPit, max * 0.05f));
        }
    }

    // A hit lands: if it came from an enemy with a signature meter (directly, or via something it cast/summoned)
    // and the target is a player, the meter fills.
    [HarmonyPatch(typeof(Attack), "ExecuteAttack")]
    static class SignatureHitPatch
    {
        static void Postfix(Attack __instance, Collider2D __0)
        {
            try
            {
                if (__0 == null || __0.GetComponentInParent<Player>() == null) return;
                Enemy owner = OwnerContext.OwnerOf(__instance);
                if (owner == null && __instance.atkInfo != null) owner = __instance.atkInfo.entity as Enemy;
                if (owner != null) Signature.AddHit(owner);
            }
            catch (Exception) { }
        }
    }
    // ---------------------------------------------------------------------------------------------
    // Rival basics: the Contestant's chase state switches to its built-in "Attack" (a Wind Slash basic whose stats
    // only exist in its ally category). For rivals that switch goes to their assigned elemental basic arcana instead,
    // so they only ever use what their intro shows. If the basic is cooling down, the switch is skipped.
    // ---------------------------------------------------------------------------------------------
    internal static class RivalBasics
    {
        static readonly Dictionary<FSM, Enemy> byFsm = new Dictionary<FSM, Enemy>();

        internal static void Register(Enemy e, RivalMarker mk)
        {
            if (e != null && e.fsm != null) byFsm[e.fsm] = e;
        }

        internal static Enemy RivalOf(FSM fsm)
        {
            Enemy e;
            if (fsm == null || !byFsm.TryGetValue(fsm, out e)) return null;
            if (e == null) { byFsm.Remove(fsm); return null; }
            return e;
        }
    }

    [HarmonyPatch(typeof(FSM), "ChangeState", new Type[] { typeof(string), typeof(bool) })]
    static class RivalAttackRedirectPatch
    {
        // The Contestant's own spells, by FSM state name -> the arcana they stand for.
        static readonly Dictionary<string, string> OwnSpells = new Dictionary<string, string>
        {
            { "Fireball", "ShootFireball" }, { "Tornado", "SummonTornado" }, { "SwordThrow", "SwordThrow" },
        };

        static bool Prefix(FSM __instance, ref string __0)
        {
            if (__0 == null) return true;
            string own;
            if (OwnSpells.TryGetValue(__0, out own))
            {
                // Hard block at the source: an off-theme own spell never starts (whatever path asks for it).
                // Go to Chase instead of cancelling, so a state that already ended never gets left running.
                Enemy r = RivalBasics.RivalOf(__instance);
                if (r == null || Rivals.SignatureAllowed(r.GetComponent<RivalMarker>(), own)) return true;
                if (__instance.currentStateName == "Chase") return false;
                __0 = "Chase";
                return true;
            }
            if (__0 != "Attack") return true;
            Enemy e = RivalBasics.RivalOf(__instance);
            if (e == null) return true;
            RivalMarker mk = e.GetComponent<RivalMarker>();
            if (mk == null || mk.Basic == null) return true;
            // Basics aren't offered to the AI, so look the state up in the FSM's own state table.
            IDictionary states = Traverse.Create(__instance).Field("states").GetValue() as IDictionary;
            string name = "Arcana" + mk.Basic.SkillID;
            Enemy.SkillState basic = states != null && states.Contains(name) ? states[name] as Enemy.SkillState : null;
            if (basic != null && basic.IsReady()) basic.Transition(false); // its elemental basic instead of the built-in Wind Slash
            return false;                                                  // never the built-in basic
        }
    }

    // A rival that keeps "running" in place (blocked path) while the player is out of reach dashes at the player.
    internal class RivalUnstuck : MonoBehaviour
    {
        public Enemy Owner;
        Vector2 lastPos;
        float stillSince, nextAllowed;

        Vector2 animPos;
        float animStill;
        bool idleShown;

        // Chase keeps the Run cycle playing even when the rival stands still (at the player waiting on its
        // basic, or blocked): show Idle while it stands, Run again once it moves.
        void FixAnimation(string state)
        {
            Vector2 pos = Owner.transform.position;
            bool moved = (pos - animPos).sqrMagnitude > 0.0004f;
            animPos = pos;
            if (!state.Contains("Chase")) { idleShown = false; animStill = Time.time; return; }
            if (moved)
            {
                animStill = Time.time;
                if (idleShown) { idleShown = false; Play("Run" + Owner.facingDirection, "Run"); }
                return;
            }
            if (!idleShown && Time.time - animStill > 0.15f && Play("Idle" + Owner.facingDirection, "Idle")) idleShown = true;
        }

        bool Play(params string[] names)
        {
            Animator a = Owner.anim;
            if (a == null) return false;
            foreach (string n in names)
            {
                int hash = Animator.StringToHash(n);
                if (a.HasState(0, hash)) { a.Play(hash, 0, 0f); return true; }
            }
            return false;
        }

        void Update()
        {
            if (Owner == null || !Owner.enabled || Owner.fsm == null || Owner.health == null || Owner.health.CurrentHealthValue <= 0) return;
            string sn = Owner.fsm.currentStateName ?? "";
            if (sn.StartsWith("WolArcanos") || ArcanosBridge.Incapacitated(Owner)) { stillSince = Time.time; return; } // Arcanos ailment: leave it be
            FixAnimation(sn);
            Vector2 pos = Owner.transform.position;
            if ((pos - lastPos).sqrMagnitude > 0.04f) { lastPos = pos; stillSince = Time.time; return; }
            string state = Owner.fsm.currentStateName ?? "";
            if (!state.Contains("Chase")) { stillSince = Time.time; return; }
            if (Time.time - stillSince < 1f || Time.time < nextAllowed) return;

            Player p = GameController.playerScripts != null && GameController.playerScripts.Length > 0 ? GameController.playerScripts[0] : null;
            if (p == null || Vector2.Distance(p.transform.position, pos) < 3.5f) return;
            nextAllowed = Time.time + 2.5f;
            stillSince = Time.time;
            try
            {
                Traverse.Create(Owner).Method("CalculateDashVector", new object[] { p.transform }).GetValue();
                Owner.fsm.ChangeState("Dash", false);
                Plugin.Log.LogInfo("RIVAL unstuck: dashing at the player");
            }
            catch (Exception ex) { Plugin.Log.LogWarning("RIVAL unstuck failed: " + ex.Message); }
        }
    }
}