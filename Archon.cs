using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WolHybridBosses
{
    // v1.1.1: Archons (the final champions that replace Sura, and the Archons behind Hard rival portals) look like
    // primordial versions of their element: the whole body in the element's palette (as the Primordial Alchemist is in
    // white) with a permanent aura of its colour. In the final room they also speak before the fight, as the element
    // itself: the Primordial Alchemist's eldest children.
    internal static class ArchonLook
    {
        // Ramp key for a rival prefix (Flame, Frost, Earth, Wind, Thunder, Chaos, Radiant). Prism has its own cycling
        // look, so it keeps it.
        internal static string RampFor(Enemy e, string prefix)
        {
            if (prefix == "Chaos") return "Chaos";
            if (prefix == "Radiant") return "Radiant";
            if (prefix == "Prism") return null;
            ElementType el;
            return ElementalGiants.TryGetElement(e, prefix, out el) ? ElementRamps.ForElement(el) : null;
        }

        // Without an enemy at hand (the loading board).
        internal static string RampForPrefix(string prefix)
        {
            switch (prefix)
            {
                case "Flame": return "Fire";
                case "Frost": return "Water";
                case "Earth": return "Earth";
                case "Wind": return "Wind";
                case "Thunder": return "Thunder";
                case "Chaos": return "Chaos";
                case "Radiant": return "Radiant";
                default: return null;
            }
        }

        internal static void Setup(Enemy e, string prefix)
        {
            if (e == null) return;
            string key = RampFor(e, prefix);
            if (key != null) try { RecolorEngine.RecolorBody(e, key, false); } catch (Exception ex) { Plugin.Log.LogWarning("ARCHON recolour failed: " + ex.Message); }
            Color c = ElementalGiants.ElementColor(prefix);
            WhiteAura.Attach(e.transform, e.spriteRenderer != null ? e.spriteRenderer.material : null, new Color(c.r * 1.5f, c.g * 1.5f, c.b * 1.5f, 0.5f));
            Plugin.Log.LogInfo("ARCHON look: " + prefix + " (" + (key ?? "own look") + "), aura");
        }
    }

    internal static class ArchonLines
    {
        static string Pick(string[] o) { return o[UnityEngine.Random.Range(0, o.Length)]; }

        static readonly Dictionary<string, string[]> Hello = new Dictionary<string, string[]>
        {
            { "Flame", new[] { "I am the first fire. Every spark you have cast was a candle lit from me.", "Before there was warmth, there was me. I am the Fire Archon." } },
            { "Frost", new[] { "I am the first tide and the first frost. Every drop remembers my name.", "The oceans are my breath held still. I am the Water Archon." } },
            { "Earth", new[] { "I am the bedrock under every world. Mountains are my bones.", "Patience made flesh. I am the Earth Archon, and I have waited for you." } },
            { "Wind", new[] { "I am the first breath. Every gust you have ridden was me, passing through.", "You cannot cage the sky. I am the Wind Archon." } },
            { "Thunder", new[] { "I am the first storm. Every bolt you throw is an echo of my voice.", "The sky splits when I speak. I am the Thunder Archon." } },
            { "Chaos", new[] { "I am what the elements were before they had names. I am the Chaos Sovereign.", "Order is a story you tell yourself. I am the Chaos Sovereign, and I am done listening." } },
            { "Radiant", new[] { "I am the first light, the dawn before all dawns. I am the Radiant Archon.", "Every shadow you cast, I made possible. I am the Radiant Archon." } },
            { "Prism", new[] { "I am every colour at once, light split and reborn. I am the Prismatic Archon.", "One light, endless faces. I am the Prismatic Archon." } },
        };

        static readonly string[] Throne =
        {
            "Sura's throne is empty tonight. The elements have sent their eldest instead.",
            "The Council's master is not here. You face something older.",
            "Sura borrowed our power for years. Tonight we take the stage ourselves.",
        };

        static readonly string[] Challenge =
        {
            "Show me you are worthy of the power you have borrowed.",
            "Come, little wizard. Return what is ours.",
            "Let us see if the elements still answer you when I am in the room.",
        };

        internal static List<string> IntroLines(string prefix)
        {
            string[] h;
            List<string> l = new List<string> { Hello.TryGetValue(prefix ?? "", out h) ? Pick(h) : "I am an Archon, the element made flesh." };
            l.Add(Pick(Throne));
            l.Add(Pick(Challenge));
            return l;
        }
    }

    // A champion's spoken intro in the final room: frozen and untouchable while it speaks, the camera on it.
    internal static class ChampionSpeech
    {
        internal static IEnumerator Monologue(Enemy boss, string speaker, List<string> lines, Color colour)
        {
            if (boss == null || lines == null || lines.Count == 0) yield break;
            float total = 0f;
            foreach (string l in lines) total += 3.2f + l.Length * 0.035f;
            boss.enabled = false;
            if (boss.health != null) try { boss.health.SetInvulnerabilityDuration(total + 1f, false); } catch (Exception) { }
            try { CameraController cam = CameraController.Instance; if (cam != null) cam.OverrideAndMoveCameraToLocation(boss.transform, null, total, true, false, false, false); } catch (Exception) { }
            foreach (string l in lines)
            {
                if (boss == null) yield break;
                float wait = 3.2f + l.Length * 0.035f;
                RivalSpeech.Say(speaker, l, colour, wait - 0.4f);
                yield return new WaitForSeconds(wait);
            }
            Plugin.Log.LogInfo("FINAL " + speaker + " spoke " + lines.Count + " lines");
        }
    }

    // v1.1.1: alternate endings. Sura keeps the game's own ending. When a champion falls instead, the room's victory
    // runs as usual (Sura's victory dialogue is off on champion runs), and this epilogue plays in its place: the
    // champion's last words while you stand in the recoloured castle, then an ending card of its own. The exit portal
    // and the game's ending scene follow as normal.
    internal static class ChampionEpilogue
    {
        static readonly Dictionary<string, string[]> Last = new Dictionary<string, string[]>
        {
            { "Flame", new[] { "So... the spark outshines the fire.", "I return to the first flame. Every ember you light will remember tonight.", "Burn brightly, wizard. Burn for both of us." } },
            { "Frost", new[] { "The tide... recedes.", "I flow back into every sea and every frost. You will find me in the rain.", "Be like water, wizard. Do not let them hold you." } },
            { "Earth", new[] { "Even mountains wear away.", "I sink back into the bedrock. Every stone you raise is mine, lent to you.", "Stand firm, wizard. The ground will hold you." } },
            { "Wind", new[] { "Ha... caught at last.", "I scatter into every breeze. You will feel me on every road you run.", "Run free, wizard. Nothing should cage you." } },
            { "Thunder", new[] { "The storm... falls quiet.", "I go back into the clouds. Listen for me in every thunderclap.", "Strike first, wizard. Strike true." } },
            { "Chaos", new[] { "Order wins... for now.", "I unravel into everything at once. You will never be rid of me, and you would not want to be.", "Stay strange, wizard. It suits you." } },
            { "Radiant", new[] { "The light... dims.", "I return to the dawn. Every morning, I will rise where you can see me.", "Shine, wizard. Someone is always watching for your light." } },
            { "Prism", new[] { "Every colour... fading to one.", "I split back into the spectrum. Look for me where the light bends.", "Be many things, wizard. Be all of them well." } },
        };

        static readonly Dictionary<string, string> Endings = new Dictionary<string, string>
        {
            { "Flame", "The First Fire Returns" }, { "Frost", "The Tide Goes Out" }, { "Earth", "The Mountain Sleeps" }, { "Wind", "The Gale Is Free" },
            { "Thunder", "The Storm Falls Silent" }, { "Chaos", "The Sovereign Unravels" }, { "Radiant", "The Last Dawn" }, { "Prism", "The Spectrum Scatters" },
        };

        static readonly string[] AlchemistLast =
        {
            "So this is what it feels like... to be transmuted.",
            "Do not mourn me. I was never one thing. I was always every element, waiting to be mixed.",
            "Now I settle into the one who fused me. Carry the essence, wizard. Every spark you cast is mine, and yours.",
        };

        internal static IEnumerator Play(string prefix, bool alchemist, string title)
        {
            // After the victory card and the reward cards.
            float wait = 0f;
            while (IntroUI.Busy && wait < 10f) { wait += 0.25f; yield return new WaitForSeconds(0.25f); }
            yield return new WaitForSeconds(1f);
            Color colour = alchemist ? WhiteAlchemist.White : ElementalGiants.ElementColor(prefix);
            string[] last;
            if (alchemist) last = AlchemistLast;
            else if (!Last.TryGetValue(prefix ?? "", out last)) last = new[] { "The element returns to its source." };
            foreach (string l in last)
            {
                float t = 3.2f + l.Length * 0.035f;
                RivalSpeech.Say(title, l, colour, t - 0.4f);
                yield return new WaitForSeconds(t);
            }
            string ending;
            if (alchemist) ending = "The Primordial Return";
            else if (!Endings.TryGetValue(prefix ?? "", out ending)) ending = "The Element Returns";
            IntroUI.Show("Ending: " + ending, alchemist ? "The essence of every element lives on in you" : "Sura's throne stays empty  ·  " + title + " has returned to its element", colour, null, 6f);
            Plugin.Log.LogInfo("FINAL epilogue: " + ending);
        }
    }
}