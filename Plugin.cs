using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace WolHybridBosses
{
    [BepInPlugin("osi.wol.hybridbosses", "Hybrid Bosses", Version)]
    [BepInDependency("wolarcanos.arcanos", BepInDependency.DependencyFlags.SoftDependency)] // optional: Radiant / Prism variants
    public class Plugin : BaseUnityPlugin
    {
        public const string Version = "0.9.0";
        internal static ManualLogSource Log;

        internal static ConfigEntry<bool> ElementalGiants;
        internal static ConfigEntry<float> ElementalGiantChance;
        internal static ConfigEntry<bool> MatchStageElement;
        internal static ConfigEntry<bool> IgnoreSkillDifficulty;
        internal static ConfigEntry<bool> DebugKeys;
        internal static ConfigEntry<float> ArcanaDamageMultiplier;
        internal static ConfigEntry<bool> ArcanaForAllGiants;
        internal static ConfigEntry<float> ArcanaCastGap;
        internal static ConfigEntry<float> EnhancedArcanaChance;
        internal static ConfigEntry<string> ArcanaExcludeFamilies;
        internal static ConfigEntry<float> ChaosRivalChance;
        internal static ConfigEntry<bool> RivalEvents;
        internal static ConfigEntry<float> RivalEventChance;
        internal static ConfigEntry<float> RivalIntroSeconds;
        internal static ConfigEntry<bool> RivalRobes;
        internal static ConfigEntry<bool> RivalRelics;
        internal static ConfigEntry<bool> GiantIntros;
        internal static ConfigEntry<bool> FinalChampions;
        internal static ConfigEntry<float> FinalChampionChance;
        internal static ConfigEntry<float> FinalChaosChance;
        internal static ConfigEntry<bool> FinalRoomBadge;
        internal static ConfigEntry<float> ChaosStageChance;
        internal static ConfigEntry<float> ChaosStageRewards;
        internal static ConfigEntry<float> VariantChaosShare, VariantRadiantShare, VariantPrismShare, VariantArchonChance, RadiantHealthDropChance;
        internal static ConfigEntry<bool> InfusionEnabled, InfusedCouncil, ElitesEnabled, TrialsEnabled, EchoEnabled, RivalScaleWithProgress;
        internal static ConfigEntry<float> EliteChanceMultiplier, EliteInfusedDropChance, TrialChance;
        internal static ConfigEntry<string> InfusionButtons;
        internal static ConfigEntry<float> InfusedDropChance, InfusedStockChance;
        internal static ConfigEntry<int> ChaosGiantPalette;
        internal static ConfigEntry<float> ChaosEnemyChance;
        internal static ConfigEntry<int> GiantMinElementMoves;
        internal static ConfigEntry<bool> BossBars;
        internal static ConfigEntry<bool> ChampionPhases;
        internal static ConfigEntry<float> ArchonRivalChance;
        internal static ConfigEntry<bool> CorruptedCouncil;
        internal static ConfigEntry<bool> GiantBossBars;

        void Awake()
        {
            Log = Logger;
            ElementalGiants = Config.Bind("ElementalGiants", "Enabled", true,
                "Giant (Super*) enemies can spawn with an element, its palette and that element's enemy skills.");
            MatchStageElement = Config.Bind("ElementalGiants", "MatchStageElement", true,
                "On an elemental stage (Forest/Earth, Ice, Fire, Air, Lightning) every giant takes that stage's element: Ice stage -> Water giants, and so on.");
            ElementalGiantChance = Config.Bind("ElementalGiants", "NeutralStageChance", 0.5f,
                "On a neutral stage (castle, final stage), or on any stage if MatchStageElement is off: chance (0 to 1) that a giant gets a random element. Applies on every difficulty, including normal.");
            IgnoreSkillDifficulty = Config.Bind("ElementalGiants", "IgnoreSkillDifficulty", true,
                "Borrowed elemental skills are usable on normal difficulty (the game normally locks some behind higher difficulty).");
            ArcanaDamageMultiplier = Config.Bind("EnemyArcana", "DamageMultiplier", 1f,
                "Damage multiplier for player arcana cast by enemies (arcana damage comes from the player's own stat data).");
            ArcanaForAllGiants = Config.Bind("EnemyArcana", "AllGiants", false,
                "Every elemental giant also casts player arcana of its element, not just the giant Coffin.");
            ArcanaCastGap = Config.Bind("EnemyArcana", "CastGap", 2.5f,
                "Minimum seconds between two arcana from the same giant (rivals use their tier's gap instead).");
            ArcanaExcludeFamilies = Config.Bind("EnemyArcana", "ExcludeFamilies", "",
                "Kinds of player arcana enemies must NOT use, comma separated: Projectile, Spell, Jump, Melee (basic arcana), Dash, Minion, Seeker, Ward. Empty = all of them.");
            ChaosRivalChance = Config.Bind("RivalWizards", "ChaosChance", 0.15f,
                "Chance (0 to 1) that a rival is a Chaos 'jack of all trades' wizard with arcana from every element, instead of the stage's element.");
            EnhancedArcanaChance = Config.Bind("EnemyArcana", "EnhancedChance", 0.2f,
                "Chance (0 to 1) that an arcana cast by an enemy (giant, rival, champion) is the enhanced version (more shots, +30% damage). The signature version is separate: only a rival's signature arcana cast with a full meter.");
            GiantMinElementMoves = Config.Bind("ElementalGiants", "MinElementMoves", 2,
                "Every elemental giant gets at least this many element moves. Giants with fewer borrowed enemy skills (Wind/Thunder Knight, Fire/Thunder Archer have none) are topped up with player arcana of their element that suit their style. 0 = off.");
            ChampionPhases = Config.Bind("FinalBoss", "Phases", true,
                "Final champions shift at half health: an Archon becomes a two-element hybrid (keeps 2 arcana, gains 3 of a second element, casts faster); the Chaos Sovereign gains 2 more arcana and casts faster.");
            ArchonRivalChance = Config.Bind("RivalWizards", "ArchonChance", 0.25f,
                "Chance (0 to 1) that the Hard portal of a rival arena holds an Archon (a final champion with its second phase; Chaos Sovereign on Chaos stages) instead of a Hard rival. It drops a gold chest.");
            CorruptedCouncil = Config.Bind("ChaosStages", "CorruptedCouncil", true,
                "A Council member whose tier had a Chaos stage is corrupted: dark purple sprites and attacks, glitched dialog with Chaos whispers, 'Corrupted' on the boss bar, and a purple boss circle on the loading-screen board.");
            BossBars = Config.Bind("BossBar", "Enabled", true,
                "Rivals and final champions get the game's big boss health bar (the one the Council and Sura use) once their intro ends.");
            GiantBossBars = Config.Bind("BossBar", "Giants", true,
                "Elemental giants get the boss health bar too, when their intro shows.");
            RivalEvents = Config.Bind("RivalWizards", "Enabled", true,
                "A rival portal can open somewhere on a stage: it leads to a selector room (Easy/Medium/Hard portals) and an arena, then back. The stage's misc room is left alone.");
            RivalEventChance = Config.Bind("RivalWizards", "Chance", 0.3f,
                "Chance (0 to 1) per regular stage that a rival portal opens.");
            RivalIntroSeconds = Config.Bind("RivalWizards", "IntroSeconds", 4f,
                "How long the camera shows the rival and their arcana before the fight.");
            RivalRobes = Config.Bind("RivalWizards", "Robes", true,
                "Rivals wear a random player robe (its colours) and get that robe's stat bonuses.");
            RivalRelics = Config.Bind("RivalWizards", "Relics", true,
                "Rivals carry a random stat relic (shown in the intro) and get its bonus.");
            GiantIntros = Config.Bind("ElementalGiants", "Intro", true,
                "When you first get close to an elemental giant, its skills and arcana appear at the top of the screen.");
            FinalChampions = Config.Bind("FinalBoss", "Enabled", true,
                "The final boss room can hold a champion instead of Sura. Beating it ends the run normally (victory, Chaos arcana unlock, ending).");
            FinalChampionChance = Config.Bind("FinalBoss", "Chance", 0.35f,
                "Chance (0 to 1) per run that the final boss is a champion instead of Sura.");
            FinalChaosChance = Config.Bind("FinalBoss", "ChaosSovereignChance", 0.3f,
                "When a champion is rolled: chance it is the Chaos Sovereign (arcana of every element) instead of an elemental Archon.");
            FinalRoomBadge = Config.Bind("FinalBoss", "RoomBadge", false,
                "Also show the 'NOT SURA' badge in the corner of the final room (the loading-screen board always shows the champion icon).");
            ChaosStageChance = Config.Bind("ChaosStages", "StageChance", 0.15f,
                "Chance (0 to 1) that a regular stage is its Chaos version: purple tiles and lights, enemies with Chaos arcana, a Chaos rival in its arena, better rewards, 'Chaos' in the title and a purple marker on the loading-screen board. Boss floors stay normal. Shift+F11 toggles it on the current stage.");
            ChaosStageRewards = Config.Bind("ChaosStages", "RewardMultiplier", 1.5f,
                "Reward multiplier on variant stages: Chaos gold and item drops; Radiant half as much extra gold (its enemies may drop health instead); Prism item drops and Chaos gems.");
            VariantChaosShare = Config.Bind("StageVariants", "ChaosShare", 0.5f, "With WoL Arcanos installed, the share of variant stages that are Chaos. (Without Arcanos every variant stage is Chaos.)");
            VariantRadiantShare = Config.Bind("StageVariants", "RadiantShare", 0.35f, "With WoL Arcanos installed, the share of variant stages that are Radiant (Arcanos' Fulgor element).");
            VariantPrismShare = Config.Bind("StageVariants", "PrismShare", 0.15f, "With WoL Arcanos installed, the share of variant stages that are Prism (Arcanos' Prisma element).");
            VariantArchonChance = Config.Bind("StageVariants", "ArchonChance", 0.25f, "With WoL Arcanos installed, chance that an elemental Archon (final champion or arena Archon) is a Radiant or Prism Archon instead.");
            InfusionEnabled = Config.Bind("Infusion", "Enabled", true, "An Infusion Altar stands beside the arcana merchant: keep one arcana, sacrifice another, and the kept one takes the sacrificed one's element (+10% damage). Run only.");
            InfusedCouncil = Config.Bind("Infusion", "InfusedCouncil", true, "A Council member (2nd and 3rd tier, when the tier has no Chaos/Radiant/Prism member) may have infused their element with another: same moveset, the other element's damage, status and colours, new lines, and a reward of that element. 15% / 25%, +20% from the second loop.");
            RivalScaleWithProgress = Config.Bind("RivalWizards", "ScaleWithProgress", true, "The rival portal's room offers one rival scaled to the run (first tier Easy, second Medium, third Hard, always Hard from the second loop) instead of three difficulty portals.");
            EchoEnabled = Config.Bind("RivalWizards", "EchoOfLastRun", true, "The rival portal's room also offers the Echo of your last run (your arcana with their infusions, robe and stat relics), once per run. Beating it gives a gold chest.");
            ElitesEnabled = Config.Bind("Elites", "Enabled", true, "Now and then a regular enemy is an elite with one trait (Shielded, Hasted, Splitting, Draining, Blinking): a coloured outline, an Elite label, more gold and sometimes an infused arcana. Chance 4% +3% per tier, +4% from the second loop.");
            EliteChanceMultiplier = Config.Bind("Elites", "ChanceMultiplier", 1f, "Multiplies the elite chance (0 turns elites off, 2 doubles them).");
            EliteInfusedDropChance = Config.Bind("Elites", "InfusedDropChance", 0.12f, "Chance that an elite drops an arcana that is already infused.");
            TrialsEnabled = Config.Bind("Trials", "Enabled", true, "Sometimes a trial portal opens on a stage: three waves with all your arcana forced into one element; clearing it drops an arcana already infused with that element.");
            TrialChance = Config.Bind("Trials", "Chance", 0.12f, "Chance per stage that a trial portal opens.");
            InfusionButtons = Config.Bind("Infusion", "Buttons", "Skill1,Skill2,Skill3", "Controller actions (the game's own input action names) that act as the infuse key [I] in Tomi's spellbook. Tomi's menu only uses Confirm and Cancel, so the arcana buttons are free. Other names: Interact, EquipMenu, Slide, Skill0..Skill5.");
            InfusedDropChance = Config.Bind("Infusion", "InfusedDropChance", 0.04f, "Chance that a dropped arcana comes already infused with another element.");
            InfusedStockChance = Config.Bind("Infusion", "InfusedStockChance", 0.08f, "Chance that an arcana in the merchant's stock is already infused (25% pricier).");
            RadiantHealthDropChance = Config.Bind("StageVariants", "RadiantHealthDrop", 0.15f, "Chance that an enemy on a Radiant stage leaves a health pickup when it dies.");
            ChaosEnemyChance = Config.Bind("ChaosStages", "EnemyChance", 0.5f,
                "On a Chaos stage, chance (0 to 1) that each Knight, Rogue, Archer, Mage, Summoner or Lancer is a Chaos version (1 Chaos arcana + purple colours). Like elemental stages, not every one is. Giants are always Chaos there.");
            ChaosGiantPalette = Config.Bind("ChaosStages", "GiantPalette", -1,
                "Palette row for Chaos giants. -1 = find the purple row automatically from each giant's palette texture (purple tint when it has none). Set a number (F6 cycles rows on the nearest enemy) to force one.");
            DebugKeys = Config.Bind("Debug", "Hotkeys", true,
                "F5 list enemies, F6 cycle palette, F7 dump nearest enemy, F8 spawn test giant, F9 dump element->palette, F10 reroll nearest giant's element, F11 open a rival arena right here, F12 final champion test (forces the next final room, or spawns a champion fight here).");

            Log.LogInfo("Hybrid Bosses " + Version + " starting; patches wait for the game's asset bundles.");
            StartCoroutine(PatchWhenGameReady());
        }

        // IMPORTANT: no patching in Awake. Harmony has Mono compile each original method, and Mono runs the static
        // constructors of classes it touches. Some originals touch LootManager (e.g. FinalBossRoomEventHandler.Start
        // reads LootManager.chaosSkillList), and LootManager's static constructor loads item prefabs from the asset
        // bundles. Before the bundles are loaded that constructor throws, and a failed static constructor breaks the
        // class for the whole session: no loot, no items, Compass errors, and other mods' patches fail (v0.4.0/0.4.1 bug).
        // So everything is patched only once ChaosBundle has loaded its bundles (as WoL Arcanos does with its patches).
        IEnumerator PatchWhenGameReady()
        {
            float waited = 0f;
            Traverse loaded = Traverse.Create(typeof(ChaosBundle)).Field("bundleLoaded");
            while (!(loaded.FieldExists() && loaded.GetValue<bool>()))
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            yield return null; // one more frame, so the game's own first-frame setup has run

            Harmony harmony = new Harmony("osi.wol.hybridbosses");
            try { harmony.PatchAll(typeof(Plugin).Assembly); }
            catch (Exception ex) { Log.LogError("Patching failed: " + ex); }
            try
            {
                ArcanaCatalog.DiscoverModded();               // other mods' arcana (LegendAPI skills, WoL Arcanos...)
                OwnerContext.PatchPrefabClasses(harmony);     // ownership on every arcana prefab class
                ChaosStageRunner.Ensure();                    // chaos stage variants
            }
            catch (Exception ex) { Log.LogError("ARCANA setup failed: " + ex); }
            Log.LogInfo("Hybrid Bosses " + Version + " loaded (patched " + Mathf.RoundToInt(waited * 1000f) + " ms after start, once the bundles were in). Elemental giants "
                + (ElementalGiants.Value ? "on" : "off") + ", chance " + ElementalGiantChance.Value);
        }

        void Update()
        {
            if (!DebugKeys.Value) return;
            if (Input.GetKeyDown(KeyCode.F5)) Debug.ListEnemies();
            if (Input.GetKeyDown(KeyCode.F6) && !(Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))) Debug.CyclePalette();
            if (Input.GetKeyDown(KeyCode.F7) && !(Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))) Debug.DumpNearestEnemy();
            bool ctrlKey = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            if (Input.GetKeyDown(KeyCode.F6) && ctrlKey) TrialEvent.OpenHere();
            if (Input.GetKeyDown(KeyCode.F7) && ctrlKey) Elites.TestNearest();
            if (Input.GetKeyDown(KeyCode.F8) && ctrlKey) TomiInfusion.SpawnHere();
            else if (Input.GetKeyDown(KeyCode.F8)) Debug.SpawnNextTestGiant();
            if (Input.GetKeyDown(KeyCode.F9) && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))) Debug.DumpArcanaVisuals();
            else if (Input.GetKeyDown(KeyCode.F9)) Debug.DumpElementPalette();
            if (Input.GetKeyDown(KeyCode.F10) && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))) InfusionPreview.Cycle();
            else if (Input.GetKeyDown(KeyCode.F10)) Debug.RerollNearestGiant();
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            if (Input.GetKeyDown(KeyCode.F11) && ctrl && shift) EchoRecorder.RecordNow();
            else if (Input.GetKeyDown(KeyCode.F11) && ctrl) Debug.SkipToNextStage();
            else if (Input.GetKeyDown(KeyCode.F11) && shift) ChaosStages.ToggleOnCurrentStage();
            else if (Input.GetKeyDown(KeyCode.F11)) Debug.OpenRivalArenaHere();
            if (Input.GetKeyDown(KeyCode.F12) && ctrl && shift) Debug.JumpToFinal();
            else if (Input.GetKeyDown(KeyCode.F12) && ctrl) CouncilCorruption.JumpToCouncil();
            else if (Input.GetKeyDown(KeyCode.F12) && shift) CouncilCorruption.ToggleForce();
            else if (Input.GetKeyDown(KeyCode.F12)) Debug.FinalChampionTest();
        }
    }

    // ------------------------------------------------------------------
    // Elemental giants
    // ------------------------------------------------------------------

    // One skill borrowed from an elemental enemy. Values come from that enemy's InitFSM in Assembly-CSharp.
    internal class SkillDef
    {
        public string Element, Kind, StateType, Name;
        public int Req, Prio;
        public float Min, Max, Cd;
        public bool Auto;
        public string CastAnim; // overrides SkillState.castingAnimName when the giant's animator lacks the original
        public SkillDef(string element, string kind, string stateType, string name, float req, float prio, float min, float max, float cd, bool auto, string castAnim = null)
        {
            Element = element; Kind = kind; StateType = stateType; Name = name;
            Req = (int)req; Prio = (int)prio; Min = min; Max = max; Cd = cd; Auto = auto; CastAnim = castAnim;
        }
    }

    internal static class ElementalGiants
    {
        // Elemental enemy prefix -> game ElementType name. Frost is tried as Water then Ice.
        internal static readonly string[] Prefixes = { "Flame", "Frost", "Earth", "Wind", "Thunder" };

        // Giant class -> regular enemy kind whose elemental skills it borrows.
        internal static readonly Dictionary<string, string> GiantKind = new Dictionary<string, string>
        {
            { "SuperKnight", "Knight" }, { "SuperMage", "Mage" }, { "SuperRogue", "Rogue" },
            { "SuperArcher", "Archer" }, { "SuperLancer", "Lancer" },
            { "SuperSummoner", "Mage" },   // summoners have no transplantable skills; borrow the mage set
            { "SuperCoffin", "" },         // no elemental coffin exists; casts player arcana instead (EnemyArcana)
        };

        // Only skills whose constructor accepts a plain Enemy (others cast to their own class and would break).
        // Extracted from the game DLL. A few rows had a computed argument, so Install() sanity-checks ranges.
        internal static readonly SkillDef[] Skills =
        {
            new SkillDef("Flame", "Knight", "FlameKnight+FlameKnightFlameShieldState", "FlameKnightFlameShield", 0, 7, 0f, 10f, 10f, true),
            new SkillDef("Flame", "Knight", "FlameKnight+FlameKnightCombustionWaveState", "FlameKnightCombustionWave", 1, 6, 0f, 5f, 6f, true),
            new SkillDef("Flame", "Knight", "FlameKnight+FlameKnightFireWallState", "FlameKnightFireWall", 2, 5, 0f, 4.5f, 8f, true),
            new SkillDef("Flame", "Mage", "FlameMage+FlameMageIgnitionOrbState", "FlameMageIgnitionOrb", 1, 2, 2f, 6f, 7f, true),
            new SkillDef("Flame", "Rogue", "FlameRogue+FlameRogueFireShurikenState", "FlameRogueFireShuriken", 0, 6, 3f, 8f, 7f, true),
            new SkillDef("Flame", "Rogue", "FlameRogue+FlameRogueFlameRushState", "FlameRogueFlameRush", 1, 5, 0f, 6f, 8f, true),
            new SkillDef("Flame", "Rogue", "FlameRogue+FlameRogueFireBlastState", "FlameRogueFireBlast", 2, 7, 0f, 5f, 8f, true),
            new SkillDef("Frost", "Knight", "FrostKnight+FrostKnightIceFieldState", "FrostKnightIceField", 2, 5, 0f, 6f, 8f, true),
            new SkillDef("Frost", "Mage", "FrostMage+FrostMageFrostBlast", "FrostMageFrostBlast", 1, 1, 0f, 5f, 6f, true),
            new SkillDef("Frost", "Rogue", "FrostRogue+FrostRogueIceDecoyState", "FrostRogueIceDecoy", 1, 7, 0f, 4f, 7f, true),
            new SkillDef("Frost", "Rogue", "FrostRogue+FrostRogueIceBombState", "FrostRogueIceBomb", 2, 6, 0f, 3.25f, 8f, true),
            new SkillDef("Frost", "Archer", "FrostArcher+FrostArcherIceTrapState", "FrostArcherIceTrap", 1, 2, 0f, 7f, 8f, true),
            new SkillDef("Frost", "Lancer", "FrostLancer+FrostLancerIceBeamState", "FrostLancerIceBeam", 1, 5, 0f, 6.5f, 7f, true),
            new SkillDef("Frost", "Lancer", "FrostLancer+FrostLancerIceLanceState", "FrostLancerIceLance", 2, 2, 0f, 9f, 8f, true),
            new SkillDef("Earth", "Knight", "EarthKnight+EarthKnightExplosionShieldState", "EarthKnightExplosionShield", 1, 7, 0f, 2.75f, 6f, true),
            new SkillDef("Earth", "Knight", "EarthKnight+EarthKnightPoisonBolasState", "EarthKnightPoisonBolas", 2, 5, 3f, 8f, 8f, true),
            new SkillDef("Earth", "Mage", "EarthMage+EarthMageThrowBoulderState", "EarthMageThrowBoulder", 1, 5, 3f, 9f, 6f, true),
            new SkillDef("Earth", "Mage", "EarthMage+EarthMageEarthWaveState", "EarthMageEarthWave", 2, 6, 3f, 9f, 7f, true),
            new SkillDef("Earth", "Rogue", "EarthRogue+EarthRogueBurrowAttackState", "EarthRogueBurrowAttack", 0, 6, 0f, 7f, 7f, true),
            new SkillDef("Earth", "Rogue", "EarthRogue+EarthRogueEarthCaltropState", "EarthRogueEarthCaltrop", 1, 7, 3f, 8f, 8f, true),
            new SkillDef("Earth", "Rogue", "EarthRogue+EarthRogueEarthDashState", "EarthRogueEarthDash", 2, 5, 0f, 5f, 7f, true),
            new SkillDef("Earth", "Archer", "EarthArcher+EarthArcherPoisonBombState", "EarthArcherPoisonBomb", 1, 5, 0f, 2f, 7f, true),
            new SkillDef("Earth", "Archer", "EarthArcher+EarthArcherVineWaveState", "EarthArcherVineWave", 1, 2, 0f, 7f, 8f, true),
            new SkillDef("Earth", "Lancer", "EarthLancer+EarthLancerDrillChargeState", "EarthLancerDrillCharge", 1, 5, 1f, 5f, 7f, true),
            new SkillDef("Earth", "Lancer", "EarthLancer+EarthLancerTremorLanceState", "EarthLancerTremorLance", 2, 1, 0f, 5f, 8f, true),
            new SkillDef("Wind", "Mage", "WindMage+WindMageSummonTwister", "WindMageSummonTwister", 1, 5, 0f, 9f, 7f, true),
            new SkillDef("Wind", "Mage", "WindMage+WindMageSummonTornadoState", "WindMageSummonTornado", 2, 5, 0f, 4.25f, 8f, true),
            new SkillDef("Wind", "Rogue", "WindRogue+WindRogueWindShurikenState", "WindRogueWindShuriken", 0, 5, 3f, 8f, 7f, true),
            new SkillDef("Wind", "Rogue", "WindRogue+WindRogueSlicingBarrageState", "WindRogueSlicingBarrage", 1, 5, 0f, 5f, 8f, true),
            new SkillDef("Wind", "Rogue", "WindRogue+WindRogueBlastDashState", "WindRogueBlastDash", 2, 5, 0f, 2.25f, 7.5f, true),
            new SkillDef("Wind", "Archer", "WindArcher+WindArcherWindTrapState", "WindArcherWindTrap", 1, 1, 3f, 5f, 10f, true),
            new SkillDef("Wind", "Lancer", "WindLancer+WindLancerWindLanceState", "WindLancerWindLance", 0, 5, 0f, 1.5f, 7f, true),
            new SkillDef("Wind", "Lancer", "WindLancer+WindLancerWindFlurryState", "WindLancerWindFlurry", 1, 7, 0f, 8f, 8f, true),
            new SkillDef("Thunder", "Mage", "ThunderMage+ThunderMageThunderWaveState", "ThunderMageThunderWave", 1, 1, 0f, 5f, 7.5f, true),
            new SkillDef("Thunder", "Mage", "ThunderMage+ThunderMageThunderBlastState", "ThunderMageThunderBlast", 2, 2, 0f, 9f, 8f, true),
            new SkillDef("Thunder", "Rogue", "ThunderRogue+ThunderRogueTeleStrikeState", "ThunderRogueTeleStrike", 1, 10, 0f, 5.5f, 7.5f, true),
            new SkillDef("Thunder", "Rogue", "ThunderRogue+ThunderRogueShockBombState", "ThunderRogueShockBomb", 2, 10, 0f, 7f, 8f, true),
            new SkillDef("Thunder", "Lancer", "ThunderLancer+ThunderLancerPiercingShockState", "ThunderLancerPiercingShock", 1, 5, 0f, 7f, 8f, true),
            new SkillDef("Thunder", "Lancer", "ThunderLancer+ThunderLancerGroundShockState", "ThunderLancerGroundShock", 2, 2, 0f, 5f, 7.5f, true),
        };

        // Giants with no elemental enemy to borrow from cast player arcana instead (see EnemyArcana.cs).
        internal static bool UsesArcana(Enemy e) { return e.GetType().Name == "SuperCoffin"; }

        // Giants whose sprite sheet has no elemental palette rows (they render pink): tint + outline instead.
        internal static bool NoElementPalettes(Enemy e) { return e.GetType().Name == "SuperCoffin"; }

        internal static string ElementColorName(string prefix)
        {
            switch (prefix)
            {
                case "Flame": return "Fire";
                case "Frost": return "Water";
                case "Wind": return "Wind";
                case "Earth": return "Earth";
                case "Chaos": return "Chaos";
                case "Radiant": return "Radiant";
                case "Prism": return "Prismatic";
                default: return "Thunder";
            }
        }

        internal static Color ElementColor(string prefix)
        {
            switch (prefix)
            {
                case "Flame": return new Color(1f, 0.45f, 0.1f);
                case "Frost": return new Color(0.25f, 0.6f, 1f);
                case "Earth": return new Color(0.55f, 0.85f, 0.25f);
                case "Wind": return new Color(0.6f, 1f, 0.85f);
                case "Chaos": return new Color(0.75f, 0.35f, 1f);
                case "Radiant": return new Color(1f, 0.9f, 0.55f);
                case "Prism": return new Color(1f, 0.55f, 0.9f);
                case "Neutral": return new Color(0.85f, 0.85f, 0.85f);
                default: return new Color(1f, 0.9f, 0.2f);
            }
        }

        static readonly HashSet<int> Done = new HashSet<int>();

        internal static bool IsGiant(Enemy e)
        {
            return e != null && GiantKind.ContainsKey(e.GetType().Name);
        }

        // Map a prefix (Flame/Frost/...) to an ElementType present in this enemy's elementToPalette table.
        internal static bool TryGetElement(Enemy e, string prefix, out ElementType element)
        {
            string[] names;
            switch (prefix)
            {
                case "Flame": names = new[] { "Fire" }; break;
                case "Frost": names = new[] { "Water", "Ice" }; break;
                case "Earth": names = new[] { "Earth" }; break;
                case "Wind": names = new[] { "Air" }; break;
                default: names = new[] { "Lightning" }; break;
            }
            IDictionary table = e.elementToPalette as IDictionary;
            foreach (string n in names)
            {
                ElementType el = (ElementType)Enum.Parse(typeof(ElementType), n);
                if (table == null || table.Contains(el)) { element = el; return true; }
            }
            element = ElementType.Neutral;
            return false;
        }

        // Level.element is set by NextLevelLoader.SetLevelInfo from the stage name:
        // Forest/Earth -> Earth, Ice -> Water, Fire -> Fire, Air -> Air, Lightning -> Lightning, anything else -> Neutral.
        internal static string StagePrefix()
        {
            ElementType stage = Level.element;
            foreach (string p in Prefixes)
                if (PrefixMatches(p, stage)) return p;
            return null;
        }

        internal static string RandomPrefix()
        {
            return Prefixes[UnityEngine.Random.Range(0, Prefixes.Length)];
        }

        static bool PrefixMatches(string prefix, ElementType el)
        {
            switch (prefix)
            {
                case "Flame": return el == ElementType.Fire;
                case "Frost": return el == ElementType.Water || el == ElementType.Ice;
                case "Earth": return el == ElementType.Earth;
                case "Wind": return el == ElementType.Air;
                default: return el == ElementType.Lightning;
            }
        }

        // Called after the giant's own Start has finished (MiniBoss.Start forces the gray mini-boss palette,
        // and SuperLancer/SuperCoffin re-apply it to their extra sprites afterwards).
        internal static void TryMakeElemental(Enemy e)
        {
            if (!IsGiant(e) || !Plugin.ElementalGiants.Value) return;
            if (!Done.Add(e.GetInstanceID())) return;
            GiantWatchdog.Attach(e); // a giant stuck in one action (e.g. a charge) goes back to Chase

            // On a Chaos stage giants become Chaos giants instead of taking the stage element.
            if (ChaosStages.Planned) { ChaosGiants.Apply(e); return; }

            string stage = StagePrefix();
            if (stage != null && Plugin.MatchStageElement.Value)
            {
                Apply(e, stage);
                return;
            }
            if (UnityEngine.Random.value < Plugin.ElementalGiantChance.Value)
            {
                Apply(e, RandomPrefix());
                return;
            }
            Plugin.Log.LogInfo("GIANT " + e.GetType().Name + " stays neutral (stage " + Level.element + ", chance roll)");
        }

        internal static void Apply(Enemy e, string prefix)
        {
            ElementType element;
            if (!TryGetElement(e, prefix, out element))
            {
                Plugin.Log.LogWarning("GIANT " + e.GetType().Name + ": no palette for " + prefix + ", skipped");
                return;
            }

            e.element = element;
            int palette;
            if (NoElementPalettes(e)) { Tint(e, ElementColor(prefix)); palette = -1; }
            else palette = SetPalette(e, element);

            int added = 0;
            string kind = GiantKind[e.GetType().Name];
            if (!string.IsNullOrEmpty(kind))
            {
                foreach (SkillDef d in Skills)
                {
                    if (d.Element != prefix || d.Kind != kind) continue;
                    if (Install(e, d)) added++;
                }
            }

            int arcana = 0;
            if (UsesArcana(e) || Plugin.ArcanaForAllGiants.Value)
            {
                arcana = EnemyArcana.Give(e, Rivals.PickVaried(ArcanaCatalog.Pool(prefix), 3, false));
                List<ArcanaDef> dashes = ArcanaCatalog.DashPool(prefix);
                if (dashes.Count > 0) DashArcana.Give(e, dashes[UnityEngine.Random.Range(0, dashes.Count)]);
                // No signature meter: signatures are for the player, rivals and the Council (they have their own).
                // Giants can still roll enhanced arcana (EnemyArcana.EnhancedChance).
            }
            else if (added < Plugin.GiantMinElementMoves.Value)
            {
                // Gap filler: some giants have (almost) nothing to borrow (no Wind/Thunder Knight or Fire/Thunder Archer
                // enemy exists). Top them up with player arcana of their element that suit how they fight.
                List<ArcanaDef> fill = GapArcana(kind, prefix, Plugin.GiantMinElementMoves.Value - added);
                arcana = EnemyArcana.Give(e, fill);
            }

            if (Plugin.GiantIntros.Value) GiantIntro.Attach(e, prefix, kind);

            string minions = SwapSummonMinions(e, prefix);
            Plugin.Log.LogInfo("GIANT " + e.GetType().Name + " -> " + prefix + " (" + element + "), stage " + Level.element
                + " (" + Level.tileSetName + "), palette " + (palette < 0 ? "tint" : palette.ToString())
                + ", skills added " + added + ", arcana " + arcana + ", summons " + minions);
        }

        // Arcana families that fit each giant's fighting style, best first. Melee basics, dashes and summons stay out:
        // giants already have their own swings/charges, and summoning giants already summon.
        static readonly Dictionary<string, ArcanaFamily[]> GapFamilies = new Dictionary<string, ArcanaFamily[]>
        {
            { "Knight", new[] { ArcanaFamily.Spell, ArcanaFamily.Jump, ArcanaFamily.Ward } },     // close-range blasts, slams
            { "Archer", new[] { ArcanaFamily.Projectile, ArcanaFamily.Spell } },                 // shots from range
            { "Mage",   new[] { ArcanaFamily.Projectile, ArcanaFamily.Spell } },
            { "Rogue",  new[] { ArcanaFamily.Projectile, ArcanaFamily.Jump, ArcanaFamily.Spell } },
            { "Lancer", new[] { ArcanaFamily.Spell, ArcanaFamily.Projectile } },
        };

        internal static List<ArcanaDef> GapArcana(string kind, string prefix, int n)
        {
            List<ArcanaDef> picked = new List<ArcanaDef>();
            if (n <= 0) return picked;
            ArcanaFamily[] fams;
            if (!GapFamilies.TryGetValue(kind ?? "", out fams)) fams = new[] { ArcanaFamily.Spell, ArcanaFamily.Projectile };
            List<ArcanaDef> pool = ArcanaCatalog.Pool(prefix);
            pool.RemoveAll(d => EnemyArcana.Broken.Contains(d.SkillID));
            // Preferred families first, one arcana per family before repeating one, then anything left of the element.
            for (int round = 0; round < 2 && picked.Count < n; round++)
                foreach (ArcanaFamily f in fams)
                {
                    if (picked.Count >= n) break;
                    List<ArcanaDef> opts = pool.FindAll(d => d.Family == f && !picked.Contains(d));
                    if (opts.Count > 0) picked.Add(opts[UnityEngine.Random.Range(0, opts.Count)]);
                }
            if (picked.Count < n)
                foreach (ArcanaDef d in Rivals.PickVaried(pool.FindAll(d => d.Family != ArcanaFamily.Melee && d.Family != ArcanaFamily.Minion
                    && d.Family != ArcanaFamily.Seeker && !picked.Contains(d)), n - picked.Count, false)) picked.Add(d);
            return picked;
        }

        // Keep the gray mini-boss palette and tint the sprites (and outline) in the element's colour.
        internal static void Tint(Enemy e, Color c)
        {
            Color tint = Color.Lerp(Color.white, c, 0.55f);
            foreach (SpriteRenderer sr in e.GetComponentsInChildren<SpriteRenderer>(true))
            {
                Material m = sr.sharedMaterial;
                if (m == null || !m.HasProperty("_PaletteIndex")) continue; // only the enemy's own sprites, not effects
                sr.color = tint;
                Material mine = sr.material;
                if (mine.HasProperty("_Color")) mine.SetColor("_Color", tint);
                if (mine.HasProperty("_OutlineColor")) mine.SetColor("_OutlineColor", c);
                if (mine.HasProperty("_OutlineColor2")) mine.SetColor("_OutlineColor2", c);
            }
        }

        static readonly AccessTools.FieldRef<Enemy, int> PaletteIndexRef = AccessTools.FieldRefAccess<Enemy, int>("paletteIndex");

        static int SetPalette(Enemy e, ElementType element)
        {
            int idx = -1;
            IDictionary table = e.elementToPalette as IDictionary;
            if (table != null && table.Contains(element)) idx = (int)table[element];
            if (idx < 0) return idx;
            SetPaletteIndexAll(e, idx);
            return idx;
        }

        // Body plus extra parts: SuperArcher arms/bow, SuperLancer lance/attack sprite, SuperCoffin coffin.
        internal static void SetPaletteIndexAll(Enemy e, int idx)
        {
            foreach (SpriteRenderer sr in e.GetComponentsInChildren<SpriteRenderer>(true))
            {
                Material m = sr.sharedMaterial;
                if (m == null || !m.HasProperty("_PaletteIndex")) continue;
                sr.material.SetFloat("_PaletteIndex", idx);
            }
            PaletteIndexRef(e) = idx;
        }

        // Giants' "Summon" skill calls regular minions (SuperKnight -> "Knight"). Swap to the elemental prefab
        // ("WindKnight") when one exists. Coffins summon "Coffin", which has no elemental version, so it stays.
        static string SwapSummonMinions(Enemy e, string prefix)
        {
            if (e.availableEnemySkillsDict == null || !e.availableEnemySkillsDict.ContainsKey("Summon")) return "nothing";
            Traverse summon = Traverse.Create(e.availableEnemySkillsDict["Summon"]).Field("enemyPrefabKey");
            string key = summon.GetValue<string>();
            if (string.IsNullOrEmpty(key)) return "unknown";

            // Strip an element we added earlier (F10 rerolls), then add the new one.
            string baseKey = key;
            foreach (string p in Prefixes)
                if (baseKey.StartsWith(p) && baseKey.Length > p.Length) { baseKey = baseKey.Substring(p.Length); break; }

            string wanted = prefix + (baseKey == "SkeletonMage" ? "Mage" : baseKey); // FlameMage etc. drop "Skeleton"
            IDictionary paths = Traverse.Create(typeof(Enemy)).Field("enemyPrefabPaths").GetValue() as IDictionary;
            if (paths == null) paths = Traverse.Create(typeof(Enemy)).Property("enemyPrefabPaths").GetValue() as IDictionary;
            if (paths != null && paths.Contains(wanted))
            {
                summon.SetValue(wanted);
                return wanted;
            }
            return key + " (no " + wanted + " prefab)";
        }

        static readonly HashSet<string> warnedMissing = new HashSet<string>();

        static bool Install(Enemy e, SkillDef d)
        {
            try
            {
                if (e.availableEnemySkillsDict != null && e.availableEnemySkillsDict.ContainsKey(d.Name)) return false;

                Type t = typeof(Enemy).Assembly.GetType(d.StateType);
                if (t == null)
                {
                    if (warnedMissing.Add(d.StateType)) Plugin.Log.LogWarning("Skill type not found: " + d.StateType);
                    return false;
                }

                int req = Plugin.IgnoreSkillDifficulty.Value ? 0 : d.Req;
                float min = Mathf.Max(0f, d.Min);
                float max = d.Max > min ? d.Max : Mathf.Max(min + 4f, e.attackRadius);
                int prio = Mathf.Clamp(d.Prio, 0, 10);
                float cd = d.Cd > 0f ? d.Cd : 7f;

                object state = null;
                foreach (ConstructorInfo c in t.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    ParameterInfo[] ps = c.GetParameters();
                    if (ps.Length < 8 || ps[2].ParameterType != typeof(Enemy)) continue;
                    object[] args = ps.Length >= 9
                        ? new object[] { d.Name, e.fsm, e, req, prio, min, max, cd, d.Auto }
                        : new object[] { d.Name, e.fsm, e, req, prio, min, max, cd };
                    state = c.Invoke(args); // SkillState's constructor registers itself in availableEnemySkillsDict
                    break;
                }
                if (state == null)
                {
                    Plugin.Log.LogWarning("No usable constructor on " + d.StateType);
                    return false;
                }

                if (d.CastAnim != null) Traverse.Create(state).Field("castingAnimName").SetValue(d.CastAnim);
                e.fsm.AddState((IState)state);
                return true;
            }
            catch (Exception ex)
            {
                Exception inner = ex.InnerException ?? ex;
                Plugin.Log.LogWarning("Failed to add " + d.Name + " to " + e.GetType().Name + ": " + inner.GetType().Name + " " + inner.Message);
                return false;
            }
        }
    }

    // Patches MiniBoss.Start and every giant's own Start override. Only the most-derived Start applies the
    // element, so it runs after SuperLancer/SuperCoffin have re-applied the gray palette to their extra sprites.
    [HarmonyPatch]
    static class GiantStartPatch
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.DeclaredMethod(typeof(MiniBoss), "Start");
            foreach (string name in ElementalGiants.GiantKind.Keys)
            {
                Type t = AccessTools.TypeByName(name);
                MethodInfo m = t == null ? null : AccessTools.DeclaredMethod(t, "Start");
                if (m != null) yield return m;
            }
        }

        static void Postfix(MiniBoss __instance, MethodBase __originalMethod)
        {
            try
            {
                MethodInfo mostDerived = null;
                for (Type t = __instance.GetType(); t != null && mostDerived == null; t = t.BaseType)
                    mostDerived = AccessTools.DeclaredMethod(t, "Start");
                if (mostDerived == null || mostDerived.DeclaringType != __originalMethod.DeclaringType) return;
                ElementalGiants.TryMakeElemental(__instance);
            }
            catch (Exception ex) { Plugin.Log.LogError("Elemental giant patch failed: " + ex); }
        }
    }

    // ------------------------------------------------------------------
    // Debug hotkeys
    // ------------------------------------------------------------------

    internal static class Debug
    {
        static ManualLogSource Log { get { return Plugin.Log; } }

        static Player GetPlayer() { return UnityEngine.Object.FindObjectOfType<Player>(); }

        static Enemy GetNearestEnemy(Func<Enemy, bool> filter)
        {
            Player p = GetPlayer();
            if (p == null) return null;
            Enemy best = null;
            float bestDist = float.MaxValue;
            foreach (Enemy e in UnityEngine.Object.FindObjectsOfType<Enemy>())
            {
                if (filter != null && !filter(e)) continue;
                float d = Vector2.Distance(p.transform.position, e.transform.position);
                if (d < bestDist) { bestDist = d; best = e; }
            }
            return best;
        }

        static string Describe(object v)
        {
            if (v == null) return "null";
            Type t = v.GetType();
            if (t.IsPrimitive || t.IsEnum || v is string) return v.ToString();
            UnityEngine.Object uo = v as UnityEngine.Object;
            if (uo != null) return t.Name + " '" + uo.name + "'";
            Array arr = v as Array;
            if (arr != null) return t.Name + " length " + arr.Length;
            ICollection col = v as ICollection;
            if (col != null) return t.Name + " count " + col.Count;
            return t.Name;
        }

        static string PaletteInfo(Enemy e)
        {
            SpriteRenderer sr = e.spriteRenderer;
            if (sr == null) return "no spriteRenderer";
            Material m = sr.sharedMaterial;
            if (m == null) return "no material";
            string idx = m.HasProperty("_PaletteIndex") ? m.GetFloat("_PaletteIndex").ToString() : "-";
            string cnt = m.HasProperty("_PaletteCount") ? m.GetFloat("_PaletteCount").ToString() : "-";
            return m.shader.name + " palette " + idx + "/" + cnt;
        }

        // F5: live enemies grouped by class/element/palette, plus skills for giants.
        internal static void ListEnemies()
        {
            Dictionary<string, int> groups = new Dictionary<string, int>();
            Enemy[] all = UnityEngine.Object.FindObjectsOfType<Enemy>();
            foreach (Enemy e in all)
            {
                string key = e.GetType().Name + " | " + e.element + " | " + PaletteInfo(e);
                int n; groups.TryGetValue(key, out n); groups[key] = n + 1;
                if (ElementalGiants.IsGiant(e) && e.availableEnemySkillsDict != null)
                    Log.LogInfo("GIANTSKILLS " + e.GetType().Name + " (" + e.element + "): " + string.Join(", ", new List<string>(e.availableEnemySkillsDict.Keys).ToArray()));
            }
            Log.LogInfo("ENEMIES found: " + all.Length);
            foreach (KeyValuePair<string, int> kv in groups) Log.LogInfo("ENEMY x" + kv.Value + " " + kv.Key);
        }

        // F6: step nearest enemy's palette.
        internal static void CyclePalette()
        {
            Enemy e = GetNearestEnemy(null);
            if (e == null) { Log.LogWarning("No enemy found"); return; }
            SpriteRenderer sr = e.spriteRenderer;
            if (sr == null) { Log.LogWarning("Enemy has no spriteRenderer"); return; }
            Material m = sr.material;
            if (!m.HasProperty("_PaletteIndex") || !m.HasProperty("_PaletteCount"))
            {
                Log.LogWarning(e.gameObject.name + " has no palette properties (shader " + m.shader.name + ")");
                return;
            }
            float count = m.GetFloat("_PaletteCount");
            float next = (m.GetFloat("_PaletteIndex") + 1f) % count;
            m.SetFloat("_PaletteIndex", next);
            Log.LogInfo("PALETTE " + e.gameObject.name + " (" + e.GetType().Name + ", " + e.element + ") -> " + next + " of " + count);
        }

        // F7: all fields of nearest enemy's class chain.
        internal static void DumpNearestEnemy()
        {
            Enemy e = GetNearestEnemy(null);
            if (e == null) { Log.LogWarning("No enemy found"); return; }
            BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            Log.LogInfo("DUMP of " + e.gameObject.name);
            for (Type t = e.GetType(); t != null && t != typeof(MonoBehaviour); t = t.BaseType)
            {
                Log.LogInfo("=== fields of " + t.FullName + " ===");
                foreach (FieldInfo f in t.GetFields(all))
                {
                    string value;
                    try { value = Describe(f.GetValue(e)); }
                    catch (Exception ex) { value = "<error " + ex.GetType().Name + ">"; }
                    Log.LogInfo("  " + f.FieldType.Name + " " + f.Name + " = " + value);
                }
            }
        }

        // F8: spawn the next giant from the list next to the player.
        static readonly string[] TestKeys = { "SuperKnight", "SuperMage", "SuperRogue", "SuperArcher", "SuperLancer", "SuperSummoner", "SuperCoffin" };
        static int testIndex;

        internal static void SpawnNextTestGiant()
        {
            Player p = GetPlayer();
            if (p == null) { Log.LogWarning("No player found"); return; }
            string key = TestKeys[testIndex % TestKeys.Length];
            testIndex++;
            Vector3 pos = p.transform.position + new Vector3(4f, 0f, 0f);
            try
            {
                Enemy e = Enemy.Spawn(key, pos, null, false, true, false, false, false);
                Log.LogInfo("SPAWN " + key + " -> " + (e == null ? "null" : e.GetType().Name));
            }
            catch (Exception ex) { Log.LogError("SPAWN " + key + " failed: " + ex); }
        }

        // F9: element -> palette index table.
        // Ctrl+F9 (v0.6.4, infusion research): what each castable arcana's prefab is made of, to learn how vanilla
        // sells an element's look: sprites (and their shaders), animators, particle systems (and start colours),
        // trails, lines, lights. One line per arcana plus a per-element summary in the log.
        // Ctrl+Shift+F12: straight to the final fight (Sura, or this run's champion), as if the three Council floors
        // were done (NextLevelLoader loads "FinalBossLevel" once tierCount reaches 3).
        internal static void JumpToFinal()
        {
            string scene = Application.loadedLevelName;
            if (scene == "Hub" || scene == "Tutorial" || scene == "TitleScreen")
            {
                Plugin.Log.LogWarning("FINAL jump: start a run first (scene " + scene + ")");
                return;
            }
            FinalRun.Tick();
            GameController.tierCount = 3;
            GameController.stageCount = 0;
            Plugin.Log.LogInfo("FINAL jump: " + scene + " -> FinalBossLevel (" + (FinalRun.ChampionTitle ?? "Sura") + ")");
            GameController.LoadLevel("FinalBossLevel");
        }

        internal static void DumpArcanaVisuals()
        {
            Dictionary<string, int[]> perEl = new Dictionary<string, int[]>(); // arcana, with particles, with lights, with trails
            int n = 0;
            List<ArcanaDef> defs = ArcanaCatalog.Pool("Chaos");
            defs.AddRange(ArcanaCatalog.DashPool("Chaos"));
            foreach (ArcanaDef d in defs)
            {
                if (d.Port != ArcanosPort.None) continue;
                GameObject prefab = null;
                try { prefab = EnemyArcana.GetPrefab(d); } catch (Exception) { }
                if (prefab == null) continue;
                SpriteRenderer[] srs = prefab.GetComponentsInChildren<SpriteRenderer>(true);
                ParticleSystem[] pss = prefab.GetComponentsInChildren<ParticleSystem>(true);
                TrailRenderer[] trs = prefab.GetComponentsInChildren<TrailRenderer>(true);
                LineRenderer[] lrs = prefab.GetComponentsInChildren<LineRenderer>(true);
                Light[] ls = prefab.GetComponentsInChildren<Light>(true);
                Animator[] ans = prefab.GetComponentsInChildren<Animator>(true);
                HashSet<string> shaders = new HashSet<string>();
                foreach (SpriteRenderer sr in srs) if (sr.sharedMaterial != null && sr.sharedMaterial.shader != null) shaders.Add(sr.sharedMaterial.shader.name);
                List<string> pcol = new List<string>();
                foreach (ParticleSystem ps in pss)
                {
                    try
                    {
                        ParticleSystem.MainModule mm = ps.main;
                        Color c = mm.startColor.color;
                        ParticleSystemRenderer pr = ps.GetComponent<ParticleSystemRenderer>();
                        string tex = pr != null && pr.sharedMaterial != null && pr.sharedMaterial.mainTexture != null ? pr.sharedMaterial.mainTexture.name : "-";
                        pcol.Add(ps.name + "(" + mm.startColor.mode + " " + ColorUtility.ToHtmlStringRGB(c) + ", tex " + tex + ")");
                    }
                    catch (Exception) { pcol.Add(ps.name); }
                }
                List<string> lcol = new List<string>();
                foreach (Light l in ls) lcol.Add(ColorUtility.ToHtmlStringRGB(l.color));
                Plugin.Log.LogInfo("VISUALS " + d.Element + " " + d.SkillID + ": " + srs.Length + " sprites [" + string.Join(", ", new List<string>(shaders).ToArray()) + "], "
                    + ans.Length + " animators, " + pss.Length + " particles [" + string.Join("; ", pcol.ToArray()) + "], " + trs.Length + " trails, " + lrs.Length + " lines, "
                    + ls.Length + " lights [" + string.Join(", ", lcol.ToArray()) + "]");
                int[] s;
                if (!perEl.TryGetValue(d.Element, out s)) perEl[d.Element] = s = new int[4];
                s[0]++; if (pss.Length > 0) s[1]++; if (ls.Length > 0) s[2]++; if (trs.Length + lrs.Length > 0) s[3]++;
                n++;
            }
            foreach (KeyValuePair<string, int[]> kv in perEl)
                Plugin.Log.LogInfo("VISUALS summary " + kv.Key + ": " + kv.Value[0] + " arcana, " + kv.Value[1] + " with particles, " + kv.Value[2] + " with lights, " + kv.Value[3] + " with trails/lines");
            Plugin.Log.LogInfo("VISUALS dumped " + n + " arcana prefabs");
        }

        internal static void DumpElementPalette()
        {
            Enemy e = GetNearestEnemy(null);
            if (e == null) { Log.LogWarning("No enemy found"); return; }
            IDictionary table = e.elementToPalette as IDictionary;
            if (table == null) { Log.LogWarning("elementToPalette is null on " + e.GetType().Name); return; }
            Log.LogInfo("ELEMENTPALETTE (from " + e.GetType().Name + ", count " + table.Count + ", difficulty " + GameController.CurrentDifficulty + ", floor element " + Level.element + ")");
            foreach (DictionaryEntry de in table) Log.LogInfo("ELEMENTPALETTE " + de.Key + " -> " + de.Value);
        }

        // F12: in the final boss room before the fight -> make it a champion now. Elsewhere -> force the next final
        // room to roll a champion, and spawn a Final-tier champion beside you to try the fight (no ending here).
        internal static void FinalChampionTest()
        {
            FinalBossRoomEventHandler room = UnityEngine.Object.FindObjectOfType<FinalBossRoomEventHandler>();
            if (room != null && room.GetComponent<FinalChampion>() == null)
            {
                FinalChampion.ForceNext = true;
                FinalChampion.Roll(room);
                Log.LogInfo("FINAL test: this final room now holds a champion");
                return;
            }
            FinalRun.ForceChampion(); // this run's final boss is now a champion (the next loading screen shows it)
            Player p = GetPlayer();
            if (p == null) return;
            string prefix = UnityEngine.Random.value < 0.3f ? "Chaos" : ElementalGiants.RandomPrefix();
            Enemy e = Rivals.Spawn(p.transform.position + new Vector3(0f, 5f, 0f), prefix, RivalTier.Final);
            if (e != null) p.StartCoroutine(TestIntro(e, prefix));
            Log.LogInfo("FINAL test: champion spawned here; the next final room will also hold one");
        }

        static IEnumerator TestIntro(Enemy e, string prefix)
        {
            yield return null;
            yield return null;
            string title = prefix == "Chaos" ? "Chaos Sovereign" : ElementalGiants.ElementColorName(prefix) + " Archon";
            yield return e.StartCoroutine(Rivals.Intro(e, title, "Final Champion (test)"));
        }

        // F11: open a rival arena around the player (stage element, or random on neutral stages).
        static int rivalTestIndex;

        // Ctrl+F11: leave this stage as if through its exit portal (NextLevelLoader.LoadNextLevel: next regular
        // stage, the Council floor after a tier's last stage, the next tier after a Council floor).
        internal static void SkipToNextStage()
        {
            // 0.5.14: the exit is inactive until reached, so look through inactive scene objects too.
            NextLevelLoader loader = UnityEngine.Object.FindObjectOfType<NextLevelLoader>();
            if (loader == null)
                foreach (NextLevelLoader l in Resources.FindObjectsOfTypeAll<NextLevelLoader>())
                    if (l != null && l.gameObject.scene.IsValid()) { loader = l; break; }
            if (loader == null)
            {
                // Same choice LoadNextLevel makes for a regular stage: the Council floor after the tier's last stage,
                // else another stage of this tier (the tier scene itself, regenerated).
                string scene = Application.loadedLevelName;
                string next = null;
                if (GameController.stageCount > GameController.stagesPerTier && GameController.bossLevelNameDict != null && GameController.bossLevelNameDict.ContainsKey(scene))
                    next = GameController.bossLevelNameDict[scene];
                else if (GameController.bossLevelNameDict != null && GameController.bossLevelNameDict.ContainsKey(scene))
                    next = scene;
                if (next == null)
                {
                    Plugin.Log.LogWarning("SKIP: no exit on this floor (" + scene + "; a Council floor opens its exit after the fight)");
                    return;
                }
                Plugin.Log.LogInfo("SKIP: no exit object found, loading " + next + " directly (tier " + GameController.tierCount + ", stage " + GameController.stageCount + ")");
                GameController.LoadLevel(next);
                return;
            }
            Plugin.Log.LogInfo("SKIP: leaving " + Application.loadedLevelName + " (tier " + GameController.tierCount + ", stage " + GameController.stageCount + ")");
            AccessTools.Method(typeof(NextLevelLoader), "LoadNextLevel").Invoke(loader, null);
        }

        internal static void OpenRivalArenaHere()
        {
            Player p = GetPlayer();
            if (p == null) { Log.LogWarning("No player found"); return; }
            // Cycles Fire, Water, Earth, Wind, Thunder, Chaos so every pool can be tested on one stage.
            string[] order = ArcanosBridge.Present ? new[] { "Flame", "Frost", "Earth", "Wind", "Thunder", "Chaos", "Radiant", "Prism" } : new[] { "Flame", "Frost", "Earth", "Wind", "Thunder", "Chaos" };
            string prefix = order[rivalTestIndex++ % order.Length];
            RivalEvent.OpenHere(prefix); // the full event: portal beside you -> selector room -> arena -> way back
            Log.LogInfo("RIVAL test event opened (" + prefix + ") at " + p.transform.position);
        }

        // F10: give the nearest giant the next element in order (testing all five quickly).
        static int rerollIndex;

        internal static void RerollNearestGiant()
        {
            Enemy e = GetNearestEnemy(ElementalGiants.IsGiant);
            if (e == null) { Log.LogWarning("No giant found"); return; }
            string prefix = ElementalGiants.Prefixes[rerollIndex % ElementalGiants.Prefixes.Length];
            rerollIndex++;
            ElementalGiants.Apply(e, prefix);
        }
    }
}
