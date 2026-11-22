using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace WolHybridBosses
{
    // v0.7.0: Arcana infusion (ROADMAP 6). During a run the player picks an arcana to keep and one to sacrifice; the
    // kept arcana takes the sacrificed one's element: damage type and status, +10% damage (never stacking), and its
    // look (the recolour engine) and icon. Re-infusing replaces. Infused arcana also turn up rarely as drops and in
    // the arcana merchant's stock. Run only: cleared in the Hub.
    //
    // Where (v0.7.1): Tomi comes to the arcana merchant on every shop visit, and infusing happens in his spellbook menu
    // (TomiInfusion). Cost: the sacrificed arcana plus gold (80 + 40 per tier). Infused versions stay unlocked and can
    // be chosen per arcana at Tomi in the Hub (InfusionUnlocks).
    internal static class Infusion
    {
        internal const float DamageBonus = 1.10f;
        internal static ElementType? ForcedElement; // v0.8.0: set while an infusion trial runs (TrialEvent)
        static readonly Dictionary<string, ElementType> infused = new Dictionary<string, ElementType>();
        static readonly Dictionary<string, ElementType> pending = new Dictionary<string, ElementType>(); // pre-infused drops / stock, until picked up
        static readonly HashSet<AttackInfo> done = new HashSet<AttackInfo>();
        // v0.9.16: dual infusion (late run): a second element; the arcana's casts alternate between the two.
        static readonly Dictionary<string, ElementType> second = new Dictionary<string, ElementType>();
        static readonly Dictionary<AttackInfo, ElementType> dualPick = new Dictionary<AttackInfo, ElementType>();
        static readonly Dictionary<string, int> dualCount = new Dictionary<string, int>();

        // From the third section of the castle (or any later loop) on.
        internal static bool DualAllowed { get { return GameController.tierCount >= 2 || GameController.loopCount >= 1; } }
        internal static bool InfusedThisRun(string skillID) { return !string.IsNullOrEmpty(skillID) && infused.ContainsKey(skillID); }
        // The second element of a dual: this run's dual infusion; else (when not infused this run) the pair chosen at
        // Tomi in the Hub; else a dual version rolled for this run (v0.9.18).
        internal static bool TryGetSecond(string skillID, out ElementType el)
        {
            el = ElementType.Neutral;
            if (string.IsNullOrEmpty(skillID)) return false;
            if (second.TryGetValue(skillID, out el)) return true;
            if (infused.ContainsKey(skillID)) return false;
            string shuffled;
            if (runPick.TryGetValue(skillID, out shuffled)) { ElementType? s2 = PickSecond(shuffled); if (s2.HasValue) { el = s2.Value; return true; } return false; }
            if (InfusionUnlocks.SelectedRaw(skillID) != null)
            {
                ElementType? s = InfusionUnlocks.SelectedSecond(skillID);
                if (s.HasValue) { el = s.Value; return true; }
                return false;
            }
            ElementType[] rolled;
            if (runDual.TryGetValue(skillID, out rolled)) { el = rolled[1]; return true; }
            return false;
        }

        internal static void SetSecond(string skillID, ElementType el)
        {
            second[skillID] = el;
            InfusionUnlocks.Unlock(skillID, el); // the single version of that element unlocks too
            Plugin.Log.LogInfo("INFUSION " + skillID + " dual: " + Name(infused[skillID]) + " / " + Name(el));
        }

        internal static bool Any { get { return infused.Count > 0 || pending.Count > 0 || runDual.Count > 0 || runPick.Count > 0 || InfusionUnlocks.AnySelected; } }

        // v0.9.18: dual versions rolled for this run (arcana with 2+ unlocked versions and no version chosen at Tomi).
        static readonly Dictionary<string, ElementType[]> runDual = new Dictionary<string, ElementType[]>();
        internal const float RunDualChance = 0.15f;

        // v0.9.19: versions rolled by Nocturne's shuffle (paid randomized build): skill ID -> "" (original), "Fire" or
        // "Fire+Ice". They win over your Tomi choice for that run.
        static readonly Dictionary<string, string> runPick = new Dictionary<string, string>();

        internal static void ClearRunDuals() { runDual.Clear(); runPick.Clear(); }

        internal static void RollShuffled(Player p, bool preferInfused = false)
        {
            if (p == null || p.assignedSkills == null || !Plugin.InfusionEnabled.Value) return;
            List<string> names = new List<string>();
            foreach (Player.SkillState s in p.assignedSkills)
            {
                if (s == null || string.IsNullOrEmpty(s.skillID)) continue;
                List<string> opts = InfusionUnlocks.Options(s.skillID);
                if (opts.Count <= 1) continue;
                // v1.1.0: Nocturne's Alchemist preset lands on an infused / dual version whenever there is one.
                string pick = preferInfused ? opts[UnityEngine.Random.Range(1, opts.Count)] : opts[UnityEngine.Random.Range(0, opts.Count)];
                runPick[s.skillID] = pick;
                runDual.Remove(s.skillID);
                if (pick.Length > 0) names.Add(IntroUI.SkillName(s.skillID) + " (" + InfusionUnlocks.Label(pick) + ")");
            }
            Plugin.Log.LogInfo("INFUSION Nocturne's shuffle rolled versions: " + (names.Count > 0 ? string.Join(", ", names.ToArray()) : "all original"));
            if (names.Count > 0) IntroUI.Show("Shuffled versions", string.Join("  ·  ", names.GetRange(0, Mathf.Min(3, names.Count)).ToArray()) + (names.Count > 3 ? "  ·  ..." : ""), new Color(1f, 0.82f, 0.3f), null, 4f);
        }

        static ElementType? PickFirst(string raw) { if (string.IsNullOrEmpty(raw)) return null; int plus = raw.IndexOf('+'); return InfusionUnlocks.FromName(plus >= 0 ? raw.Substring(0, plus) : raw); }
        static ElementType? PickSecond(string raw) { int plus = raw != null ? raw.IndexOf('+') : -1; return plus >= 0 ? InfusionUnlocks.FromName(raw.Substring(plus + 1)) : null; }

        internal static void RollRunDuals()
        {
            runDual.Clear();
            if (!Plugin.InfusionEnabled.Value) return;
            List<string> names = new List<string>();
            foreach (string id in InfusionUnlocks.UnlockedIds)
            {
                if (InfusionUnlocks.SelectedRaw(id) != null || runPick.ContainsKey(id)) continue; // your choice at Tomi / the shuffle wins
                List<ElementType[]> pairs = InfusionUnlocks.Pairs(id);
                if (pairs.Count == 0 || UnityEngine.Random.value >= RunDualChance) continue;
                runDual[id] = pairs[UnityEngine.Random.Range(0, pairs.Count)];
                names.Add(IntroUI.SkillName(id) + " (" + Name(runDual[id][0]) + " / " + Name(runDual[id][1]) + ")");
            }
            if (names.Count == 0) return;
            Plugin.Log.LogInfo("INFUSION dual versions this run: " + string.Join(", ", names.ToArray()));
            IntroUI.Show("Dual versions this run", string.Join("  ·  ", names.GetRange(0, Mathf.Min(3, names.Count)).ToArray()) + (names.Count > 3 ? "  ·  ..." : ""), new Color(1f, 0.82f, 0.3f), null, 4f);
        }

        // This run's infusion, else the version chosen at Tomi in the Hub (an unlocked infused version).
        internal static bool TryGet(string skillID, out ElementType el)
        {
            el = ElementType.Neutral;
            if (string.IsNullOrEmpty(skillID)) return false;
            if (infused.TryGetValue(skillID, out el)) return true;
            string shuffled;
            if (runPick.TryGetValue(skillID, out shuffled)) { ElementType? f = PickFirst(shuffled); if (f.HasValue) { el = f.Value; return true; } return false; }
            ElementType? chosen = InfusionUnlocks.Selected(skillID);
            if (chosen.HasValue) { el = chosen.Value; return true; }
            ElementType[] rolled;
            if (runDual.TryGetValue(skillID, out rolled)) { el = rolled[0]; return true; }
            return false;
        }

        // Infused or about to be (shown on the card / icon either way).
        internal static bool TryGetShown(string skillID, out ElementType el)
        {
            if (TryGet(skillID, out el)) return true;
            return !string.IsNullOrEmpty(skillID) && pending.TryGetValue(skillID, out el);
        }

        internal static void Set(string skillID, ElementType el)
        {
            infused[skillID] = el;
            pending.Remove(skillID);
            InfusionUnlocks.Unlock(skillID, el); // every infused version you get stays unlocked (choose it at Tomi in the Hub)
            Plugin.Log.LogInfo("INFUSION " + skillID + " -> " + el);
        }

        internal static void SetPending(string skillID, ElementType el)
        {
            if (string.IsNullOrEmpty(skillID) || infused.ContainsKey(skillID)) return;
            pending[skillID] = el;
            Plugin.Log.LogInfo("INFUSION pre-infused " + skillID + " (" + el + ") on offer");
        }

        internal static void RemovePending(string skillID) { if (skillID != null) pending.Remove(skillID); }

        internal static void PickedUp(string skillID)
        {
            ElementType el;
            if (!string.IsNullOrEmpty(skillID) && pending.TryGetValue(skillID, out el)) Set(skillID, el);
        }

        internal static void Clear()
        {
            if (infused.Count == 0 && pending.Count == 0 && second.Count == 0) return;
            infused.Clear(); pending.Clear(); done.Clear(); second.Clear(); dualPick.Clear(); dualCount.Clear();
            Plugin.Log.LogInfo("INFUSION cleared (new run)");
        }

        // ---------- elements ----------

        // The elements an infusion can give, with their names (Radiant / Prism only with Arcanos, and only from an
        // arcana the player owns, so already unlocked).
        internal static string Name(ElementType el)
        {
            switch (el)
            {
                case ElementType.Fire: return "Fire";
                case ElementType.Water: return "Water";
                case ElementType.Ice: return "Ice";
                case ElementType.Earth: case ElementType.Plant: case ElementType.Poison: return "Earth";
                case ElementType.Air: return "Wind";
                case ElementType.Lightning: return "Thunder";
                case ElementType.Chaos: return "Chaos";
            }
            ElementType? ra = ArcanosBridge.ElementFor(StageVariant.Radiant), pr = ArcanosBridge.ElementFor(StageVariant.Prism);
            if (ra.HasValue && el == ra.Value) return "Radiant";
            if (pr.HasValue && el == pr.Value) return "Prism";
            return el.ToString();
        }

        internal static Color ColorOf(ElementType el)
        {
            string key = RampKey(el);
            if (key == "Chaos") return ElementalGiants.ElementColor("Chaos"); // v0.9.15: the same dark purple everywhere
            if (key != null) return ElementRamps.Mid(key);
            return Name(el) == "Prism" ? new Color(1f, 0.55f, 0.9f) : Color.white;
        }

        // v0.9.18: an arcana's shown colour: a dual version's is the mix of both elements.
        internal static Color ShownColour(string skillID, ElementType el)
        {
            ElementType el2;
            return TryGetSecond(skillID, out el2) ? Color.Lerp(ColorOf(el), ColorOf(el2), 0.5f) : ColorOf(el);
        }

        internal static string RampKey(ElementType el)
        {
            string k = ElementRamps.ForElement(el);
            if (k != null) return k;
            return Name(el) == "Radiant" ? "Radiant" : null;
        }

        static bool Same(ElementType a, ElementType b) { return Name(a) == Name(b); }

        // The element a player arcana deals now (its infusion, else its own).
        internal static ElementType CurrentElement(Player.SkillState s)
        {
            ElementType el;
            return s != null && TryGet(s.skillID, out el) ? el : (s != null ? s.element : ElementType.Neutral);
        }

        // A random element other than the arcana's own, for pre-infused drops / stock.
        internal static ElementType RandomOther(string skillID)
        {
            ElementType own = ElementType.Neutral;
            ArcanaDef d = ArcanaCatalog.Get(skillID);
            if (d != null)
                switch (d.Element)
                {
                    case "Flame": own = ElementType.Fire; break;
                    case "Frost": own = ElementType.Water; break;
                    case "Earth": own = ElementType.Earth; break;
                    case "Wind": own = ElementType.Air; break;
                    case "Thunder": own = ElementType.Lightning; break;
                    case "Chaos": own = ElementType.Chaos; break;
                }
            List<ElementType> options = new List<ElementType> { ElementType.Fire, ElementType.Water, ElementType.Ice, ElementType.Earth, ElementType.Air, ElementType.Lightning };
            if (UnityEngine.Random.value < 0.15f) options.Add(ElementType.Chaos);
            options.RemoveAll(e => Same(e, own) || (own == ElementType.Water && e == ElementType.Ice));
            return options[UnityEngine.Random.Range(0, options.Count)];
        }

        // ---------- the player's attacks ----------

        internal static bool IsPlayersAttack(Attack atk)
        {
            if (atk == null || atk.atkInfo == null) return false;
            if (OwnerContext.Current != null || OwnerContext.OwnerOf(atk) != null) return false; // an enemy casting the same arcana
            if (atk.atkInfo.entity is Player) return true;
            return atk.atkInfo.entity == null && atk.atkInfo.skillCategory == "Player" && atk.GetComponentInParent<Enemy>() == null;
        }

        // SetAttackInfo postfix (VariantAttackPatch): an infused arcana's attack takes its element, status, +10%
        // damage (once per attack info: re-infusing never stacks it) and the element's look.
        internal static bool ApplyToAttack(Attack atk)
        {
            bool forced = ForcedElement.HasValue;
            if ((!Any && !forced) || !IsPlayersAttack(atk)) return false;
            ElementType el;
            // v0.8.0: an infusion trial forces every arcana into its element (no damage bonus); otherwise the
            // arcana's own infusion.
            if (forced) el = ForcedElement.Value;
            else if (!TryGet(atk.atkInfo.skillID, out el)) return false;
            AttackInfo info = atk.atkInfo;
            // v0.9.16: a dual-infused arcana alternates its two elements cast by cast (decided once per attack info).
            ElementType el2;
            if (!forced && TryGetSecond(info.skillID, out el2))
            {
                ElementType pick;
                if (!dualPick.TryGetValue(info, out pick))
                {
                    int n;
                    dualCount.TryGetValue(info.skillID, out n);
                    dualCount[info.skillID] = n + 1;
                    pick = n % 2 == 0 ? el : el2;
                    if (dualPick.Count > 8192) dualPick.Clear();
                    dualPick[info] = pick;
                }
                el = pick;
            }
            string name = Name(el);
            if (done.Add(info))
            {
                if (done.Count > 8192) done.Clear();
                float chance = Mathf.Max(Mathf.Max(info.burnChance, info.slowChance), Mathf.Max(Mathf.Max(info.poisonChance, info.shockChance), Mathf.Max(info.freezeChance, info.chaosChance)));
                int level = Mathf.Max(1, Mathf.Max(Mathf.Max(info.burnLevel, info.slowLevel), Mathf.Max(info.poisonLevel, info.shockLevel)));
                if (chance <= 0f) chance = 0.15f; // a move without a status still gets a little of its new element's
                info.burnChance = info.slowChance = info.poisonChance = info.shockChance = info.freezeChance = info.chaosChance = 0f;
                if (!forced) info.damage = Mathf.Max(1, Mathf.RoundToInt(info.damage * DamageBonus));
                if (!forced && WhiteMantle.Active) info.damage = Mathf.Max(1, Mathf.RoundToInt(info.damage * WhiteMantle.DamageBonus)); // v1.1.0
                if (name == "Prism")
                {
                    // Prism, like Arcanos' own: each cast is the next of the five base elements, with its status.
                    PrismElement pe = Prism.Next(atk.atkInfo.entity != null ? (Component)atk.atkInfo.entity : atk);
                    info.elementType = pe.Type;
                    pe.ApplyStatus(info);
                    CouncilCorruption.PreviewRecolor(atk, ElementRamps.ForElement(pe.Type));
                    Recent(ElementRamps.ForElement(pe.Type));
                    return true;
                }
                if (name == "Radiant")
                {
                    if (!ArcanosBridge.SetElement(atk, el)) info.elementType = el; // Arcanos' own element on a player attack, as its arcana do
                }
                else SetStatus(info, el, chance, level);
            }
            string key = RampKey(el);
            if (key != null) { CouncilCorruption.PreviewRecolor(atk, key); PaletteFx(atk, key); }
            Recent(key);
            return true;
        }

        // v1.1.2: some arcana draw their strike with the game's palette shader (Shock Assault's bolt: the player's
        // palette row), which sprite twinning skips. Its material instance gets the palette on the element's ramp.
        // Only the attack's own small object (its parent when that holds just a few parts), never a body.
        static readonly Dictionary<Material, Texture> palOriginal = new Dictionary<Material, Texture>();
        static readonly HashSet<string> palLogged = new HashSet<string>();

        static void PaletteFx(Attack atk, string key)
        {
            try
            {
                Transform t = atk.transform;
                Transform p = t.parent;
                if (p != null && p.childCount <= 4 && p.GetComponent<Entity>() == null && p.GetComponentInParent<Enemy>() == null) t = p;
                foreach (SpriteRenderer sr in t.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (sr == null || !RecolorEngine.PaletteSprite(sr) || sr.GetComponentInParent<Entity>() != null) continue;
                    Material m = sr.material;
                    Texture pal;
                    if (!palOriginal.TryGetValue(m, out pal)) { pal = m.GetTexture("_Palette"); if (palOriginal.Count > 512) palOriginal.Clear(); palOriginal[m] = pal; }
                    if (pal == null) continue;
                    Texture2D twin = RecolorEngine.PaletteTwin(pal, key);
                    if (twin == null) continue;
                    m.SetTexture("_Palette", twin);
                    if (palLogged.Add(atk.atkInfo.skillID)) Plugin.Log.LogInfo("INFUSION palette strike " + atk.atkInfo.skillID + " -> " + key + " (" + sr.name + ")");
                }
            }
            catch (Exception) { }
        }

        // v1.1.2: the look of the infused cast made this frame. Effects an arcana draws apart from its attack (Shock
        // Assault's lightning line, a pooled trail emitter) take its colour from this, in the same frame.
        internal static string RecentKey;
        internal static int RecentFrame = -1;
        static void Recent(string key) { RecentKey = key; RecentFrame = Time.frameCount; }

        internal static void SetStatus(AttackInfo info, ElementType el, float chance, int level)
        {
            info.elementType = el;
            switch (Name(el))
            {
                case "Fire": info.burnChance = chance; info.burnLevel = Mathf.Max(info.burnLevel, level); break;
                // v1.1.2: slow is the game's Air status (Health.HandleAirStatusEffect); water arcana freeze.
                case "Wind": info.slowChance = chance; info.slowLevel = Mathf.Max(info.slowLevel, level); break;
                case "Water": info.freezeChance = chance * 0.6f; break; // as Prism's water: freezing is strong
                case "Ice": info.freezeChance = chance; break;
                case "Earth": info.poisonChance = chance; info.poisonLevel = Mathf.Max(info.poisonLevel, level); break;
                case "Thunder": info.shockChance = chance; info.shockLevel = Mathf.Max(info.shockLevel, level); break;
                case "Chaos": info.chaosChance = chance; info.chaosLevel = Mathf.Max(info.chaosLevel, level); break;
            }
        }

        // v0.8.0: an enemy's attack (an infused Council member's) converted to a base element: damage type and its
        // status (the strongest existing status chance moves over), no damage bonus. Once per attack info.
        static readonly HashSet<AttackInfo> enemyDone = new HashSet<AttackInfo>();

        internal static void ConvertEnemyInfo(AttackInfo info, ElementType el)
        {
            if (info == null || !enemyDone.Add(info)) return;
            if (enemyDone.Count > 8192) enemyDone.Clear();
            float chance = Mathf.Max(Mathf.Max(info.burnChance, info.slowChance), Mathf.Max(Mathf.Max(info.poisonChance, info.shockChance), Mathf.Max(info.freezeChance, info.chaosChance)));
            int level = Mathf.Max(1, Mathf.Max(Mathf.Max(info.burnLevel, info.slowLevel), Mathf.Max(info.poisonLevel, info.shockLevel)));
            info.burnChance = info.slowChance = info.poisonChance = info.shockChance = info.freezeChance = info.chaosChance = 0f;
            if (chance > 0f) SetStatus(info, el, chance, level); else info.elementType = el;
        }

        // The game prefix for a base element ("Flame", "Frost", "Earth", "Wind", "Thunder").
        internal static string Prefix(ElementType el)
        {
            switch (Name(el))
            {
                case "Fire": return "Flame";
                case "Water": case "Ice": return "Frost";
                case "Earth": return "Earth";
                case "Wind": return "Wind";
                case "Thunder": return "Thunder";
                case "Chaos": return "Chaos";
            }
            return null;
        }
    }

    // ---------- icons and cards: an infused arcana's icon in its new element's colours ----------

    [HarmonyPatch(typeof(IconManager), "GetSkillIcon")]
    static class InfusionIconPatch
    {
        static readonly HashSet<string> logged = new HashSet<string>();

        static void Postfix(string __0, ref Sprite __result)
        {
            if (__result == null || !Infusion.Any) return;
            ElementType el, el2;
            if (!Infusion.TryGetShown(__0, out el)) return;
            string key = Infusion.RampKey(el);
            // v0.9.18: a dual version's icon shows both elements (split gradient).
            string key2 = Infusion.TryGetSecond(__0, out el2) ? Infusion.RampKey(el2) : null;
            Sprite twin = key != null && key2 != null ? RecolorEngine.DualTwin(__result, key, key2) : key != null ? RecolorEngine.Twin(__result, key) : null;
            if (key2 != null) key = key + "+" + key2;
            if (logged.Add(__0 + "|" + key)) Plugin.Log.LogInfo("INFUSION icon " + __0 + " -> " + key + (twin != null ? " (recoloured)" : " (could not recolour)"));
            if (twin != null) __result = twin;
        }
    }

    // v0.7.2: infused cards stand out more than by their art alone: the card's frame takes the element's colour too,
    // on dropped / world cards (Card.SetSkillIcon) and in the merchant's stock (SkillStoreItem.SetCardSprite).
    [HarmonyPatch(typeof(Card), "SetSkillIcon")]
    static class InfusionCardFramePatch
    {
        static readonly HashSet<int> tinted = new HashSet<int>();

        static void Postfix(Card __instance, string __0)
        {
            try
            {
                if (__instance == null) return;
                Traverse tr = Traverse.Create(__instance);
                SpriteRenderer[] frames = { tr.Field("frameSpriteRenderer").GetValue() as SpriteRenderer, tr.Field("backSpriteRenderer").GetValue() as SpriteRenderer };
                ElementType el;
                bool infused = Infusion.Any && Infusion.TryGetShown(__0, out el);
                if (!infused)
                {
                    // A pooled card we tinted before, now showing a normal arcana: back to white.
                    if (tinted.Remove(__instance.GetInstanceID())) foreach (SpriteRenderer sr in frames) if (sr != null) sr.color = Color.white;
                    return;
                }
                Infusion.TryGetShown(__0, out el);
                Color c = Color.Lerp(Color.white, Infusion.ShownColour(__0, el), 0.55f);
                foreach (SpriteRenderer sr in frames) if (sr != null) sr.color = c;
                tinted.Add(__instance.GetInstanceID());
            }
            catch (Exception) { }
        }
    }

    // Tomi's spellbook pages: each card whose icon is a recoloured (infused) one gets its card image tinted in the
    // element's colour, on the spell page, the info page and the loadout page.
    static class TomiCardTint
    {
        internal static void Tint(SpellBookUI ui)
        {
            try
            {
                if (ui == null) return;
                object sbRef = Traverse.Create(ui).Field("sbRef").GetValue();
                if (sbRef == null) return;
                Traverse r = Traverse.Create(sbRef);
                object spell = r.Field("spellPage").GetValue(), info = r.Field("infoPage").GetValue(), player = r.Field("playerPage").GetValue();
                if (spell != null) Pair(Traverse.Create(spell).Field("iconArray").GetValue() as UnityEngine.UI.Image[], Traverse.Create(spell).Field("cardImgArray").GetValue() as UnityEngine.UI.Image[]);
                if (player != null) Pair(Traverse.Create(player).Field("spellIconArray").GetValue() as UnityEngine.UI.Image[], Traverse.Create(player).Field("cardBGArray").GetValue() as UnityEngine.UI.Image[]);
                if (info != null)
                {
                    UnityEngine.UI.Image[] icons = Traverse.Create(info).Field("iconArray").GetValue() as UnityEngine.UI.Image[];
                    GameObject[] cards = Traverse.Create(info).Field("cardArray").GetValue() as GameObject[];
                    if (icons != null && cards != null)
                    {
                        UnityEngine.UI.Image[] cardImgs = new UnityEngine.UI.Image[cards.Length];
                        for (int i = 0; i < cards.Length; i++) cardImgs[i] = cards[i] != null ? cards[i].GetComponent<UnityEngine.UI.Image>() : null;
                        Pair(icons, cardImgs);
                    }
                }
            }
            catch (Exception ex) { Plugin.Log.LogWarning("INFUSION Tomi card tint failed: " + ex.Message); }
        }

        static void Pair(UnityEngine.UI.Image[] icons, UnityEngine.UI.Image[] cards)
        {
            if (icons == null || cards == null) return;
            for (int i = 0; i < icons.Length && i < cards.Length; i++)
            {
                if (cards[i] == null) continue;
                string key = icons[i] != null ? RecolorEngine.KeyOf(icons[i].sprite) : null;
                cards[i].color = key != null ? Color.Lerp(Color.white, ElementRamps.MidOf(key), 0.55f) : Color.white; // v0.9.18: duals mix both
            }
        }
    }

    // v1.1.2: a filtered book (a run, or Tomix's at home) can have an element tab with no cards; the card view then
    // indexed past the end of an empty list every frame. Swallow just that error there.
    [HarmonyPatch(typeof(SpellBookUI), "HandleInfoSelection")]
    static class SpellBookEmptyTabGuard
    {
        static bool logged;
        static Exception Finalizer(Exception __exception)
        {
            if (__exception is ArgumentOutOfRangeException)
            {
                if (!logged) { logged = true; Plugin.Log.LogInfo("INFUSION book: an empty card tab (filtered list) was skipped"); }
                return null;
            }
            return __exception;
        }
    }

    // v1.1.2: lightning lines and other sectioned trails (Shock Assault's bolt between its strikes is one, from the pool,
    // not part of the attack) emitted in the same frame as an infused cast: the cast's element colour, when the
    // caller didn't pick a colour itself.
    [HarmonyPatch(typeof(SectionedTrailEmitter), "Emit")]
    static class InfusionTrailColourPatch
    {
        static bool logged;
        // The pooled emitter's own look before we touched it: its texture and colour over lifetime, put back on the next
        // emit that isn't an infused cast (enemies share the pool).
        class Orig { public Texture tex; public bool col; public ParticleSystem.MinMaxGradient grad; }
        static readonly Dictionary<ParticleSystem, Orig> orig = new Dictionary<ParticleSystem, Orig>();

        static void Prefix(SectionedTrailEmitter __instance, ref Color32? __8)
        {
            try
            {
                bool infused = !__8.HasValue && Infusion.RecentFrame == Time.frameCount && ElementRamps.Has(Infusion.RecentKey);
                string key = Infusion.RecentKey;
                bool retextured = false;
                foreach (ParticleSystem ps in __instance.GetComponentsInChildren<ParticleSystem>(true))
                {
                    ParticleSystemRenderer r = ps != null ? ps.GetComponent<ParticleSystemRenderer>() : null;
                    if (r == null || r.sharedMaterial == null) continue;
                    Orig o;
                    if (!infused)
                    {
                        if (orig.TryGetValue(ps, out o))
                        {
                            r.material.mainTexture = o.tex;
                            if (o.col) { ParticleSystem.ColorOverLifetimeModule m = ps.colorOverLifetime; m.color = o.grad; }
                            orig.Remove(ps);
                        }
                        continue;
                    }
                    if (!orig.TryGetValue(ps, out o))
                    {
                        o = new Orig { tex = r.sharedMaterial.mainTexture, col = ps.colorOverLifetime.enabled };
                        if (o.col) o.grad = ps.colorOverLifetime.color;
                        orig[ps] = o;
                    }
                    Texture2D twin = RecolorEngine.AtlasTwin(o.tex as Texture2D, key);
                    if (twin != null) { r.material.mainTexture = twin; retextured = true; }
                    if (o.col) { ParticleSystem.ColorOverLifetimeModule m = ps.colorOverLifetime; m.color = RecolorEngine.Map(o.grad, key); }
                }
                if (!infused) return;
                // The emitter's own start colour on the ramp over a recoloured texture; the element's colour when it couldn't be twinned.
                Color start = Color.white;
                ParticleSystem first = __instance.GetComponentInChildren<ParticleSystem>(true);
                try { if (first != null) start = first.main.startColor.color; } catch (Exception) { }
                __8 = retextured ? (Color32)ElementRamps.Map(key, start) : (Color32)Color.Lerp(ElementRamps.Mid(key), Color.white, 0.2f);
                if (!logged) { logged = true; Plugin.Log.LogInfo("INFUSION trail coloured " + key + (retextured ? " (texture recoloured)" : " (tint only)")); }
            }
            catch (Exception ex) { Plugin.Log.LogWarning("INFUSION trail colour: " + ex.Message); }
        }
    }

    [HarmonyPatch(typeof(SpellBookUI), "LoadSpellPage")]
    static class TomiSpellPageTintPatch { static void Postfix(SpellBookUI __instance) { TomiCardTint.Tint(__instance); } }

    [HarmonyPatch(typeof(SpellBookUI), "LoadInfoPage")]
    static class TomiInfoPageTintPatch { static void Postfix(SpellBookUI __instance) { TomiCardTint.Tint(__instance); } }

    [HarmonyPatch(typeof(SpellBookUI), "SetEquipSlots")]
    static class TomiEquipTintPatch { static void Postfix(SpellBookUI __instance) { TomiCardTint.Tint(__instance); } }

    [HarmonyPatch(typeof(SkillStoreItem), "SetCardSprite")]
    static class InfusionStockFramePatch
    {
        static void Postfix(SkillStoreItem __instance)
        {
            try
            {
                ElementType el;
                if (__instance == null || !Infusion.Any || !Infusion.TryGetShown(__instance.skillID, out el)) return;
                Color c = Color.Lerp(Color.white, Infusion.ShownColour(__instance.skillID, el), 0.55f); // v0.9.18: duals mix both
                foreach (SpriteRenderer sr in __instance.GetComponentsInChildren<SpriteRenderer>(true))
                    if (sr != null && sr.name.IndexOf("Frame", StringComparison.OrdinalIgnoreCase) >= 0) sr.color = c;
            }
            catch (Exception) { }
        }
    }

    // v0.9.19: Nocturne the Cardist's paid shuffle (Player.RandomizeBuild, via RandRunMod): each shuffled arcana with
    // unlocked versions lands on one of them at random (original, a single infused version or a dual pair) for the run.
    [HarmonyPatch(typeof(Player), "RandomizeBuild")]
    static class InfusionShufflePatch
    {
        static void Postfix(Player __instance, bool __2)
        {
            if (!__2) return; // v1.1.0: arcana weren't shuffled (Nocturne's "Robe and relic")
            try { Infusion.RollShuffled(__instance, NocturneShuffle.PreferInfused); } catch (Exception ex) { Plugin.Log.LogWarning("INFUSION shuffle roll failed: " + ex.Message); }
        }
    }

    // ---------- rare pre-infused drops and stock ----------

    // A drop rolls once whether it comes infused; LootManager may try several arcana ids before settling, so only
    // the latest candidate stays pending (the card shows it as it spawns).
    [HarmonyPatch(typeof(LootManager), "DropSkill")]
    static class InfusionDropPatch
    {
        internal static bool infusedDrop;
        internal static bool ForceNext; // an elite's drop: always infused
        internal static string candidate;
        static void Prefix(string __2)
        {
            infusedDrop = Plugin.InfusionEnabled.Value && (ForceNext || UnityEngine.Random.value < Plugin.InfusedDropChance.Value);
            candidate = null;
            if (infusedDrop && !string.IsNullOrEmpty(__2)) { candidate = __2; Infusion.SetPending(__2, Infusion.RandomOther(__2)); }
        }
        static void Finalizer() { infusedDrop = false; candidate = null; }
    }

    [HarmonyPatch(typeof(LootManager), "GetSkillID")]
    static class InfusionDropIdPatch
    {
        static void Postfix(string __result)
        {
            if (!InfusionDropPatch.infusedDrop || string.IsNullOrEmpty(__result) || __result == InfusionDropPatch.candidate) return;
            if (InfusionDropPatch.candidate != null) Infusion.RemovePending(InfusionDropPatch.candidate);
            InfusionDropPatch.candidate = __result;
            Infusion.SetPending(__result, Infusion.RandomOther(__result));
        }
    }

    // The arcana merchant: now and then one of its arcana is on offer already infused, for a bit more gold.
    [HarmonyPatch(typeof(SkillStoreItem), "Initialize")]
    static class InfusionStockPatch
    {
        static void Postfix(SkillStoreItem __instance)
        {
            try
            {
                if (!Plugin.InfusionEnabled.Value || __instance == null || string.IsNullOrEmpty(__instance.skillID) || __instance.signatureOnly) return;
                // v1.1.1: an arcana you've unlocked infused versions of is often sold as one of them.
                List<ElementType> mine = InfusionUnlocks.Versions(__instance.skillID);
                if (mine.Count > 0 && UnityEngine.Random.value < Plugin.UnlockedStockChance.Value)
                {
                    ElementType v = mine[UnityEngine.Random.Range(0, mine.Count)];
                    Infusion.SetPending(__instance.skillID, v);
                    try { __instance.costStat.AddMod(new NumVarStatMod("HB_Infused", 1.1f, 10, VarStatModType.Multiplicative, false)); } catch (Exception) { }
                    Traverse.Create(__instance).Method("SetCardSprite").GetValue();
                    return;
                }
                if (UnityEngine.Random.value >= Plugin.InfusedStockChance.Value) return;
                Infusion.SetPending(__instance.skillID, Infusion.RandomOther(__instance.skillID));
                try { __instance.costStat.AddMod(new NumVarStatMod("HB_Infused", 1.25f, 10, VarStatModType.Multiplicative, false)); } catch (Exception) { }
                Traverse.Create(__instance).Method("SetCardSprite").GetValue();
            }
            catch (Exception ex) { Plugin.Log.LogWarning("INFUSION stock failed: " + ex.Message); }
        }
    }

    [HarmonyPatch(typeof(Player), "PickUpSkill")]
    static class InfusionPickUpPatch
    {
        static void Postfix(string __0) { try { Infusion.PickedUp(__0); } catch (Exception) { } }
    }

    // ---------- Tomi: infusing in his spellbook menu (v0.7.1) ----------
    //
    // Tomi (SpellBookNpc) teleports in beside the arcana merchant on every shop visit (the merchant's own SpawnTomi).
    // During a run his spellbook only lists the arcana you own this run (LootManager.currentPlayerSkills + equipped).
    // In it, [I] marks the arcana under the cursor to keep, [I] on another marks the sacrifice, [I] again infuses
    // (gold: 80 + 40 per tier). Every infused version you get unlocks for good: back in the Hub, [I] in Tomi's
    // spellbook switches an arcana between its unlocked versions, and the chosen one is what you cast in runs.

    [HarmonyPatch(typeof(SkillStoreItem), "Start")]
    static class TomiAtMerchantPatch
    {
        static int lastScene = -1;

        static void Postfix(SkillStoreItem __instance)
        {
            try
            {
                // v0.9.1: runs only: the Hub's shop already has its own Tomi (this added a second one beside him).
                if (!Plugin.InfusionEnabled.Value || __instance == null || Level.isBossRushMode || !TomiInfusion.InRun) return;
                int scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetHashCode() ^ (GameController.tierCount * 131 + GameController.stageCount * 17);
                if (scene == lastScene) return;
                lastScene = scene;
                __instance.StartCoroutine(SpawnLater(__instance));
            }
            catch (Exception ex) { Plugin.Log.LogWarning("INFUSION Tomi spawn failed: " + ex.Message); }
        }

        static IEnumerator SpawnLater(SkillStoreItem item)
        {
            yield return new WaitForSeconds(0.5f);
            if (item == null) yield break;
            // v0.7.2: the merchant's own SpawnTomi always puts him at (14, 8), his spot in the Hub's shop, so in a run
            // he landed off somewhere in the level. Instead: just left of the merchant's arcana row, a bit above it.
            Vector2 c = item.transform.position;
            float minX = c.x, sumY = 0f;
            int n = 0;
            foreach (SkillStoreItem s in UnityEngine.Object.FindObjectsOfType<SkillStoreItem>())
            {
                if (s == null || Vector2.Distance(s.transform.position, c) > 12f) continue;
                minX = Mathf.Min(minX, s.transform.position.x);
                sumY += s.transform.position.y;
                n++;
            }
            Vector2 at = new Vector2(minX - 2.5f, (n > 0 ? sumY / n : c.y) + 2.2f);
            foreach (SpellBookNpc t in UnityEngine.Object.FindObjectsOfType<SpellBookNpc>())
                if (t != null && Vector2.Distance(t.transform.position, at) < 15f) yield break; // he's already here
            TomiInfusion.SpawnAt(at);
            ArcanosBridge.Fx("Teletransporte", at);
            Plugin.Log.LogInfo("INFUSION Tomi came to the arcana merchant at " + at);
        }
    }

    // In a run, Tomi's spellbook lists only the arcana you own this run.
    // v0.9.4: LegendApi hooks LoadEleSkillDict too (a MonoMod hook around it) and refills the lists after our
    // postfix ran, so every arcana showed. The lists are now filtered again right before each page is drawn
    // (LoadSpellPage) and after the menu opens, which LegendApi doesn't touch.
    [HarmonyPatch(typeof(SpellBookUI), "LoadEleSkillDict")]
    static class TomiRunListPatch
    {
        [HarmonyPriority(Priority.Last)]
        static void Postfix(SpellBookUI __instance, Player __0) { Filter(__instance, __0); }

        internal static void Filter(SpellBookUI ui, Player p)
        {
            try
            {
                if (ui == null) return;
                if (p == null) p = Traverse.Create(ui).Field("player").GetValue() as Player;
                if (p == null) return;
                HashSet<string> owned = TomiInfusion.BookFilter(p); // v1.1.2: also Tomix's book at home
                if (owned == null) return;
                int removed = 0;
                foreach (string field in new[] { "eleSpellDict", "eleOverdriveDict" })
                {
                    IDictionary spells = Traverse.Create(ui).Field(field).GetValue() as IDictionary;
                    if (spells == null) continue;
                    foreach (object list in spells.Values)
                    {
                        List<Player.SkillState> l = list as List<Player.SkillState>;
                        if (l != null) removed += l.RemoveAll(s => s == null || !owned.Contains(s.skillID));
                    }
                }
                // (The card view's per-element lists are left alone: the book keeps indices into them, and trimming them
                // after it had counted crashed HandleInfoSelection. Their entries come through AddSkillToInfoSkills, which
                // is filtered.)
                if (removed > 0 && Plugin.DebugKeys.Value) Plugin.Log.LogInfo("INFUSION book list: " + owned.Count + " arcana shown, " + removed + " entries hidden");
            }
            catch (Exception ex) { Plugin.Log.LogWarning("INFUSION Tomi list filter failed: " + ex.Message); }
        }
    }

    // v0.9.5: the card view ("view all arcana", or opening one of your arcana) is a separate list: LoadInfoSkills
    // walks player.skillsDict (every unlocked arcana) and adds each through AddSkillToInfoSkills, into
    // infoSkillList and the per-element tabs. In a run, arcana you don't hold are skipped there.
    [HarmonyPatch(typeof(SpellBookUI), "AddSkillToInfoSkills")]
    static class TomiRunInfoListPatch
    {
        static HashSet<string> owned;
        static int ownedFrame = -1;

        static bool Prefix(SpellBookUI __instance, Player.SkillState __0)
        {
            try
            {
                if (__0 == null) return true;
                if (ownedFrame != Time.frameCount)
                {
                    owned = TomiInfusion.BookFilter(Traverse.Create(__instance).Field("player").GetValue() as Player);
                    ownedFrame = Time.frameCount;
                }
                if (owned == null) return true;
                return (TomiInfusion.InRun && owned.Count == 0) || owned.Contains(__0.skillID);
            }
            catch (Exception) { return true; }
        }
    }

    [HarmonyPatch(typeof(SpellBookUI), "LoadSpellPage")]
    static class TomiRunListPagePatch { static void Prefix(SpellBookUI __instance) { TomiRunListPatch.Filter(__instance, null); } }

    [HarmonyPatch(typeof(SpellBookUI), "Activate")]
    static class TomiRunListOpenPatch { [HarmonyPriority(Priority.Last)] static void Postfix(SpellBookUI __instance) { TomiRunListPatch.Filter(__instance, null); } }

    [HarmonyPatch(typeof(SpellBookUI), "Activate")]
    static class TomiOpenPatch { static void Postfix(SpellBookUI __instance) { TomiInfusion.Opened(__instance); } }

    [HarmonyPatch(typeof(SpellBookUI), "Deactivate")]
    static class TomiClosePatch { static void Postfix(SpellBookUI __instance) { TomiInfusion.Closed(__instance); } }

    internal class TomiInfusion : MonoBehaviour
    {
        static TomiInfusion instance;
        SpellBookUI ui;
        Player.SkillState keep, sacrifice;
        string message;
        float messageUntil;

        static readonly string[] RunStages = { "Fire", "Ice", "Earth", "Air", "Lightning" };

        // v1.1.1: the Hub or your house (the title screen and loading scenes are neither home nor a run).
        internal static bool AtHome
        {
            get { string s = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name; return s == "Hub" || s == "PlayerRoom"; }
        }

        internal static bool InRun
        {
            get
            {
                // v1.1.1: only the run's own scenes (the stages, their boss levels, the final level). Every other scene is
                // home: the Hub, your house (PlayerRoom, where Tomi's book showed only your equipped arcana), PvP, menus.
                string s = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                if (string.IsNullOrEmpty(s)) return false;
                if (s == "FinalBossLevel") return true;
                foreach (string st in RunStages) if (s == st || s == st + "BossLevel") return true;
                return false;
            }
        }

        static int Cost { get { return 80 + 40 * Mathf.Max(0, GameController.tierCount); } }

        internal static HashSet<string> OwnedIds(Player p, bool includeShelf = true)
        {
            // v0.9.4: LootManager.currentPlayerSkills is never filled by the game (only reset), so it isn't the run's
            // list. What the player holds in a run is what the equip menu shows: the equipped slots (assignedSkills)
            // and the one held arcana (stockSkill, set by PickUpSkill / RemoveSkill / the equip menu's swaps).
            HashSet<string> ids = new HashSet<string>();
            if (p == null) return ids;
            if (p.assignedSkills != null) foreach (Player.SkillState s in p.assignedSkills) if (s != null && !string.IsNullOrEmpty(s.skillID)) ids.Add(s.skillID);
            try
            {
                Player.SkillState stock = Traverse.Create(p).Field("stockSkill").GetValue() as Player.SkillState;
                if (stock != null && !string.IsNullOrEmpty(stock.skillID)) ids.Add(stock.skillID);
            }
            catch (Exception) { }
            // v0.9.6: the merchant's unsold arcana cards nearby also show, as sacrifices only.
            if (includeShelf) foreach (string id in Shelf(p).Keys) ids.Add(id);
            return ids;
        }

        // v0.9.15: what the player holds this run (equipped + stock), for "give something new" reward checks
        // (LootManager.currentPlayerSkills, which those used, is never filled by the game).
        internal static HashSet<string> HeldIds() { return OwnedIds(Elites.Player0, false); }

        // ---------- v0.9.6: sacrificing from the merchant's shelf ----------
        // You can hold only one basic and one dash, so buying one just to sacrifice it was pointless. While Tomi is at
        // the merchant, the merchant's unsold cards (within 20 units, not signature upgrades) appear in his run menu,
        // marked "on the shelf". Picking one as the sacrifice costs its shelf price plus the infusion cost, and the
        // card leaves the shelf (price tag "x" and the card removed, as the shop's own Buy does). A pre-infused card
        // gives its infused element. Shelf cards can't be kept (only your own arcana take an element). Locked arcana
        // on the shelf don't appear (Tomi's book only lists unlocked ones).
        static readonly HashSet<int> consumed = new HashSet<int>();
        static Dictionary<string, SkillStoreItem> shelf = new Dictionary<string, SkillStoreItem>();
        static int shelfFrame = -1;

        internal static Dictionary<string, SkillStoreItem> Shelf(Player p)
        {
            if (shelfFrame == Time.frameCount) return shelf;
            shelfFrame = Time.frameCount;
            shelf = new Dictionary<string, SkillStoreItem>();
            if (p == null || !InRun) return shelf;
            foreach (SkillStoreItem s in UnityEngine.Object.FindObjectsOfType<SkillStoreItem>())
            {
                if (s == null || !s.gameObject.activeInHierarchy || consumed.Contains(s.GetInstanceID())) continue;
                if (string.IsNullOrEmpty(s.skillID) || s.signatureOnly || s.usePlatinumCost) continue;
                if (Vector2.Distance(s.transform.position, p.transform.position) > 20f) continue;
                if (!shelf.ContainsKey(s.skillID)) shelf[s.skillID] = s;
            }
            return shelf;
        }

        // On the shelf and not one you hold (one you hold is your own copy).
        SkillStoreItem ShelfOnly(Player p, Player.SkillState s)
        {
            if (s == null) return null;
            SkillStoreItem item;
            if (!Shelf(p).TryGetValue(s.skillID, out item)) return null;
            if (p != null && p.assignedSkills != null) foreach (Player.SkillState a in p.assignedSkills) if (a != null && a.skillID == s.skillID) return null;
            try { Player.SkillState stock = Traverse.Create(p).Field("stockSkill").GetValue() as Player.SkillState; if (stock != null && stock.skillID == s.skillID) return null; } catch (Exception) { }
            return item;
        }

        // The element a sacrifice gives: a shelf card's own (or its pre-infused one), else the arcana's current one.
        ElementType GiveOf(Player p, Player.SkillState s)
        {
            ElementType el;
            if (ShelfOnly(p, s) != null) return Infusion.TryGetShown(s.skillID, out el) ? el : s.element;
            return Infusion.CurrentElement(s);
        }

        static int ShelfPrice(SkillStoreItem item) { try { return item.Cost; } catch (Exception) { return 0; } }

        static void Consume(SkillStoreItem item)
        {
            consumed.Add(item.GetInstanceID());
            shelfFrame = -1;
            Infusion.RemovePending(item.skillID);
            try { PriceMarker pm = Traverse.Create(item).Field("priceMarker").GetValue() as PriceMarker; if (pm != null) pm.SetText("x"); } catch (Exception) { }
            Destroy(item.gameObject);
        }

        static TomiInfusion Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject go = new GameObject("HybridBosses_TomiInfusion");
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<TomiInfusion>();
                    go.AddComponent<TomixJournal>(); // v1.1.1
                    go.AddComponent<TomixBookText>(); // v1.1.2
                }
                return instance;
            }
        }

        // v1.1.1: only Tomix's book infuses / switches versions; Tomi's is the game's own. The book is still tracked
        // either way (the modifiers menu opens from it on a pad).
        bool infusing;
        internal static bool TomixOpen { get { return instance != null && instance.Active && instance.infusing; } }
        internal static SpellBookUI UI { get { return instance != null ? instance.ui : null; } }

        // v1.1.2: what a spellbook lists. In a run: the arcana you hold (and the shelf). At home in Tomix's book (open,
        // or opening: the game builds the lists inside Activate, before our Activate postfix): only arcana with
        // unlocked infused versions. Tomi's own book at home: everything (null = no filter).
        internal static HashSet<string> BookFilter(Player p)
        {
            if (InRun) return OwnedIds(p);
            if (!TomixOpen && !Tomix.Opening) return null;
            HashSet<string> ids = new HashSet<string>();
            foreach (string id in InfusionUnlocks.UnlockedIds) if (InfusionUnlocks.Versions(id).Count > 0) ids.Add(id);
            return ids;
        }
        internal static void Opened(SpellBookUI ui) { bool tomix = Tomix.ConsumeOpen(); if (!Plugin.InfusionEnabled.Value) return; Instance.ui = ui; Instance.infusing = tomix; Instance.keep = Instance.sacrifice = null; Instance.message = null; }
        internal static void Closed(SpellBookUI ui) { if (instance != null && instance.ui == ui) { instance.ui = null; instance.keep = instance.sacrifice = null; } }

        // Ctrl+F8 test key / fallback: Tomi right here.
        // v1.1.1: Tomix (Tomi's prefab, recoloured and renamed) unless asked otherwise.
        internal static SpellBookNpc SpawnAt(Vector2 at, bool makeTomix = true)
        {
            try
            {
                GameObject prefab = SpellBookNpc.Prefab;
                if (prefab == null) return null;
                SpellBookNpc npc = Globals.ChaosInst<SpellBookNpc>(prefab, null, (Vector3)at, null);
                if (makeTomix) Tomix.Make(npc);
                Plugin.Log.LogInfo("INFUSION spellbook spawned at " + at);
                return npc;
            }
            catch (Exception ex) { Plugin.Log.LogWarning("INFUSION spellbook spawn failed: " + ex.Message); return null; }
        }

        internal static void SpawnHere()
        {
            Player p = GameController.playerScripts != null && GameController.playerScripts.Length > 0 ? GameController.playerScripts[0] : null;
            if (p != null) SpawnAt((Vector2)p.transform.position + new Vector2(3f, 0f));
        }

        Player.SkillState Cursor()
        {
            if (ui == null) return null;
            try { return Traverse.Create(ui).Field("currentSkill").GetValue() as Player.SkillState; } catch (Exception) { return null; }
        }

        Player PlayerOf()
        {
            Player p = ui != null ? Traverse.Create(ui).Field("player").GetValue() as Player : null;
            if (p == null && GameController.playerScripts != null && GameController.playerScripts.Length > 0) p = GameController.playerScripts[0];
            return p;
        }

        bool Active { get { return ui != null && Traverse.Create(ui).Field("isActive").GetValue<bool>(); } }

        void Say(string m) { message = m; messageUntil = Time.unscaledTime + 3f; }

        // v0.9.16: for the modifiers menu (opened from Tomi's spellbook in the Hub, with its input device).
        internal static bool MenuOpen { get { return instance != null && instance.Active; } }
        internal static ChaosInputDevice Device
        {
            get
            {
                ChaosInputDevice d = null;
                try { if (instance != null && instance.ui != null) d = Traverse.Create(instance.ui).Field("inputDev").GetValue() as ChaosInputDevice; } catch (Exception) { }
                if (d == null) { Player p = Elites.Player0; if (p != null) d = p.inputDevice; }
                return d;
            }
        }

        void Update()
        {
            if (!Active || !infusing || ModifiersMenu.IsOpen || TomixJournal.IsOpen) return;
            if (!Input.GetKeyDown(KeyCode.I) && !PadPressed()) return;
            Player.SkillState cur = Cursor();
            if (InRun) RunPress(cur); else HubPress(cur);
        }

        // v0.7.3: controller (Steam Deck): the game's own input actions (Rewired) through the menu's input device.
        // Tomi's menu only reads Confirm and Cancel, so the arcana buttons (config Infusion.Buttons, default
        // Skill1,Skill2,Skill3) are free; a press that is also Confirm / Cancel this frame is ignored.
        bool PadPressed()
        {
            try
            {
                ChaosInputDevice dev = Traverse.Create(ui).Field("inputDev").GetValue() as ChaosInputDevice;
                if (dev == null) { Player p = PlayerOf(); dev = p != null ? p.inputDevice : null; }
                if (dev == null) return false;
                if (dev.GetButtonDown("Confirm") || dev.GetButtonDown("Cancel")) return false;
                foreach (string action in Plugin.InfusionButtons.Value.Split(',', ';', ' '))
                    if (action.Length > 0 && dev.GetButtonDown(action.Trim())) return true;
            }
            catch (Exception) { }
            return false;
        }

        static string Key { get { return "[I] / arcana button"; } }

        void RunPress(Player.SkillState cur)
        {
            Player p = PlayerOf();
            if (cur == null) { Say("Point the cursor at one of your arcana first."); return; }
            if (cur.isSignature) { Say("Signatures can't be infused."); return; }
            if (keep == null)
            {
                if (ShelfOnly(p, cur) != null) { Say(IntroUI.SkillName(cur.skillID) + " is on the merchant's shelf: it can only be sacrificed. Pick one of your own to keep first."); return; }
                keep = cur; sacrifice = null; Say("Keeping " + IntroUI.SkillName(cur.skillID) + ". Now point at the arcana to sacrifice and press [I]."); return;
            }
            if (cur == keep) { keep = sacrifice = null; Say("Cleared."); return; }
            if (sacrifice != cur)
            {
                ElementType giveEl = GiveOf(p, cur), keepSecond;
                bool hasSecond = Infusion.TryGetSecond(keep.skillID, out keepSecond);
                if (Infusion.Name(giveEl) == Infusion.Name(Infusion.CurrentElement(keep)) || (hasSecond && Infusion.Name(giveEl) == Infusion.Name(keepSecond))) { Say("That one has the same element."); return; }
                if ((cur.isBasic || cur.isDash) && Equipped(p, cur)) { Say("You can't sacrifice your equipped basic or dash."); return; }
                sacrifice = cur;
                Say("Press [I] again to infuse for " + TotalCost(p, cur) + " gold.");
                return;
            }
            Infuse(p);
        }

        // Infusion cost, plus the shelf price when the sacrifice comes off the merchant's shelf.
        int TotalCost(Player p, Player.SkillState sac)
        {
            SkillStoreItem item = ShelfOnly(p, sac);
            return Cost * (Dual(keep) ? 2 : 1) + (item != null ? ShelfPrice(item) : 0);
        }

        // v0.9.16: late in a run, infusing an arcana already infused this run adds a second element (dual infusion,
        // double cost); infusing a dual one replaces its second element. Earlier, re-infusing replaces as before.
        static bool Dual(Player.SkillState k) { return k != null && Infusion.DualAllowed && Infusion.InfusedThisRun(k.skillID); }

        static bool Equipped(Player p, Player.SkillState s)
        {
            if (p == null || p.assignedSkills == null) return false;
            foreach (Player.SkillState a in p.assignedSkills) if (a == s) return true;
            return false;
        }

        void Infuse(Player p)
        {
            SkillStoreItem fromShelf = ShelfOnly(p, sacrifice);
            int total = TotalCost(p, sacrifice);
            int gold = Player.goldWallet != null ? Player.goldWallet.balance : 0;
            if (gold < total || !Player.goldWallet.Withdraw(total)) { Say("Not enough gold (" + total + ")."); return; }
            ElementType give = GiveOf(p, sacrifice);
            string keepId = keep.skillID, sacId = sacrifice.skillID;
            bool dual = Dual(keep);
            if (fromShelf != null) Consume(fromShelf);
            else
            {
                try { p.RemoveSkill(sacId); } catch (Exception ex) { Plugin.Log.LogWarning("INFUSION could not remove " + sacId + ": " + ex.Message); }
            }
            ElementType first = Infusion.CurrentElement(keep);
            if (dual) Infusion.SetSecond(keepId, give); else Infusion.Set(keepId, give);
            try { p.AnnounceSkillChanged(keep); } catch (Exception) { }
            keep = sacrifice = null;
            Refresh(p);
            if (dual) Say(IntroUI.SkillName(keepId) + " now alternates " + Infusion.Name(first) + " and " + Infusion.Name(give) + "!  (" + IntroUI.SkillName(sacId) + (fromShelf != null ? " was taken from the shelf)" : " was consumed)"));
            else Say(Infusion.Name(give) + "-infused " + IntroUI.SkillName(keepId) + "!  (" + IntroUI.SkillName(sacId) + (fromShelf != null ? " was taken from the shelf)" : " was consumed)"));
            Bounties.Report(BountyKind.Infuse, null); // v0.9.16
            Tomix.SayMixed(); // v1.1.1
            Plugin.Log.LogInfo("INFUSION " + keepId + " took " + Infusion.Name(give) + " from " + sacId + (fromShelf != null ? " (from the shelf)" : "") + " for " + total + " gold");
        }

        void HubPress(Player.SkillState cur)
        {
            // v1.1.2: only on Tomix's arcana pages (spells, signatures, cards), not on your loadout slots: switching
            // from the loadout acted on the wrong arcana. Pick the version here, then equip it to try it out.
            string focus = "";
            try { focus = Traverse.Create(ui).Field("currentFocus").GetValue().ToString(); } catch (Exception) { }
            if (focus == "Player" || focus == "Element" || focus == "Tab") { Say("Open one of Tomix's pages and point at an arcana to switch its version."); return; }
            if (cur == null) { Say("Point the cursor at an arcana."); return; }
            // v0.9.18: the original, each single version, then each unlocked dual pair.
            List<string> options = InfusionUnlocks.Options(cur.skillID);
            if (options.Count <= 1) { Say(IntroUI.SkillName(cur.skillID) + " has no infused versions yet. Bring it to Tomix during a run."); return; }
            int i = options.IndexOf(InfusionUnlocks.SelectedRaw(cur.skillID) ?? "");
            InfusionUnlocks.SelectRaw(cur.skillID, options[(i + 1) % options.Count]);
            Player hp = PlayerOf();
            Refresh(hp);
            try { if (hp != null) hp.AnnounceSkillChanged(cur); } catch (Exception) { } // the HUD's icon, if it's equipped
            Plugin.Log.LogInfo("INFUSION version " + cur.skillID + " -> " + InfusionUnlocks.Label(InfusionUnlocks.SelectedRaw(cur.skillID)) + " (focus " + focus + ")");
            message = null; // the bar itself shows the chosen version (a second line repeated it and overlapped)
        }

        // Rebuild the menu's lists and the shown page so icons and lists update.
        void Refresh(Player p)
        {
            if (ui == null) return;
            // v1.1.2: rebuilding the lists is only needed in runs (an infusion consumes an arcana). At home a version
            // switch changes no list, and rebuilding them out of the book's own flow dropped basics into the arcana
            // grid (a card you couldn't reach): just redraw.
            if (InRun) try { Traverse.Create(ui).Method("LoadEleSkillDict", new[] { typeof(Player) }).GetValue(p); } catch (Exception) { }
            // Redraw the page that's showing: the signature (Overdrive) page lists other cards than the normal one, and
            // redrawing the normal one there showed its cards (a Stone Fist) over the signature page's cursor.
            string focus = "";
            try { focus = Traverse.Create(ui).Field("currentFocus").GetValue().ToString(); } catch (Exception) { }
            if (focus != "Info") try { Traverse.Create(ui).Method("LoadSpellPage", new[] { typeof(bool) }).GetValue(focus == "Overdrive"); } catch (Exception) { }
            try { Traverse.Create(ui).Method("SetEquipSlots", new[] { typeof(Player) }).GetValue(p); } catch (Exception) { }
            try { Traverse.Create(ui).Method("LoadInfoPage").GetValue(); } catch (Exception) { }
        }

        void OnGUI()
        {
            if (!Active || !infusing || TomixJournal.IsOpen) return;
            Player.SkillState cur = Cursor();
            string line;
            if (InRun)
            {
                Player p = PlayerOf();
                if (keep == null) line = "<b>[I] Infuse</b>  point at the arcana to keep  ·  cost " + Cost + " gold" + (Infusion.DualAllowed ? "  ·  an infused arcana takes a second element" : "");
                else if (sacrifice == null) line = "<b>Keeping " + IntroUI.SkillName(keep.skillID) + "</b>" + (Dual(keep) ? " (dual infusion)" : "") + "  ·  [I] on the arcana to sacrifice  ·  [I] on it again to clear";
                else if (Dual(keep)) line = "<b>" + IntroUI.SkillName(keep.skillID) + "</b> alternates <b>" + Infusion.Name(Infusion.CurrentElement(keep)) + "</b> and <b>" + Infusion.Name(GiveOf(p, sacrifice)) + "</b>  ·  [I] dual infuse (" + TotalCost(p, sacrifice) + " gold)";
                else line = "<b>" + IntroUI.SkillName(keep.skillID) + "</b> takes <b>" + Infusion.Name(GiveOf(p, sacrifice)) + "</b> from " + IntroUI.SkillName(sacrifice.skillID) + "  ·  [I] infuse (" + TotalCost(p, sacrifice) + " gold)";
                // v0.9.6: under the cursor, a card from the merchant's shelf (sacrifice only) and what it costs.
                SkillStoreItem onShelf = cur != null ? ShelfOnly(p, cur) : null;
                if (onShelf != null) line += "\n<color=#ffd27a>" + IntroUI.SkillName(cur.skillID) + ": on the merchant's shelf  ·  sacrifice only  ·  +" + ShelfPrice(onShelf) + " gold</color>";
            }
            else
            {
                List<ElementType> v = cur != null ? InfusionUnlocks.Versions(cur.skillID) : new List<ElementType>();
                int opts = cur != null ? InfusionUnlocks.Options(cur.skillID).Count : 1;
                // v1.1.2: Tomix's book at home lists only your infused arcana.
                if (cur == null || v.Count == 0)
                    line = "Tomix's book: only your infused arcana  ·  open an element, then [I] on an arcana to pick its version  ·  J / Interact: journal";
                else
                {
                    int duals = InfusionUnlocks.Pairs(cur.skillID).Count;
                    line = "<b>[I] Switch version</b>  ·  " + IntroUI.SkillName(cur.skillID) + ": " + InfusionUnlocks.Label(InfusionUnlocks.SelectedRaw(cur.skillID))
                        + "  ·  " + (opts - 1) + " infused version" + (opts - 1 == 1 ? "" : "s") + (duals > 0 ? " (" + duals + " dual)" : "") + "  ·  J / Interact: journal";
                    line += "\n" + Memory(cur, v);
                }
            }
            if (message != null && Time.unscaledTime < messageUntil) line += "\n<color=#ffe9a8>" + message + "</color>";
            line = line.Replace("<b>", "").Replace("</b>", "").Replace("[I]", Key);
            // v0.7.3: the game's look: its font with a drop shadow on a dark band (no Unity box skin).
            if (hintStyle == null || GameFont.Changed(ref hintFont)) hintStyle = GameFont.Style(GUI.skin.label, 18, TextAnchor.MiddleCenter, false, true);
            // v0.7.4: at the top of the screen (the bottom has the game's own prompts) and as tall as its text.
            float w = Mathf.Min(900, Screen.width - 40);
            float h = hintStyle.CalcHeight(new GUIContent(line), w - 24) + 14f;
            Rect band = new Rect((Screen.width - w) / 2, 18f, w, h);
            Color old = GUI.color;
            GUI.color = new Color(0.04f, 0.03f, 0.07f, 0.82f);
            GUI.DrawTexture(band, Texture2D.whiteTexture);
            GUI.color = new Color(0.95f, 0.8f, 0.45f, 0.9f);
            GUI.DrawTexture(new Rect(band.x, band.y, band.width, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(band.x, band.yMax - 2f, band.width, 2f), Texture2D.whiteTexture);
            GUI.color = old;
            GameFont.Shadowed(new Rect(band.x + 12, band.y + 4, band.width - 24, band.height - 8), line, hintStyle, new Color(1f, 0.97f, 0.9f));
        }

        static GUIStyle hintStyle;
        static Font hintFont;

        // v0.8.2: element memory. Every element this arcana can be infused with (all but its own; Radiant and Prism
        // with Arcanos): collected versions in their colour, missing ones in grey, and the count.
        static string Memory(Player.SkillState s, List<ElementType> have)
        {
            List<ElementType> all = new List<ElementType> { ElementType.Fire, ElementType.Water, ElementType.Ice, ElementType.Earth, ElementType.Air, ElementType.Lightning, ElementType.Chaos };
            ElementType? ra = ArcanosBridge.ElementFor(StageVariant.Radiant), pr = ArcanosBridge.ElementFor(StageVariant.Prism);
            if (ra.HasValue) all.Add(ra.Value);
            if (pr.HasValue) all.Add(pr.Value);
            string own = Infusion.Name(s.element);
            all.RemoveAll(e => Infusion.Name(e) == own);
            HashSet<string> got = new HashSet<string>();
            foreach (ElementType e in have) got.Add(Infusion.Name(e));
            List<string> parts = new List<string>();
            int n = 0;
            foreach (ElementType e in all)
            {
                string name = Infusion.Name(e);
                if (got.Contains(name)) { n++; parts.Add("<color=#" + ColorUtility.ToHtmlStringRGB(Infusion.ColorOf(e)) + ">" + name + "</color>"); }
                else parts.Add("<color=#5a5560>" + name + "</color>");
            }
            return "Collected " + n + " / " + all.Count + ":  " + string.Join("  ", parts.ToArray());
        }
    }

    // Infused versions the player has unlocked (kept between runs) and which one is chosen for each arcana.
    // Stored next to the mod's config: BepInEx/config/osi.wol.hybridbosses.infusions.txt, one arcana per line:
    // skillID|Fire,Water|Fire   (unlocked versions | chosen version, empty = the original).
    internal static class InfusionUnlocks
    {
        static Dictionary<string, List<string>> unlocked;
        static Dictionary<string, string> selected;
        static string FilePath { get { return System.IO.Path.Combine(BepInEx.Paths.ConfigPath, "osi.wol.hybridbosses.infusions.txt"); } }

        static void Load()
        {
            if (unlocked != null) return;
            unlocked = new Dictionary<string, List<string>>();
            selected = new Dictionary<string, string>();
            try
            {
                if (!System.IO.File.Exists(FilePath)) return;
                foreach (string line in System.IO.File.ReadAllLines(FilePath))
                {
                    string[] parts = line.Split('|');
                    if (parts.Length < 2 || parts[0].Length == 0) continue;
                    unlocked[parts[0]] = new List<string>(parts[1].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
                    if (parts.Length > 2 && parts[2].Length > 0) selected[parts[0]] = parts[2];
                }
            }
            catch (Exception ex) { Plugin.Log.LogWarning("INFUSION could not read unlocks: " + ex.Message); }
        }

        static void Save()
        {
            if (Plugin.TestMode) return; // v1.1.1: test hotkeys on: kept in memory only
            try
            {
                List<string> lines = new List<string>();
                foreach (KeyValuePair<string, List<string>> kv in unlocked)
                {
                    string sel;
                    selected.TryGetValue(kv.Key, out sel);
                    lines.Add(kv.Key + "|" + string.Join(",", kv.Value.ToArray()) + "|" + (sel ?? ""));
                }
                System.IO.File.WriteAllLines(FilePath, lines.ToArray());
            }
            catch (Exception ex) { Plugin.Log.LogWarning("INFUSION could not save unlocks: " + ex.Message); }
        }

        internal static void Unlock(string skillID, ElementType el)
        {
            Load();
            string name = Infusion.Name(el);
            List<string> list;
            if (!unlocked.TryGetValue(skillID, out list)) unlocked[skillID] = list = new List<string>();
            if (list.Contains(name)) return;
            list.Add(name);
            Save();
            Plugin.Log.LogInfo("INFUSION unlocked " + name + "-infused " + skillID + " for good");
        }

        internal static bool AnySelected { get { Load(); return selected.Count > 0; } }

        internal static List<ElementType> Versions(string skillID)
        {
            Load();
            List<ElementType> res = new List<ElementType>();
            List<string> list;
            if (skillID == null || !unlocked.TryGetValue(skillID, out list)) return res;
            foreach (string n in list) { ElementType? e = FromName(n); if (e.HasValue) res.Add(e.Value); }
            return res;
        }

        // v0.9.18: a chosen version is "Fire" (single) or "Fire+Ice" (a dual pair: any two unlocked singles).
        internal static ElementType? Selected(string skillID)
        {
            string raw = SelectedRaw(skillID);
            if (raw == null) return null;
            int plus = raw.IndexOf('+');
            return FromName(plus >= 0 ? raw.Substring(0, plus) : raw);
        }

        internal static ElementType? SelectedSecond(string skillID)
        {
            string raw = SelectedRaw(skillID);
            int plus = raw != null ? raw.IndexOf('+') : -1;
            return plus >= 0 ? FromName(raw.Substring(plus + 1)) : null;
        }

        internal static string SelectedRaw(string skillID)
        {
            Load();
            string n;
            return skillID != null && selected.TryGetValue(skillID, out n) && n.Length > 0 ? n : null;
        }

        internal static void Select(string skillID, ElementType? el) { SelectRaw(skillID, el.HasValue ? Infusion.Name(el.Value) : null); }

        internal static void SelectRaw(string skillID, string raw)
        {
            Load();
            if (!string.IsNullOrEmpty(raw)) selected[skillID] = raw; else selected.Remove(skillID);
            Save();
        }

        // Everything Tomi can switch an arcana to: the original (""), each single version, each dual pair.
        internal static List<string> Options(string skillID)
        {
            List<string> opts = new List<string> { "" };
            List<ElementType> v = Versions(skillID);
            foreach (ElementType e in v) opts.Add(Infusion.Name(e));
            foreach (ElementType[] p in Pairs(skillID)) opts.Add(Infusion.Name(p[0]) + "+" + Infusion.Name(p[1]));
            return opts;
        }

        // Dual pairs: v0.9.19: the arcana's own element counts, so one infused version already gives a pair
        // (Stone Fist + Fire-infused = Earth / Fire); two infused versions add a pair of those two, and so on. Prism is
        // never part of a pair (it already cycles through the five elements). A Neutral arcana has no own element.
        internal static List<ElementType[]> Pairs(string skillID)
        {
            List<ElementType> v = Versions(skillID);
            v.RemoveAll(e => Infusion.Name(e) == "Prism");
            List<ElementType[]> res = new List<ElementType[]>();
            ElementType? own = OwnElement(skillID);
            if (own.HasValue) foreach (ElementType e in v) if (Infusion.Name(e) != Infusion.Name(own.Value)) res.Add(new[] { own.Value, e });
            for (int i = 0; i < v.Count; i++) for (int j = i + 1; j < v.Count; j++) res.Add(new[] { v[i], v[j] });
            return res;
        }

        // The arcana's own element (the player's skill state when known, else the catalog), null for Neutral / Prism.
        internal static ElementType? OwnElement(string skillID)
        {
            ElementType el = ElementType.Neutral;
            bool found = false;
            try
            {
                Player p = Elites.Player0;
                Player.SkillState s;
                if (p != null && p.skillsDict != null && p.skillsDict.TryGetValue(skillID, out s) && s != null) { el = s.element; found = true; }
            }
            catch (Exception) { }
            if (!found)
            {
                ArcanaDef d = ArcanaCatalog.Get(skillID);
                if (d != null)
                    switch (d.Element)
                    {
                        case "Flame": el = ElementType.Fire; break;
                        case "Frost": el = ElementType.Water; break;
                        case "Earth": el = ElementType.Earth; break;
                        case "Wind": el = ElementType.Air; break;
                        case "Thunder": el = ElementType.Lightning; break;
                        case "Chaos": el = ElementType.Chaos; break;
                    }
            }
            string n = Infusion.Name(el);
            if (el == ElementType.Neutral || n == "Prism" || FromName(n) == null) return null;
            return FromName(n);
        }

        internal static IEnumerable<string> UnlockedIds { get { Load(); return new List<string>(unlocked.Keys); } }

        internal static string Label(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "original";
            return raw.Contains("+") ? raw.Replace("+", " / ") + " dual" : raw + "-infused";
        }

        internal static ElementType? FromName(string n)
        {
            switch (n)
            {
                case "Fire": return ElementType.Fire;
                case "Water": return ElementType.Water;
                case "Ice": return ElementType.Ice;
                case "Earth": return ElementType.Earth;
                case "Wind": return ElementType.Air;
                case "Thunder": return ElementType.Lightning;
                case "Chaos": return ElementType.Chaos;
                case "Radiant": return ArcanosBridge.ElementFor(StageVariant.Radiant);
                case "Prism": return ArcanosBridge.ElementFor(StageVariant.Prism);
            }
            return null;
        }
    }
}