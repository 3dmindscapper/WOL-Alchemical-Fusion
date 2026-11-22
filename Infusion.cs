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
        static readonly Dictionary<string, ElementType> infused = new Dictionary<string, ElementType>();
        static readonly Dictionary<string, ElementType> pending = new Dictionary<string, ElementType>(); // pre-infused drops / stock, until picked up
        static readonly HashSet<AttackInfo> done = new HashSet<AttackInfo>();

        internal static bool Any { get { return infused.Count > 0 || pending.Count > 0 || InfusionUnlocks.AnySelected; } }

        // This run's infusion, else the version chosen at Tomi in the Hub (an unlocked infused version).
        internal static bool TryGet(string skillID, out ElementType el)
        {
            el = ElementType.Neutral;
            if (string.IsNullOrEmpty(skillID)) return false;
            if (infused.TryGetValue(skillID, out el)) return true;
            ElementType? chosen = InfusionUnlocks.Selected(skillID);
            if (chosen.HasValue) { el = chosen.Value; return true; }
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
            if (infused.Count == 0 && pending.Count == 0) return;
            infused.Clear(); pending.Clear(); done.Clear();
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
            if (key != null) return ElementRamps.Mid(key);
            return Name(el) == "Prism" ? new Color(1f, 0.55f, 0.9f) : Color.white;
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

        static bool IsPlayersAttack(Attack atk)
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
            if (!Any || !IsPlayersAttack(atk)) return false;
            ElementType el;
            if (!TryGet(atk.atkInfo.skillID, out el)) return false;
            AttackInfo info = atk.atkInfo;
            string name = Name(el);
            if (done.Add(info))
            {
                if (done.Count > 8192) done.Clear();
                float chance = Mathf.Max(Mathf.Max(info.burnChance, info.slowChance), Mathf.Max(Mathf.Max(info.poisonChance, info.shockChance), Mathf.Max(info.freezeChance, info.chaosChance)));
                int level = Mathf.Max(1, Mathf.Max(Mathf.Max(info.burnLevel, info.slowLevel), Mathf.Max(info.poisonLevel, info.shockLevel)));
                if (chance <= 0f) chance = 0.15f; // a move without a status still gets a little of its new element's
                info.burnChance = info.slowChance = info.poisonChance = info.shockChance = info.freezeChance = info.chaosChance = 0f;
                info.damage = Mathf.Max(1, Mathf.RoundToInt(info.damage * DamageBonus));
                if (name == "Prism")
                {
                    // Prism, like Arcanos' own: each cast is the next of the five base elements, with its status.
                    PrismElement pe = Prism.Next(atk.atkInfo.entity != null ? (Component)atk.atkInfo.entity : atk);
                    info.elementType = pe.Type;
                    pe.ApplyStatus(info);
                    CouncilCorruption.PreviewRecolor(atk, ElementRamps.ForElement(pe.Type));
                    return true;
                }
                if (name == "Radiant")
                {
                    if (!ArcanosBridge.SetElement(atk, el)) info.elementType = el; // Arcanos' own element on a player attack, as its arcana do
                }
                else
                {
                    info.elementType = el;
                    switch (name)
                    {
                        case "Fire": info.burnChance = chance; info.burnLevel = Mathf.Max(info.burnLevel, level); break;
                        case "Water": case "Wind": info.slowChance = chance; info.slowLevel = Mathf.Max(info.slowLevel, level); break;
                        case "Ice": info.freezeChance = chance; break;
                        case "Earth": info.poisonChance = chance; info.poisonLevel = Mathf.Max(info.poisonLevel, level); break;
                        case "Thunder": info.shockChance = chance; info.shockLevel = Mathf.Max(info.shockLevel, level); break;
                        case "Chaos": info.chaosChance = chance; info.chaosLevel = Mathf.Max(info.chaosLevel, level); break;
                    }
                }
            }
            string key = RampKey(el);
            if (key != null) CouncilCorruption.PreviewRecolor(atk, key);
            return true;
        }
    }

    // ---------- icons and cards: an infused arcana's icon in its new element's colours ----------

    [HarmonyPatch(typeof(IconManager), "GetSkillIcon")]
    static class InfusionIconPatch
    {
        static void Postfix(string __0, ref Sprite __result)
        {
            if (__result == null || !Infusion.Any) return;
            ElementType el;
            if (!Infusion.TryGetShown(__0, out el)) return;
            string key = Infusion.RampKey(el);
            Sprite twin = key != null ? RecolorEngine.Twin(__result, key) : null;
            if (twin != null) __result = twin;
        }
    }

    // ---------- rare pre-infused drops and stock ----------

    // A drop rolls once whether it comes infused; LootManager may try several arcana ids before settling, so only
    // the latest candidate stays pending (the card shows it as it spawns).
    [HarmonyPatch(typeof(LootManager), "DropSkill")]
    static class InfusionDropPatch
    {
        internal static bool infusedDrop;
        internal static string candidate;
        static void Prefix(string __2)
        {
            infusedDrop = Plugin.InfusionEnabled.Value && UnityEngine.Random.value < Plugin.InfusedDropChance.Value;
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
                if (!Plugin.InfusionEnabled.Value || __instance == null || Level.isBossRushMode) return;
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
            if (item == null || UnityEngine.Object.FindObjectOfType<SpellBookNpc>() != null) yield break; // the game already brought him
            try
            {
                Traverse.Create(item).Method("SpawnTomi").GetValue();
                Traverse.Create(item).Field("tomiSpawned").SetValue(true);
                Plugin.Log.LogInfo("INFUSION Tomi came to the arcana merchant");
            }
            catch (Exception ex) { Plugin.Log.LogWarning("INFUSION SpawnTomi failed: " + ex.Message); TomiInfusion.SpawnAt((Vector2)item.transform.position + new Vector2(-3.5f, -1f)); }
        }
    }

    // In a run, Tomi's spellbook lists only the arcana you own this run.
    [HarmonyPatch(typeof(SpellBookUI), "LoadEleSkillDict")]
    static class TomiRunListPatch
    {
        static void Postfix(SpellBookUI __instance, Player __0)
        {
            try
            {
                if (!TomiInfusion.InRun || __0 == null) return;
                HashSet<string> owned = TomiInfusion.OwnedIds(__0);
                IDictionary spells = Traverse.Create(__instance).Field("eleSpellDict").GetValue() as IDictionary;
                if (spells == null) return;
                foreach (object list in spells.Values)
                {
                    List<Player.SkillState> l = list as List<Player.SkillState>;
                    if (l != null) l.RemoveAll(s => s == null || !owned.Contains(s.skillID));
                }
            }
            catch (Exception ex) { Plugin.Log.LogWarning("INFUSION Tomi list filter failed: " + ex.Message); }
        }
    }

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

        internal static bool InRun
        {
            get
            {
                string s = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                return s != "Hub" && s != "Tutorial" && !string.IsNullOrEmpty(s);
            }
        }

        static int Cost { get { return 80 + 40 * Mathf.Max(0, GameController.tierCount); } }

        internal static HashSet<string> OwnedIds(Player p)
        {
            HashSet<string> ids = new HashSet<string>();
            List<string> cur = Traverse.Create(typeof(LootManager)).Field("currentPlayerSkills").GetValue() as List<string>;
            if (cur != null) ids.UnionWith(cur);
            if (p != null && p.assignedSkills != null) foreach (Player.SkillState s in p.assignedSkills) if (s != null) ids.Add(s.skillID);
            return ids;
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
                }
                return instance;
            }
        }

        internal static void Opened(SpellBookUI ui) { if (!Plugin.InfusionEnabled.Value) return; Instance.ui = ui; Instance.keep = Instance.sacrifice = null; Instance.message = null; }
        internal static void Closed(SpellBookUI ui) { if (instance != null && instance.ui == ui) { instance.ui = null; instance.keep = instance.sacrifice = null; } }

        // Ctrl+F8 test key / fallback: Tomi right here.
        internal static void SpawnAt(Vector2 at)
        {
            try
            {
                GameObject prefab = SpellBookNpc.Prefab;
                if (prefab == null) return;
                Globals.ChaosInst<SpellBookNpc>(prefab, null, (Vector3)at, null);
                Plugin.Log.LogInfo("INFUSION Tomi spawned at " + at);
            }
            catch (Exception ex) { Plugin.Log.LogWarning("INFUSION Tomi spawn failed: " + ex.Message); }
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

        void Update()
        {
            if (!Active) return;
            if (!Input.GetKeyDown(KeyCode.I)) return;
            Player.SkillState cur = Cursor();
            if (InRun) RunPress(cur); else HubPress(cur);
        }

        void RunPress(Player.SkillState cur)
        {
            Player p = PlayerOf();
            if (cur == null) { Say("Point the cursor at one of your arcana first."); return; }
            if (cur.isSignature) { Say("Signatures can't be infused."); return; }
            if (keep == null) { keep = cur; sacrifice = null; Say("Keeping " + IntroUI.SkillName(cur.skillID) + ". Now point at the arcana to sacrifice and press [I]."); return; }
            if (cur == keep) { keep = sacrifice = null; Say("Cleared."); return; }
            if (sacrifice != cur)
            {
                if (Infusion.Name(Infusion.CurrentElement(cur)) == Infusion.Name(Infusion.CurrentElement(keep))) { Say("That one has the same element."); return; }
                if ((cur.isBasic || cur.isDash) && Equipped(p, cur)) { Say("You can't sacrifice your equipped basic or dash."); return; }
                sacrifice = cur;
                Say("Press [I] again to infuse for " + Cost + " gold.");
                return;
            }
            Infuse(p);
        }

        static bool Equipped(Player p, Player.SkillState s)
        {
            if (p == null || p.assignedSkills == null) return false;
            foreach (Player.SkillState a in p.assignedSkills) if (a == s) return true;
            return false;
        }

        void Infuse(Player p)
        {
            int gold = Player.goldWallet != null ? Player.goldWallet.balance : 0;
            if (gold < Cost || !Player.goldWallet.Withdraw(Cost)) { Say("Not enough gold (" + Cost + ")."); return; }
            ElementType give = Infusion.CurrentElement(sacrifice);
            string keepId = keep.skillID, sacId = sacrifice.skillID;
            try { p.RemoveSkill(sacId); } catch (Exception ex) { Plugin.Log.LogWarning("INFUSION could not remove " + sacId + ": " + ex.Message); }
            List<string> cur = Traverse.Create(typeof(LootManager)).Field("currentPlayerSkills").GetValue() as List<string>;
            if (cur != null) cur.Remove(sacId);
            Infusion.Set(keepId, give);
            try { p.AnnounceSkillChanged(keep); } catch (Exception) { }
            keep = sacrifice = null;
            Refresh(p);
            Say(Infusion.Name(give) + "-infused " + IntroUI.SkillName(keepId) + "!  (" + IntroUI.SkillName(sacId) + " was consumed)");
        }

        void HubPress(Player.SkillState cur)
        {
            if (cur == null) { Say("Point the cursor at an arcana."); return; }
            List<ElementType> versions = InfusionUnlocks.Versions(cur.skillID);
            if (versions.Count == 0) { Say(IntroUI.SkillName(cur.skillID) + " has no infused versions yet. Infuse it at Tomi during a run."); return; }
            ElementType? now = InfusionUnlocks.Selected(cur.skillID);
            int i = now.HasValue ? versions.IndexOf(now.Value) : -1;
            ElementType? next = i + 1 < versions.Count ? (ElementType?)versions[i + 1] : null;
            InfusionUnlocks.Select(cur.skillID, next);
            Refresh(PlayerOf());
            Say(IntroUI.SkillName(cur.skillID) + ": " + (next.HasValue ? Infusion.Name(next.Value) + "-infused" : "original"));
        }

        // Rebuild the menu's lists and the shown page so icons and lists update.
        void Refresh(Player p)
        {
            if (ui == null) return;
            try { Traverse.Create(ui).Method("LoadEleSkillDict", new[] { typeof(Player) }).GetValue(p); } catch (Exception) { }
            try { Traverse.Create(ui).Method("LoadSpellPage", new[] { typeof(bool) }).GetValue(false); } catch (Exception) { }
            try { Traverse.Create(ui).Method("SetEquipSlots", new[] { typeof(Player) }).GetValue(p); } catch (Exception) { }
            try { Traverse.Create(ui).Method("LoadInfoPage").GetValue(); } catch (Exception) { }
        }

        void OnGUI()
        {
            if (!Active) return;
            Player.SkillState cur = Cursor();
            string line;
            if (InRun)
            {
                if (keep == null) line = "<b>[I] Infuse</b>  point at the arcana to keep  ·  cost " + Cost + " gold";
                else if (sacrifice == null) line = "<b>Keeping " + IntroUI.SkillName(keep.skillID) + "</b>  ·  [I] on the arcana to sacrifice  ·  [I] on it again to clear";
                else line = "<b>" + IntroUI.SkillName(keep.skillID) + "</b> takes <b>" + Infusion.Name(Infusion.CurrentElement(sacrifice)) + "</b> from " + IntroUI.SkillName(sacrifice.skillID) + "  ·  [I] infuse (" + Cost + " gold)";
            }
            else
            {
                List<ElementType> v = cur != null ? InfusionUnlocks.Versions(cur.skillID) : new List<ElementType>();
                ElementType? sel = cur != null ? InfusionUnlocks.Selected(cur.skillID) : null;
                line = v.Count == 0 ? "Infused versions you unlock in runs can be chosen here with [I]"
                    : "<b>[I] Switch version</b>  " + IntroUI.SkillName(cur.skillID) + ": " + (sel.HasValue ? Infusion.Name(sel.Value) + "-infused" : "original") + "  (" + (v.Count + 1) + " versions)";
            }
            if (message != null && Time.unscaledTime < messageUntil) line += "\n" + message;
            GUIStyle st = new GUIStyle(GUI.skin.box) { richText = true, fontSize = 14, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            st.normal.textColor = new Color(1f, 0.96f, 0.85f);
            float w = Mathf.Min(760, Screen.width - 40);
            GUI.Box(new Rect((Screen.width - w) / 2, Screen.height - 86, w, 58), line, st);
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

        internal static ElementType? Selected(string skillID)
        {
            Load();
            string n;
            return skillID != null && selected.TryGetValue(skillID, out n) ? FromName(n) : null;
        }

        internal static void Select(string skillID, ElementType? el)
        {
            Load();
            if (el.HasValue) selected[skillID] = Infusion.Name(el.Value); else selected.Remove(skillID);
            Save();
        }

        static ElementType? FromName(string n)
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