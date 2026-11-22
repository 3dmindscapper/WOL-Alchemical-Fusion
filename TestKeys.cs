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
            }
            catch (Exception ex) { Plugin.Log.LogWarning("TEST key failed: " + ex.Message); }
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
}
