using System;
using System.Collections.Generic;
using UnityEngine;

namespace WolHybridBosses
{
    // v1.1.0: the Rival League. Each element's rival is a named wizard who remembers you: your record against them
    // (wins / losses) is kept between runs and shown on their intro; their greeting depends on it; they always wear the
    // same robe. Beating rivals earns league points by difficulty (Easy 1, Medium 2, Hard 3, Archon 4, tournament
    // rounds +1); the points give a rank shown in the Hub (Novice, Adept, Duelist, Champion, Grandmaster). Higher ranks
    // make Hard rivals more likely and pay a gold bonus per win. The Echo of your last run isn't in the league.
    internal sealed class LeagueRival
    {
        public string Prefix, Name;
        public int Wins { get { return Progress.GetInt("league." + Prefix + ".wins"); } }     // yours, against them
        public int Losses { get { return Progress.GetInt("league." + Prefix + ".losses"); } }
        public string Record { get { return "Record " + Wins + "–" + Losses; } }
    }

    internal static class League
    {
        static readonly Dictionary<string, string> Names = new Dictionary<string, string>
        {
            { "Flame", "Ember Vex" }, { "Frost", "Mira Frostwhisper" }, { "Earth", "Gravel Thorne" }, { "Wind", "Zephy Quill" },
            { "Thunder", "Volta Brash" }, { "Chaos", "Nyx Havoc" }, { "Radiant", "Sol Aurelian" }, { "Prism", "Iris Spectra" },
        };

        static readonly string[] RankNames = { "Novice", "Adept", "Duelist", "Champion", "Grandmaster" };
        static readonly int[] RankPoints = { 0, 6, 16, 32, 55 };

        internal static bool On { get { return Plugin.LeagueEnabled.Value; } }

        internal static LeagueRival For(string prefix)
        {
            string n;
            if (!On || prefix == null || !Names.TryGetValue(prefix, out n)) return null;
            return new LeagueRival { Prefix = prefix, Name = n };
        }

        internal static int Points { get { return Progress.GetInt("league.points"); } }

        internal static int RankIndex
        {
            get { int pts = Points, r = 0; for (int i = 0; i < RankPoints.Length; i++) if (pts >= RankPoints[i]) r = i; return r; }
        }

        internal static string RankName { get { return RankNames[RankIndex]; } }

        internal static string NextRankText
        {
            get { int r = RankIndex; return r + 1 < RankPoints.Length ? (RankPoints[r + 1] - Points) + " pts to " + RankNames[r + 1] : "top rank"; }
        }

        static int PointsFor(RivalTier t, bool tournament)
        {
            int p = t == RivalTier.Easy ? 1 : t == RivalTier.Medium ? 2 : t == RivalTier.Hard ? 3 : 4;
            return p + (tournament ? 1 : 0);
        }

        // You won: record, points, gold bonus by rank, a card when you rank up.
        internal static void RecordWin(LeagueRival r, RivalTier tier, Vector2 at, bool tournament = false)
        {
            if (r == null) return;
            int before = RankIndex;
            Progress.AddInt("league." + r.Prefix + ".wins", 1);
            Progress.AddInt("league.points", PointsFor(tier, tournament));
            int bonus = RankIndex * 10 * ((int)Mathf.Min((int)tier, 2) + 1);
            if (bonus > 0) try { LootManager.DropGold(at, bonus); } catch (Exception) { }
            Plugin.Log.LogInfo("LEAGUE beat " + r.Name + " (" + r.Record + "), " + Points + " pts, " + RankName + (bonus > 0 ? ", +" + bonus + " gold" : ""));
            if (RankIndex > before) IntroUI.Show("League rank: " + RankName, Points + " pts  ·  rivals get tougher, wins pay more", new Color(1f, 0.85f, 0.45f), null, 4f);
        }

        internal static void RecordLoss(LeagueRival r)
        {
            if (r == null) return;
            Progress.AddInt("league." + r.Prefix + ".losses", 1);
            Plugin.Log.LogInfo("LEAGUE lost to " + r.Name + " (" + r.Record + ")");
        }

        // Each named rival always wears the same robe (picked from the robe list by their name).
        internal static string OutfitFor(LeagueRival r)
        {
            try
            {
                List<Outfit> list = Outfit.OutfitList;
                if (list == null || list.Count == 0) return null;
                int h = 0;
                foreach (char c in r.Name) h = h * 31 + c;
                return list[Mathf.Abs(h) % list.Count].outfitID;
            }
            catch (Exception) { return null; }
        }

        // Hard rivals are more likely at higher ranks (added to the Hard weight of the difficulty roll).
        internal static int HardWeightBonus { get { return On ? RankIndex * 6 : 0; } }

        static string Pick(string[] o) { return o[UnityEngine.Random.Range(0, o.Length)]; }

        // Their greeting, from your record against them.
        internal static string Greet(LeagueRival r)
        {
            int w = r.Wins, l = r.Losses;
            if (w == 0 && l == 0)
                return Pick(new[] { "I'm " + r.Name + ". Remember the name. You'll be hearing it a lot.", "So you're the one everyone's talking about. I'm " + r.Name + ". Let's see.", r.Name + ", at your service. Well, at your defeat." });
            if (w > l)
                return Pick(new[] { "You're " + w + "-" + l + " against me. That ends today!", "I've been training since our last fight. " + w + " losses is plenty!", "Not you again... fine. This time's different!" });
            if (l > w)
                return Pick(new[] { "I'm " + l + "-" + w + " against you. Want to make it " + (l + 1) + "?", "Back for more? I do love a regular.", "You know how this goes. But please, try." });
            return Pick(new[] { "Even at " + w + " apiece. Let's settle it.", "Tied record. Not for long!", "Our score's level. Time to break the tie." });
        }
    }
}
