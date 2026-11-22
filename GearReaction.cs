using System.Collections;
using UnityEngine;

namespace WolHybridBosses
{
    // v1.1.1: rivals, Archons and the Primordial Alchemist notice what you wear: a Primordial robe (their own
    // element's, or another's), the White Mantle, the Grandmaster's Robe, or your Grandmaster rank. One extra line,
    // after their greeting (rivals) or before their challenge (champions). null when there's nothing to remark on.
    internal static class GearReaction
    {
        static string Pick(params string[] o) { return o[Random.Range(0, o.Length)]; }

        static string El(PrimordialRobe r) { return r.Element == "Water" ? "water" : r.Element.ToLowerInvariant(); }

        internal static string ForRival(string prefix)
        {
            PrimordialRobe r = PrimordialRobes.Worn;
            if (r != null && r.Prefix == prefix)
                return Pick("That robe... you beat the " + r.Element + " Archon? Then even my own element is on your side. Unfair!",
                            "You're wearing the " + r.Element + " Archon's robe to fight ME? That's just rude.");
            if (r != null)
                return Pick("Is that the " + r.Element + " Archon's robe? So the rumours are true.",
                            "The " + r.Name + "... you didn't buy that. Fine, I'll take you seriously.");
            if (WhiteMantle.Worn)
                return Pick("That white... you've faced the Primordial Alchemist. And walked away?", "The White Mantle. I thought it was a story.");
            if (GrandmasterRobe.Worn)
                return Pick("The Grandmaster's gold! Every rival in the league wants that robe off your back.", "Gold robe, big target. Let's go.");
            if (League.On && League.RankIndex >= League.TopRank)
                return Pick("The Grandmaster, in person. Let's see if you earned it.", "Beat the Grandmaster and I'm a legend. Hold still.");
            return null;
        }

        internal static string ForArchon(string prefix)
        {
            PrimordialRobe r = PrimordialRobes.Worn;
            if (r != null && r.Prefix == prefix) return Pick("You come wearing my essence. Bold... or foolish.", "That robe was woven from me. I want it back.");
            if (r != null) return Pick("My sibling's robe. You have beaten one of us. You will not beat another.", "You wear the " + r.Element + " Archon's skin. It will not save you from mine.");
            if (WhiteMantle.Worn) return Pick("You wear our parent's white. You have no right.", "The Alchemist's mantle... so you are the one it chose.");
            if (GrandmasterRobe.Worn) return Pick("Gold? Trinkets of the arena. They mean nothing here.", "The league's champion. How small that title sounds in this room.");
            return null;
        }

        internal static string ForAlchemist()
        {
            PrimordialRobe r = PrimordialRobes.Worn;
            if (WhiteMantle.Worn) return Pick("You wear my mantle to face me? Then you know what you are fighting.", "My own white, on your shoulders. Let us see if it fits.");
            if (r != null) return Pick("The " + r.Element + " robe. I remember spinning that thread from the first " + El(r) + ".", "You wear one of my children. Do they whisper to you?");
            if (GrandmasterRobe.Worn) return Pick("The arena's gold. A toy, next to what I am.", "Grandmaster of wizards. I am grandmaster of everything they cast.");
            return null;
        }

        // A rival's remark a moment after its greeting.
        internal static IEnumerator FollowRival(string speaker, string prefix, Color colour)
        {
            string line = ForRival(prefix);
            if (line == null) yield break;
            yield return new WaitForSeconds(3.6f);
            RivalSpeech.Say(speaker, line, colour, 3.2f);
        }
    }
}
