using System;
using System.Collections.Generic;
using UnityEngine;

namespace WolHybridBosses
{
    // v0.9.20: rival voice lines, for flavour. A dialog box at the bottom of the screen with the speaker's name, typed
    // out in the game's font: when the fight starts (after the intro card), once at half health, and on defeat. Each
    // element has its own personality (Fire hot-headed, Water cool, Earth steady, Wind playful, Thunder loud, Chaos
    // gleeful, Radiant noble, Prism whimsical); the Echo of your last run, Archons and rematches have their own lines.
    internal enum RivalVoice { Rival, Echo, Archon, Rematch, Primordial }

    internal static class RivalLines
    {
        static readonly Dictionary<string, string[][]> ByPrefix = new Dictionary<string, string[][]>
        {
            { "Flame", new[] {
                new[] { "Hope you brought sunscreen, because I'm bringing the heat!", "Let's make this quick. My arcana are already warmed up!", "Flames up! Show me what you've got!" },
                new[] { "Ow, ow! Okay, now I'm fired up for real!", "Is it getting hot in here, or is that just me losing?", "You're good... but I burn brighter!" },
                new[] { "Fizzled out... That was a blast, though!", "Gah! I'll rekindle, you'll see!", "Okay, okay, you win this round. Toasty work!" } } },
            { "Frost", new[] {
                new[] { "Stay cool. This will only hurt a lot.", "Let's keep this civil... and chilly.", "Mind the ice. Actually, please slip." },
                new[] { "Hm. You're colder than I expected.", "Composure... composure!", "A crack in the ice. Impressive." },
                new[] { "Melted... how embarrassing.", "Well played. I'll go chill out now.", "You thawed me fair and square." } } },
            { "Earth", new[] {
                new[] { "I am the mountain. Go ahead, try to move me.", "Solid footing, solid fight. Let's begin.", "Rock beats everything. Probably." },
                new[] { "You've chipped the stone... not bad.", "I can take a few more of those. A few.", "Steady... steady..." },
                new[] { "Crumbled... Respect, wizard.", "Even mountains erode. Well fought.", "Back to the quarry for me." } } },
            { "Wind", new[] {
                new[] { "Catch me if you can! Bet you can't!", "Breezy fight, no hard feelings, okay?", "Whoosh! Did you see that? Neither did I!" },
                new[] { "Hey! That one actually landed!", "Okay, okay, the wind's changing!", "Gusty move. I like it!" },
                new[] { "Blown away... literally!", "Aw, gone with the wind...", "You win! I'm gonna go drift somewhere." } } },
            { "Thunder", new[] {
                new[] { "Get ready for a SHOCKING performance!", "Zap first, questions later!", "I've been charging all day for this!" },
                new[] { "Ngh! Overloading... in a good way!", "That jolted me! Do it again, I dare you!", "My circuits are fine! Totally fine!" },
                new[] { "Short-circuited... ugh.", "Grounded... for now!", "Power's out. Good game, wizard!" } } },
            { "Chaos", new[] {
                new[] { "Order is boring. Let's make a mess!", "The Council won't like this. That's the fun part!", "Chaos, chaos, chaos! Ahaha!" },
                new[] { "Oh, that's delightful! More!", "You're disrupting my disruption!", "Unpredictable! I love it!" },
                new[] { "Ahaha... what a beautiful mess...", "Order wins? Ugh, how dull.", "I'll be back. Probably. Maybe. Who knows!" } } },
            { "Radiant", new[] {
                new[] { "Behold the light! Try not to squint.", "I shine for the crowd. Let's give them a show.", "The dawn is on my side today." },
                new[] { "My glow flickers... but it holds!", "A shadow falls... temporarily!", "Bright move, wizard!" },
                new[] { "Dimmed... you outshone me.", "The light fades... with grace.", "Radiantly defeated. Well done." } } },
            { "Prism", new[] {
                new[] { "Every colour at once! Pick your favourite!", "Red, blue, green... which one hits next? Surprise!", "Let's paint this arena!" },
                new[] { "My spectrum's all scrambled!", "Ooh, you've got colourful moves too!", "That one left a mark. A rainbow mark!" },
                new[] { "All my colours... washed out!", "Refracted and rejected...", "You win! That was beautiful!" } } },
        };

        static readonly string[][] Echo = {
            new[] { "Hey, it's me! Well... you. From last time.", "I remember every spell you cast. Let's see if you do.", "Same arcana, same robe. Better me." },
            new[] { "Huh. So that's how it feels.", "We're evenly matched. Annoying, right?", "You've gotten better since I left!" },
            new[] { "Good. Never be the old you.", "Next run, I'll be you again. See you there.", "Fading... nice growth, wizard." } };

        static readonly string[][] Archon = {
            new[] { "An Archon does not fall to hopefuls.", "You climbed all the way up here? Then fall from the top!", "The arena bows to me. You will too." },
            new[] { "Half my power is still more than yours!", "Impressive... now I stop holding back!", "You dare scratch an Archon?!" },
            new[] { "The arena... has a new champion.", "Defeated by a contestant... remarkable.", "Take the title. You've earned it." } };

        static readonly string[] RematchGreet = { "Remember me? I've been practising!", "Round two! I brought some new tricks.", "You got lucky last time. Let's prove it!" };
        static readonly string[] RematchPromise = { "This isn't over! I'll find you later!", "Next time I'll be ready for you!", "Rematch! I demand a rematch!" };

        static string Pick(string[] options) { return options[UnityEngine.Random.Range(0, options.Length)]; }

        static string[][] Set(RivalVoice voice, string prefix)
        {
            if (voice == RivalVoice.Echo) return Echo;
            if (voice == RivalVoice.Archon) return Archon;
            string[][] s;
            return prefix != null && ByPrefix.TryGetValue(prefix, out s) ? s : ByPrefix["Flame"];
        }

        internal static string Greet(RivalVoice voice, string prefix) { return voice == RivalVoice.Rematch ? Pick(RematchGreet) : Pick(Set(voice, prefix)[0]); }
        internal static string Hurt(RivalVoice voice, string prefix) { return voice == RivalVoice.Primordial ? WhiteAlchemist.HurtLine() : Pick(Set(voice, prefix)[1]); }
        internal static string Defeat(RivalVoice voice, string prefix) { return voice == RivalVoice.Primordial ? WhiteAlchemist.DefeatLine() : Pick(Set(voice, prefix)[2]); }
        internal static string Promise() { return Pick(RematchPromise); }
    }

    // The dialog box (one at a time; a new line replaces the current one).
    internal class RivalSpeech : MonoBehaviour
    {
        static RivalSpeech instance;
        string speaker, text;
        Color colour;
        float start, until;
        static GUIStyle nameStyle, textStyle;
        static Font styleFont;

        // v1.1.1: conversations (ChampionSpeech.Converse): a "next" hint, typing that can be finished early, and hiding.
        internal static bool Skippable;
        internal static bool FullyShown { get { return instance == null || instance.text == null || (Time.unscaledTime - instance.start) * 45f >= instance.text.Length; } }
        internal static void ShowAll() { if (instance != null && instance.text != null) instance.start = Time.unscaledTime - instance.text.Length / 45f - 0.05f; }
        internal static void Hide() { if (instance != null) instance.until = 0f; }

        internal static void Say(string speaker, string line, Color colour, float seconds = 4f)
        {
            if (string.IsNullOrEmpty(line)) return;
            if (instance == null)
            {
                GameObject go = new GameObject("HybridBosses_RivalSpeech");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<RivalSpeech>();
            }
            instance.speaker = speaker;
            instance.text = line;
            instance.colour = colour;
            instance.start = Time.unscaledTime;
            instance.until = Time.unscaledTime + seconds + line.Length * 0.03f;
            Plugin.Log.LogInfo("RIVAL says (" + speaker + "): " + line);
        }

        void OnGUI()
        {
            if (text == null || Time.unscaledTime > until || ModifiersMenu.IsOpen) return;
            if (textStyle == null || GameFont.Changed(ref styleFont))
            {
                nameStyle = GameFont.Style(GUI.skin.label, 16, TextAnchor.MiddleLeft, true);
                textStyle = GameFont.Style(GUI.skin.label, 18, TextAnchor.UpperLeft, false, true);
            }
            int shown = Mathf.Clamp(Mathf.FloorToInt((Time.unscaledTime - start) * 45f), 0, text.Length); // typed out
            string visible = text.Substring(0, shown);
            float w = Mathf.Min(760f, Screen.width - 40f), h = 92f;
            Rect box = new Rect((Screen.width - w) / 2f, Screen.height - h - 120f, w, h);
            Color old = GUI.color;
            GUI.color = new Color(0.04f, 0.03f, 0.07f, 0.85f);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = new Color(colour.r, colour.g, colour.b, 0.95f);
            GUI.DrawTexture(new Rect(box.x, box.y, 4f, box.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(box.x, box.y, box.width, 2f), Texture2D.whiteTexture);
            GUI.color = old;
            GameFont.Shadowed(new Rect(box.x + 18f, box.y + 6f, box.width - 36f, 24f), speaker, nameStyle, colour);
            GameFont.Shadowed(new Rect(box.x + 18f, box.y + 32f, box.width - 36f, box.height - 38f), "\"" + visible + (shown >= text.Length ? "\"" : ""), textStyle, new Color(1f, 0.97f, 0.9f));
            if (Skippable && shown >= text.Length) // v1.1.1
            {
                if (hintStyle == null || hintStyle.font != nameStyle.font) hintStyle = new GUIStyle(nameStyle) { alignment = TextAnchor.MiddleRight, fontSize = 13 };
                float blink = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 3f));
                GameFont.Shadowed(new Rect(box.x, box.yMax - 22f, box.width - 14f, 18f), "Confirm  ▶", hintStyle, new Color(1f, 0.92f, 0.7f, blink));
            }
        }

        static GUIStyle hintStyle;
    }

    // On a rival: its half-health and defeat lines.
    internal class RivalBanter : MonoBehaviour
    {
        public RivalVoice Voice;
        public string Prefix, Speaker;
        public Color Colour;
        Enemy enemy;
        int maxHp;
        bool hurtSaid, defeatSaid;

        void Start() { enemy = GetComponent<Enemy>(); }

        void Update()
        {
            if (enemy == null || enemy.health == null || defeatSaid) return;
            int hp = enemy.health.CurrentHealthValue;
            if (maxHp <= 0) { maxHp = Mathf.Max(1, hp); return; }
            if (hp <= 0)
            {
                defeatSaid = true;
                RivalSpeech.Say(Speaker, RivalLines.Defeat(Voice, Prefix), Colour, 4f);
                return;
            }
            if (!hurtSaid && hp <= maxHp * 0.5f)
            {
                hurtSaid = true;
                RivalSpeech.Say(Speaker, RivalLines.Hurt(Voice, Prefix), Colour, 3f);
            }
        }
    }
}
