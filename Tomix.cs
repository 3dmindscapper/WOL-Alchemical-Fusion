using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace WolHybridBosses
{
    // v1.1.1: Tomix, the Mixing Tome: Tomi's gold-leafed, scorch-marked cousin, and the one who does the infusing now.
    //   - Hub: he floats beside Tomi. His book is where you choose each arcana's version ([I] / pad arcana buttons),
    //     and his journal (J / pad Interact while his book is open) lists your infusions page by page, per element.
    //   - Runs: he (not Tomi) comes to the arcana merchant, and his book is where you infuse.
    //   Tomi's own book is the game's again, with nothing of ours in it.
    // He's a recoloured spellbook NPC (Tomi's prefab on the Radiant ramp: gold), with a name over his head. Talking to
    // him skips Tomi's dialogue (it would show Tomi's name and portrait): he says one of his own lines in the speech
    // box and opens his book straight away.
    internal class TomixMark : MonoBehaviour
    {
        static GUIStyle nameStyle, subStyle;
        static Font styleFont;

        // v1.1.2: his pages glow through every element in turn: a soft tint that fades from one element's colour to
        // the next (the book's own detail stays; a full recolour flattened it into a yellow blob), with a little burst of
        // the element as each one arrives. An infusion tome, not a blue Tomi.
        static readonly ElementType[] Cycle = { ElementType.Fire, ElementType.Water, ElementType.Earth, ElementType.Air, ElementType.Lightning, ElementType.Chaos, ElementType.Ice };
        const float StepSeconds = 1.8f;
        SpriteRenderer[] parts;
        Color[] baseColours;
        int lastStep = -1;

        void Start()
        {
            parts = GetComponentsInChildren<SpriteRenderer>(true);
            baseColours = new Color[parts.Length];
            for (int i = 0; i < parts.Length; i++) baseColours[i] = parts[i] != null ? parts[i].color : Color.white;
        }

        void Update()
        {
            if (parts == null) return;
            float t = Time.time / StepSeconds;
            int step = Mathf.FloorToInt(t) % Cycle.Length;
            float f = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - Mathf.Floor(t)) * 2.5f)); // fade in the first 40% of a step
            Color from = Infusion.ColorOf(Cycle[(step + Cycle.Length - 1) % Cycle.Length]), to = Infusion.ColorOf(Cycle[step]);
            Color glow = Color.Lerp(from, to, f);
            Color tint = Color.Lerp(Color.white, glow, 0.5f + 0.08f * Mathf.Sin(Time.time * 4f));
            for (int i = 0; i < parts.Length; i++)
                if (parts[i] != null) parts[i].color = new Color(baseColours[i].r * tint.r, baseColours[i].g * tint.g, baseColours[i].b * tint.b, baseColours[i].a);
            if (step == lastStep) return;
            lastStep = step;
            Player p = Elites.Player0;
            if (p != null && Vector2.Distance(p.transform.position, transform.position) < 14f)
                HitFlair.Spawn(transform.position + new Vector3(0f, 0.9f, 0f), Cycle[step], parts.Length > 0 ? parts[0] : null);
        }
        void OnGUI()
        {
            if (TomiInfusion.MenuOpen || ModifiersMenu.IsOpen || LeagueMenu.IsOpen || NocturneMenu.IsOpen) return;
            Player p = Elites.Player0;
            Camera cam = Camera.main;
            if (p == null || cam == null || Vector2.Distance(p.transform.position, transform.position) > 6f) return;
            Vector3 sp = cam.WorldToScreenPoint(transform.position + new Vector3(0f, 1.9f, 0f));
            if (sp.z < 0f) return;
            if (nameStyle == null || GameFont.Changed(ref styleFont))
            {
                nameStyle = GameFont.Style(GUI.skin.label, 15, TextAnchor.MiddleCenter, false, true);
                subStyle = GameFont.Style(GUI.skin.label, 12, TextAnchor.MiddleCenter, false, true);
            }
            GameFont.Shadowed(new Rect(sp.x - 150f, Screen.height - sp.y - 30f, 300f, 22f), Tomix.Name, nameStyle, Tomix.Gold);
            GameFont.Shadowed(new Rect(sp.x - 150f, Screen.height - sp.y - 12f, 300f, 18f), TomiInfusion.InRun ? "infusions" : "versions  ·  journal", subStyle, new Color(0.9f, 0.85f, 0.7f));
        }
    }

    internal static class Tomix
    {
        internal const string Name = "Tomix, the Mixing Tome";
        internal static readonly Color Gold = new Color(1f, 0.8f, 0.35f);

        static bool openedByTomix;
        internal static bool ConsumeOpen() { bool b = openedByTomix; openedByTomix = false; return b; }
        internal static bool Opening { get { return openedByTomix; } } // between his interaction and the book's Activate postfix

        static string Pick(string[] o) { return o[UnityEngine.Random.Range(0, o.Length)]; }

        static readonly string[] HubHello =
        {
            "Tomix, Grand Mixing Tome! Mind the scorch marks. And the smell. Mostly the smell.",
            "Ah, a test subj- a valued customer! Which reagent shall we admire today?",
            "Tomi catalogues spells. I improve them. Loudly.",
            "Note to self: the last fusion only exploded a LITTLE. Progress!",
            "Every element goes into every other element, given enough heat and enough nerve.",
        };

        static readonly string[] RunHello =
        {
            "Psst! Two arcana and a little gold, and I'll mix you something wonderful. Probably wonderful.",
            "I followed you down here. Tomi says I'm banned from experimenting in the library.",
            "Fire into ice? Thunder into stone? Hand them over, I'm itching to stir something!",
            "Fresh reagents! Oh, you've brought fresh reagents!",
        };

        static readonly string[] Mixed =
        {
            "Ha HA! It lives! ...the arcana, I mean. Obviously.",
            "Beautiful. Unstable, but beautiful.",
            "Write that one down! No, wait, I'm the book. I'll write it down.",
            "Smell that? That's the smell of SCIENCE. And a bit of singed paper.",
        };

        internal static void SayMixed() { RivalSpeech.Say(Name, Pick(Mixed), Gold, 3f); }

        // Make a spellbook NPC into Tomix.
        internal static void Make(SpellBookNpc npc)
        {
            if (npc == null || npc.GetComponent<TomixMark>() != null) return;
            npc.gameObject.AddComponent<TomixMark>();
            npc.name = "HB_Tomix";
            Plugin.Log.LogInfo("TOMIX is here at " + (Vector2)npc.transform.position);
        }

        internal static bool IsTomix(SpellBookNpc npc) { return npc != null && npc.GetComponent<TomixMark>() != null; }

        // His interaction: his own line, then his book.
        internal static bool Interact(SpellBookNpc npc)
        {
            try
            {
                PlayerRoomUI room = PlayerRoomUI.CurrentUI;
                if (room == null || room.AnyUIOpen) return false;
                Player p = Traverse.Create(npc).Field("player").GetValue() as Player ?? Elites.Player0;
                if (p == null) return false;
                RivalSpeech.Say(Name, Pick(TomiInfusion.InRun ? RunHello : HubHello), Gold, 3.5f);
                openedByTomix = true;
                try { if (npc.anim != null) npc.anim.Play("Straighten", 0, 0f); } catch (Exception) { }
                room.OpenSpellBook(p, npc.transform.position);
                Plugin.Log.LogInfo("TOMIX opened his book");
            }
            catch (Exception ex) { openedByTomix = false; Plugin.Log.LogWarning("TOMIX could not open his book: " + ex.Message); }
            return false;
        }

        // In the Hub: beside Tomi, once per visit.
        // v1.1.2: across from Tomi. Tomi hovers over a planter; the matching planter nearby (same sprite) gets Tomix,
        // at the same offset. Waits up to 10s for Tomi (he can appear a moment after the scene). Fallback: 3 to his right.
        internal static IEnumerator BesideTomi()
        {
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            SpellBookNpc tomi = null;
            for (float t = 0f; t < 10f && tomi == null; t += 0.5f)
            {
                yield return new WaitForSeconds(0.5f);
                if (!Plugin.InfusionEnabled.Value || !TomiInfusion.AtHome || UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != scene) yield break;
                foreach (SpellBookNpc s in UnityEngine.Object.FindObjectsOfType<SpellBookNpc>())
                {
                    if (IsTomix(s)) yield break; // already here
                    if (tomi == null) tomi = s;
                }
            }
            if (tomi == null) { Plugin.Log.LogInfo("TOMIX: no Tomi in " + scene); yield break; }
            // The Hub's planters are built from small pieces the search can't pair, so the Hub uses the measured spot:
            // the matching planter is about 8 units to Tomi's right. Elsewhere (your house) the search, then a fallback.
            Vector2 at = scene == "Hub" ? (Vector2)tomi.transform.position + new Vector2(8.1f, 0f) : Opposite(tomi);
            SpellBookNpc spawned = TomiInfusion.SpawnAt(at, false);
            if (spawned != null) { Make(spawned); if (scene == "Hub") BreakTree(at); CopyLight(tomi, at - (Vector2)tomi.transform.position); }
            Plugin.Log.LogInfo("TOMIX placed in " + scene + " at " + at + " (Tomi at " + (Vector2)tomi.transform.position + ")");
        }

        // v1.1.2: the shaft of light that falls through the tree onto Tomi: a scenery sprite over him (not part of his
        // object). Copied over Tomix at the same offset. Everything near and above Tomi is logged, to check in game.
        static readonly string[] LightWords = { "light", "ray", "beam", "shaft", "sun", "god", "glow", "shine", "teleport" };

        // The planter tree where Tomix lands breaks, as Tomi's did (the game's own Tree.Break: wood and leaves fly).
        static void BreakTree(Vector2 at)
        {
            try
            {
                global::Tree best = null;
                float bestD = 4.5f;
                foreach (global::Tree t in UnityEngine.Object.FindObjectsOfType<global::Tree>())
                {
                    if (t == null || !t.gameObject.activeInHierarchy) continue;
                    float d = Vector2.Distance(t.transform.position, at);
                    if (d < bestD) { bestD = d; best = t; }
                }
                if (best == null) { Plugin.Log.LogInfo("TOMIX: no tree where he lands"); return; }
                Traverse.Create(best).Method("Break").GetValue();
                Plugin.Log.LogInfo("TOMIX broke the tree " + best.name + " (" + bestD.ToString("0.0") + " away)");
            }
            catch (Exception ex) { Plugin.Log.LogWarning("TOMIX tree: " + ex.Message); }
        }

        static void CopyLight(SpellBookNpc tomi, Vector2 delta)
        {
            try
            {
                Vector2 tp = tomi.transform.position;
                List<string> near = new List<string>();
                List<GameObject> copy = new List<GameObject>();
                foreach (SpriteRenderer sr in UnityEngine.Object.FindObjectsOfType<SpriteRenderer>())
                {
                    if (sr == null || sr.sprite == null || !sr.enabled || sr.transform.IsChildOf(tomi.transform)) continue;
                    if (sr.transform.root.name.StartsWith("HB_") || sr.GetComponentInParent<Entity>() != null || sr.GetComponentInParent<Canvas>() != null) continue;
                    Bounds b = sr.bounds;
                    if (Mathf.Abs(b.center.x - tp.x) > 3f || b.max.y < tp.y + 1f || b.min.y > tp.y + 3f) continue;
                    string n = (sr.name + " " + sr.sprite.name).ToLowerInvariant();
                    if (near.Count < 14) near.Add(sr.name + "/" + sr.sprite.name + " " + b.size.ToString("0.0"));
                    bool lightish = false;
                    foreach (string w in LightWords) if (n.Contains(w)) { lightish = true; break; }
                    if (!lightish || b.size.x > 8f) continue;
                    // A bare "Sprite" child of an effect: copy the effect (its animator lives on the parent).
                    GameObject node = sr.gameObject;
                    Transform par = node.transform.parent;
                    if (par != null && par.childCount <= 4 && par.GetComponent<Entity>() == null && par.GetComponent<Npc>() == null) node = par.gameObject;
                    if (!copy.Contains(node)) copy.Add(node);
                }
                copy.RemoveAll(go => copy.Exists(o => o != go && go.transform.IsChildOf(o.transform))); // a parent's copy brings its children
                foreach (GameObject go in copy)
                {
                    // v1.1.3: outside its pool (a pooled effect's copy in the pool could be handed out or returned
                    // by the game), and gone once it has played: the beam is Tomi's arrival effect.
                    bool pooled = go.transform.parent != null && go.transform.parent.name.IndexOf("Pool", StringComparison.OrdinalIgnoreCase) >= 0;
                    GameObject c = UnityEngine.Object.Instantiate(go, pooled ? null : go.transform.parent);
                    c.name = "HB_TomixLight " + go.name;
                    c.transform.position = go.transform.position + (Vector3)delta;
                    c.transform.localScale = pooled ? go.transform.lossyScale : go.transform.localScale;
                    if (pooled) UnityEngine.Object.Destroy(c, 6f);
                }
                List<string> names = new List<string>();
                foreach (GameObject go in copy) names.Add(go.name + (go.transform.parent != null ? " in " + go.transform.parent.name : ""));
                Plugin.Log.LogInfo("TOMIX light: " + (copy.Count > 0 ? "copied " + string.Join(", ", names.ToArray()) : "none found") + " | near Tomi: " + string.Join("; ", near.ToArray()));
            }
            catch (Exception ex) { Plugin.Log.LogWarning("TOMIX light failed: " + ex.Message); }
        }

        static readonly string[] NotStands ={ "arrow", "pickup", "shadow", "prompt", "icon", "glow", "light", "halo", "emote", "cursor", "card" };

        static Vector2 Opposite(SpellBookNpc tomi)
        {
            Vector2 tp = tomi.transform.position;
            try
            {
                SpriteRenderer[] all = UnityEngine.Object.FindObjectsOfType<SpriteRenderer>();
                // What Tomi hovers over: big-ish scenery sprites under him (not arrows, pickups, shadows, UI bits).
                List<SpriteRenderer> stands = new List<SpriteRenderer>();
                foreach (SpriteRenderer sr in all)
                {
                    if (sr == null || sr.sprite == null || !sr.enabled || sr.transform.IsChildOf(tomi.transform) || sr.GetComponentInParent<Entity>() != null || sr.GetComponentInParent<Canvas>() != null) continue;
                    string n = sr.sprite.name.ToLowerInvariant() + " " + sr.name.ToLowerInvariant();
                    bool bad = false;
                    foreach (string b in NotStands) if (n.Contains(b)) { bad = true; break; }
                    if (bad) continue;
                    Vector3 c = sr.bounds.center;
                    float area = sr.bounds.size.x * sr.bounds.size.y;
                    if (area < 1.2f || area > 30f || Mathf.Abs(c.x - tp.x) > 2.5f || c.y > tp.y + 0.3f || c.y < tp.y - 4f) continue;
                    stands.Add(sr);
                }
                stands.Sort((a, b) => (b.bounds.size.x * b.bounds.size.y).CompareTo(a.bounds.size.x * a.bounds.size.y));
                List<string> tried = new List<string>();
                foreach (SpriteRenderer stand in stands)
                {
                    tried.Add(stand.sprite.name);
                    SpriteRenderer twin = null;
                    float bestD = float.MaxValue;
                    foreach (SpriteRenderer sr in all)
                    {
                        if (sr == null || sr == stand || sr.sprite == null || sr.sprite.name != stand.sprite.name) continue;
                        float dy = Mathf.Abs(sr.bounds.center.y - stand.bounds.center.y), d = Vector2.Distance(sr.bounds.center, stand.bounds.center);
                        if (dy > 1.5f || d < 3.5f || d > 18f) continue;
                        if (d < bestD) { bestD = d; twin = sr; }
                    }
                    if (twin == null) continue;
                    Plugin.Log.LogInfo("TOMIX: Tomi floats over " + stand.sprite.name + "; its twin is " + bestD.ToString("0.0") + " away (tried " + string.Join(", ", tried.ToArray()) + ")");
                    return (Vector2)twin.bounds.center + (tp - (Vector2)stand.bounds.center);
                }
                Plugin.Log.LogInfo("TOMIX: no twin for what Tomi floats over (candidates: " + (tried.Count > 0 ? string.Join(", ", tried.ToArray()) : "none") + ")");
            }
            catch (Exception ex) { Plugin.Log.LogWarning("TOMIX placement: " + ex.Message); }
            return tp + new Vector2(3f, 0f);
        }
    }

    [HarmonyPatch(typeof(SpellBookNpc), "Interacted")]
    static class TomixInteractPatch
    {
        static bool Prefix(SpellBookNpc __instance) { return !Tomix.IsTomix(__instance) || Tomix.Interact(__instance); }
    }

    // Tomix's journal: open over his book in the Hub. One page per element, listing every arcana with a single
    // version of it and every dual pair that includes it.
    internal class TomixJournal : MonoBehaviour
    {
        internal static bool IsOpen { get; private set; }
        int page, scroll;
        float openedAt, bookSince = -1f;
        const int Rows = 9;
        static GUIStyle titleStyle, rowStyle, small;
        static Font styleFont;

        static List<string> Elements()
        {
            List<string> l = new List<string> { "Fire", "Water", "Ice", "Earth", "Wind", "Thunder", "Chaos" };
            if (ArcanosBridge.Present) { l.Add("Radiant"); l.Add("Prism"); }
            return l;
        }

        static bool Pad(ChaosInputDevice dev, string a) { if (dev == null) return false; try { return dev.GetButtonDown(a); } catch (Exception) { return false; } }

        void Update()
        {
            if (!TomiInfusion.TomixOpen || TomiInfusion.InRun || ModifiersMenu.IsOpen) { IsOpen = false; bookSince = -1f; return; }
            if (bookSince < 0f) bookSince = Time.unscaledTime;
            ChaosInputDevice dev = TomiInfusion.Device;
            if (!IsOpen)
            {
                // v1.1.2: not from the Interact press that opened his book (it popped the journal open the first time).
                if (Time.unscaledTime - bookSince < 0.6f) return;
                if (Input.GetKeyDown(KeyCode.J) || Pad(dev, "Interact")) { IsOpen = true; page = 0; scroll = 0; openedAt = Time.unscaledTime; }
                return;
            }
            if (Time.unscaledTime - openedAt < 0.2f) return;
            if (Input.GetKeyDown(KeyCode.J) || Input.GetKeyDown(KeyCode.Escape) || Pad(dev, "Interact")) { IsOpen = false; return; }
            int n = Elements().Count;
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D) || Pad(dev, "Skill2")) { page = (page + 1) % n; scroll = 0; }
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A) || Pad(dev, "Skill1")) { page = (page - 1 + n) % n; scroll = 0; }
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S) || Pad(dev, "Skill3")) scroll++;
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) scroll = Mathf.Max(0, scroll - 1);
        }

        sealed class Entry { public string Id, Text; public bool Dual; }

        static List<Entry> EntriesFor(string el)
        {
            List<Entry> list = new List<Entry>();
            foreach (string id in InfusionUnlocks.UnlockedIds)
            {
                foreach (ElementType v in InfusionUnlocks.Versions(id))
                    if (Infusion.Name(v) == el) list.Add(new Entry { Id = id, Text = el + "-infused" });
                foreach (ElementType[] pr in InfusionUnlocks.Pairs(id))
                {
                    string a = Infusion.Name(pr[0]), b = Infusion.Name(pr[1]);
                    if (a != el && b != el) continue;
                    string other = a == el ? b : a;
                    list.Add(new Entry { Id = id, Text = "dual with <color=#" + ColorUtility.ToHtmlStringRGB(Infusion.ColorOf(a == el ? pr[1] : pr[0])) + ">" + other + "</color>", Dual = true });
                }
            }
            list.Sort((x, y) => { int c = string.Compare(IntroUI.SkillName(x.Id), IntroUI.SkillName(y.Id), StringComparison.OrdinalIgnoreCase); return c != 0 ? c : x.Dual.CompareTo(y.Dual); });
            return list;
        }

        static ElementType? ElementByName(string n) { return InfusionUnlocks.FromName(n); }

        void OnGUI()
        {
            if (!IsOpen) return;
            if (rowStyle == null || GameFont.Changed(ref styleFont))
            {
                titleStyle = GameFont.Style(GUI.skin.label, 22, TextAnchor.MiddleCenter, true);
                rowStyle = GameFont.Style(GUI.skin.label, 16, TextAnchor.MiddleLeft, false, true);
                small = GameFont.Style(GUI.skin.label, 12, TextAnchor.MiddleCenter, false, true);
            }
            List<string> els = Elements();
            page = Mathf.Clamp(page, 0, els.Count - 1);
            string el = els[page];
            ElementType? et = ElementByName(el);
            Color ec = et.HasValue ? Infusion.ColorOf(et.Value) : Color.white;
            List<Entry> entries = EntriesFor(el);
            int singles = entries.FindAll(x => !x.Dual).Count;
            scroll = entries.Count <= Rows ? 0 : scroll % (entries.Count - Rows + 1);

            float w = Mathf.Min(620f, Screen.width - 40f), h = 132f + 36f * Rows;
            Rect box = new Rect((Screen.width - w) / 2f, Mathf.Max(20f, (Screen.height - h) / 2f), w, h);
            Color old = GUI.color;
            GUI.color = new Color(0.07f, 0.05f, 0.03f, 0.95f);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = new Color(ec.r, ec.g, ec.b, 0.95f);
            GUI.DrawTexture(new Rect(box.x, box.y, box.width, 3f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(box.x, box.yMax - 3f, box.width, 3f), Texture2D.whiteTexture);
            GUI.color = old;
            GameFont.Shadowed(new Rect(box.x, box.y + 8f, box.width, 30f), "Tomix's Journal  ·  " + el, titleStyle, ec);
            GameFont.Shadowed(new Rect(box.x, box.y + 38f, box.width, 18f), "page " + (page + 1) + " of " + els.Count + "  ·  " + singles + " infused  ·  " + (entries.Count - singles) + " dual", small, new Color(0.85f, 0.8f, 0.7f));
            float y = box.y + 62f;
            if (entries.Count == 0)
                GameFont.Shadowed(new Rect(box.x + 30f, y, box.width - 60f, 24f), "Nothing mixed with " + el + " yet. Bring me reagents during a run!", rowStyle, new Color(0.75f, 0.7f, 0.6f));
            for (int i = scroll; i < entries.Count && i < scroll + Rows; i++)
            {
                Entry e = entries[i];
                Sprite icon = IntroUI.SkillIcon(e.Id);
                if (icon != null) IntroUI.DrawSprite(new Rect(box.x + 28f, y + 2f, 30f, 30f), icon, Color.white);
                GameFont.Shadowed(new Rect(box.x + 68f, y + 6f, box.width - 96f, 24f), IntroUI.SkillName(e.Id) + "   <color=#" + ColorUtility.ToHtmlStringRGB(ec) + ">" + (e.Dual ? "" : e.Text) + "</color>" + (e.Dual ? e.Text : ""), rowStyle, new Color(0.95f, 0.92f, 0.85f));
                y += 36f;
            }
            GameFont.Shadowed(new Rect(box.x, box.yMax - 28f, box.width, 20f),
                "Left / Right (pad Skill1 / Skill2): element  ·  Up / Down (pad Skill3): scroll  ·  J / Esc (pad Interact): close", small, new Color(0.8f, 0.75f, 0.65f));
        }
    }

    // v1.1.2: Tomix's book reads as his: while it's open, "Arcana" in the book's labels becomes "Infusions", and each
    // element's "collected / total" count becomes how many of that element's arcana you've infused. Tomi uses the
    // same book screen, so everything changed is put back when it's his book again.
    internal class TomixBookText : MonoBehaviour
    {
        readonly Dictionary<UnityEngine.UI.Text, string> original = new Dictionary<UnityEngine.UI.Text, string>();
        readonly Dictionary<UnityEngine.UI.Text, string> written = new Dictionary<UnityEngine.UI.Text, string>();
        List<UnityEngine.UI.Text> texts = new List<UnityEngine.UI.Text>();
        SpellBookUI cachedFor;
        float nextScan;
        static readonly System.Text.RegularExpressions.Regex Count = new System.Text.RegularExpressions.Regex(@"^\s*\d+\s*/\s*\d+\s*$");
        static readonly System.Text.RegularExpressions.Regex Arcana = new System.Text.RegularExpressions.Regex("arcana", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        void Scan(SpellBookUI ui)
        {
            texts = new List<UnityEngine.UI.Text>();
            Component sb = Traverse.Create(ui).Field("sbRef").GetValue() as Component;
            if (sb != null) texts.AddRange(sb.GetComponentsInChildren<UnityEngine.UI.Text>(true));
            foreach (UnityEngine.UI.Text t in ui.GetComponentsInChildren<UnityEngine.UI.Text>(true)) if (!texts.Contains(t)) texts.Add(t);
            cachedFor = ui;
            nextScan = Time.unscaledTime + 1f;
        }

        // The element a count belongs to: a label beside it naming the element (English labels).
        static ElementType? RowElement(UnityEngine.UI.Text count)
        {
            for (Transform t = count.transform.parent; t != null && t.parent != null; t = t.parent)
            {
                foreach (UnityEngine.UI.Text other in t.GetComponentsInChildren<UnityEngine.UI.Text>(true))
                {
                    if (other == count || other.text == null) continue;
                    switch (other.text.Trim().ToUpperInvariant())
                    {
                        case "FIRE": return ElementType.Fire;
                        case "WATER": return ElementType.Water;
                        case "AIR": case "WIND": return ElementType.Air;
                        case "EARTH": return ElementType.Earth;
                        case "LIGHTNING": case "THUNDER": return ElementType.Lightning;
                        case "CHAOS": return ElementType.Chaos;
                        case "RADIANCE": case "RADIANT": return ArcanosBridge.ElementFor(StageVariant.Radiant);
                        case "PRISM": return ArcanosBridge.ElementFor(StageVariant.Prism);
                    }
                }
                if (t.GetComponentsInChildren<UnityEngine.UI.Text>(true).Length > 4) break; // reached the page, not the row
            }
            return null;
        }

        static int InfusedOf(SpellBookUI ui, ElementType el)
        {
            System.Collections.IDictionary d = Traverse.Create(ui).Field("eleSpellDict").GetValue() as System.Collections.IDictionary;
            if (d == null || !d.Contains(el)) return 0;
            System.Collections.ICollection l = d[el] as System.Collections.ICollection;
            return l != null ? l.Count : 0;
        }

        void LateUpdate()
        {
            SpellBookUI ui = TomiInfusion.UI;
            bool tomix = TomiInfusion.TomixOpen && !TomiInfusion.InRun;
            if (!tomix) { Restore(); return; }
            if (ui != cachedFor || Time.unscaledTime > nextScan) Scan(ui);
            foreach (UnityEngine.UI.Text t in texts)
            {
                if (t == null || string.IsNullOrEmpty(t.text)) continue;
                string w;
                if (written.TryGetValue(t, out w) && t.text == w) continue; // ours, still shown
                string cur = t.text, next = null;
                if (Count.IsMatch(cur))
                {
                    ElementType? el = RowElement(t);
                    if (el.HasValue) next = InfusedOf(ui, el.Value) + " infused";
                }
                else if (Arcana.IsMatch(cur))
                    next = Arcana.Replace(cur, m => m.Value == m.Value.ToUpperInvariant() ? "INFUSIONS" : "Infusions");
                if (next == null || next == cur) continue;
                original[t] = cur; // the game's text (it rewrites labels as you browse: the latest one is kept)
                written[t] = next;
                t.text = next;
            }
        }

        void Restore()
        {
            if (written.Count == 0) return;
            foreach (KeyValuePair<UnityEngine.UI.Text, string> kv in written)
                if (kv.Key != null && kv.Key.text == kv.Value) kv.Key.text = original[kv.Key];
            written.Clear();
            original.Clear();
        }
    }
}