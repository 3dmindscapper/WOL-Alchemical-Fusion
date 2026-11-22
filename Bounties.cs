using System;
using System.Collections.Generic;
using UnityEngine;

namespace WolHybridBosses
{
    // v0.9.16: one bounty per run. Rolled when you arrive in the Hub (shown there, top left), tracked during the run;
    // finishing it drops gold at your feet (60 + 30 per tier) with a card. Progress shows for a few seconds at the start
    // of the run and after each step. Only bounties for systems that are on are offered.
    internal enum BountyKind { Elite, Elites, Special, Rival, Trial, Infuse }

    internal static class Bounties
    {
        internal static BountyKind Kind;
        internal static string Detail;   // Elite: the trait
        internal static int Need, Have;
        internal static bool Active, Done;
        internal static float ShowUntil;

        internal static void Roll()
        {
            Active = Done = false;
            Have = 0;
            if (!Plugin.BountiesEnabled.Value) return;
            List<BountyKind> options = new List<BountyKind>();
            if (Plugin.ElitesEnabled.Value && Plugin.EliteChanceMultiplier.Value > 0f) { options.Add(BountyKind.Elite); options.Add(BountyKind.Elites); }
            if (Plugin.BossFightsEnabled.Value && Plugin.MinibossFightChance.Value > 0f) options.Add(BountyKind.Special);
            if (Plugin.RivalEvents.Value && Plugin.RivalEventChance.Value > 0f) options.Add(BountyKind.Rival);
            if (Plugin.TrialsEnabled.Value && Plugin.TrialChance.Value > 0f) options.Add(BountyKind.Trial);
            if (Plugin.InfusionEnabled.Value) options.Add(BountyKind.Infuse);
            if (options.Count == 0) return;
            Kind = options[UnityEngine.Random.Range(0, options.Count)];
            Need = Kind == BountyKind.Elites ? 5 : Kind == BountyKind.Infuse ? 2 : 1;
            Detail = Kind == BountyKind.Elite ? ((EliteTrait)UnityEngine.Random.Range(0, Elites.TraitCount)).ToString() : null;
            Active = true;
            Plugin.Log.LogInfo("BOUNTY for the next run: " + Text);
        }

        internal static string Text
        {
            get
            {
                switch (Kind)
                {
                    case BountyKind.Elite: return "Slay a " + Detail + " elite";
                    case BountyKind.Elites: return "Slay " + Need + " elites";
                    case BountyKind.Special: return "Defeat a special mini-boss";
                    case BountyKind.Rival: return "Defeat a rival wizard";
                    case BountyKind.Trial: return "Clear an infusion trial";
                    default: return "Infuse " + Need + " arcana at Tomi";
                }
            }
        }

        static int Reward { get { return 60 + 30 * Mathf.Max(0, GameController.tierCount); } }

        internal static void Report(BountyKind k, string detail)
        {
            try
            {
                if (!Active || Done || !Plugin.BountiesEnabled.Value || !TomiInfusion.InRun) return;
                bool match = k == Kind && (Kind != BountyKind.Elite || detail == Detail);
                if (Kind == BountyKind.Elites && k == BountyKind.Elite) match = true;
                if (!match) return;
                Have++;
                ShowUntil = Time.unscaledTime + 5f;
                Plugin.Log.LogInfo("BOUNTY progress " + Have + "/" + Need);
                if (Have >= Need) Complete();
            }
            catch (Exception) { }
        }

        static void Complete()
        {
            Done = true;
            int gold = Reward;
            Player p = Elites.Player0;
            if (p != null) try { LootManager.DropGold(p.transform.position, gold); } catch (Exception) { }
            IntroUI.Show("Bounty complete", Text + "  ·  " + gold + " gold", new Color(1f, 0.82f, 0.3f), null, 3f);
            Plugin.Log.LogInfo("BOUNTY complete: " + Text + " (" + gold + " gold)");
        }
    }

    // Rolls the bounty on Hub arrival, ends grudges with the run, and draws the bounty line.
    internal class BountyBoard : MonoBehaviour
    {
        bool wasInHub;
        static GUIStyle style;
        static Font styleFont;

        void Update()
        {
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name ?? "";
            bool inHub = scene == "Hub";
            if (inHub && !wasInHub)
            {
                Bounties.Roll();
                Grudges.EndRun();
                Infusion.ClearRunDuals(); // v0.9.18
            }
            if (!inHub && wasInHub)
            {
                // The run begins.
                if (Bounties.Active) Bounties.ShowUntil = Time.unscaledTime + 6f;
                try { Infusion.RollRunDuals(); } catch (Exception ex) { Plugin.Log.LogWarning("INFUSION dual roll failed: " + ex.Message); } // v0.9.18
            }
            wasInHub = inHub;
        }

        void OnGUI()
        {
            if (!Bounties.Active || !Plugin.BountiesEnabled.Value || ModifiersMenu.IsOpen) return;
            bool inHub = !TomiInfusion.InRun;
            if (!inHub && (Bounties.Done || Time.unscaledTime > Bounties.ShowUntil)) return;
            if (style == null || GameFont.Changed(ref styleFont)) style = GameFont.Style(GUI.skin.label, 15, TextAnchor.UpperLeft, false, true);
            string line = (inHub ? "Next run's bounty:  " : "Bounty:  ") + Bounties.Text + (Bounties.Need > 1 ? "  (" + Bounties.Have + "/" + Bounties.Need + ")" : "")
                + "  ·  " + (60 + 30 * Mathf.Max(0, GameController.tierCount)) + "+ gold";
            GameFont.Shadowed(new Rect(16f, 14f, 620f, 24f), line, style, new Color(1f, 0.85f, 0.45f));
        }
    }
}
