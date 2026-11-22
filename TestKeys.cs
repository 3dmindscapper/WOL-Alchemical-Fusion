using System;
using System.Collections.Generic;
using UnityEngine;

namespace WolHybridBosses
{
    // v1.0.0: test keys for the newest systems (Alt + F-keys; only with Debug.Hotkeys on).
    //   Alt+F5   a rematch rival portal beside you
    //   Alt+F6   one step of progress on this run's bounty (rolls one first if there's none)
    //   Alt+F7   +500 gold
    //   Alt+F8   infusion test pack: each equipped arcana unlocks one random infused version for good (so dual pairs
    //            exist), then Nocturne's shuffle is rolled on your loadout as if you had paid for it
    //   Alt+F10  the Echo of your last run, beside you (records your current loadout first if there's no Echo yet)
    //   Alt+F9   a rival tournament portal beside you;  Alt+F11  a team match portal beside you
    //   Alt+F12  jump to the final fight against the Primordial Alchemist
    // v1.1.1: Alt+number twins (Alt+F9 is taken by NVIDIA's recorder on many PCs):
    //   Alt+1  tournament portal   Alt+2  team match portal   Alt+3  final vs the Primordial Alchemist
    //   Alt+4  final vs an Archon (or the Chaos Sovereign)   Alt+5  a Hard rival portal that holds an Archon
    //   Alt+6  instakill on / off (every enemy at 1 HP)
    //   Alt+7  every Primordial robe (this session only), then the Alchemist final: its true form
    // With the hotkeys on, unlocks aren't saved (Plugin.TestMode; NoTestUnlocksPatch below).
    internal static class TestKeys
    {
        internal static void Update()
        {
            if (!(Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))) return;
            try
            {
                if (Input.GetKeyDown(KeyCode.F5)) RivalEvent.OpenRematchHere();
                else if (Input.GetKeyDown(KeyCode.F6)) BountyStep();
                else if (Input.GetKeyDown(KeyCode.F7)) Gold(500);
                else if (Input.GetKeyDown(KeyCode.F8)) InfusionPack();
                else if (Input.GetKeyDown(KeyCode.F10)) RivalEvent.OpenEchoHere();
                else if (Input.GetKeyDown(KeyCode.F9)) RivalEvent.OpenModeHere(RivalMode.Tournament);  // v1.1.0
                else if (Input.GetKeyDown(KeyCode.F11)) RivalEvent.OpenModeHere(RivalMode.Team);        // v1.1.0
                else if (Input.GetKeyDown(KeyCode.F12)) { FinalRun.ForceAlchemist(); Debug.JumpToFinal(); } // v1.1.0
                else if (Input.GetKeyDown(KeyCode.Alpha1)) RivalEvent.OpenModeHere(RivalMode.Tournament); // v1.1.1
                else if (Input.GetKeyDown(KeyCode.Alpha2)) RivalEvent.OpenModeHere(RivalMode.Team);
                else if (Input.GetKeyDown(KeyCode.Alpha3)) { FinalRun.ForceAlchemist(); Debug.JumpToFinal(); }
                else if (Input.GetKeyDown(KeyCode.Alpha4)) { FinalRun.ForceArchon(); Debug.JumpToFinal(); }
                else if (Input.GetKeyDown(KeyCode.Alpha5)) RivalEvent.OpenArchonHere();
                else if (Input.GetKeyDown(KeyCode.Alpha7)) { foreach (PrimordialRobe r in PrimordialRobes.All) Progress.SetBool("robe." + r.Prefix, true); FinalRun.ForceAlchemist(); Debug.JumpToFinal(); } // v1.1.1
                else if (Input.GetKeyDown(KeyCode.Alpha6)) { Instakill = !Instakill; IntroUI.Show("Instakill " + (Instakill ? "on" : "off"), "Test key  ·  every enemy dies to one hit", new Color(1f, 0.5f, 0.4f), null, 2f); Plugin.Log.LogInfo("TEST instakill " + Instakill); }
            }
            catch (Exception ex) { Plugin.Log.LogWarning("TEST key failed: " + ex.Message); }
        }

        // v1.1.1 (Alt+6): every enemy sits at 1 HP, so any hit kills (allies and players untouched).
        internal static bool Instakill;
        static float nextKillTick;

        internal static void Tick()
        {
            if (!Instakill || Time.time < nextKillTick || GameController.enemies == null) return;
            nextKillTick = Time.time + 0.2f;
            foreach (GameObject go in GameController.enemies.ToArray())
            {
                if (go == null) continue;
                Enemy e = go.GetComponent<Enemy>();
                if (e == null || e.health == null || e.health.CurrentHealthValue <= 1) continue;
                try { e.health.CurrentHealthValue = 1; } catch (Exception) { }
            }
        }

        static void BountyStep()
        {
            if (!Bounties.Active || Bounties.Done) Bounties.Roll();
            if (!Bounties.Active) { IntroUI.Show("No bounty", "Bounties are off (or every system they use is)", Color.white, null, 2f); return; }
            Bounties.Have = Mathf.Max(0, Bounties.Need - 1);
            BountyKind k = Bounties.Kind == BountyKind.Elites ? BountyKind.Elite : Bounties.Kind;
            Bounties.Report(k, Bounties.Detail);
        }

        static void Gold(int amount)
        {
            if (Player.goldWallet == null) return;
            Player.goldWallet.Deposit(amount);
            IntroUI.Show("+" + amount + " gold", "Test key", new Color(1f, 0.82f, 0.3f), null, 1.5f);
        }

        static void InfusionPack()
        {
            Player p = Elites.Player0;
            if (p == null || p.assignedSkills == null) return;
            List<string> unlocked = new List<string>();
            foreach (Player.SkillState s in p.assignedSkills)
            {
                if (s == null || string.IsNullOrEmpty(s.skillID) || s.isSignature) continue;
                ElementType el = Infusion.RandomOther(s.skillID);
                InfusionUnlocks.Unlock(s.skillID, el);
                unlocked.Add(IntroUI.SkillName(s.skillID) + " (" + Infusion.Name(el) + ")");
            }
            Infusion.RollShuffled(p);
            Plugin.Log.LogInfo("TEST infusion pack: unlocked " + string.Join(", ", unlocked.ToArray()));
            IntroUI.Show("Infusion test pack", "Unlocked: " + string.Join("  ·  ", unlocked.GetRange(0, Mathf.Min(3, unlocked.Count)).ToArray()) + "  ·  shuffle rolled", new Color(1f, 0.82f, 0.3f), null, 4f);
        }
    }

    // v1.1.1: test mode (hotkeys on): the game's own arcana unlocks are skipped too, picked-up drops and the final
    // boss's Chaos arcana alike, so testing doesn't unlock anything in your save.
    [HarmonyLib.HarmonyPatch(typeof(Player), "HandleSkillUnlock", new[] { typeof(Player.SkillState), typeof(bool) })]
    static class NoTestUnlocksPatch
    {
        static bool Prefix(Player.SkillState __0)
        {
            if (!Plugin.TestMode) return true;
            Plugin.Log.LogInfo("TEST mode: unlock of " + (__0 != null ? __0.skillID : "?") + " not saved");
            return false;
        }
    }

    [HarmonyLib.HarmonyPatch(typeof(LootManager), "GetLockedChaosArcana")]
    static class NoTestChaosUnlockPatch
    {
        static bool Prefix(ref string __result)
        {
            if (!Plugin.TestMode) return true;
            __result = string.Empty;
            return false;
        }
    }
}