# Wizard of Legend 1 mod: Hybrid Bosses and Elemental Giants

Handoff from a chat session to code mode. Read this first, then `reference/`.

## Goal (the user's words, condensed)
- Make the **giant/large versions of regular enemies** have **elemental variants**. Currently, on normal difficulty they are always gray and non-elemental. The elemental variants should appear **on normal difficulty too**, not only on higher difficulty or in elemental zones. Variety is the point.
- Add **rival-wizard bosses** that reuse the player's assets and arcana, the way the final boss does. Sprinkle them through a run as events, and maybe add new final bosses.
- Everything uses assets already in the game ("clever re-use", asset flipping). The mod ships code only, no copied assets.
- User is new to modding and wants either working code or very specific steps. Casual, direct tone; push back honestly.

## Environment (confirmed)
- Windows. Game: Steam, `C:\Program Files (x86)\Steam\steamapps\common\Wizard of Legend` (not yet confirmed by a successful build, but the decompile header showed `...\Wizard of Legend\WizardOfLegend_Data\Managed\Assembly-CSharp.dll`).
- Mod manager: **r2modman**. BepInEx profile: `C:\Users\migne\AppData\Roaming\r2modmanPlus-local\WizardOfLegend\profiles\Default`. Log: `<profile>\BepInEx\LogOutput.log`.
- BepInEx 5.4.21, Unity 5.6.7, Mono CLR 2.0, so the plugin targets **net35**. HarmonyX is available (`0Harmony.dll` in `BepInEx\core`). LegendAPI 2.2.1 is installed (items, outfits, skills/arcana, elements, music; **nothing for enemies or bosses**).
- Project: git repo `Desktop\WOLHybridBosses\WOL-Hybrid_Bosses\` (GitHub `3dmindscapper/WOL-Hybrid_Bosses`) with `WolHybridBosses.csproj`, `Plugin.cs`, `ROADMAP.md`. `dotnet build` there copies the DLL into the profile's `BepInEx\plugins`. Builds confirmed working. The parent `Desktop\WOLHybridBosses\` only holds old bin/obj. v0.0.6 compiled cleanly; not yet run in game.
- `ROADMAP.md` is the user's feature plan (elemental mini-bosses, rival-wizard portal events, chaos stages, Arcanos/Radiance compat, final-boss pool, modded arcana). Keep it updated.
- Useful log filter (PowerShell): `Get-Content "$env:APPDATA\r2modmanPlus-local\WizardOfLegend\profiles\Default\BepInEx\LogOutput.log" | Select-String "Hybrid Bosses" | Out-File "$HOME\Desktop\hb.txt"` (output is UTF-16; convert before reading).

## Findings so far

### Player-clone approach is a dead end (for now)
Cloning the player GameObject and driving it from script failed:
- `Player.Start()` claims the HUD (`GameUI.ClaimLowerHUD` -> `StockSkillUI.RefreshUI` NRE).
- After spawning a clone, **the real player's** `TransitionToSkill` threw NREs (arcana charged but did nothing); `lowerHUD` and `inputDevice` were null on the real player. Cause not fully pinned down.
- Player arcana are nested `Player.SkillState` classes (204 of them, `Player+UseFireBeam` etc.) held in `skillIDs`, `skillsDict`, `skillStates`, `assignedSkills[6]`, fired via `Player.TransitionToSkill(int skillNo, bool checkButtonHeld)`. They are tightly coupled to player input and HUD.
- Decision: **build bosses on the enemy side** instead.

### The game already has the hybrid-boss pattern (from the decompile)
- Class chain: `Entity` -> `Enemy` -> `Boss` / `MiniBoss`. Elemental bosses: `FireBoss`, `IceBoss`, `EarthBoss`, `AirBoss`, `LightningBoss`; `FinalBoss : Boss`.
- `FinalBoss` defines states that **subclass other bosses' attack states**, e.g. `FinalBossFireBossState : FireBoss.FireBossDragonLineState`, `FinalBossWaterBossState : IceBoss.IceBossAquaBeamState`, `FinalBossEarthBossState : EarthBoss.EarthBossRockThrowState`, `FinalBossAirBossState : AirBoss.AirBossGaleBlastState`, plus a custom lightning state. They override timing (`StdWindupTime`, `StdExitTime`) and animation names (`animOption0 = "Forehand"`, `slideAnimName = "SlideRight"`) so the borrowed attacks fit the final boss's animator.
- `FinalBoss.InitFSM()` registers all states with `fsm.AddState(...)`, and builds dictionaries: `skillRangeDict` (state -> range), `skillEleDict` (state -> `ElementType`), `bossSkillDict`. Before each attack it calls `EmitElement(skillEleDict[name])` for an element-coloured particle burst. `TransitionToNextState()` picks the next attack name from `standardPatternList`/`superPatternList` and routes through a dash state.
- `Boss`: `attackPatternList` (List<List<BossSkillState>>), `attackPatternIndex/Index`, `attackIndexCurrentMax`, `TransitionToNextState()` does `fsm.ChangeState(attackPatternList[i][j].name, allowSelfTransition: true)`. HP is set in `InitComponents` via a `NumVarStatMod("BossHP", 1500 + 125 * difficulty, ...)`. Uses `EnemyHealthBar`, `bossDisplayName`.
- `MiniBoss : Enemy` (HP 750 + 75*difficulty) sets `_PaletteIndex = miniBossPaletteIndex (3)` on its sprite material.
- Risk: borrowed attacks need each source boss's animations, so hybrids will need animation-name adaptation like `FinalBoss` does.

### Enemy recolouring is a shader palette (confirmed in-game)
- Regular enemies use shader `Chaos/Palette Swap` with `_PaletteIndex` / `_PaletteCount` = **9**. Pressing F6 cycled Archer and Lancer through 0..8: the user saw their enhanced palette and the elemental palettes.
- `Enemy` has `Dictionary elementToPalette` (6 entries) and `_enemyElementCount = 6`. **F9 (dump element->palette mapping) has not been run yet.** Do this first to map element -> index.
- Some enemies don't use palette swap: Dove and ThunderZoneEnemy use `Chaos/Sprite Diffuse Outline` (no palette); EnemyTurret uses `Sprites/Default`.
- A live `ThunderArcher` (element Lightning) had `paletteIndex 8`, which suggests **index 8 = lightning**. Unconfirmed.

### Enemy prefab keys (74, from `Enemy.enemyPrefabPaths`, spawnable via `Enemy.Spawn(key, ...)`)
- Base: Blob, BlobMini, BlobSpitter, BlobRoller, Ghoul, **GhoulLarge**, Coffin, MovingStatue, EnemyTurret, PoisonPlant, LaserOrb, Cyclops, MimicEnemy, **MimicEnemyLarge**, Spider, SpikeSpider, Archer, Knight, SkeletonMage, Rogue, Lancer, Summoner, Orb*Familiar.
- Elemental sets (Archer/Knight/Mage/Rogue/Lancer/Summoner) for **Frost, Earth, Flame, Wind, Thunder**, plus `*ZoneEnemy` for each.
- **Super\***: SuperKnight, SuperMage, SuperRogue, SuperArcher, SuperLancer, SuperSummoner, SuperCoffin, SuperBlob, SuperMovingStatue. These are probably the "giant" enemies the user means. **Unverified.** Which keys are the gray giants is the first thing to settle (F8 spawns candidates).
- Other: Contestant (PvP), Dove, Rat, Eagle.
- Elemental variants are separate prefabs and classes (e.g. `ThunderArcher`), not a field flip.

### PvP code exists (unread)
`BossRoomEventHandler+<StartPvP>`, `PvpEnemySpawner`, `Contestant`. Possible foundation for rival-wizard encounters. Read before designing rivals.

### Other types seen in the F10 dump (not yet decompiled)
`ZoneEnemy` (+ Flame/Frost/Earth/Wind/Thunder subclasses), `EnemyFactory` (spawns via `Enemy.Spawn(prefabKey, pos, parent, dropLoot, alerted, spawnCard, autoAlert, disableCountDeath)`), `ElementalPrefabSpawner`, `SpawnRandomPrefab`, `MiniBossSummonState`, `EndingRoomEventHandler` (spawns all five element bosses), `BossRoomEventHandler`, `FinalBossRoomEventHandler`.

### Settled by reading the DLL's IL (session 2, no decompiler needed)
- **Giants = `Super*` classes, all `: MiniBoss`** (SuperKnight/Mage/Rogue/Archer/Lancer/Summoner/Coffin). SuperBlob : Blob, SuperMovingStatue : MovingStatue, GhoulLarge : Enemy are not MiniBoss.
- `Enemy.Spawn(string key, Vector3 pos, Transform parent, bool dropLoot, bool alerted, bool spawnCard, bool survivalMode, bool overrideCountDeath)` (static; also an `EName` enum overload).
- Palette: `Enemy.Start -> InitElement -> SetPaletteIndex`: index = `elementToPalette[element]`; Neutral enemies on difficulty > 0 get `Clamp(difficulty, 0, 2)` (enhanced looks). **`MiniBoss.Start` then always overwrites with `miniBossPaletteIndex` (3) — that's why giants are always gray.** `Entity.Start` sets `element = Level.element` if `elementFromTileSet`.
- Elemental regular enemies (e.g. FlameKnight : Knight) differ only in `InitFSM`: different `attackSkillID` plus extra nested `SkillState`s. `Enemy.SkillState` ctor `(name, fsm, ent, reqDifficulty, priority, minRadius, maxRadius, cooldown[, auto])` calls `RegisterSkill` -> `availableEnemySkillsDict`; `HandleExtraSkills` picks by difficulty >= req, range, LOS, IsReady, highest priority. Then `fsm.AddState(IState)`.
- ~40 elemental skills take a plain `Enemy` in their ctor, so they can be transplanted onto giants. Ones typed to their own class (FrostKnight IceShield/IceBash, all WindKnight, ThunderKnight, Archer arrow attacks, FlameLancer etc.) can't. Table is in `Plugin.cs` (`ElementalGiants.Skills`). Some rows had computed args (e.g. `attackRadius * x`), so I hand-fixed min/max and Install() sanity-checks ranges.
- IL tooling (scratch, may be gone): a PowerShell `ReflectionOnlyLoadFrom` + IL opcode walker. Easy to recreate; or install ILSpy.

- Stage element: `NextLevelLoader.SetLevelInfo(name)` sets `Level.element`/`Level.tileSetName`: Forest->Earth, Earth->Earth, Ice->**Water**, Fire->Fire, Air->Air, Lightning->Lightning, PlayerRoom->Neutral (Home), else Neutral (CastleBase). ElementType order: Fire, Water, Air, Lightning, Earth, Neutral, Chaos, Ice, Plant, Poison.
- Giant `Start` overrides: SuperMage/Archer/Lancer/Summoner/Coffin call `MiniBoss.Start` then do more. **SuperLancer resets paletteIndex to 3 and recolours lance/attack sprites; SuperCoffin recolours `coffinRenderer` to 3; SuperArcher copies paletteIndex onto LeftArm/Bow/Arm.** So the element must be applied after the most-derived Start, on all child palette-swap renderers.

## In-game results
- v0.0.6 tested by user: stage matching works (Wind stage: every giant Wind). Element->palette confirmed: Neutral 0, Water 4, Fire 5, Earth 6, Air 7, Lightning 8. No mod errors in the log.
- SuperCoffin moveset: JumpAttack (stomp), BlockBounce (`CreateCoffinBlock`, the "block"), Summon "Coffin", Ready/Hurt/Dead. There's no elemental coffin, so v0.0.7 gives it hand-picked "Coffin" SkillDef rows with `CastAnim = "Ready"`.
- Non-MiniBoss giants (not handled yet): SuperMovingStatue : MovingStatue (Start calls Enemy.Start, no palette code), SuperBlob : Blob (Start forces miniBossPaletteIndex itself), GhoulLarge : Enemy, MimicEnemyLarge : MimicEnemy.

- v0.0.7 tested: summon swap works (logged EarthKnight, FlameLancer etc.). SuperCoffin goes **pink** on palettes 4–8, so its sheet has no elemental rows: keep palette 3 and tint or outline it (MiniBoss uses `_OutlineColor`/`_OutlineColor2`). The user wants the coffin (and rivals) to cast **player arcana** instead.
- Arcana research: `Contestant : Enemy` (PvP wizard) reimplements Tornado/SwordThrow/Fireball as its own states (ctor typed to Contestant). Its FireballState instantiates the prefab and calls `Attack.SetAttackInfo("Player", "ShootFireball", level, isUltimate)`, the same call player arcana make (e.g. `UseFireBeam.CreateFireBeam`: `FireBeam.Prefab` + `ChaosInst` + `SetAttackInfo`). There are 100 `Player+Use*` arcana. Plan: our own data-driven `ArcanaState : Enemy.SkillState` (skillID, prefab, delivery type). See ROADMAP "Enemy Arcana".

## v1.1.0: Nocturne overhaul, White Alchemist + Mantle, Rival League, tournaments, team matches, Bestiary (built, not yet run in game)
- **Progress.cs:** `BepInEx/config/osi.wol.hybridbosses.progress.txt`, key=value lines (`GetInt`/`SetInt`/`AddInt`/`GetBool`/`SetBool`). Keys:
  - `league.points`, `league.<Prefix>.wins` / `.losses`
  - `alchemist.wins`, `mantle.unlocked`, `mantle.on`
- **Nocturne.cs:** the game's flow:
  - `RandomBuildNpc.HandleConditionalInteraction` checks `Player.platWallet` against its `cost` field, withdraws, and adds `GameController.runModifier` "RandomRun".
  - `RandRunMod.Activate` then calls `Player.RandomizeBuild(randOutfit = outfitID != "Pride", randRelic, randArcana)`.
  - `RandomizeBuild` builds the arcana pool with `UpdateRandSkillsDict(ElementType)`: Neutral (5) means all elements, and the Seven Flush relics pass an element. It returns false if the pool can't fill the slots, and then the game rerolls relics and falls back to Neutral.

  Our patches:
  - Interact prefix sets `cost` to 0 (`Nocturne.Free`). The postfix opens `NocturneMenu` when "RandomRun" is active (1.5s debounce).
  - `RandRunMod.Activate` prefix/postfix/finalizer set `NocturneShuffle.Shuffling`, plus `ForcedElement` (preset 3, a random one of the 5 base elements) and `PreferInfused` (preset 4).
  - `RandomizeBuild` prefix (`Priority.First`, ref `__0..__2`): preset 1 = arcana only, preset 2 = no arcana.
  - `UpdateRandSkillsDict` prefix swaps Neutral for the forced element. Its postfix retries with Neutral via reflection (`retrying` guard) when the element can't fill the slots.
  - `InfusionShufflePatch` skips when arcana weren't shuffled (`__2`) and passes `PreferInfused` to `RollShuffled(p, preferInfused)` (picks options[1..]).
  - The menu (Hub; keys / pad like ModifiersMenu) writes `Nocturne.Preset`.
- **Alchemist.cs:**
  - `FinalRun.Alchemist`: `Pick` rolls `FinalBoss.AlchemistChance` (0.25) first, giving prefix "Chaos" with the title `WhiteAlchemist.Name`. `ForceAlchemist()` is the Alt+F12 test.
  - Board: white portrait/ring, ChaosIcon hidden.
  - `FinalChampion.Swap` sets `RivalMarker.Alchemist` before Start. `Grudges.DualKit` chance is 1 for it. The end of `MakeHostile` calls `WhiteAlchemist.Setup`: `RecolorBody(e, "Pure", false)` (new `Pure` ramp in ElementRamps) plus `WhiteAura.Attach`, which is `OverdriveEffects` with `Initialize` and `ToggleAura(true)`.
  - `ChampionPhase`: the Alchemist branch "…, Transmuted" adds 2 arcana and re-runs `DualKit`.
  - `FinalChampion.Finish` calls `WhiteAlchemist.Rewards`:
    - `TeachInfusion` unlocks a random not-yet-unlocked element (not its own) for an equipped non-signature arcana.
    - It counts `alchemist.wins`; the first win sets `mantle.unlocked` and `mantle.on`.
  - **`WhiteMantle`** (on the runner via `EchoRecorder.Ensure`) checks once a second while worn:
    - The player's palette-shader materials get `PaletteTwin(_, "Pure")`, with originals kept and restored.
    - It adds a white aura on the player.
    - Infused equipped skills get `StatManager.GetSkillData("Player", id).ModifyMod(StatData.cdStr, ×0.85 mod, add)`, removed when no longer infused or when the mantle comes off.
    - `Infusion.ApplyToAttack` gives ×1.15 damage when `WhiteMantle.Active`.

    It's toggled from a ModifiersMenu row. No game Outfit is added: a modded wardrobe entry could break save data if the mod were removed.
- **MantleRobe.cs (wardrobe robe via LegendAPI, optional):**
  - Setup: the csproj references `LegendApi.dll` (Private false), and Plugin has a soft `BepInDependency` on `xyz.yekoc.wizardoflegend.LegendAPI`.
  - Registration: `MantleRobe.TryRegister()` runs in Awake (data only; `Outfit` has no static constructor, so it's safe early). When LegendAPI is loaded (`Chainloader.PluginInfos`), `RegisterWithLegendApi` builds `new Outfit("AlchemicalWhiteMantle", 0, [OutfitModStat(LegendAPI.Outfits.CustomModType, …)], false, false)` and calls `LegendAPI.Outfits.Register(OutfitInfo { name "White Mantle", unlockCondition = Progress mantle.unlocked, customMod sets MantleRobe.Wearing, customDesc })`. The method is only JIT-compiled when LegendAPI is present.
  - Effect: when registered, `WhiteMantle.Worn` = robe equipped, the modifiers row is read-only, and the reward card says "a new robe in the wardrobe". Otherwise the old menu toggle applies.
  - Wardrobe preview: colour row 0. The white look comes from our palette recolour while it's worn.
- **League.cs:** named rivals per prefix, with `LeagueRival` (`Wins`/`Losses`/`Record`) and `League.For(prefix)` (config `RivalWizards.League`).
  - Points: Easy 1, Medium 2, Hard 3, Final 4, +1 in a tournament. Ranks Novice/Adept/Duelist/Champion/Grandmaster at 0/6/16/32/55.
  - Rank effects: `RecordWin` gold bonus = rank × 10 × (min(tier, 2) + 1), with a rank-up card. `HardWeightBonus` = rank × 6 is added to `RivalEvent.RollTier`'s Hard weight.
  - Looks and lines: `OutfitFor` is a deterministic robe from `Outfit.OutfitList` by name hash; `MakeHostile` uses it when `mk.League` is set. `Greet` depends on the record.
  - Duel integration: `league` is set for non-Echo, non-Archon rivals (rematches included). The title is "Name, Element Rival[, rematch]" and the subtitle adds the record. A loss is recorded if the player is down while the rival lives; a win after it dies.
  - The Hub shows rank, points, next rank and mantle status under the bounty.
- **RivalEvent modes (`RivalMode` Duel / Tournament / Team):** rolled in `Open` for non-Echo, non-rematch portals (`TournamentChance` 0.12, `TeamMatchChance` 0.12), with their own portal colours and announcements. `OpenModeHere` handles the Alt+F9 / Alt+F11 tests.
  - **`RunTournament`:** 3 rounds of `Challenger` (spawn, `mk.NoChest`, league, intro, greeting, banter), with tiers Down/base/Up and elements base plus 2 others. Each win records a league win (+1) and a bounty report; between rounds the player heals 20% of max over a 5s breather. The prize is `RivalRewards.SpawnChest(gold)` plus a 50% `PrizeArcana` (pre-infused, unlocked, not held).
  - **`RunTeam`:** `SpawnAlly` is a plain "Contestant" with no RivalMarker, so it stays the game's friendly AI. It gets an element palette, `BossEventEnemy` (never elite) and Hard tier HP. Two rivals one tier down follow, each introduced. Every rival patch checks `Rivals.IsRival`, so the ally's spells aren't re-owned. The ally is removed after the win.
- **BossFights.cs Bestiary** (`MinibossFight` is now 11): Golem Gauntlet ("SuperMovingStatue"), Turret Fortress ("EnemyTurret"), Summoner Coven / Lancer Phalanx / Archer Volley (`ElementKey(kind)` = stage prefix + kind, plain on neutral and variant stages; the variant conversion arms them).
  - Shared helpers: `TopUp(list, key, max, …)`, `AnyAlive`, `Charge` (phalanx +40% speed for 4s), and Blinking elites on archers via `Elites.Make`.
- **New config:** `Nocturne.Free`, `Nocturne.Preset`, `FinalBoss.AlchemistChance`, `RivalWizards.League`, `RivalWizards.TournamentChance`, `RivalWizards.TeamMatchChance`. Modifiers menu rows were added for these and for the White Mantle.
- **Test keys:** Alt+F9 tournament, Alt+F11 team match, Alt+F12 Primordial Alchemist final.

## v1.0.0 rename: released as **WOL Alchemical Fusion**
- **What changed:** the Thunderstore package name (`WOL_Alchemical_Fusion`), the BepInPlugin display name ("WOL Alchemical Fusion"; the log source shows it), the startup log lines, the modifiers menu title, the README / ROADMAP / CHANGELOG titles, and the icon ("AF" / "ALCHEMICAL FUSION"). The zip is now `release/WOL_Alchemical_Fusion-1.0.0.zip`.
- **Kept on purpose:** the plugin GUID `osi.wol.hybridbosses` (so the config file, the unlocks file and the Echo file carry over), `WolHybridBosses.dll`, the namespace, the internal `HybridBosses_*` object names, and the GitHub repo name.

## v1.0.0: first public release
- **TestKeys.cs (Alt + F-keys, only with Debug.Hotkeys):**
  - Alt+F5: rematch portal (`RivalEvent.OpenRematchHere`)
  - Alt+F6: bounty step
  - Alt+F7: +500 gold (`Wallet.Deposit`)
  - Alt+F8: infusion test pack (unlock a random version per equipped arcana, then `Infusion.RollShuffled`)
  - Alt+F10: forced Echo portal (`RivalEvent.OpenEchoHere`; records one first if none; `Open(..., forceEcho)`)

  `Plugin.Update` routes Alt to `TestKeys` before the other keys.
- **Release defaults:** `Debug.Hotkeys` now defaults to false (existing cfgs keep their value). Stale config descriptions were refreshed (Infusion, Archon chance, Relics, Rival portal, and the two unused entries marked "No longer used").
- **ROADMAP.md** was rewritten for 1.0.0: a feature summary by area, what to confirm in play, ideas, and the full hotkey table.
- **release/:** `manifest.json` (Thunderstore; depends on `Modding_Council-BepInExPack_of_Legend-5.4.2100`), an original `icon.png` (256×256, drawn with System.Drawing, no game art), a player `README.md` (also copied to the repo root), `CHANGELOG.md`, and `Hybrid_Bosses-1.0.0.zip` (the zip and DLL are gitignored).

## v0.9.21: real relics on rivals where safe; cursed relics
- **Relics work on any entity:** `Entity.InitComponents` gives every entity an `Inventory`. `Inventory.AddItem` sets `Item.parentEntity` and calls `Activate`.
- **But most relics aren't holder-only.** An offline IL scan of all 367 relic classes (their own and base methods) flagged 326 as unsafe: they read `parentPlayer`, call into Player / LootManager / GameController / Wallet / GameUI, touch those classes' statics or Enemy statics, or call `StatManager` (category-wide stat data). The flagged set includes, for example, `DamageUp` → `StatManager.ModifyAllStatData(parentSkillCategory, ...)`. Rivals use the shared enemy category and cast arcana loaded from the "Player" category, so running these on a rival would buff every enemy or the player.
- **Real relics (`RivalLoadout.RealOnRival`):** 29 classes only touch `parentEntity`; 27 of them are used (`HealthUp` / `HealthUpWithDefRelics` are left out because the rival's HP Override mod would cancel them). Those go into the rival's own inventory via `EquipReal(e, mk.RelicIDs)` (`AddItem(id, false, false)`), called at the end of `MakeHostile` after stats and HP. They aren't also translated to stats.
- **Everything else** keeps the stat translation (`RelicEffects`, else name pattern, else category).
- **Cursed relics:** now in the pool (`cursedIds`), with an extra trade-off of ×1.15 damage and ×0.88 HP, on top of their effect.
- **Intro and loadout:** `RivalMarker.RelicIDs` holds every relic it carries (the Echo up to 4, from `UseRelics`, which now returns the list). The intro row lists them all, with "(cursed)" where it applies.

## v0.9.20: rivals use every relic; rival voice lines; no ailment bridge
- **Ailment bridge: not added.** The user's condition was "only if Arcanos always applies them with its arcana". Arcanos' own descriptions show the ailments are per-arcana design (e.g. one geyser arcana: "of water: soaks"; Refraction: "the reflection of that element"), not every arcana of an element, so the condition isn't met.
- **Relics (RivalLoadout.cs):** `BuildRelicTable` now admits every relic that isn't cursed, `hubOnly`, `combineOnly` or `isGroupItem` (not just the 29 with a stat match).
  - `relicEffectById` stores each relic's effect. The exact `RelicEffects` stay; others get `EffectFor(class, category)`:
    - Resistance / Armor / Defense / Shield / Guard / Immune → +8% armour
    - Evade / Parry / Invulnerable → +8% evade
    - Crit → +10% crit
    - Health / HP / Regen / Heal / Vampire / Resurrect → ×1.1 HP
    - Cooldown / CD / Overdrive / Charge / Combo → ×0.92 cooldowns
    - Speed / Dash / Move → ×1.08 speed
    - Damage / Aura / Chance / Level / Explosion / Bomb / Stanza → ×1.08 damage
    - anything else, by category: Offense ×1.06 damage, Defense ×1.08 HP, Misc ×0.95 cooldowns
  - `PickRelic` / `UseRelics` (random rivals and the Echo, whose relics now all count) go through `ApplyRelic`. `RivalBonus.Clamp` still caps the totals.
- **RivalSpeech.cs:** `RivalSpeech.Say(speaker, line, colour)` draws a typed-out dialog box at the bottom of the screen (game font, element-coloured edge, one at a time).
  - `RivalLines` holds 3 lines × greet / half-health / defeat for each prefix (Flame, Frost, Earth, Wind, Thunder, Chaos, Radiant, Prism), plus Echo and Archon sets, rematch greetings and rematch promises.
  - `RivalEvent.Run` says the greeting after the intro card and adds `RivalBanter`, which says the half-health line once and the defeat line on death.
  - `Grudges.MaybeRecord` now returns bool. Its "This isn't over" card was replaced by a spoken promise 3s after the defeat line.

## v0.9.19: original element pairs, no Prism pairs, Nocturne's shuffle rolls versions
- **Pairs:** `InfusionUnlocks.Pairs` now pairs the arcana's own element with each unlocked infused version (Stone Fist + Fire-infused = Earth / Fire), plus every pair of infused versions.
  - Prism is excluded from pairs (it already cycles the five elements); it stays a single version.
  - `OwnElement`: the player's `skillsDict` element, else the catalog prefix; null for Neutral / Prism.
  - Tomi's bar shows "N dual" whenever any pair exists.
- **Nocturne the Cardist's paid shuffle** is `Player.RandomizeBuild` (called by `RandRunMod.Activate`). The `InfusionShufflePatch` postfix → `Infusion.RollShuffled`: each assigned arcana with unlocked versions picks uniformly among `Options` (original, singles, pairs) into `runPick`, shown on a "Shuffled versions" card.
  - Lookup order: `runPick` wins over the Tomi choice and the rolled run dual (`TryGet`, `TryGetSecond`).
  - Cleared on Hub arrival with the run duals. `RollRunDuals` skips shuffled arcana.
- **Arcanos ailments (research):** Soaked (`Empapado`), Magnetized (`Magnetizado`), Cracked (`Agrietado`), Swirled (`Arremolinado`), Muddied (`Embarrado`), Spored (`Esporado`), Burns (`Quemaduras`), Encased (`EncerradoEnHielo`), Rusted (`Oxidado`), plus `Alteraciones` (slow / electrocute).
  - They're applied only by Arcanos' own arcana, relics and robes (`Empapado.Empapar` has about 43 callers, all Arcanos skills), never by element type, and only to enemies.
  - Their payoffs are stat mods on the enemy (Soaked: `modificadorRayo`; Magnetized: `masPiedra` / `masRayo`) and a freeze prefix (`ParcheEmpapado.AntesDeCongelar`). Hits of those elements from any source, including our infusions and elemental attacks, should benefit (unverified in game).
  - We never apply them ourselves, and enemies never apply them to the player.

## v0.9.18: permanent dual versions (built, not yet run in game)
- **Unlocks (`InfusionUnlocks`):** once an arcana has 2+ unlocked single versions, every pair of them is a dual version (`Pairs`). The chosen version in osi.wol.hybridbosses.infusions.txt is now "Fire" or "Fire+Ice"; old files still read. Helpers: `SelectedRaw`/`SelectRaw`, `Selected` (first element), `SelectedSecond`, `Options` (original, singles, pairs), `Label` ("Fire / Ice dual"), and `FromName` (now internal).
- **Hub, Tomi:** [I] / pad buttons cycle through all `Options`. The hint bar shows the choice and "N versions, M dual", or "unlock one more for duals".
- **In runs:** `Infusion.TryGetSecond` reads, in order:
  1. this run's dual infusion (`second`);
  2. nothing, if the arcana was infused this run (a run infusion overrides the Hub choice);
  3. the Hub-chosen pair;
  4. a rolled run dual.

  `ApplyToAttack` alternates through `TryGetSecond`.
- **Random duals:** on leaving the Hub, `BountyBoard` → `Infusion.RollRunDuals`. Each unlocked arcana with pairs and no Hub choice has a 15% chance (`RunDualChance`) of a random pair for the run (`runDual`, read by `TryGet` and `TryGetSecond`), announced on a "Dual versions this run" card. They're cleared on Hub arrival. `Infusion.Any` counts them.
- **Enemies:** `Grudges.DualKit` (in `MakeHostile`, not the Echo) makes each arcana dual at 20% for Archons / final champions (`Tier == Final` or `ArenaArchon`) and 7% for Hard rivals: two different `RandomOther` elements in `EchoInfusions.Map` / `Second`.
  - `EchoInfusions.TryNext` alternates per attack (`MakeEnemyAttack`).
  - The intro row reads "Fire / Ice dual X" (`RivalMarker.InfusedSecond`).
- **Looks:**
  - `RecolorEngine.DualTwin(src, keyA, keyB)`: rect sprites, with A's ramp bottom-left and B's top-right, smoothstep-blended across 0.4–0.6 of the diagonal. Named "(A+B)" so `KeyOf` returns the pair. Tight sprites fall back to A.
  - The icon patch uses it when `TryGetSecond` gives a second element.
  - Card frames, the stock frame and Tomi's card tint use `Infusion.ShownColour` / `ElementRamps.MidOf`, the mix of both elements' colours.

## v0.9.17: grudges become rematches (no more stealing)
- The v0.9.16 flee-and-steal is gone (`RivalGrudge` removed; `RivalEvent.Fled` is unused).
- **Recording a rematch:** after a non-Echo, non-Archon rival dies, `Grudges.MaybeRecord` rolls `RivalWizards.GrudgeChance` (0.3). It records `Pending` (prefix, tier + 1, capped at Hard), with a "This isn't over" card.
- **The return:** each later generated stage opens the rematch portal with a 60% chance (`Grudges.OpenChance`), with the card "A rival wants a rematch".
- **The rematch rival:** `RivalMarker.Grudge` triggers `Grudges.InfuseKit` in `MakeHostile`, giving each arcana a 40% chance of `Infusion.RandomOther`. It's stored in `EchoInfusions` (`MakeEnemyAttack` converts and recolours, as for the Echo) and in `RivalMarker.Infused`, so the intro row reads "Fire-infused X" via `EchoRival.Named`.
- **Drops on its death (`GrudgeReturn`):** 12% an infused arcana (its infused element, via `SetPending`), else 35% a regular one. Picked from its arcana that are unlocked and not held.
- **Limits:** one pending at a time, and it ends with the run. The menu row is now "Rival rematches".

## v0.9.16: dual infusion, bounties, rival grudges, modifiers menu (built, not yet run in game)
- **Dual infusion (Infusion.cs):**
  - From the third section (`tierCount >= 2`) or any later loop (`Infusion.DualAllowed`), infusing an arcana already infused this run adds a second element: `SetSecond`, stored in `second`, double cost (`TomiInfusion.Dual` / `TotalCost`). Infusing a dual one replaces its second element. Earlier in a run, re-infusing replaces as before.
  - `ApplyToAttack` alternates the two elements per cast. The pick is decided once per AttackInfo (`dualPick`, `dualCount` per skill), then the usual status, recolour and +10% (once) apply.
  - Same-element checks cover both elements. Dual versions are run-only, but the added element's single version unlocks. Cleared in the Hub.
- **Bounties.cs:**
  - `BountyBoard` (added by `EchoRecorder.Ensure`) rolls one bounty on Hub arrival, only from systems that are on: Elite (a given trait), Elites ×5, Special mini-boss, Rival, Trial, Infuse ×2.
  - Shown top left in the Hub; during a run, for 6s at the start and 5s after each step. Completion drops 60 + 30/tier gold with a card.
  - Reports come from: `EliteMark.OnDeath`, `MinibossRunner.Gold` (only when the boss actually died), a plain Giant Cyclops's death, `RivalEvent.Run` (not if the rival fled), `TrialEvent.EndTrial(true)` and `TomiInfusion.Infuse`.
- **Grudges.cs:**
  - **Flee:** a Medium/Hard rival (not the Echo, not an Archon, no grudge pending) rolls `RivalWizards.GrudgeChance` (0.3) to get `RivalGrudge`. At ≤30% HP it flees: it steals a random equipped standard arcana (`p.RemoveSkill`; else 60 + 30/tier gold) and records `Grudges.Pending` (prefix, tier → Hard). It's removed via `BossKit.Remove`, so no chest, and `RivalEvent.Fled` is set.
  - **Return:** the next generated stage forces the rival portal (`OnLevelGenerated` → `Open(..., grudge)`): the same prefix, no Echo or Archon, "The rival who fled is back". `RivalMarker.Grudge` puts the stolen arcana first in its kit (if castable).
  - **Settle:** `GrudgeReturn` drops the stolen arcana on its death (its infusion is still in the run map), or double the stolen gold.
  - A grudge ends with the run (`BountyBoard` on Hub arrival → `Grudges.EndRun`).
- **ModifiersMenu.cs (Hub only):**
  - Opens with F1, or with `Modifiers.Button` (default Skill4) while Tomi's spellbook is open (`TomiInfusion.MenuOpen`, using `TomiInfusion.Device` for input). Tomi's own [I] handling pauses while it's open.
  - Rows are config toggles, or chances stepped through presets (0 = Off). Writes go straight to the `ConfigEntry` (BepInEx saves) and apply from the next roll.
  - Keys: Up/Down (W/S), Left/Right/Enter/Space to change, F1/Esc to close. Pad: Skill1/Skill2 move, Skill3 changes, Modifiers button or Cancel closes.
- **New config:** `Bounties.Enabled`, `RivalWizards.GrudgeChance`, `Modifiers.Button`.

## v0.9.15: polish-pass fixes; trial portal in the start room
- **Rewards no longer duplicate held arcana.** Trials, variant chests (`VariantRewards.PickUnlocked`) and the Council's variant / infused drops (`PickVariantArcana`) checked `LootManager.currentPlayerSkills`, which the game never fills. They use `TomiInfusion.HeldIds()` now: equipped plus stock, without the shop shelf (`OwnedIds(p, includeShelf: false)`).
- **Giant Cyclops on Chaos stages:** `ChaosEnemies.IsChaosType` excludes giant cyclops. Since Cyclops joined `ChaosTypes`, `ChaosRegularEnemyPatch` armed the giant as a regular (1 arcana, purple row) one frame before its own setup, so `ChaosGiants.Apply` was skipped.
- **Vengeful souls** skip `MiniBoss`, giant cyclops, `BossEventEnemy` and anything with `BossArmour`. Before, an escort elite could heal a mini-boss +50% three times and grow it.
- **Split copies** keep their parent's element. `EliteSplitChild.InheritPrefix` / `InheritVariant` are set in `Split`, and `ElementalNeutrals.TryApply` applies them instead of rolling again.
- **Outlines:**
  - `BossArmour` remembers the outline it found at Start (the element outline) and shows it while stunned, instead of black.
  - `ConclaveCore` no longer overwrites the outline while the armour is up (`BossArmour.Armoured`); it shimmers only while stunned.
  - The Giant Cyclops restores the outline it had before its first armour (it showed grey on Chaos stages).
- **`OutlineCache.cs` (Steam Deck):** per-frame outline writers (EliteMark pulse, Vengeance glow, ConclaveCore shimmer, plus BossArmour and the Giant Cyclops) keep the enemy's outline materials, the same instances `sr.material` returns, and refresh the list every 2s. Before, they called `GetComponentsInChildren` and `sr.material` every frame.
- **Colours:** `ElementalGiants.ElementColor` Earth is now `ElementRamps.Mid("Earth")` (brown, was green) and Chaos is (0.48, 0.2, 0.78) (dark purple, was light lavender). `Infusion.ColorOf(Chaos)` uses the same purple.
- **Trial portal:** opens in the start room. It uses `RivalEvent.StartRoomSpot` and avoids the rival portal; its settle waits 3 physics steps so the rival portal is placed first. The announcement says "In the starting room" and stays up 7s. `PickSpot` was removed.

## v0.9.14: elemental blobs and ghouls
- **Which enemies:** `ElementalNeutrals.IsBeast` covers `Blob` (and its subclasses `BlobRoller`, `SuperBlob`; the mini blob is a Blob prefab), `BlobSpitter` (Blob Barrager), `Ghoul` and `GhoulLarge` (Macho Ghoul). They're added to `IsKind`.
- **Elemental stages:** they roll `ElementalCoffinCyclopsChance` and get `Apply(e, prefix, 0)`: recoloured body and elemental attacks, no arcana.
- **Chaos / Radiant / Prism stages:** they aren't in `ChaosEnemies.ChaosTypes` (that would give them an arcana). Instead `TryApply` calls `MinibossFights.MatchStage(e, false, 0)`: `VariantMark`, the look, and converted attacks. `TryApply` now skips enemies that already have a `VariantMark`.
- **`ConvertExisting(e, el, v)`:** converts hitboxes whose info was set before the element applied (contact / body attacks set in their own Start). With v = None it uses `Infusion.ConvertEnemyInfo`, otherwise `Variants.Convert`. It's called from `Apply` and from `MatchStage`'s escort variant path, so it covers Coffin, Cyclops and the fights' escorts too.

## v0.9.13: special mini-bosses match the stage
- **Before:** only the Giant Cyclops (and so the Eye Court's giant) took the stage element or variant (its own `Start`). The Mimic King, Hive Mother and Gravewarden aren't `MiniBoss` bodies, so `ElementalGiants` never touched them, and the Conclave's Archon ignored variant stages.
- **`MinibossFights.MatchStage(e, boss, arcana, followPrefix)`:**
  - **Variant stage:** a boss → `ChaosGiants.Apply` (variant look, 2 variant arcana, `VariantMark`), and its `GiantIntro` is destroyed because the fight shows its own card. An escort with arcana that is a Chaos type (the Gravewarden's Coffin Knights) → `ChaosEnemies.ArmRegular`. Other escorts → `VariantMark.Set` plus the look (Chaos: element Chaos + `RecolorOrTint("Chaos")`; else `Variants.TintBody`).
  - **Elemental stage:** `ElementalNeutrals.Apply(e, stagePrefix, arcana)`.
  - **Neutral stage:** the boss rolls `ElementalGiantChance` for a random element; escorts follow the boss's prefix.
- **Per fight:**
  - Mimic King, Hive Mother, Gravewarden: the boss gets 2 arcana. The Mimic adds, the brood and the Ghouls follow via `Follow()`, two frames after their Start.
  - Eye Court: the orbs follow the giant's `ElementalNeutral.Prefix`, or the variant.
  - Gravewarden knights: the warden's element, else random (always elemental); on variant stages, armed as humanoids.
  - Conclave: on variant stages the Archon also becomes the variant giant, and Chaos joins its element cycle. Zoners 1–4 stay their own elements.
- **Intro cards** show the element ("Mini-boss · Fire · …") in its colour (`Sub`, `TitleColour`).

## v0.9.12: Hive Mother and Gravewarden (6 special mini-bosses)
- `MinibossFight` gained `HiveMother` ("SuperBlob") and `Gravewarden` ("GhoulLarge"). `MinibossFights.Count` = 6 is used by the swap roll and Ctrl+F5.
- **Hive Mother:** ×1.3 scale, 1000 + 380/tier, `BossArmour`. Each break spawns 4 brood (BlobSpitter, BlobRoller, BlobMini, BlobSpitter) tagged `BroodBlob`. After 6s, a living brood moves straight to her at 3 u/s (overriding its AI). On contact it heals her 8% of max (`healthStat.CurrentValue`) and is removed. On her death the brood is orphaned (stays as normal enemies).
- **Gravewarden:** ×1.35 scale, 1100 + 400/tier, plus 2 Ghouls at the start.
  - Raises a "Coffin" every 9s while fewer than 3 stand. Each is made elemental after two frames: the stage prefix, else random, via `ElementalNeutrals.Apply`, 1 arcana.
  - New `BossArmour.CanBreak` (`Func<bool>`): the armour can't break while any of its knights lives. The first refusal shows a hint card.
  - Leftover knights are removed when it dies.

## v0.9.11: the special fights join the mini-boss pool (no portal)
- **What changed:** the v0.9.10 `BossEvent` (portal plus arena) is gone. `MinibossSwap` (ElementalNeutrals.cs) replaced `GiantCyclopsSwap`. A giant spawn (Super Knight / Mage / Archer / Lancer / Summoner / Coffin) rolls `BossFights.MinibossChance` (0.3) and becomes one of four specials, picked evenly: Giant Cyclops, Mimic King, Cyclops Eye Court, Elemental Conclave. It happens in the stage's own mini-boss room, with the game's escort.
- **Config:** `Enemies.GiantCyclopsChance` and `BossFights.Chance` are no longer bound.
- **Swap:** `MinibossFights.KeyFor(f)` gives the replacement name: "Cyclops", "MimicEnemyLarge", or a random elemental Zoner (the Archon). The Spawn postfix calls `MinibossFights.Begin(enemy, f)`.
- **The enemy returned to the game is the one that must die last,** so the room's mini-boss logic holds:
  - Conclave: the Archon is `SetActive(false)` while `MinibossRunner` spawns the other four Zoners one at a time. Those use `Setup(..., chest: false)`. Then it reactivates the Archon at the spot, with armour and cycling elements.
  - Eye Court: `GiantCyclops.Mark`, plus orbs spawned by the runner. `GiantCyclops.Break` calls `MinibossFights.OnCyclopsBreak` directly (the static event was removed). The Giant Cyclops skips its own `GiantIntro` when tagged `BossEventEnemy`.
- **Helper spawns** go through `MinibossFights.Spawn`, which sets `GiantCyclops.Pending` so they're never swapped themselves.
- **Ctrl+F5:** `MinibossFights.TestHere` spawns the four in turn beside you.

## v0.9.10: boss fights from regular enemies (superseded by v0.9.11: no portal any more)
- **Roster:** checked against the wiki's enemy list (Archer, Blob, Blob Barrager, Chaos/Laser Cyclops, Coffin Knight, Ghoul, Golem, Knight, Lancer, Macho Ghoul, Mage, Mimic, Rogue, Roller Blob, Summoner, Turret, Zoner). Rats, doves and eagles in `Enemy.EName` are critters, not enemies. Laser Orbs aren't a listed enemy but are the mini-bosses' summon (`MiniBossLaserSummonState`). `MimicEnemyLarge : MimicEnemy` exists in code.
- **BossFights.cs, `BossEvent`** (shaped like `TrialEvent`):
  - Config `BossFights.Chance` (0.12), hooked in `LevelGenPatch.Generated` after the trial.
  - Portal: gold, in the start room. `RivalEvent.StartRoomSpot` is now internal with an `avoid` list, so it stays 4+ units from the rival portal. Settle waits 3 physics steps so the rival portal is placed first.
  - Arena: `BuildRoom` at start + (0, -1500).
  - Flow: the fight starts on arrival (the entrance portal is removed). On a win: gold, the boss's mini-boss chest (`BossKit.Setup` sets `dropLoot`, `dropLootChest`, "TreasureChestMiniBoss"), then the way back.
  - Ctrl+F5 opens one beside you, cycling the three fights; plain F5 still lists enemies.
- **The three fights:**
  - **Mimic King:** "MimicEnemyLarge" (fallback "MimicEnemy"), ×1.4 scale, 1100 + 400/tier. Each armour break: 3 gold drops and 2 Mimics (spawned with loot off). Gold ×1.6.
  - **Cyclops Eye Court:** `GiantCyclops.SpawnAt` plus 4 "LaserOrb"s with `allowRoam` false, orbited by `OrbitCourt` at radius 4.5. `GiantCyclops.ArmourBroken` (new static event) removes one orb per break via `BossKit.Remove`: out of `GameController.enemies` and destroyed, no loot.
  - **Elemental Conclave:** the 5 elemental Zoner classes in random order, one at a time. Zoners 1–4 have growing HP (260 + 100/tier × (1 + 0.25i)), size and speed. The fifth is the "Elemental Archon": ×1.7, 900 + 300/tier, `BossArmour`, and `ConclaveCore`, whose attacks cycle the absorbed elements (in `VariantAttackPatch`, before the elite and neutral conversions) with a shimmering outline.
- **`BossArmour`:** a generic copy of the Giant Cyclops armour cycle: threshold, stun seconds, `OnBreak(breaks)`. Enemies with no "Hurt" state (the Mimic) are slowed to 15% while stunned instead.
- **`BossEventEnemy`** tags every spawn of a fight, so they're never elites or re-elemented.

## v0.9.9: giants always elemental on elemental stages; recoloured Coffin / Cyclops sheets
- **Giants:** `ElementalGiants.TryMakeElemental` and the Giant Cyclops no longer check `MatchStageElement`. On any elemental stage (`StagePrefix() != null`) the giant takes the stage element. The setting is kept but described as unused.
  - The user's cfg already had it true. If a neutral giant shows up on an elemental stage, the log line "stays neutral (stage X …)" will show `Level.element` read Neutral there.
- **`ElementalGiants.RecolorOrTint(e, rampKey, outline)`:** `RecolorEngine.RecolorBody(e, key, restorable: false)` maps the palette texture onto the element ramp, then sets only the outline colours. It falls back to the flat `Tint` when 0 parts were recoloured. Log: `RECOLOR <type> body on the <key> ramp (N part(s))`.
  - Used for the Super Coffin (`NoElementPalettes`), for elemental Coffin / Cyclops / Giant Cyclops (`ElementalNeutrals.Apply`), and by `ChaosGiants.Apply` when a giant has no purple row (key "Chaos").
  - `RecolorBody` got `restorable = true` by default (Council). With false, the material isn't registered in `origPalettes`, so `CouncilCorruption.End` → `RestoreBodies` won't undo it.

## v0.9.8: elemental Coffin Knights / Cyclops; the Giant Cyclops (built, not yet run in game)
- **ElementalNeutrals.cs:** the game only has neutral `Coffin` and `Cyclops` (no elemental subclasses).
  - `TryApply` runs from `EliteSpawnPatch.Later`, before the elite roll, so an Infused elite sees the new element. On an elemental stage it rolls `Enemies.ElementalCoffinCyclopsChance` (0.6).
  - `Apply`:
    - `ElementalNeutral` mark and `e.element`.
    - `ElementalGiants.Tint`, because their sheets have no usable elemental rows (the Super Coffin's render pink).
    - One arcana from `GapArcana` ("Knight" families for the Coffin, "Mage" for the Cyclops), cooldown ×1.6.
    - The cyclops beam colours: `Laser.chargeColor0-2`/`shootColor0-2`, mapped on the element ramp. The laser re-applies them on every `SetTargetLineVars`, so tinting the line renderer wouldn't stick.
  - Their attacks are converted in `VariantAttackPatch` (`ElementalNeutrals.ConvertAttack`: `Infusion.ConvertEnemyInfo` plus `TintAttack`).
  - On Chaos / Radiant / Prism stages, "Coffin" and "Cyclops" were added to `ChaosEnemies.ChaosTypes`.
- **Giant Cyclops** (no Super Cyclops in the game):
  - **Spawning:** `GiantCyclopsSpawnByName` / `ByEnum` prefix both `Enemy.Spawn` overloads. A Super Knight / Mage / Archer / Lancer / Summoner / Coffin spawn rolls `Enemies.GiantCyclopsChance` (0.15) to become "Cyclops"; never the Super Rogue (its clones use Spawn), Hub, boss levels or `BossRoomEventHandler` rooms. The postfix calls `GiantCyclops.Mark`: ×1.9 scale, `dropLootChest` + `chestLootTableID = "TreasureChestMiniBoss"`. `Pending` guards nested overload calls and is cleared even when the spawn fails. F8's test cycle now ends with "GiantCyclops" (`GiantCyclops.SpawnAt`).
  - **Setup** (`GiantCyclops.Start`, two frames in): HP override (900 + 350/tier, ×1.5 from loop 1).
    - Element: the variant giant via `ChaosGiants.Apply` on Chaos stages; otherwise the stage element (MatchStageElement) or random by `ElementalGiantChance`, through `ElementalNeutrals.Apply` with 2 arcana at cooldown ×1.
    - `GiantIntro` with kind "Cyclops" (`GiantName` → "Giant Cyclops").
  - **Stun phases:** MiniBoss's armour is `bossIgnoreMod` on `ignoreHurtVarStat` / `ignoreKnockbackVarStat`, toggled by `AllowHurtState`. The giant does the same with its own `BoolVarStatMod` (`VarStat.Modify(mod, on)`) and a pale outline while armoured.
    - After 20% of max HP lost since the armour went up: break, with "ArmorDissipateEffect" (`PoolManager.GetPoolItem<ParticleEffect>` + `Emit(15, pos, …)`) and a camera shake.
    - Stun lasts 3.5s: every 0.25s it re-enters "Hurt" (`fsm.ChangeState("Hurt", true)` if `HasState`, skipping Dead/Frozen/WolArcanos states) and the laser is `Deactivate()`d.
    - Then the armour comes back. `GiantWatchdog` isn't attached.
  - **Beam:** `Cyclops.InitLaser` postfix scales `shootThickness0-2` ×2.4, `chargeThickness0-2` ×2 and `randomSizeRange0-2` ×2.4. `Laser+ShootState.Update` sizes the `BoxCollider2D` as line thickness × `atkVisualRatio`, so the hitbox widens with it. `maxRange` becomes 26, and the parent's ×1.9 scale is undone on the laser transform. `GiantCyclopsDamagePatch` makes its hits ×1.5 (undone in the postfix).
  - Not an elite candidate (`Elites.Eligible`).

## v0.9.7: seven more elite traits; gold by risk
- **Elites.cs:** `EliteTrait` now has 12 traits (`Elites.TraitCount`), and the Ctrl+F7 test key cycles through all of them.
  - **Volatile:** `EliteBlast.Spawn` (radius 2.4, 30 + 12/tier, its element, 0.9s delay). The warning is the game's `CastingCirclePool.Spawn("Draw", …, useCastTime, delay, size, CastingCircle.enemyColor)`, the same call `ArrowShower` makes. Then `Explosion.Prefab` + `Explode(scale)` (as `SuperMovingStatue`'s ground slam does), the "MeteorExplosion" sound, a camera shake, and one frame of `Attack.CirclePrefab`. Its info is `NeutralKnightAttack` re-owned by the elite (entity null if the body is gone), with damage and element status set (`Infusion.SetStatus`, now internal).
  - **Vengeful:** `EliteSoul` flies to the nearest living enemy within 12 units. `Vengeance.Empower`: +20% speed, heals 50% of its current HP, ×1.12 scale, red outline; up to 3 stacks.
  - **Haunting:** `EliteGhost` is a pale copy of the sprite. It rises for 0.6s, then drifts at 3.4 u/s through walls for about 6s and bursts on contact (radius 1.5, 20 + 8/tier).
  - **Anchored:** `BoolVarStatMod` on `health.ignoreKnockbackVarStat` and `ignoreHurtVarStat` (the Earth Knight shield's switches), plus ×1.1 scale. `Pull()` moves the player 1.3 u/s toward it within 1.6–7 units, unless a Wall/Pit/Obstacle linecast blocks it.
  - **Overcharged:** while `Charged`, its next hit on a player deals double. The `TakeDamage` prefix doubles `info.damage` and the postfix restores it, because infos are reused. `Discharged()` → recharges after 8s. Fast yellow flicker plus spark after-images while charged.
  - **Infused:** `InfusedElement` is a base element other than its own (Chaos 15%). `Elites.InfuseAttack` runs in `VariantAttackPatch` before variants: `Infusion.ConvertEnemyInfo`, then `TintAttack` with the element's ramp. Outline and label use the element's colour ("Fire-infused Knight"). No arcana drop.
  - **Thief:** each landed hit steals min(balance, 8 + 4/tier), up to 60 + 20/tier in total. It blinks about 7 units away 0.35s later. The label shows what it carries, and it drops double that on death.
- **Gold:** `Elites.GoldFor(trait)` = (15 + 10/tier) × trait multiplier × 1.25 from loop 1.
  - ×1.4: Overcharged
  - ×1.25: Shielded, Vengeful, Infused, Anchored
  - ×1.15: Hasted, Draining, Blinking, Volatile, Haunting
  - ×1: Splitting (its copies drop too), Thief (pays back its theft)
- `EliteDrainPatch` now handles Draining, Overcharged and Thief in one prefix/postfix pair (state struct).

## v0.9.6: sacrifice from the merchant's shelf
- **Why:** a run holds one basic and one dash, so buying a basic or dash only to sacrifice it was pointless.
- **What shows:** `TomiInfusion.Shelf(p)` collects `SkillStoreItem`s within 20 units of the player. It skips:
  - `signatureOnly` and platinum items
  - cards already used this way (`consumed`)
  - anything outside a run

  `OwnedIds` adds their IDs, so they appear in Tomi's run menu. Locked arcana on the shelf don't appear, because Tomi's book only lists unlocked ones.
- **Keep or sacrifice:** `ShelfOnly(p, s)` means "on the shelf and not held". Such a card can only be the sacrifice; pressing [I] on it as the keep explains why.
- **Element given:** `GiveOf` uses the card's pending pre-infused element, else its base element.
- **Cost:** `TotalCost` = infusion cost + `item.Cost` (shelf price, sale included).
- **Taking the card:** `Consume` does what `SkillStoreItem.Buy` does: price marker "x", `Destroy` the card. It also clears the pending infusion.
- **Hint bar:** under the cursor, a shelf card gets the line "on the merchant's shelf · sacrifice only · +N gold".
- **Log:** `INFUSION <keep> took <El> from <sac> (from the shelf) for N gold`.

## v0.9.5: Tomi's card view filtered too
- The element pages were filtered in v0.9.4 (log: "5 owned arcana shown, 275 others hidden"), but the card view uses its own list: "view all arcana", or opening one of your arcana.
  - `LoadInfoSkills` walks `player.skillsDict` (every unlocked arcana) and adds each through `AddSkillToInfoSkills` into `infoSkillList` and `infoEleSkillTupleList`.
- `TomiRunInfoListPatch` (an `AddSkillToInfoSkills` prefix) skips arcana not in `OwnedIds` during runs. The owned set is cached per frame.

## v0.9.4: enemy Mortal Coil fixed; Tomi's run list really filtered
- **Mortal Coil (the "loud Chaos trail" bug, also the v0.7.4 one blamed only on Chaos Ball):**
  - Cause: `ArcanaFinisher`'s generic `Initialize` step guessed the 7 arguments: the target as the start point, range 1, and `Enemy.globalEnemySkillCategory` for both skill category and skill ID. `SetAttackInfo` found no stats, so `atkInfo` was null. `MortalCoil.Update` threw in `Projectile.UpdateCalculatedDamage` every frame before setting `attackCreated`, so the coil respawned its projectile, `ChaosLightningTrail`/`ChaosWireTrail` and the `ChaosFormLong`/`ChaosBeamStart` sounds each frame. With no parent, the projectile also logged "has no parent, but is set to boomerang".
  - Fix: new **EnemyMortalCoil.cs** wires it the way `Player+UseMortalCoil.InitMortalCoil` does: `Initialize(start, aim, "Player", skillID, signature, caster.transform, 8 / 14 signature)`, `delayStopwatchTime` 0.125.
  - Postfixes on `MortalCoil.InitAttackBox` (the line attack) and `CreateMortalCoilProj` (the returning projectile, then `UpdateCalculatedDamage(true)`) run `EnemyArcana.MakeEnemyAttack` on the attacks it makes later, so they hit the player. The coil returns to its caster.
  - Removed from `ArcanaFinisher.Steps`; enemies still cast it.
- **Tomi's run list:**
  - Cause 1: `LootManager.currentPlayerSkills` is never filled by the game (only `ResetAvailableSkills` touches it). The run's arcana are `assignedSkills` plus `stockSkill`, which is what the equip menu shows. `OwnedIds` uses those now.
  - Cause 2: **LegendApi** hooks `SpellBookUI.LoadEleSkillDict` (MonoMod) and refills `eleSpellDict` after our postfix.
  - Fix: the filter (`TomiRunListPatch.Filter`, also covering `eleOverdriveDict`) runs as a LoadEleSkillDict postfix at `Priority.Last`, a `LoadSpellPage` prefix and an `Activate` postfix. Debug log line: `INFUSION Tomi list: N owned arcana shown, M others hidden`.

## v0.9.3: weighted rival difficulty; portal in the start room
- **Difficulty:** `RivalEvent.RollTier(atLeastMedium)` rolls from `TierWeights` (Easy/Medium/Hard %), indexed by `tierCount` 0–2, or 3 from loop 1:
  - tier 1: 60/30/10
  - tier 2: 30/45/25
  - tier 3: 15/40/45
  - later loops: 5/30/65

  The Echo rolls with Easy's weight dropped (at least Medium). With `ScaleWithProgress` off it's an even roll. `ScaledTier` is gone.
- **Portal placement:** the stage's rival portal opens at `Level.startPosition + (4, 0)`. After two physics steps, `Settle` moves it to `StartRoomSpot`: the most open floor 3.5–6 units from the arrival, along 16 directions, keeping only spots with an unblocked linecast from the arrival so it stays in the start room. If nothing has 1+ clearance, it falls back to `Roomy`. `PickEntrance` (a random checkpoint elsewhere on the stage) is no longer used by the event. F11 still opens it beside the player.
- **Announcement:** stays up 7s (`AnnounceSeconds`, was 4s) and says "In the starting room".

## v0.9.2: no selector room; the rival portal leads straight to the arena
- `RivalEvent` builds only the arena now. The stage portal carries the `RivalPortal` tag and teleports straight to `arenaArrival`. Entering it calls `Choose`, and `Run` works as before: it moves any player left behind, spawns, shows the intro, then opens the way back.
- What the portal holds is rolled in `Open`:
  - The Echo, when `EchoRecorder.Available` and the `RivalWizards.EchoChance` roll passes (new config, default 0.35). Its tier is `EchoTier`.
  - Otherwise a rival at `ScaledTier`, or a random tier when `ScaleWithProgress` is off.
- The portal colour shows which: pale `EchoColor` for the Echo, the element colour otherwise. So does the announcement ("The Echo of your last run has opened a portal" / "A rival's portal has opened · <element> rival · <tier>").
- Removed: selector room, difficulty portals, the selector's way back, `PortalOffsets`.

## v0.9.1: fixes from the first Echo / Hub test
- **Hub duplicate Tomi:** `TomiAtMerchantPatch` also fired on the Hub shop's `SkillStoreItem`s and spawned a second Tomi at (4.5, 6.2) beside the Hub's own. It's now limited to runs (`TomiInfusion.InRun`).
- **Doubled hint text:** `GameFont.Shadowed` drew the shadow pass with the same rich text, so `<color>` words (the element-memory line) drew a second coloured copy 2px off. The shadow pass now strips `<color>` tags.
- **Echo fight, player invisible:** the record's dash was the plain default `Dash`, and `EchoRival.Def` skipped the `Castable` check every other rival pool applies (`ArcanaCatalog.Filter`), so the Echo got the player's own dash as an "arcana". In the test the player vanished while the Echo fought on. `Def` now rejects `Dash` and non-castable defs, so the Echo falls back to its element's dash pool. This is the likely cause; not confirmed in game.

## v0.9.0: Echo of your last run; rivals scaled to the run (built, not yet run in game)
- **Echo.cs, recording:** `EchoRecorder` sits on the ChaosStageRunner object (DontDestroyOnLoad). Every 4s during a run it notes the loadout:
  - `assignedSkills` with role basic/dash/signature/standard, plus `Infusion.TryGet` names;
  - `equippedOutfit.outfitID`, `inventory.GetItemIDList()`, and tier/stage/loop.
  - Arriving in the Hub commits it to `BepInEx/config/osi.wol.hybridbosses.echo.txt`, if it has at least 2 arcana. `Last` is the previous run, and `FoughtThisRun` resets in the Hub.
  - Ctrl+Shift+F11 records the current loadout now (test).
- **Echo.cs, the fight:** `RivalMarker.Echo` (an EchoRecord) is set right after `Rivals.Spawn`, before Start. MakeHostile then uses:
  - `RivalLoadout.UseOutfit` (robe colour row plus mods) and `UseRelics` (stat relics, up to 4; the first one is shown);
  - `EchoInfusions` (skillID → element);
  - the record's basic/dash if castable, and `EchoRival.Arcana` (the record's castable standard arcana, topped up from its element).
  - `EchoRival.Prefix` is the record's main element. Intro names read "Fire-infused X" (`EchoRival.Named`).
  - `MakeEnemyAttack` converts and recolours its infused arcana (`ConvertEnemyInfo` plus the ramp) before the variant pass. The Echo reward is a gold chest.
- **Selector:** with `RivalWizards.ScaleWithProgress` (default true) there's one rival portal at `ScaledTier` (tier 0 Easy, tier 1 Medium, tier 2+ Hard, Hard from loop 1). With `EchoOfLastRun` there's also an Echo portal when available, coloured pale lilac, at `EchoTier` (at least Medium). `RivalPortal.IsEcho` leads to `Choose(tier, echo)`.
  - The Echo has no Archon roll and is titled "Echo of your last run" with the subtitle "You reached tier N". Setting `ScaleWithProgress = false` brings back the three difficulty portals.

## v0.8.2: rivals hunt the player; element memory at Tomi (built, not yet run in game)
- **Rivals:** `Contestant+ContestantChaseState.Update` only looks for a target within `Enemy.escapeRadius`. With none, it calls ChangeState("GoBack") (the PvP leash back to its post), so a player sniping from range was never chased.
  - RivalAttackRedirectPatch turns "GoBack" into "Chase" for rivals.
  - `RivalUnstuck.Start` raises escapeRadius / alertRadius / maxAlertDistance to 60 and sets `forceChaseState`.
  - `RivalUnstuck.GapClose`: if the player stays at 7+ units for over 2.2s while the rival is chasing, it does `CalculateDashVector(player)` then Dash. It has a 3.5s cooldown and logs `RIVAL closing in`. Champions get it too, since they're rivals.
- **Element memory:** in the Hub, Tomi's hint bar adds a second line, `TomiInfusion.Memory`: every element the arcana can take (Fire, Water, Ice, Earth, Wind, Thunder, Chaos, plus Radiant/Prism with Arcanos, minus its own). Collected versions show in their colour, missing ones in grey, as "Collected n / m".

## v0.8.1: trial spawns; Council body recolour (built, not yet run in game)
- **Trials:** the v0.8.0 wave keys were raw object names ("FrostMage(!)", "Knight (1)", "OrbChargeFamiliar (3)", "FrostZoneEnemy(!)"). Only "Summoner" spawned, and an empty wave counted as cleared.
  - Keys now come only from humanoid kinds (`ChaosEnemies.IsChaosType`), using `CleanName` (cut at "(") and the type name, plus a Fallback list (Knight, Archer, Rogue, Mage, Lancer, Summoner).
  - `SpawnWave` drops a key that fails and tries another (up to count×4 attempts), returning the number spawned. If a wave spawns 0, the trial is cancelled with no reward ("The trial fizzles out").
- **Council bodies:** `RecolorEngine.RecolorBody(body, key)`.
  - Palette-shader sprites get `PaletteTwin` (the `_Palette` texture gradient-mapped; strength 0.25 + 0.7 × saturation, so robes recolour fully and skin and greys only partly), set on the instance material, with originals kept for `RestoreBodies` (called from CouncilCorruption.End).
  - Other sprites go through `RecolorSprites` with `AllowBodies`.
  - `CorruptBoss` uses it for infused (element ramp), Chaos and Radiant; Prism keeps PrismCycle. The multiply tint only applies as a fallback.
- **ToggleForce** inside a Council room re-runs CorruptBoss/PickEchoes on the boss already there.
- **Freiya's lines in the test:** the variant was switched to Infused while in her room, after her intro, so only her victory lines could change. Test by choosing with Shift+F12 first, then Ctrl+F12 from a stage.

## v0.8.0: infused Council, elite enemies, infusion trials (built, not yet run in game)
- **Infused Council (CouncilCorruption):**
  - **Activation:** `Infused` (ElementType?), `BeginInfused(room, el)` (uses Begin with Chaos internally), `InfusedFight`, and `CurrentInfo` (an infused VariantInfo built per element: title "<El>-infused", ramp colours, `InfusedTiles` = target-hue tiles per element).
  - **Choice:** `CouncilInfusion(tier, bossKey)` is deterministic (seed hash): tier ≥ 1, 15% / 25%, +20% from loop 1, never when the tier has a variant member. The element is one of Fire/Water/Earth/Air/Lightning minus the boss's own (`OwnElement` from "FireBoss…", "IceBoss…" → Water, and so on). The board marks the portrait in the element colour (plan shows "Fire-infused").
  - **Attacks:** `Variants.Apply` diverts to `InfuseAttack` (`Infusion.ConvertEnemyInfo`: element plus moved status, no bonus; recolour with the element ramp). Covers VariantAttackPatch, MakeEnemyAttack and the runner scan. TintTree uses the element ramp when infused.
  - **Fight content:** echoes from `ArcanaCatalog.Pool(prefix)`. Lines: `InfusedLines` per boss with `{el}`. Chest: half the time an arcana of the borrowed element, half the time one of the boss's own element pre-infused with it (unlocked only).
  - **Rules:** Shift+F12 cycles … → Prism → Infused (random other element) → off. Config `Infusion.InfusedCouncil`.
- **Elites.cs:**
  - **Roll:** EliteSpawnPatch (Enemy.Start, 2 frames later) calls `Elites.TryMake`. Eligible means not MiniBoss/Boss/rival/our summons/split copies, not on boss floors or in BossRoomEventHandler rooms. Chance 4% + 3%/tier, +4% from loop 1, × config.
  - **Look:** `EliteMark` gives a pulsing trait-colour outline (`_OutlineColor`) and an OnGUI label "<Trait> <Name>" in the game font.
  - **Traits:**
    - Shielded: `health.CurrentShieldValue` = 40% max HP.
    - Hasted: `movement.moveSpeedStat` ×1.35, anim speed 1.3, `FadeOut` after-images.
    - Draining: `EliteDrainPatch` (Health.TakeDamage prefix/postfix; the victim is a player from playerScripts) heals the attacker half the health lost.
    - Blinking: every 3.5–5s, a teleport 3.5 units from the player via GetAvailableNearbyLocation, not through walls, skipped in hurt/frozen/Arcanos states.
    - Splitting: on death, `Enemy.Spawn(gameObject name, then type name)` ×2 with `EliteSplitChild`, 0.75 scale and 40% HP.
  - **Death:** `DropGold(15 + 10×tier)`; `EliteInfusedDropChance` (0.12) calls `DropSkill` with `InfusionDropPatch.ForceNext`.
  - Ctrl+F7 makes the nearest enemy the next trait.
- **TrialEvent.cs:**
  - **Opening:** `OnLevelGenerated` (called after RivalEvent from LevelGenPatch.Generated) with `Trials.Chance` 0.12. The portal goes on a checkpoint 12–80 units from the start, 15+ away from the rival portal.
  - **Room:** `RivalEvent.BuildRoom` (its helpers are now internal) at startPosition + (0, -1140). In Settle: Roomy arrival, a way-back portal from the start, and enemy keys taken from the stage's enemies (gameObject names).
  - **Run:** on entering, `Infusion.ForcedElement` is set (ApplyToAttack converts every player attack, no bonus). Three waves of 2/3/4 + tier `Enemy.Spawn` around the arrival; a 120s guard per wave.
  - **Clear:** gold, plus an unlocked arcana of another element, not owned, set pending with the trial element and dropped via `DropSkill`. Leaving the room ends the trial (forced element cleared, also on destroy).
  - Ctrl+F6 opens a trial beside you.

## v0.7.4: final-fight hotkey; Tomi hint overlap; Chaos Ball and Chaos accents (built, not yet run in game)
- **Ctrl+Shift+F12 → `Debug.JumpToFinal`:** sets tierCount = 3 and stageCount = 0, then `GameController.LoadLevel("FinalBossLevel")` (Sura, or the run's champion). It only works from inside a run.
- **Tomi hint:** in the Hub the switched version was shown twice (the bar line plus a status message), and two lines didn't fit the fixed 70px band. The message is dropped, and the band moved to the top of the screen (the game's prompts sit at the bottom), sized with `CalcHeight`.
- **Chaos noise/trail:** `UseChaosBall` is a player charge arcana (`ProjectileChargeAttackState` with sub-balls, lightning and Chaos pillars) that enemies never release. It's now in `EnemyArcana.Broken` at startup.
  - `Variants.Apply` skips attacks that already are Chaos on Chaos stages, since recolouring them onto the Chaos ramp made a murky blob.
  - `Accent` fires once per attack object (`accented`) and at most every 1.5s per caster. Attacks that re-set their info in flight had left spark trails and repeated sounds.

## v0.7.3: controller infusion; game font on mod UI (built, not yet run in game)
- **Controller:** the game reads input through `ChaosInputDevice.GetButtonDown(actionName)` (Rewired). Every action name the game uses: Cancel, Confirm, EquipMenu, Interact, Menu, Skill, Skill0–Skill5, Slide, Slot. SpellBookUI.Update only reads Confirm and Cancel; navigation is directional.
  - `TomiInfusion.PadPressed` reads `SpellBookUI.inputDev` (fallback `player.inputDevice`). Any action in config `Infusion.Buttons` (default `Skill1,Skill2,Skill3`) acts as [I]. A press that's also Confirm/Cancel on the same frame is ignored.
- **GameFont.cs:** `Get()` picks the font most of the game's `UI.Text` use (`Resources.FindObjectsOfTypeAll`, retried every 2s until found, logged as `UI using the game's font`).
  - `Style()` builds GUIStyles in it (bold dropped, since the pixel font has none), `Changed(ref seen)` rebuilds styles when the font turns up, and `Shadowed()` draws text with a drop shadow.
  - It's used by IntroUI (all announcement cards: rival portal, Council, intros), SignatureMeter labels, the champion overlay, and Tomi's hint bar (now a dark band with gold edge lines instead of Unity's box skin).
- `IntroUI.DrawSprite` falls back to `sprite.rect` for tightly packed sprites (textureRect throws).

## v0.7.2: Tomi placed at the merchant; infused cards tinted everywhere (built, not yet run in game)
- **Bug:** `SkillStoreItem.SpawnTomi` hard-codes (14, 8), his Plaza spot in the Hub. In runs he spawned out of sight, even though the log said he came.
- **Fix:** `TomiAtMerchantPatch` spawns him itself at the left end of the shop's arcana row (SkillStoreItems within 12 units) plus 2.2 up, with Arcanos' "Teletransporte" effect. It skips if a Tomi is already within 15 units.
- **Infused cards:**
  - `InfusionIconPatch` logs each icon request once: `INFUSION icon <id> -> <ramp> (recoloured / could not recolour)`.
  - `InfusionCardFramePatch` (Card.SetSkillIcon) tints the frame and back renderers (via Traverse) in the element's colour on dropped/world cards, and resets pooled cards.
  - `InfusionStockFramePatch` tints the merchant card's "Frame" renderers.
  - `TomiCardTint` (after SpellBookUI LoadSpellPage / LoadInfoPage / SetEquipSlots) tints the card image wherever the paired icon is one of our twins (`RecolorEngine.KeyOf`, which reads the ramp from the twin's name). It covers the spell page (iconArray/cardImgArray), loadout (spellIconArray/cardBGArray) and info page (iconArray/cardArray).

## v0.7.1: real attack recolour; infusion moved to Tomi; persistent infused versions (built, not yet run in game)
- **Bug:** the icon was recoloured, but attacks weren't. There were two causes:
  - effect sprites are tightly packed in atlases, so `textureRect` throws, the twin failed and only a weak tint was applied;
  - `RecolorSprites` skipped any sprite with an Entity anywhere above it, which includes effects parented to the player.
- **Fix:**
  - `RecolorEngine.TightTwin`: a whole-atlas recolour (`AtlasTwin`, cached per atlas + ramp), `Sprite.Create` over the original's UV box, then `OverrideGeometry` with the original mesh. The pivot comes from vertex 0.
  - `BodyWithin` only skips bodies inside the recolour root.
  - `PreviewRecolor` (player attacks) climbs up to 3 parents (stopping at an Entity, more than 8 children, or names containing "Pool") so the whole effect is covered.
- **Tomi (`SpellBookNpc`) replaces the crystal altar:**
  - `TomiAtMerchantPatch` (SkillStoreItem.Start, once per stage) calls the merchant's own `SpawnTomi` 0.5s later unless he's already there. The fallback is `ChaosInst(SpellBookNpc.Prefab)`.
  - `TomiRunListPatch` (SpellBookUI.LoadEleSkillDict postfix): in a run, the `eleSpellDict` lists are filtered to `LootManager.currentPlayerSkills` plus equipped. During runs, `Player.skillsDict` holds every unlocked arcana, so v0.7.0's altar listed arcana the player didn't own.
  - `TomiInfusion` (follows SpellBookUI Activate/Deactivate) handles [I] presses on `currentSkill`: keep, then sacrifice, then confirm. It pays gold, calls `RemoveSkill`, removes the id from currentPlayerSkills, calls `Infusion.Set`, then refreshes (LoadEleSkillDict, LoadSpellPage(false), SetEquipSlots, LoadInfoPage). An OnGUI hint bar sits at the bottom.
- **InfusionUnlocks:** stored in `BepInEx/config/osi.wol.hybridbosses.infusions.txt` as `skillID|Fire,Water|Fire`.
  - `Infusion.Set` unlocks the version (altar, Tomi, or picking up a pre-infused arcana).
  - In the Hub, [I] at Tomi cycles the arcana under the cursor: original → each unlocked version.
  - `Infusion.TryGet` falls back to the chosen version, so it applies in runs and on icons. Run infusions override it, and Clear keeps the choices.
- Ctrl+F8 now spawns Tomi beside you.

## v0.7.0: arcana infusion (built, not yet run in game)
- **Infusion.cs state:** `infused` (skillID → ElementType) and `pending` (pre-infused drops/stock until `Player.PickUpSkill`). Cleared in the Hub (ChaosStageRunner.Update).
- **Applied in VariantAttackPatch first** (`Infusion.ApplyToAttack`). It counts as the player's attack only if there's no OwnerContext and no `OwnerOf`, and the entity is the Player (or null, category "Player", and not under an Enemy).
- **Once per AttackInfo:**
  - damage ×1.10, so re-infusing never stacks;
  - the element switches, and the strongest existing status chance (min 0.15) moves to the new element's status: Fire burn, Water/Wind slow, Ice freeze, Earth poison, Thunder shock, Chaos chaos;
  - Radiant uses `ArcanosBridge.SetElement` (Arcanos' element on a player attack, as its own arcana do);
  - Prism cycles base elements via `Prism.Next(player)`;
  - the look comes from `CouncilCorruption.PreviewRecolor` with the element's ramp.
- **Icons:** `IconManager.GetSkillIcon` postfix returns `RecolorEngine.Twin(icon, ramp)` for infused/pending ids, which covers HUD, spellbook, cards and shop.
- **Drops:** the `LootManager.DropSkill` prefix rolls `Infusion.InfusedDropChance` (0.04). `GetSkillID` postfix keeps only the latest candidate pending.
- **Stock:** the `SkillStoreItem.Initialize` postfix rolls `InfusedStockChance` (0.08). It adds a ×1.25 `costStat` mod, then calls `SetCardSprite` again.
- **Altar:** `SkillStoreItem.Start` postfix → `InfusionAltar.EnsureNear` places one per scene at the first shop item + (-4.5, -1.5), on open floor.
  - It uses Arcanos' `cristal_prisma` sprite (fallback: Fireball icon) with a shifting glow. Proximity is 2.2 units; F or E opens it.
  - The OnGUI picker pauses the game (timeScale 0). Steps: keep → sacrifice → confirm.
  - Sacrifice rules: not the signature, not the same current element, not an equipped basic/dash.
  - Cost is `80 + 40×tier` gold from `Player.goldWallet` (static). `Player.RemoveSkill(sacrifice)`, then `AnnounceSkillChanged(keep)`.
- **Test keys:** Ctrl+F8 spawns an altar beside you (Debug F8 stays the giant spawn). Config section `Infusion`: Enabled, InfusedDropChance, InfusedStockChance.
- **Not done yet:** "Fire-infused X" names in the game's own UI (spellbook/HUD text); controller input for the altar (keyboard F/E and mouse only).

## v0.6.5: recolour engine (gradient maps) for variant attacks; Ctrl+F10 infusion preview (built, not yet run in game)
- **RecolorEngine.cs:**
  - `ElementRamps`: ramps for Fire / Water / Ice / Earth / Thunder / Wind / Chaos / Radiant. `Map(key, colour)` maps a colour by brightness (70% luminance, 30% max channel, slight contrast stretch).
  - `RecolorEngine.Twin(sprite, key)`: a recoloured copy of the sprite's textureRect, built from the atlas read once by GPU blit + ReadPixels (last 2 atlases cached). Twins are cached per (sprite, key). Tightly packed sprites throw on textureRect, so they're marked failed and tinted instead.
  - `RecolorEngine.PaletteSprite`: renderers whose material has `_Palette` are never recoloured (their pixels are palette indices).
  - `RecolorSprites` (MonoBehaviour on the attack's visual root): in LateUpdate it swaps each frame the Animator sets for its twin. Static sprites are restored on removal. It removes itself when `Source.atkInfo` changes (pooled re-setup), and `RestoreAll` comes from `CouncilCorruption.RestoreTints`.
- **CouncilCorruption.TintTree:** when a ramp key exists (Chaos, Radiant, or a Prism cycle element via `ElementRamps.ForElement`), `RecolorTree` uses the engine for sprites, particles (`RecolorEngine.Map(MinMaxGradient)`: all modes), lights (ramp mid), trails and lines. A pooled root whose RecolorSprites removed itself can be tinted again.
- **Variants.Apply** passes the Prism element's key and adds `Accent`: Arcanos effects at the attack (ChispasCaos / Destellos / per-element), at most once per caster every 0.35s.
- **InfusionPreview (Ctrl+F10)** cycles a ramp onto the player's own attacks (VariantAttackPatch → `CouncilCorruption.PreviewRecolor`). It's look only; off restores.

## v0.6.4: variant stages always get tinted; arcana visuals dump (built, not yet run in game)
- **Bug:** a Prism Ice stage was planned (`CHAOS stage planned: Prism ... IceTileSet`) but never tinted. ChaosStageRunner gave up 6.5s after the scene change, and big stages finish generating (which is when PlanCurrentStage runs) later than that.
- **Fix:** the runner now applies whenever `Planned && Active == null` (from 1.5s after the scene change), with no time limit.
- **Ctrl+F9 → `Debug.DumpArcanaVisuals`** logs each castable arcana prefab's sprites and shaders, animators, particles (start colour mode/colour, texture), trails/lines and lights as `VISUALS ...`, plus a per-element summary. It's for the infusion research (ROADMAP 6a).
- **Research:** vanilla arcana visuals are Animator-driven, hand-coloured sprite sheets with particle accents on a few. The `_Palette` shader is only used for cards, enemies, outfits and some enemy projectiles (EnemyFireball.SetColor, FlameLance/TremorLance.SetPaletteIndex). The card frame/back palette row is rarity (Card.InitCardBack); the element look is the icon art.

## v0.6.3: Arcanos Radiant/Prism arcana rebuilt for enemies; Prism cycle; Arcanos ailments respected (built, not yet run in game)
- **ArcanosArts.cs** has 7 ports, registered through ArcanaCatalog (they replace any prefab entry). Each has `ArcanaDef.Port`, and ArcanaCaster.Cast dispatches ports before the prefab lookup.
  - Radiant: LanzaDeLuz, FogonazoDeLuz, HaloSagrado, LatigoDeLuz, AlasDeLuz (feathers).
  - Prism: PrismaElemental, Triada.
- Each port uses:
  - Arcanos sprites via `ArcanosBridge.Sprite` (= `Recursos.Sprite(name, 16)`; names without .png);
  - its effects via `ArcanosBridge.Fx` (`Efectos.<name>(Vector2, ...)`, args converted by reflection) and `Sound` (`Efectos.Sonido`);
  - the game's hitbox: `Globals.ChaosInst<Attack>(Attack.CirclePrefab)`, radius, `MakeEnemyAttack` (Arcanos stat data, re-owned), then `EnableForOneFrame(true, false)`. This is the same recipe as Arcanos' `ManiquiAtacante.Tajo`. Without stat data it falls back to enemy "NeutralKnightAttack".
- **Port elements are always base ones.** Radiant ports deal Neutral damage plus Flashes; Prism ports use the cycle element and its status.
- **Prism** (Arcanos `Prismatico.Ciclo`): `Prism.Next(caster)` cycles Fire → Water → Earth → Air → Lightning. Every Prism attack is converted once per AttackInfo (`prismDone`): element plus burn / freeze / poison / slow / shock at 25%. It's tinted with the element's colour (`TintTree(root, v, elementColor)`). `Variants.Apply` is the single convert-plus-tint entry, used by the patch, MakeEnemyAttack and the Council runner.
- **RadiantFlash** is the controlled player-side version of Arcanos' Flash → Blind. A Radiant port hit that really lowers the player's health adds Flashes, which are forgotten after 6s. At 3, the player is Dazzled for 2.5s: an OnGUI glare that fades. Controls are untouched.
- **Arcanos ailments:** `ArcanosBridge.Incapacitated(e)` = `Ceguera.Ciego || ParcheEstadosEnemigos.SinControl`.
  - ArcanaState.IsReady, the SignatureMeter forced cast and RivalUnstuck all skip while it's true.
  - GiantWatchdog treats it, and any state named "WolArcanos*" (its stun "WolArcanosAturdidoPorPulso"), as free.
  - Arcanos' elemental ailments (EstadoPropio: Calor, Empapado, Agrietado, Remolino, Magnetizado, Maldicion) need nothing from us: they apply to our enemies through Arcanos itself.
- **Rivals can't use player versions:** Contestant is an Enemy, while Arcanos' states are Player.SkillState (they need a Player: input, inventory, HUD).

## v0.6.2: Radiant look rebalanced; infused arcana (built, not yet run in game)
- **Finding:** the catalog had 0 castable Radiant and 1 castable Prism arcana. Almost all of Arcanos' arcana derive from its own `ArcanoDeEstado` / `EstadoDeLanza` state classes, which have no prefab EnemyArcana can fire. Only TriadaEstado (Prism) is a ProjectileAttackState.
  - Arcanos unlock rules (Desbloqueo.Vigilar text): Fulgor = beating bosses without taking damage; Prisma = the board's challenges.
  - ArcanaCatalog.LogVariantArcana logs each Radiant/Prism id with the reason it's castable or not.
- **Radiant tiles** keep more of the stage's colours (HueSpread 0.35, Saturation 0.55, ValueLift 0.18).
- **Radiant attacks:** TintTree remaps their sprites onto a brighter ramp (gold core 0.95/0.7/0.22 to pearl white, never dim) instead of a faint multiply.
- **EnemyArcana.MakeEnemyAttack** applies Convert/TintAttack after ReOwn. At SetAttackInfo time the arcana still reads as the player's, so VariantAttackPatch skipped it.
- **Variants.Named:** intros (rivals, Council echoes, giants) show infused base arcana as "Radiant X" / "Prismatic X".

## v0.6.1: never write Arcanos' element values onto enemies (built, not yet run in game)
- **Bug:** a Prism giant Coffin took no damage. Arcanos' ElementType values are huge numbers (log: Radiant 2115412493, Prism -1896111776), and setting `e.element` to one broke the game's damage lookup.
- **Fix:** ChaosGiants.Apply changes the body element only for Chaos.
- **Fix:** Variants.Convert now changes damage type only for Chaos. Radiant/Prism enemy attacks keep their own element and only get the variant's tint. ArcanosBridge.SetElement is no longer called on enemy attacks.
- **Rule:** don't put Fulgor/Prisma ElementType values on enemy bodies or enemy attacks. Arcanos' own arcana keep whatever element Arcanos gives them.

## v0.6.0: stage variants Chaos / Radiant / Prism (built, not yet run in game)
- **Variants.cs** holds the shared layer:
  - `StageVariant` {None, Chaos, Radiant, Prism}, with a `VariantInfo` per variant: prefix, titles, colours, ramp, board colours and tile recolour.
  - `Variants.Split` (shares from config), `Ramp`, `CastPool`/`DashPool`, `Convert` (damage type), `AttackVariant`, `TintBody`.
  - Components and patches: `VariantMark` (on casters; Radiant health drop), `PrismCycle` (hue-cycling bodies), `VariantAttackPatch` (SetAttackInfo postfix; replaces the old ChaosStageAttackPatch and CouncilAttackTintPatch), `VariantChest` with `VariantRewards`, and `Archons.MaybeVariant`.
- **ArcanosBridge** reads WoL Arcanos (`wolarcanos.arcanos`) by reflection; the plugin declares it as a SoftDependency.
  - `Desbloqueo.ElementoDe/IdsDe/Desbloqueado` (Arcanos' element names "Fulgor"/"Prisma").
  - `ElementosNuevos.Fulgor/Prisma` (ElementType values), `Icono(ElementType, Uso)`, `Elemento(Attack, ElementType)`. All of these are static on internal classes.
- **Plan:** ChaosRun.IsChaos is still the 15% "variant stage" roll. `VariantOf(tier, stage)` picks the variant from a second hash of the same seed, and `TierVariant` gives the Council its tier's first variant. Without Arcanos the split always returns Chaos.
- **ChaosStages:** `PlannedVariant`/`ActiveVariant`; `Apply(visualOnly, which)`.
  - `ChaosVariant` is now target-hue / rainbow recolour. The old relative hue shift turned grass red.
  - Rewards per variant. Title uses `StageTitle`. Board squares get the variant's colour and icon; plan letters C/R/P. Shift+F11 cycles off → Chaos → Radiant → Prism.
- **Enemies/giants:** humanoids and giants get a VariantMark. Chaos uses the purple row; Radiant a tint; Prism PrismCycle. They cast from the variant's pool. A Radiant/Prism giant's element is set to Arcanos' element type.
- **Catalog:** `ElementOf` returns "Radiant"/"Prism" for Arcanos' arcana via `ArcanosBridge.PrefixOf`. `Pool("Chaos")` still means every element, so the Sovereign and Chaos rivals can roll them too.
- **Council (CouncilCorruption rewritten):**
  - `Variant`, `CouncilVariant(tier)`, and line sets per variant (`ChaosLines`/`RadiantLines`/`PrismLines`). Titles are Corrupted/Radiant/Prismatic; echoes come from `CastPool`.
  - Shift+F12 cycles a forced variant.
  - **Chest:** only unlocked arcana of the variant (`ArcanosBridge.Unlocked`: the game's private lockedSkillList plus Arcanos' Desbloqueado), and lockedRate is set to 0. With none unlocked, the chest keeps its normal drop.
- **Rivals:** Radiant/Prism rivals get a VariantMark, `CastPool` arcana and `DashPool`. A Chaos melee is used as their basic if they have none, converted when swung. Own spells: Prism all, Radiant none. `StagePrefixOrRandom` gives a variant stage's rival its variant, and the off-element rival roll uses the split.
  - Their chests are `VariantChest`: unlocked arcana of their element only.
- **Archons:** `FinalRun.Pick` and the arena Archon may become Radiant/Prism (`StageVariants.ArchonChance` 0.25). ChampionPhase gives them an Ascendant/Refracted phase instead of a hybrid.
- **New config section `StageVariants`:** ChaosShare 0.5, RadiantShare 0.35, PrismShare 0.15, ArchonChance 0.25, RadiantHealthDrop 0.15.

## v0.5.15: board / stage Chaos plan agree and stay stable (built, not yet run in game)
- **GameController.stagesPerTier is 1.** NextLevelLoader goes to the boss once stageCount > stagesPerTier, so a tier has stagesPerTier + 1 = 2 regular stages, with stageCount 1 and 2.
  - Before, the board used regular = 1, giving every square its own tier, and IsChaos rejected each tier's 2nd stage.
  - Now ChaosRun.RegularStages (= stagesPerTier + 1) is used everywhere: IsChaos, board, IsCorrupted, JumpToCouncil. The board cross-checks against curMaxStagesPerTier - 1.
- **Seed:** FinalRun.Tick rolls the run once on arriving in the Hub. It used to roll on leaving, after the first board had already drawn the previous run's plan. ChaosStageRunner.Update calls Tick every frame.
- **Ctrl+F11 fallback** now uses the game's rule (stageCount > stagesPerTier → boss).

## v0.5.14: Chaos stages actually apply; all humanoids turn Chaos (built, not yet run in game)
- **Planned but never tinted:** the log had `CHAOS stage planned` but no `CHAOS stage on`. ChaosStageRunner required Level.levelContainer and gave up silently.
  - It now applies as soon as Planned, and keeps checking for 5s.
  - It logs `CHAOS stage was planned for X but this stage is Y` if the stage key never matches.
  - Apply falls back to all scene sprites when levelContainer is null.
- **Enemies:** ChaosEnemies.ArmRegular no longer rolls, so every Knight/Rogue/Archer/Mage/Summoner/Lancer turns Chaos. The ChaosStages.EnemyChance config is now unused.
- **Attacks:** ChaosStageAttackPatch (SetAttackInfo postfix, Chaos stage only, not during a corrupted Council fight) chaosifies and tints enemy attacks that carry an element.
  - It uses CouncilCorruption.TintAttack(atk, stage: true). RestoreTints is now internal, and Revert calls it.
- **Ctrl+F11:** finds an inactive NextLevelLoader via Resources.FindObjectsOfTypeAll. Otherwise it loads directly: the boss floor after the tier's last stage, else the tier scene again.

## v0.5.13: Ctrl+F11 skips to the next stage (built, not yet run in game)
- Debug.SkipToNextStage finds the floor's NextLevelLoader (the exit portal) and invokes LoadNextLevel. That picks the next regular stage, the Council floor after a tier's last stage, or the next tier, then calls GameController.LoadLevel.
- It does nothing (logs `SKIP: no exit`) on a floor without a NextLevelLoader.

## v0.5.12: Council board tint fixed (built, not yet run in game)
- The lavender Air tier (squares, icons, ring, portrait) was ours. ChaosBoardPatch tinted every Image under a corrupted Council circle, and on the wrong tier: the Earth tier had the Chaos stage, but the Air circle got the tint.
- **Fix:** Council circles are counted in board order, skipping start, final and null entries, using the same tier numbering as the squares. Only "Portrait" images are tinted (fallback: non-circle, non-dimmer images that aren't inside a stage square).
- Logs `CHAOS board plan: squares ..C... | council <circle>=corrupted(<images>)`.
- GameController.tierCount is 0-based during stages (NextLevelLoader: tier 3 after a boss → FinalBossLevel), so CouncilRoomStartPatch's IsCorrupted(tierCount) was already right.

## v0.5.11: rival room props, spacing, arrival spots; darker Chaos squares (built, not yet run in game)
- v0.5.10 in game: the rooms work, the portals spread partly, and the rival fight works.
- **Props:** rooms keep their props and run the breakable pass (randomInteractables = true; Room.Generate → Decorator.DecorateByTileSet). Clearing interactables had left black holes on obstacle tiles.
  - After Generate, any new scene-root objects are adopted under the room before it moves.
  - Chests, portals, enemies and shop/NPC/pedestal/relic components are removed.
  - Logged as `RIVAL room ...: N breakables, ...`.
- **Spot checks:**
  - The mask is limited to Wall/Pit/Obstacle/Destructible/Environment.
  - Blocked uses a real LinecastAll that ignores colliders containing the start point.
  - Clearance() measures distance to the nearest wall. Roomy() picks the most open reachable floor within 6 units, inside the room's mesh bounds minus 2.
- **Placement:**
  - Roomy() picks the selector/arena arrival points and the stage entrance, after 2 FixedUpdates.
  - Portal offsets are ±5, with 4 units minimum spacing and clearance ≥ 1; the leave portal is at (0,-5).
- **Board:** Chaos squares are almost black purple (0.17,0.06,0.26). The Air tier's own squares are lavender, which the user mistook for Chaos.

## v0.5.10: open-spot mask from the player's collision matrix (built, not yet run in game)
- The v0.5.9 log showed every linecast hitting "LowerWalls": the arrival checkpoint sits inside one, so that layer is walkable.
- WallMask is now built from Physics2D.GetIgnoreLayerCollision for the layers of the player's solid colliders, minus the player and Enemy layers. It is logged as `RIVAL open-spot check blocks on layers: ...`.
- The path check walks the line with OverlapPointAll from 1 unit out, since a linecast starting inside a collider counts it.
- Run() moves any player still more than 40 units from the arena into it. A portal spawned on the player registered the choice without teleporting them.

## v0.5.9: open-spot check uses wall layers only (built, not yet run in game)
- v0.5.8 confirmed in game: the rooms render in place, with matching collision.
- But RivalEvent.Blocked counted every solid room collider and rejected every spot. All portals stacked on the arrival point, and the rival fell back into a wall.
- Now only colliders on the "Wall"/"LowerWall" layers (WallGenerator) and BottomlessPit-tagged ones count, and linecasts use that mask.
- On failure it logs `RIVAL no open spot ... (last block: ...)`. The rival's checkpoint fallback also skips blocked spots.

## v0.5.8: rival rooms generated at the origin, then moved (built, not yet run in game)
- v0.5.7 rendered, but the art sat near (0,0) over the real stage while the room's own colliders were far off-map.
  The game's generators create parts with `new GameObject()` at the world origin and `SetParent(room)` keeping that
  spot, and `MeshGenerator.GenerateMeshes` static-batches the meshes in place.
- RivalEvent.BuildRoom now generates with the room at (0,0,0) (`RivalEvent.Building` flag), then moves it to its spot.
- RivalRoomMeshPatch (GenerateMeshes prefix, only while Building) runs the same steps without StaticBatchingUtility.Combine, so the meshes follow the root.

## v0.5.7: real room instances (built, not yet run in game)
- **Root cause of the black rival rooms:** `Room.Load(name, instantiate, parent, pos)`: with `false` it returns the PREFAB ASSET's Room (`ChaosBundle.GetWithComponent`), only `true` does `ChaosInst` + SetParent + position. 0.5.4-0.5.6 passed false, so Generate ran on the prefab asset in memory: nothing in the scene rendered, SetActive/position "didn't stick" (0.5.6 log: root active False, meshes still at asset coordinates). Now `true`, plus a `scene.IsValid()` guard. Earlier builds polluted those prefab assets for that game session (generated children added to them): restart the game after updating.

## v0.5.6: rival rooms visible
- 0.5.5 diagnostics: both built rooms sat at **(0,0,0)** (the Room.Load position didn't stick) and every generated MeshRenderer was **inactive** ("OFF": Wall 720 verts FireTileSet, layer Default/0), so the selector/arena was a black void with working colliders. Now: `transform.position = at` before and again after `Generate`, then `Activate` sets every GameObject in the room active (except EnemiesTrans/InteractablesTrans) and enables MeshRenderers (sprites keep their own state). Diagnose also logs root active state, parent and how many meshes are on.

## v0.5.5: fixes from the 0.5.4 test
- **Giant boss bar had two names:** `MiniBoss.InitComponents` already creates and claims an `EnemyHealthBar` (`MiniBoss.healthBar`, name from `bossDisplayName`). `BossBar.Attach` now borrows it for MiniBoss (renames it, sets `bossDisplayName`, never shows/destroys it) instead of stacking a second bar.
- **Chaos stages / champion changed between stages:** `FinalRun`'s run key included `GameController.levelNameList`, which the game reshuffles during a run, so it re-rolled the seed mid-run. Key = `loopCount` only; a new run is still detected by passing through Hub/Tutorial.
- **Board marks off by one:** `stagesInOrder` starts with the Start circle (square layout log: `StartCircle[...root]`). `startCircle` is now skipped along with `bossesInOrder` and `finalBossCircle`.
- **Spawned outside the start room / blue portal off-centre:** the selector was built from a **Start** room prefab, which brings the start setup with it. Start/Exit rooms are never used now, and every `Portal` inside a built room is destroyed after Generate.
- **Rival rooms (sky + invisible walls, stacked portals):** 0.5.4 rooms had walls (colliders) but no visible floor, and only 4 checkpoints, so snapping put every portal on the same spot. Now: the arena prefers Survival/RandomSurvival, then Challenge rooms (the game's fight rooms), else the largest Standard; the selector uses the smallest Standard (>= 120 tiles). `MatchStageSorting` copies the stage floor meshes' sorting layer/order onto ours (fix attempt) and `Diagnose` logs mesh count/bounds/verts/material/sorting layer, colliders and checkpoints per room (`RIVAL room ...`). Portals and the rival use `OpenSpot`: arrival + offset, rotated/shortened until no BottomlessPit-tagged or room collider is within 0.7 and no linecast hit from the arrival, one portal per 2 units; placed 2 FixedUpdates after building so colliders are registered.

## v0.5.4: own rival portal + rooms, board marks that stick
- **Rival event no longer uses the misc room** (user request). `RivalEvent` (RivalEvent.cs): at the end of stage generation (`LevelGenPatch` adds a one-shot handler to the static `LevelGenerator.levelGeneratedEventHandlers`, invoked at the end of `Generate` after SetStartPosition/SetExitAndStorePositions), with `RivalWizards.Chance`, a Portal opens on a random stage `Checkpoint` 15-70 units from `Level.startPosition` (any checkpoint >= 6 if none), with an "A rival's portal has opened" card. It leads to a **selector room** (3 tier portals tagged `RivalPortal.Event` + a white portal back) and then the **arena**; 3s after the rival dies a white portal leads back next to the entrance. The entrance and selector portals are destroyed once a tier is picked (one fight per portal). F11 = the same event beside the player (element cycle kept).
- **Rooms:** `Level.LoadedRoomDict` (name -> Room template of this stage's tileset). `Room.Load(name, false, null, pos)` instantiates `Room.prefabPath + name + ".prefab"` from the bundle; `Room.Generate()` builds the room's own floor ("Meshes", then StaticBatchingUtility.Combine), walls ("Colliders"), pits, decorations and checkpoints under its own transform (`Globals.FindOrCreateGameObject(name, parent)` = parent.Find, so nothing global is touched; `CheckpointGenerator.DestroyOldCheckpoints` is local too). Arena = largest Standard room (>= 120 tiles), selector = a Start room, else the smallest Standard. Before Generate: randomEnemies/randomInteractables/randomPits off, generateCheckpoints on, enemy/factory/interactable lists cleared, EnemiesTrans/InteractablesTrans children destroyed. Placed at start + (0,-420) and (0,-780). The camera isn't clamped to the level (only `Level.AvgTileGridPosition` reads min/max). Checkpoints = safe floor spots: arrival = checkpoint nearest their centroid; portals and rival snapped to checkpoints.
- The misc-room hook (`TryReplaceMiscRoom`, `RivalArena`) and the Ctrl+F11 `ArenaLab` experiment are removed.
- **Board marks that stick:** the board recolours reached squares after Enable, so `BoardMarks` (component on the board) re-applies marks every LateUpdate (keeping the board's alpha) and restores originals on the next Enable. A Chaos square = root Image dark purple (0.32,0.12,0.5) + its element icon swapped for `finalBossCircle/ChaosIcon`'s sprite, tinted (0.85,0.6,1). Corrupted Council portrait images lerped 0.6 toward purple. `ChaosBoardPatch` has `[HarmonyPriority(Priority.First)]` (it clears the marks; the champion patch adds Sura's portrait mark after it). Logs the square layout once.
- **Empty final circle on champion runs:** the game keeps Sura's `BossPortrait` disabled (the circle shows the Chaos symbol). Champion runs now enable it (kept enabled through a `show` mark) and tint it in the element colour.

## v0.5.3: Chaos stage fix, Sura board tint, friendlier voicelines, arena experiment
- **Chaos stages never triggered naturally (bug since 0.4.2):** `GameController.stagesPerTier` = 2 regular stages (the boss floor is its own "<X>BossLevel" scene, loaded when `stageCount >= stagesPerTier`), and `stageCount` is 1-based during a stage (`LoadLevel` adds 1 when a tier scene loads). `IsChaos` rejected `stage >= per - 1`, i.e. every in-game stage. Now canonical = 0-based regular index: `IsChaos(tier, idx)` valid for 0 <= idx < stagesPerTier; in game idx = stageCount - 1 (no Chaos on BossLevel scenes / boss rooms). The board walks `stagesInOrder` skipping `bossesInOrder` + `finalBossCircle`: k-th square = tier k / stagesPerTier, idx k % stagesPerTier (`curMaxStagesPerTier` is 3 because it counts the boss). Council markers go on `bossesInOrder[t]`. The user's screenshot showed every tier's 2nd square purple: the old Council marker (i % per == per - 1 with forcing stuck on).
- **Ctrl+F12 = one fight only** (`ForceOnce`, consumed by the next Council room); Shift+F12 stays the lasting toggle.
- **Board icon for champions:** no more overlay arcana icon. Sura's `BossPortrait` multiplied toward the element colour (lerp 0.7), `BossCircleInner` element darkened, the ring multiplied, `ChaosIcon` kept only for the Chaos Sovereign. Originals cached per Image and restored on Sura runs.
- **Voicelines:** rewritten in the game's upbeat mentor tone (each member dabbled in Chaos and wants to drop their element; when beaten they come back to it). No glitching any more (`Corrupt()` unused; whispers dropped from dialog).
- **Arena experiment (Ctrl+F11, `ArenaLab`):** copies every renderer/collider within 14 units of `Level.miscRoomPortalInPosition` (skipping bodies/UI/objects bigger than 60 units) to 400 units below, opens a Portal beside the player to it (`createReturnPortal`), logs scene roots, the level container's children, what was copied and what was skipped as whole-level. Purpose: decide whether a dedicated rival portal can lead to our own copied arena room in the same scene (stage progress untouched) instead of borrowing the misc room.

## v0.5.2: Corrupted Council polish
- **Dialog fix:** the 0.5.0/0.5.1 glitching never showed. `DialogManager.GhostWrite` types `DialogMessage.message` directly (ParseMessage's result isn't what's typed), and the first Council line has both portraits active (`left FireBoss (True), right Player0 (True)`), so the one-speaker check skipped it. Game data (`core` asset, dialog JSON): Council dialogs `<Boss>-Intro` / `<Boss>-Win` are 2 messages, all spoken by the member (left), player portrait silent; later messages inherit speakers. Now `CouncilDialogPatch` (Activate prefix) rewrites `__0.message` with a custom line (`CouncilCorruption.Lines[FireBoss|IceBoss|EarthBoss|AirBoss|LightningBoss][intro=0|win=1][index]`, index from `dialogDict[id].messages`), lightly glitched; originals kept in `rewritten` and restored in `End()`. Postfix tints the left portrait and the text. The ParseMessage patch is gone.
- **Echo aim fix:** Chaos Beam (AtTarget) landed beside the Flame Empress: bosses' `closestTargetInfo` can be empty, and `ArcanaCaster.Cast` fell back to `origin + dir*4`. `ArcanaCaster.TargetOf(caster)` = target info if its `hurtBoxTransform` is set, else the nearest living player; used by Cast, Chain re-aim, the finisher's late re-aim and the echoes.
- **Room tint:** `ChaosStages.Apply(visualOnly: true)` 1.6s into the corrupted room (after ChaosStageRunner's scene-change revert); no rewards/arming; boss floors with no "Meshes" container take every non-body mesh, and every scene sprite (not Entity/Canvas) as decoration when there's no `Level.levelContainer`. Reverted in `End()`.
- **Darker Chaos tint:** attacks multiply (0.46,0.2,0.72); particles/trails/lines remapped by brightness onto a ramp from near-black (0.16,0.03,0.28) to violet (0.72,0.32,1) (`ChaosRamp`); lights (0.45,0.15,0.8); boss body (0.62,0.42,0.85). Effects without an Attack: baseline of all renderers/particles when the boss is found, then every 0.2s anything new that isn't UI, an Entity body, or under a root carrying a player Attack gets `TintTree`. All originals recorded and restored in `End()` (pooled effects outlive the room). PoolManager.GetPoolItem is generic, so it isn't patched.

## v0.5.1: Corrupted Council gameplay + jump key
- **Reward = a Chaos arcana.** The Council chest: `BossRoomEventHandler.ActivateExitPortal` instantiates `TreasureChest` with `lootTableName = BossRoomEventHandler.bossTreasureChestID` (static). `TreasureChest.Break` -> `LootManager.DropLoot(table, pos)` -> `DropSkill(pos, amount, skillID, lockedRate, sigRate, elementSet, bool, bool)` (an explicit skillID is used as is). `CouncilChestPatch` (Break prefix/finalizer) sets `CouncilChestOpening` for the corrupted room's chest; `CouncilChaosArcanaDropPatch` (DropSkill prefix) swaps `__2` for a `LootManager.chaosSkillList` entry not in the private `currentPlayerSkills` (amount at least 1), once.
- **Chaos damage:** `Chaosify(AttackInfo)` on every non-player attack in the fight (SetAttackInfo postfix + the 0.2s scan): `elementType = Chaos`; the strongest of burn/slow/poison/shock/freeze chance becomes `chaosChance` (level kept, min 1), the elemental ones zeroed.
- **Chaos echoes** (instead of replacing a move: boss patterns are `Boss` states in `attackPatternList`, driven by `Boss.TransitionToNextState`): 2 Chaos-element arcana (no melee/dash/summons/wards) picked when the boss is found. `CouncilEchoPatch` (TransitionToNextState postfix): 45% per attack end, 5s cooldown (first after 6s), outline flash white -> purple over 0.6s, then `ArcanaCaster.CastAll(boss, def, aim)` (can roll enhanced). Intro card once `bossSpawned` and the dialog is closed: "Corrupted <name>", echo icons, "Chaos echoes · Chaos damage · drops a Chaos arcana".
- **Ctrl+F12** `JumpToCouncil`: on a regular stage of a run, `Forced = true`, `stageCount = stagesPerTier`, `GameController.LoadLevel(bossLevelNameDict[Application.loadedLevelName])` (the scene NextLevelLoader loads after a tier). Shift+F12 turns forcing off again.

## v0.5.0: Corrupted Council
- Rule: a tier's Council member is corrupted when any regular stage of that tier is Chaos (`CouncilCorruption.IsCorrupted(tier)` over `ChaosRun.IsChaos`, same seed, so board and fight agree). Config `ChaosStages.CorruptedCouncil`. Test: Shift+F12 toggles `Forced` (every Council fight; applies to the current room too; all boss circles purple on the board while on).
- Council fights = `BossRoomEventHandler` (fields boss, bossIntroDialogID, bossVictoryDialogID, playBossDialog...; `BossIntro` coroutine instantiates `bossPrefab`, `DialogManager.HandleDialog(bossIntroDialogID)`, `Boss.ExitIntro`; `BossOutro` -> victory dialog). **`FinalBossRoomEventHandler` derives from it**: the Start postfix skips it.
- `CouncilRoomStartPatch` (Start postfix) -> `Begin` + `CouncilCorruptionRunner`: polls `boss`, then `CorruptBoss` (SpriteRenderer colour x (0.78,0.55,1), purple `_OutlineColor`, boss bar `SetName("Corrupted " + BossDisplayName)`), and every 0.2s tints all active non-player `Attack`s (pooled boss attacks get their info before we see them). `CouncilAttackTintPatch` (SetAttackInfo postfix) tints new ones at once. `TintAttack`: visual root = parent Projectile, else the attack's parent if it's not an Entity and has <= 6 children, else the attack; sprites x purple, ParticleSystem `main.startColor`, Lights, Trail/LineRenderers; once per root.
- Dialog: `DialogManager.ParseMessage` postfix corrupts the text when `currentDialogID` is the room's intro/victory ID and the active speaker isn't a player (`*Player*` speakerID; logs the speaker IDs once): plain-text glitches (`#%&*`, stutters), tags left intact, 50% a whisper suffix, seeded by the line. `Activate` postfix: boss portrait tinted, message text lerped to purple while the boss speaks (restored otherwise). No custom rich-text tags (lines are ghost-written char by char).
- `ChaosBoardPatch` also tints the boss circle (`i % per == per - 1`) of corrupted tiers.
- Visual/dialog only; moves and stats unchanged.

## v0.4.13: rival rewards, arena Archons, stun-safe watchdog
- **Rewards (user spec):** Easy 1 standard chest, Medium 2 standard, Hard or arena Archon 1 gold, the champion replacing Sura none. `e.dropLootChest = false` (the game's drop is always gold `TreasureChestMiniBoss`); `RivalRewards` spawns `Globals.ChaosInst<TreasureChest>(TreasureChest.Prefab, null, pos, null)` 1s after death, side by side via `Entity.GetAvailableNearbyLocation`, `preventMimic = true`. Standard = the prefab's own loot table; gold = `lootTableName = "TreasureChestMiniBoss"` (`TreasureChest.SetChestType`: "MiniBoss" -> MiniBoss, "Boss" -> Boss, "TreasureChestSmall" -> Mini, "TreasureChestElemental" -> Elemental, else Standard).
- **Arena Archons:** `RivalArena.Run`, Hard portal: `RivalWizards.ArchonChance` (0.25) -> `Rivals.Spawn(..., RivalTier.Final, arenaArchon: true)` ("Fire Archon" / "Chaos Sovereign" on Chaos stages), full champion kit incl. phase 2; `mk.ArenaArchon` -> gold chest.
- **Watchdog vs stun:** giant stun = "Hurt" (MiniBossHurtState: armour break, each combo hit re-enters Hurt and `HandleDurationIncrease` extends it, combo counter/achievement on exit), then "Ready" (MiniBossReadyState, 0.5-0.75s, `AllowHurtState`). Free list now also has Ready/Regen/Stealth, any name containing Hurt/Frozen, any state whose class derives from *HurtState; the timer resets while free. Recovery goes through "Ready" (the giant's own recovery), Chase only if there's none.

## v0.4.12: champion phase 2
- `ChampionPhase` (ChampionPhase.cs), attached at the end of `Rivals.Intro` when tier == Final and `FinalBoss.Phases`. Once, at `health.HealthPercentage <= 0.5`: enabled=false for 2.2s, invulnerable, ShakeCamera(2), `OverdriveEffects` burst (temporary instance, destroyed after 3s).
  - Archon: second element = random other prefix. Keeps its 2 highest-priority arcana; the rest are removed from `availableEnemySkillsDict` (FSM keeps the states); +3 `PickVaried` of the second element's pool via `EnemyArcana.Give` (tier cooldown/windup). `mk.SecondPrefix` set (SignatureAllowed then also allows that element's own Contestant spells; Blizzard stays removed since MakeHostile already dropped it). `mk.GapMultiplier` 0.85 (used in `Rivals.MarkCast`). Title "Fire–Water Archon".
  - Sovereign: +2 arcana from any element, gap x0.7, title "Chaos Sovereign, Unbound".
  - New signature arcana among the gained ones if the old one was dropped (`SignatureMeter.SkillID` updated, hits kept). BossBar renamed, IntroUI "... awakens" card with the gained arcana. Log `FINAL phase 2: ...`.

## v0.4.11: giant intro icons + giant watchdog
- "?" icons: `IconManager.GetSkillIcon` returns a placeholder for unknown IDs (borrowed enemy skills like EarthKnightPoisonBolas have none), so the intro's null-icon tint fallback never kicked in. `IntroUI.SkillIcon` now compares against the placeholder (`GetSkillIcon("HybridBossesNoSuchSkill")`) and returns null. `IntroUI.EnemySkillIcon`: hand map `EnemyIconMap` (FlameShield->FlameShieldDash, IceBeam->UseAquaBeam, DrillCharge->UseEarthDrill, ...), else the shortest catalog SkillID ending with the skill's core name (PoisonBolas->UsePoisonBolas, FireWall->SummonFireWall...), else an element-coloured tile.
- Crash (giant) Knight stuck in its charge on an Earth giant, cause unknown. Suspects: borrowed `EarthKnightExplosionShieldState` (plays "ChargeAlt" then "Charge", sets invulnerable + floorCollider trigger + ignore knockback until OnExit; IsReady needs HP < 62.5%), or the giant's own Ready -> Dash charge. `GiantWatchdog` (all giants): same non-free state > 6s -> EndMovement + ChangeState("Chase") (runs OnExit), logs `GIANT <class> stuck in '<state>'`. Use that log line to find the real cause.

## v0.4.10: giant skill gaps filled
- `ElementalGiants.Apply`: when a giant isn't an arcana giant and installed fewer than `ElementalGiants.MinElementMoves` (2) borrowed SkillDefs, `GapArcana(kind, prefix, missing)` gives player arcana of its element. Families by style (`GapFamilies`): Knight Spell/Jump/Ward, Archer Projectile/Spell, Mage Projectile/Spell, Rogue Projectile/Jump/Spell, Lancer Spell/Projectile; one per family first, then any non-melee/non-summon arcana of the element. Uses the giant cast gap (`EnemyArcana.CastGap`) and enhanced rolls; no dash arcana, no signature.
- Counts from the Skills table: 0 borrowed = Wind/Thunder Knight, Fire/Thunder Archer; 1 = Fire Mage, Water Knight/Mage/Archer, Wind Archer, Lancers have 2 (Fire Lancer 0). The log line `GIANT ... skills added N, arcana M` shows the top-up.

## v0.4.9: boss health bars
- The game's boss bar is `EnemyHealthBar` (`Assets/Prefabs/UI/EnemyHealthBar.prefab`, static `Prefab`). `Boss.InitComponents`: `Globals.ChaosInst<EnemyHealthBar>(Prefab, null, null, null)` (no parent: the prefab has its own canvas), `Claim(this)`, `SetActive(false)`, `SetName(bossDisplayName)`; `BossIntroState.OnEnter` -> `FadeIn()`. It updates health/shield itself; its `Release` is private.
- `BossBar` (BossBar.cs) does the same for our enemies: `Attach(e, title)` hidden at the start of `Rivals.Intro` (rivals + champions), `Show()` when the intro ends; `ShowFor` for giants when `GiantIntro` shows. One shown at a time (newest wins), destroyed 1.5s after death. Fallback parent = first root screen-space Canvas if the prefab has none (logs "BOSSBAR parented to canvas ..."). Config `BossBar.Enabled`, `BossBar.Giants` (both true).

## v0.4.8: player-style signature, axe/water-arc fixes
- **Signature rework (user rule: only a signature arcana can spend it, and it fires that arcana's different signature version).** From `Player.HandleOverdrive` IL: meter full -> overdriveReady (OverdriveEffects aura `ToggleAura(true)` + `Burst(10,true,false,true)` = "PlayerOverdrive" sounds, outline, the signature skill's `cooldownRef.Reset()`), decays and expires ("PlayerOverdriveExpire"). Casting the signature skill while ready: `isUltimate = true`, `TimeScaleController.StandardFreezeIntoEaseOutTimeScale()`, invulnerability, `Burst(40,false,true,true)`, ShakeCamera(1). Signature-capable arcana = states whose ctor sets `hasSignatureVariant` (list in `Signature.SignatureStates`, from `tools/fieldwriters.ps1 "Player+SkillState" hasSignatureVariant`; matched to catalog rows by `GenArcana.ClassName`).
  - Rivals: `RivalMarker.Signature` = highest-priority capable arcana in its loadout, else the last arcana is swapped for a capable one of its pool. Intro shows "(signature)". `SignatureMeter`: hits -> Ready (aura via `OverdriveEffects.Prefab` + `Initialize(color*1.6, transform, sprite material)`), Ready for `TierSignatureSeconds` (now 8/10/12/14) then expires; while Ready it forces the signature ArcanaState out of Chase/Idle every 0.5s if `IsReady()` (gap bypassed in `ArcanaState.IsReady`). `Signature.Consume` in `ArcanaState.ExecuteSkill` fires the cue and returns true -> `CastAll(..., signature: true)`.
  - The old timed "everything empowered for N seconds" window (the random glow the user saw) is gone, and so is the cast-gap halving.
  - Enhanced vs signature split: `ArcanaCaster.SignatureCast` (static, set in SafeCast) -> `SetAttackInfo(..., isUltimate: SignatureCast)` and `isUltimate` field only on signature casts; `empowered` (enhanced: isEmpowered, extra shots, +30% dmg) can now roll for rivals too (`EnhancedArcanaChance`). Log tags ", SIGNATURE" vs ", enhanced". Finisher int args use the signature level (+1).
- Earth Axe basic floated: `EarthAxeBasic.InitThrow(Vector2, bool)` (spins out, starts `StartTimer`) added to the finisher. `EnsureLifetime`: any `MeleeAttackObject` with neither `useAnimTime` nor `useLifeTime` after launch gets a life timer (lifeTime or 1.2s).
- Water Arc NRE: `Projectile.CreateProjectile` needs the Projectile on the prefab root; WaterArc keeps it on a child -> instantiate the prefab and wire the child. Finisher `Defaults`: WaterArcProjectile arcRange 5 / flyTime 0.25 (from `CreateWaterArc`). `InitPrefabFields` now also fills parentTrans/followTrans/orbitTrans (only if empty), targetVector/atkVector/attackVec/attackVector, spawnPos.

## v0.4.7: arcana finisher
- User: a Chaos Sovereign's Hammer Throw left the hammer floating. We never run the player's skill state, only spawn its object, and many states keep driving that object: `Player+UseHammerThrow` creates the `EarthHammer`, spins it, then `ThrowHammer` -> `InitThrow(isUltimate ? 5 : 4)` (sets `updateVelocity`, starts the hit bursts) + FireProjectile. Without that it never moves.
- `tools/driven.ps1` lists, for every `Player+*` state, the calls/field writes on its prefab classes. `ArcanaFinisher.Finish` (called at the end of `ArcanaCaster.Cast`) runs a table of launch steps by prefab class (Init/Initialize/InitStrike/InitBounce/InitJavelin/Activate..., args filled by type: int level, bool empowered, Vector2 dir, Transform/GameObject/Entity caster). "Late" steps (InitThrow, ThrowAxe, ReleaseBolas, FrostWing Release, EarthCascade.SetReleaseVars, AquaBlitzBall.Activate) run 1 frame + 0.1s later, re-aimed at the target, then `updateVelocity = true` + `RefreshFlightTime`. Charge shots (any `released` bool + currentCharge/maxCharge) are released at full charge, again after Start. Runs inside OwnerContext. Logs `ARCANA finisher <Class.Method>: ran/failed/not found` once each (DebugKeys).
- `EnemyArcana.Set` uses a quiet field lookup (`ArcanaFinisher.Field`); AccessTools.Field logged a warning per missing field per cast.

## v0.4.6: rival boomerang + walking in place
- The "cyclone" every rival had (user: "Cyclone_Boomerang") is the Contestant's own `SwordThrowState` (`Assets/Prefabs/Projectiles/SwordThrow.prefab`, a spinning boomerang). `SignatureAllowed` treated it as neutral; now SwordThrow = Wind (like Tornado), unknown own spells = false, Chaos keeps all.
- `RivalAttackRedirectPatch`: an off-theme own spell (Fireball/Tornado/SwordThrow) is rewritten to "Chase" (`ref string __0`) instead of cancelled, so a finished state is never left running; skipped only when already in Chase.
- Walking in place, root cause from `ContestantChaseState.Update` IL: `MoveToTarget` returns true in attack radius -> ChangeState("Attack"); we skip it while the basic cools down, so the rival stood in Chase with Run looping (Chase replays Run only on facing change). `RivalUnstuck.FixAnimation`: in Chase, still > 0.15s -> play "Idle"+facing; moves again -> "Run"+facing.
- Also: `ArcanaState.PlayCastAnimation` ("Cast"/"Attack"+facing, logs `ARCANA <class> <id> animation: ...` once with DebugKeys), basic cooldown 0.5.
- Contestant FSM states (InitFSM): Idle, GoBack, Chase, Attack (ContestantWindSlashBasic), Hurt, Dead, Fall, Frozen, Dash, Tornado, SwordThrow, Fireball, FrostMageSummonBlizzard.

## v0.4.5: rival fixes + chaos enemy chance
- Chaos: Lancer added to `ChaosEnemies.ChaosTypes`; per-enemy roll `ChaosStages.EnemyChance` (0.5), rolled once (ChaosArmed added either way).
- Rival "walking in place": `Enemy+AttackState.OnEnter` does `attack.SetAttackInfo(skillCategory, attackSkillID="ContestantWindSlashBasic")`; with our `skillCategory = globalEnemySkillCategory` that data doesn't exist, so the basic swung for nothing in a loop. Now each rival gets `mk.Basic` (a Melee-family arcana of its element, `ArcanaDef.Basic`: windup 0.3, cd 0.9, range 0-4, registered with auto=false, exempt from the cast gap). `RivalAttackRedirectPatch` (prefix on `FSM.ChangeState(string,bool)`): for a rival FSM (`RivalBasics` map), "Attack" -> its basic state (looked up in `FSM.states`) if ready, else skip; the built-in basic never runs. Melee arcana are removed from the regular loadout. `RivalUnstuck`: in Chase, moved < 0.2 for 1s, player > 3.5 away -> `CalculateDashVector(player)` + ChangeState("Dash"), 2.5s cooldown.
- Signature rework (`RivalKit.cs`): `SignatureMeter` activates when full -> outline, `CameraController.ShakeCamera(1.5, true)`, DelayCasts(0), a window of `TierSignatureSeconds` (4/5/6/7) where `Signature.Active` = every arcana empowered and `MarkCast` gap ×0.5. It forces its highest-priority non-basic, non-dash ArcanaState via `Transition(false)` once it's in Chase/Idle. OnGUI: draining bar + "SIGNATURE!" burst. `Signature.Consume/Ready` were removed.
- Audit logs: `RIVAL kit (everything it may cast): ...` after MakeHostile; `RIVAL own spell: <state>` for the Contestant's Fireball/Tornado/SwordThrow.

## v0.4.4: chaos versions of the elemental enemy types
- User: Knight, Rogue (thief), Archer, Mage (wizard), Summoner have elemental versions on other stages, so on Chaos stages they get 1 chaos arcana + their purple palette row. `ChaosEnemies.IsChaosType` walks the class chain (covers FlameKnight etc.); not MiniBoss or rivals. `ChaosRegularEnemyPatch` (Enemy.Start postfix) arms one frame later via coroutine (`ArmRegular`: PurpleRow / tint, 1 arcana at ×1.6 cooldown). Lancers were not asked for.

## v0.4.3: chaos stage corrections (user dev notes)
- Regular enemies on Chaos stages get NO chaos arcana. Giants (MiniBoss) become Chaos giants: `ElementalGiants.TryMakeElemental` -> `ChaosGiants.Apply` when `ChaosStages.Planned` (no stage element, no SkillDefs, no summon swap). element = ElementType.Chaos, 2 arcana from `ChaosEnemies.Pool()` (PoolExact "Chaos", minus Minion/Seeker/Ward), GiantIntro with prefix "Chaos".
- Purple palette: `ChaosGiants.PurpleRow` reads the material's `_Palette` texture (RenderTexture readback), one row per `_PaletteCount` (rows along Y if height == count, else columns). Row order (direct or flipped) is chosen so the Water row is bluish and the Fire row reddish. Picks the most violet (hue ~0.78) non-element row; score < 0.25 -> tint fallback. Cached per texture. Config `ChaosStages.GiantPalette` (-1 auto) overrides.
- The Enemy.Start chaos hook was removed: it ran inside MiniBoss.Start before the palette-3 reset. Late giants go through GiantStartPatch. `ChaosStages.Apply` still calls `ChaosEnemies.ArmAll` (giants only) for the Shift+F11 toggle.
- ROADMAP.md was tidied by the user's request; their dev notes are tagged 💬 there (dedicated rival portal on X-2 stages, PvP arena, evil echo of the last run, Radiant/Prismatic stages, Arcanos safekeeping copy = ask the author).

## v0.4.2: CRASH FIX + chaos stages (built, NOT yet run in game)
- **Bug in 0.4.0/0.4.1 ("borked the game", Compass.LateUpdate NRE spam, WoL Arcanos patch failures):** `PatchAll` ran in Awake. Harmony makes Mono compile each original; Mono runs static constructors of classes the method touches. `FinalBossRoomEventHandler.Start` reads `LootManager.chaosSkillList`, so patching it ran `LootManager..cctor` before the asset bundles were loaded. That cctor builds items (`DamageUpAtLowHealth.get_Prefab` -> `ChaosBundle.Get`) -> NRE -> TypeInitializationException -> LootManager dead for the session (no loot, broken level setup, Compass NREs). Our Awake also aborted, so no "loaded" line.
- **Rule from now on: never patch in Awake.** `Plugin.PatchWhenGameReady` coroutine waits for `ChaosBundle.bundleLoaded` (+1 frame), then PatchAll, DiscoverModded, ownership patches and runners. ChaosBundle's own cctor is harmless (dictionary + fields). WoL Arcanos (installed, v0.57.0, 192 arcana, Spanish logs) defers its patches the same way.
- Chaos stages, scoped by the user: purple tiles/lights + enemies with Chaos arcana; the rival arena on that stage = Chaos rival; better rewards; "Chaos" title; board marker. Council corruption (tinted attacks, sprites, dialogue) is future work.
  - `ChaosRun.IsChaos(tier, stage)` = hash(FinalRun.Seed, tier, stage) < `ChaosStages.StageChance` (0.15, new key); never the last stage of a tier, Hub/Tutorial/PvP/boss rush/final room.
  - `ChaosStages.PlanCurrentStage` runs in the LevelGenerator.SetExitAndStorePositions postfix, before TryReplaceMiscRoom (StagePrefixOrRandom -> "Chaos"; MakeHostile uses `ArcanaCatalog.PoolExact("Chaos")` first). `Planned` is only valid for the stage key it was made for. The runner applies 1.5s after load.
  - Apply: atlas recolour (single "Chaos" variant), decoration tint, lights, `LootManager.globalGoldModifier` / `itemDropModifier` ×RewardMultiplier (mods HB_ChaosGold / HB_ChaosDrops, removed on Revert), `ChaosEnemies.ArmAll` + `Enemy.Start` postfix (1 Chaos arcana for regular enemies at ×1.6 cooldown, 2 for MiniBoss; skips Boss, rivals, critters).
  - `get_StageTitle` postfix -> "Chaos " + title. `ChaosBoardPatch` (GameProgressBoard.Enable postfix): stagesInOrder[i] with IsChaos(i / perTier, i % perTier) tinted purple (dimmers skipped).

## v0.4.1: loading-screen champion icon + chaos stage prototype (broken by the Awake patching bug; fixed in 0.4.2)
- User clarified: the placeholder icon belongs on the **loading screen**'s progress board (3 stage bosses, then Sura's Chaos icon), not in the room.
- `GameProgressBoard` (LoadingScreen.gameProgressBoard): per-element `*BossCircle`/`*Dimmer` fields plus `finalBossCircle` (RectTransform) and `finalBossDimmer`; `Enable()` -> InitializeTierOrders, SetPlayerColors, ToggleFade, SetPlayerPiece.
- `FinalRun` (FinalBossPool.cs): per-run decision. Reroll when in Hub/Tutorial (flag), or when the key `GameController.loopCount|levelNameList` changes. `FinalChampion.Roll` reads it. `ProgressBoardPatch` (postfix on Enable): tints the circle's own Image, disables its child Images (except the dimmer), adds child "HB_ChampionIcon" Image (sibling index at the dimmer). Logs the circle layout once (`FINAL board circle layout: ...`) for tuning. Config `FinalBoss.RoomBadge` (off).
- Chaos stages: `TileSet.material` = one atlas; `MeshGenerator.GenerateMeshes` builds the meshes under "Meshes", then `StaticBatchingUtility.Combine`. `ChaosStages.cs`: RenderTexture readback -> HSV remap (per-colour cache) -> cloned Material on every MeshRenderer under "Meshes"; SpriteRenderers under `Level.levelContainer` without an Entity parent get tinted; all Lights blended. `ChaosStageRunner` (DontDestroyOnLoad) resets on stage change and rolls `ChaosStages.Chance` (default 0) 1.5s after load. Shift+F11 cycles.
- Check: whether tile shaders sample `mainTexture` (if no visible change, find the shader's texture property), gamma vs linear on readback (washed-out colours = use the Linear readwrite), cost in ms, the board layout log.

## v0.4.0: final-stage boss pool (built, NOT yet run in game)
- User wants: champions sometimes instead of Sura, a placeholder icon showing "you won't face Sura", and the normal ending plus Chaos arcana unlock when the champion is beaten.
- Game flow: Sura is a scene object "FinalBoss"; `FinalBossRoomEventHandler.Start` sets `boss` = it. `Update`: when boss fsm "Dead" and not all players dead and !bossDefeated: if finalBoss.chaosMode && !secondFormStarted -> ChaosIntro; if superModeAvailable && superMode && !thirdFormStarted -> SuperIntro; else bossDefeated -> AnnounceVictory -> `BossOutro` (`LootManager.GetLockedChaosArcana` -> `Player.HandleSkillUnlock`, achievement) -> `ActivateExitPortal` (RunData timestamp, RunHistoryEntry.Save, DeathSummaryUI, LevelSummary "Ending"). `FinalBossDeadState.OnEnter` with chaosMode=true takes the final ("DeadHoodless") branch.
- `FinalBossPool.cs`: postfix on `FinalBossRoomEventHandler.Start` -> `FinalChampion.Roll` (config FinalBoss.Enabled/Chance 0.35/ChaosSovereignChance 0.3). Component polls room `bossSpawned` -> HideSura (enabled=false, invulnerable, renderers and colliders off, every EnemyHealthBar hidden) -> `Rivals.Spawn(pos, prefix, RivalTier.Final)` + `Rivals.Intro` (shared intro, refactored out of RivalArena). On champion down -> Sura chaosMode=true, superMode=false, room secondFormStarted=thirdFormStarted=true, `sura.fsm.ChangeState("Dead", false)`.
- `RivalTier.Final` (TierHP 2600+200*diff, 5 arcana, gap 1.1, no mini-boss chest). `FinalChampionBadge` OnGUI top-right placeholder.
- F12 debug. Encoding note: PowerShell `Get-Content -Raw` without `-Encoding utf8` mangles UTF-8 ("·" -> "Â·"); use the Edit tool or pass -Encoding utf8.
- Risks to check: Sura's lamps/orbs or BossIntro leftovers during the champion fight; whether her death state's time-freeze or "DeadHoodless" anim is visible (she's hidden); badge timing (room Start may run at floor load).

## v0.3.2: rival fall protection
- User: v0.3.x "working great". Asked for player-like fall protection on rivals.
- Player: `Fall.Update` keeps `preFallPos` (last ground position); `Player+PlayerFallState` on finishedFalling -> `TakeFallDamage` (AttackInfo "Pit", `Player.FallDamage` stat) -> after fallDelay 1s respawn at `Entity.GetAvailableNearbyLocation(preFallPos, false, false)`. `ContestantFallState.Update` calls base `FallState.Update`, then teleports to `myOrigin` and goes to Chase.
- `RivalFallPatch` (RivalKit.cs): postfix on `Entity+FallState.Update`; for rivals, once per fall: myOrigin = safe spot (restored next frame), HP -= max(player FallDamage, 5% max HP) (never below 1), 1s invulnerability.

## v0.3.1
- User rule: **signatures only for the player, rivals and the Council members** (the Council has its own signatures in its movesets; leave them alone). Everyone else can use *enhanced* arcana. Giants no longer get a SignatureMeter. `ArcanaState.ExecuteSkill`: with a meter -> `Signature.Consume`; without -> roll `EnemyArcana.EnhancedChance` (0.2) for an enhanced cast (same empowered path).

## v0.3.0: dash arcana, signature, summons, wards (built, NOT yet run in game)
- User asks: rivals dash and charge a signature from hits like the player (so dash arcana work), and summon arcana + wards must be in ("the Summoner already summons elemental agents").
- Game recipe found: `Summoner+SummonerMinionState.SummonEnemy` (ChaosInst minionPrefab, name += skillCategory, `parentTrans`, scale 1.25, `isEnemy = true`, `SetSkillInfo(cat, "<El>SummonerMinion", 1, false)`, useSummonDuration false, useBurst if diff>0, startingHealth 35) and `SummonerSeekerState` (SetTransforms(parent, parent, null), SetThrowPosition, isEnemy, `attack.SetAttackInfo(cat, "<El>SummonerSeeker")`, health 5). All 5 elements have both stat IDs. Ctors need `Summoner`, so the recipe is reimplemented in `Summons` (ArcanaCaster.cs). Summons run WITHOUT OwnerContext (that would invert their native enemy targeting).
- Catalog: `tools/gencatalog.ps1` now emits families Minion / Seeker / Ward and kinds Minion / Seeker / Ward (Excluded = buffs + heal only). `ArcanaDef.EnemyStatID` and `Castable` (summons need enemy stats). `ArcanaCatalog.Pool` excludes Dash; `DashPool` holds them.
- New files: `ArcanaCaster.cs` (`ArcanaCaster.CastAll/SafeCast/Cast`, `Summons`, `SummonLeash`, `ArcanaState` moved here), `RivalKit.cs` (`DashArcana` + patch on Contestant+DashState / Enemy+DashState OnEnter/OnExit; `Signature`/`SignatureMeter` with OnGUI bar and outline; `Attack.ExecuteAttack` postfix counts hits on Player colliders).
- Ward: Delivery.Ward = AtSelf + `MakeHostileObject` (Ally* names -> Enemy*, layers) + Ward.spawnPosition.
- Empowered: `SetAttackInfo(..., isUltimate: true)`, isEmpowered/isUltimate bool fields set on prefab MBs, shots up, scale 1.2, damage ×1.3, windup ×1.4.
- Config: `EnemyArcana.ExcludeFamilies` (new key, so existing cfg files don't block new families).
- Check in game: `RIVAL <el> pool: N arcana, dash <id> (of N), signature after N hits`, `ARCANA ... summoned ...`, `SIGNATURE ... ready/unleashed`, whether minions/seekers attack the player, ward hurtbox breakable, dash effects placement.

## v0.2.0: Enemy Arcana v2 (built, NOT yet run in game)
- User goal: rivals and giants get ALL thematically relevant player arcana (no limiting variety). No off-element arcana (no fire on a thunder rival), but keep a "jack of all trades" rival. Also pull in modded arcana.
- Files: `ArcanaCatalog.Generated.cs` (generated, 233 rows; regenerate with `tools/gencatalog.ps1 -OutFile ArcanaCatalog.Generated.cs`), `ArcanaCatalog.cs` (runtime build: element via `StatManager.GetSkillData("Player", id)` + `AttackInfo.GetInfoFromData(player...)` `.elementType`; family -> Delivery defaults; `EnemyArcana.Tuned` overrides; modded discovery + `IlScan`), `OwnerContext.cs` (dynamic Harmony prefix+finalizer on Awake/Start/OnEnable/Update/FixedUpdate/LateUpdate/OnTrigger*/OnCollision* and iterator MoveNext of every prefab class in the catalog, plus Projectile/Wave/ThrownItem; swaps `GameController.enemies` and `players` while owned code runs; tags spawned objects; `Attack.Awake` tagging; `SetAttackInfo(AttackInfo,int)` handling).
- `EnemyArcana.cs` rewritten: Delivery Projectile/Wave/AtTarget/AtSelf/Melee; Shots/Spread/ShotDelay; `InitPrefabFields` by name; each shot runs inside `OwnerContext.Enter(parent, true)`. The `Risky` list is gone (ownership replaces it). Config `EnemyArcana.Families`.
- Rivals: `ArcanaCatalog.Pool(prefix)` ("Chaos" = all elements) + `PickVaried` (spread over Delivery, or Element for Chaos). `SignatureAllowed`: Fireball only Flame, Tornado only Wind, Blizzard only Frost, Sword Throw all; Chaos keeps all (enforced in the HandleCooldowns postfix and Blizzard removal). Config `RivalWizards.ChaosChance` 0.15. F11 cycles 6 pools.
- Log lines to check: `ARCANA catalog: N castable arcana (Flame x, ...)`, `ARCANA ownership patches: N methods on M prefab classes (ms)`, `RIVAL <el> pool: N arcana`, `ARCANA ... cast <id> [element, family/delivery xN, attacks k]`, `disabled for this session`.
- Known risks: swapping enemies/players inside owned code may confuse arcana that summon allies; Melee basics spawned unparented may look odd (remove "Melee" from Families); Dash family spawns at self only (no dash movement); ownership patching cost at startup (logged ms).

## v0.1.2 (not tested; superseded by v0.2.0)
- User test of 0.1.1 (Thunder Medium): the rival "used wind cyclone and thunder dragon, hurt himself, never tried to kill me". Log: SummonLightningStorm `attacks 0` (spawns strikes later via its own skillCategory/skillID fields + validTargets = enemies). ShockDragon targets `GameController.enemies` via GetClosestTarget. Robe Vigor logged "HP -40%".
- Only one attack layer exists (`ChaosCollisions` "Attack"), so targeting is purely attack info (targetNames / IsAllyCheck / IsSkillOriginator), not physics.
- Scratch tool `risk.ps1` (IL scan per class: SPAWNS / SETINFO / TARGETS / PLAYERREF) produced `EnemyArcana.Risky` (19 skill IDs, off unless `EnemyArcana.IncludeRisky`).
- `ArcanaOwned` component on every spawned arcana root. The SetAttackInfo postfix re-owns "Player" calls under an ArcanaOwned parent. `Attack.CheckCollision` prefix: if the attack is ArcanaOwned, or atkInfo.entity is a rival, and the target collider's Entity is an Enemy -> false.
- `ContestantSpellStates`: prefix/postfix on Fireball/Tornado/SwordThrow `OnEnter` + `Update` set `Rivals.CurrentCaster` (saved and restored).
- Robe fix: `isIncrease` is private (FieldRef). Factor: |v|<=1 -> 1±v, <=3 -> v, else flat/500. Each robe mod is logged (`RIVAL robe mod ...`) for tuning. `ARCANA <type> targets: ...` is logged once per caster.

## v0.1.1 (tested: Blizzard gated, but self-hits; see v0.1.2)
- User test of 0.1.0: robes look great. Problems: arcana "static" (log: Wind rivals always rolled from the same 3; the Hard rival never cast any; Medium cast once) and an unannounced Blizzard at fight start. Relic pool logged only 8 stat relics.
- Cause: `Contestant.InitFSM` registers `BlizzardState` "FrostMageSummonBlizzard" (windup 0, cd 10, extra skill). `ContestantChaseState.Update` calls `HandleExtraSkills(true,false,false)` before its own spell logic, so Blizzard bypassed the gap and won on priority.
- Fix: `Enemy.HandleExtraSkills` prefix (rivals: skip while the gap runs) and postfix (on true: `Rivals.OnSkillUsed` -> MarkCast + reshuffle every skill's `priorityLevel`, the used one to 0). Blizzard is removed from `availableEnemySkillsDict` unless Frost. Intro shows "SummonBlizzard" for Frost.
- Catalog grew to ~40 (see EnemyArcana.Catalog). `EnemyArcana.Broken` blacklists any arcana whose Cast throws. Tier arcana counts 2/3/4.
- Chest: `EnemyDeadState.EnemyDropLoot` instantiates `TreasureChest.Prefab` and calls `LootManager.DropLoot(chestLootTableID...)` when `dropLootChest`. Contestant's chestLootTableID is already "TreasureChestMiniBoss", so MakeHostile sets `dropLootChest = true`.

## v0.1.0: pacing, intro reveal, robes and relics (tested: robes look great)
- User test of 0.0.9: rivals work (F11 arena, tiers, MakeHostile) but "insta-cast everything". Cause: Contestant ctor cooldowns (tornado 6, swordThrow 5, fireball 8, dash 1.2 with 4 charges), ready flags true at spawn, casts after AnimPlayed(0.2). Plus full player damage. The Thunder rival logged no ArcanaState casts at all (unknown why; check).
- Pacing: `Rivals.CanCast/MarkCast/DelayCasts` (per-enemy next-cast time). `ArcanaState.IsReady` also checks CanCast, and OnEnter does MarkCast. `Contestant.HandleCooldowns` postfix forces tornado/sword/fireball ready=false during the gap; OnEnter postfix on its 3 spell states does MarkCast. Tier tables in `Rivals` (TierCastGap, TierDamage, TierWindup, TierCooldown). Giants use config `EnemyArcana.CastGap` (2.5).
- `IntroUI.cs`: OnGUI title, subtitle and icons revealed left to right (0.55s apart, pop-in); returns its duration. Sprites drawn via `GUI.DrawTextureWithTexCoords` on `sprite.textureRect`. Lookups: `IconManager.GetSkillIcon/GetItemIcon/GetWizardSprite`, `TextManager.GetSkillName/GetItemName/GetOutfitName` (all fail soft).
- `RivalLoadout.cs`: `Outfit.OutfitList` -> random robe; `outfitColorIndex` -> `_PaletteIndex` on the rival sprite (Contestant.InitElement uses 24, i.e. the player sheet; unverified it's the same sheet). `OutfitModStat` (modType + add/multiModifier.modValue) -> `RivalBonus`. Relics: `LootManager.availableItemIDList` (private, via Traverse) -> `Item.CreateItem(id)` class name -> `RelicEffects` table (29 classes). Bonus: Hp/Damage/Cooldown/Speed multipliers, Armor/Evade via stat AddMod, Crit via ReOwn. Config `RivalWizards.Robes/Relics`.
- `GiantIntro.cs`: attached in `ElementalGiants.Apply`; shows the giant's installed SkillDefs and arcana within 9 units, once. Config `ElementalGiants.Intro`.
- Check in game: robe colour (pink = wrong sheet), relic pool size in log (`RIVAL relic pool: N`), icons null for enemy skills (they fall back to coloured squares), outfit stat magnitudes (log line `bonuses:`).

## v0.0.9: Rival wizards (tested by user: works, needed pacing)
- `RivalWizards.cs`. Postfix on `LevelGenerator.SetExitAndStorePositions` -> `Rivals.TryReplaceMiscRoom`: find the `RoomType.Misc` room via `Level.GridRoomTupleList` (Tuple `.Second` = Room component), hide `Interactables/*` except `Portal`, then `RivalArena.Create(Level.miscRoomPortalInPosition, stage prefix or random)`.
- RivalArena: 3 `Portal.Prefab` instances (fields set via Traverse: targetLocation, targetSpawnOffset, color, createReturnPortal) tagged with `RivalPortal`. A `Portal.OnTriggerEnter2D` prefix calls `Choose(tier)`. Coroutine: wait 1.2s, `Enemy.Spawn("Contestant", ...)` + `RivalMarker` (added before Start), wait 2 frames, `enabled=false`, invulnerable, camera override, OnGUI banner, wait IntroSeconds, enabled=true.
- `Contestant.Start` postfix -> `Rivals.MakeHostile`: rename AllyHurtBox/AllyFloorContact back, layers Player->Enemy and AllyFloorContact->EnemyFloorContact, `skillCategory = Enemy.globalEnemySkillCategory`, allies->enemies lists, `targetGroup = GameController.players`, HP via `NumVarStatMod("RivalHP", hp, 10, Override, true)` on `health.healthStat`, arcana via `EnemyArcana.Give(e, defs, cdMult)`.
- Contestant's own states use "Player" stats: a prefix/postfix on `Contestant.Update` sets `Rivals.CurrentCaster`; an `Attack.SetAttackInfo(string,string,int,bool)` postfix calls `EnemyArcana.ReOwn` while it's set. `ReOwn` is idempotent (the damage multiplier is applied once per AttackInfo). The target-list template falls back to `NeutralKnightAttack` in `globalEnemySkillCategory`.
- Config `RivalWizards.Enabled/Chance(0.3)/IntroSeconds(4)`. F11 opens an arena at the player.
- Risks to check: misc-room NPCs might spawn after generation (then still visible); portal colours applied in Awake before our fields are set; portal/rival offsets vs room size; rival palette; Contestant states that SetAttackInfo outside its Update (coroutines) won't be re-owned; whether the dodge logic still makes sense when hostile.

## v0.0.8: Enemy Arcana (built, NOT yet run in game)
- New file `EnemyArcana.cs`: `ArcanaDef` (element, player skillID = arcana `staticID`, prefab class with static `Prefab`, Delivery Projectile/AtTarget/AtSelf, windup/cd/range/prio) and `ArcanaState : Enemy.SkillState`. OnEnter: base + `movement.EndMovement` + `windupStopwatchID = ChaosStopwatch.Begin(windup,...)`. ExecuteSkill: base (queues exit, `exitTime` 0.4) + spawn. Projectiles go through `Projectile.CreateProjectile(parent, prefab, pos, rot, null)`, then set parentObject/parentEntity/moveVector; others use `Instantiate`. Every child `Attack` goes through `MakeEnemyAttack`.
- **Team fix:** `Attack.SetAttackInfo("Player", id)` gives real stats, but `GenerateAttackInfo` credits a "Player" category to `GameController.playerScripts[0]` and uses the player's `targetNames`. So we overwrite skillCategory, entity, gameObject and targetNames (from `StatManager.GetSkillData(enemy.skillCategory, enemy.attackSkillID)` + `AttackInfo.GetInfoFromData`). `IsAllyCheck` only skips when the category starts with "Player". `ExecuteAttack` uses atkInfo as-is (no regeneration).
- Arcana catalog dump: all `Player+*` skill states with staticID and prefab classes; regenerate with an IL scan of `get_Prefab` calls and `.cctor` strings. ~200 entries (including Basic/Dash variants).
- SuperCoffin: `GiantKind` "" (no borrowed enemy skills), `UsesArcana` and `NoElementPalettes`, so it keeps palette 3 and gets `Tint` (sr.color, _Color, _OutlineColor/_OutlineColor2). Config `EnemyArcana.DamageMultiplier` (1.0) and `EnemyArcana.AllGiants` (false).
- Things to check in game: projectiles hit the player and not enemies; AtTarget prefabs (BoulderDrop, Tornado, WaterBlast, LightningStrike) may need init that the player state normally does; some prefabs may follow the player or a cursor; tint/outline visibility; whether MiniBoss armor flashes clear the outline.

## v0.0.7 (coffin skills + summon swap)
- Coffin skills as above. `SwapSummonMinions`: the giant's "Summon" SkillState `enemyPrefabKey` is swapped to `<Prefix><Key>` if that prefab exists (SkeletonMage -> Mage). F10 rerolls add skills cumulatively (debug only).

## Current plugin (v0.0.6 base)
- Feature: Harmony postfix on `MiniBoss.Start` plus every giant's own `Start`; applies only from the most-derived one. On an elemental stage the giant always takes the stage element (`MatchStageElement`, default on). On neutral stages `NeutralStageChance` (0.5) picks a random element. Sets `element`, palette on all child palette-swap sprites, and adds that element's matching enemy skills (Knight->Knight set, Summoner->Mage set, Coffin colour only). Config in `BepInEx\config\osi.wol.hybridbosses.cfg`.
- Hotkeys: F5 grouped enemy list + giant skill lists, F6 cycle palette, F7 field dump, F8 spawn next Super* giant beside player, F9 dump element->palette, F10 cycle nearest giant through the five elements.

## Next steps
1. In a run: F8 to spawn each giant, F5 to see `GIANT ... -> element, palette, skills added N` and `GIANTSKILLS`, F9 for the palette table, F10 to try every element. Watch the log for `Failed to add` / exceptions while giants use borrowed skills (likely failure: missing animation names on the giant's animator, or a skill casting `ent` to its own class).
2. Fix whatever breaks; consider giving giants the element's `attackSkillID` damage element, an element-coloured aura/outline, and covering Wind/Thunder Knight (no transplantable skills yet) by borrowing Rogue/Lancer skills.
3. **Hybrid mini-boss/boss:** subclass `MiniBoss` or `Boss`, register states in `InitFSM` the way `FinalBoss` does, borrowing attack states from `FireBoss`/`IceBoss`/`EarthBoss`/`AirBoss`, with a phase list (element, states, palette) and an element particle/palette change as the telegraph. Read `FireBoss` (simplest) first.
4. Rival wizards and extra final bosses: after studying the PvP classes and `FinalBossRoomEventHandler`.

## Files
- `Plugin.cs`, `WolHybridBosses.csproj`: current plugin and build
- `reference/decompiled_FinalBoss_Boss_MiniBoss_EnemyFactory.cs.txt`: **missing from the folder** (never saved, as was the earlier v0.0.5 draft). Re-export from ILSpy if needed. Was: ILSpy output (FinalBoss ~lines 1-3200, Boss ~3210, MiniBoss ~4103, EnemyFactory ~4502). Strip CRLF before grepping.
- Decompiling: ILSpy -> expand `Assembly-CSharp` -> `-` namespace -> right-click class -> Save Code.

## v1.1.1: centred bounty cards, the Primordial Alchemist (built, not yet run in game)
- **Bounties:** BountyBoard.Announce queues a centred IntroUI card (waits while IntroUI.Busy or a menu is open): Hub arrival (2.5s delay, 6s), run start (1.5s, 5s), each progress step (3s). The in-run top-left line is gone; the Hub keeps its line.
- **Primordial Alchemist:** WhiteAlchemist.Name is now "The Primordial Alchemist" (class names unchanged). WhiteAlchemist.Monologue runs in FinalChampion.Swap before Rivals.Intro: a self-introduction, a line on the player's dual-infused arcana (else single infused, else none), a line on the most-carried element (Infusion.CurrentElement over assignedSkills), and a challenge; boss frozen, invulnerable, camera on it. Then a RivalBanter with RivalVoice.Primordial gives half-health and defeat lines.
- **LegendAPI** is a hard dependency (manifest + BepInDependency); Arcanos stays soft (README: recommended). MantleRobe keeps its no-LegendAPI fallback.
- **Nocturne:** NocturneInteractPatch prefix (Free on, not yet signed up) sets CurrentDialogIndex to 2 (Traverse property) so lines 0-1 (the 10-gem pitch) are skipped; line 2 signs up at cost 0, then line 3 (Thanks) and the preset menu.
- **Team match ally:** Contestant.targetEnemyGroup = true right after Enemy.Spawn (Contestant.Start reads it: Ally hurtbox/floor names and layers, skillCategory EnemyOnly). AllyHealth then sets targetGroup = enemies, currentGroup = allies, moves it from GameController.enemies to allies. EnemyArcanaFriendlyFirePatch lets enemy-vs-enemy hits through when exactly one side is in GameController.allies. The 1.1.0 ally was hostile (log: ARCANA Contestant targets AllyHurtBox...).
- **Team match flow:** BuildRoom(..., largest) picks the biggest Survival/Challenge/Standard room. Ally and both rivals spawn at once, frozen; Rivals.IntroCard (freeze, card, camera, invulnerable for card + extraHold) per rival, then Rivals.Release for all. Rivals.Intro = IntroCard + wait + Release.
- **League board:** LeagueMenu (on the Echo host object) finds the Hub statue as the largest enabled SpriteRenderer whose name / sprite / parent name contains "statue" (logs `LEAGUE statue: ... candidates` once); Interact within 2.8 of its bounds opens it, F2 anywhere in the Hub. BountyBoard.OnGUI is gone.
- **Test mode:** Plugin.TestMode (latched once Debug.Hotkeys is seen on): Progress.Save and InfusionUnlocks.Save skip; NoTestUnlocksPatch skips Player.HandleSkillUnlock(SkillState, bool) (picked-up arcana, final Chaos arcana); NoTestChaosUnlockPatch makes LootManager.GetLockedChaosArcana return empty. The game's relic / robe unlocks are not covered.
- **Test keys:** Alt+1 tournament, Alt+2 team, Alt+3 ForceAlchemist + JumpToFinal, Alt+4 FinalRun.ForceArchon + JumpToFinal, Alt+5 RivalEvent.OpenArchonHere (forceArchon). Alt+F9 is NVIDIA ShadowPlay's record key, the likely reason it did nothing.
- **Ally attacks now:** the Contestant prefab has onlyTargetPlayers = true, and Entity.GetClosestTarget then skips any target not tagged "Player"; AllyHealth clears it.
- **InRun is a whitelist:** Fire / Ice / Earth / Air / Lightning (+ "BossLevel") and FinalBossLevel. PlayerRoom (your house, where Tomi's book was filtered to held arcana), PvP, PvPArena, menus count as home. BountyBoard's home/run switch uses !InRun.
- **Final room on champion runs:** FinalChampion.Roll sets the room's playBossDialog = false (BossIntro then skips the walk-up, torches and dialog block). Update hides Sura's renderers as soon as the room's boss is known. Swap: HideSura, LightTorches (regularModeTorches + every Torch, Toggle(true), staggered with MageFireballRelease), camera on the throne, Arcanos "Teletransporte" flash, spawn, Monologue (Alchemist: WhiteAlchemist.Monologue; Archons: ChampionSpeech.Monologue with ArchonLines.IntroLines), Rivals.Intro, then RivalBanter (Primordial / Archon voice).
- **Archon look (Archon.cs):** ArchonLook.Setup at the end of MakeHostile for Tier Final (not Alchemist, not Echo): RecolorBody with the prefix's ramp (Prism keeps its own look) + WhiteAura.Attach(owner, mat, colour) in the element colour.
- **Portal labels (PortalLabel.cs):** OnGUI text under rival / trial / way-back portals within 14 units.
- **Alt+6 instakill:** TestKeys.Tick (from Plugin.Update while hotkeys are on) sets every GameController.enemies member above 1 HP to 1 HP every 0.2s.
- **Loading board, Alchemist runs:** Sura's portrait and ring are multiplied by WhiteAlchemist.White (about 0.96-1.0), so they look almost unchanged; only the Chaos icon is hidden.
- **Sura hidden from the start:** FinalChampion.HideSuraEarly runs every frame until the swap (room boss, else finalBoss, else FindObjectOfType<FinalBoss>): renderers off, Boss.healthBar inactive, no damage numbers. Doing it once let her intro states and the bar's FadeIn turn them back on.
- **Arena tint (ArenaTint.cs):** called in Swap after LightTorches with "Pure" (Alchemist, bright lights) or the prefix ramp (Prism: lights only). MeshRenderers outside entities, UI and our objects get mainTexture = RecolorEngine.PaletteTwin(texture, key). Torches and the FinalBossArena / FinalBossStageObjects / FinalBossWalkup roots go through RecolorBody (inactive children too). Lights are lerped to the colour, and the camera background is shifted.
- **Board portrait:** ProgressBoardPatch swaps BossPortrait's sprite for RecolorEngine.Twin(original, "Pure" or ArchonLook.RampForPrefix), restoring the original sprite each Enable; it falls back to the old tint.
- **Final victory fix:** FinalBossRoomEventHandler.Update returns early while finalBoss.chaosMode is true (waiting for the Chaos form) and only then declares bossDefeated. Finish set chaosMode = true for the DeadHoodless branch, so the room waited forever. Now, a frame after ChangeState("Dead"), chaosMode = false and the room's superModeAvailable = false; the log says `FINAL victory declared`, or warns.
- **Castle tint:** ArenaTint.ApplyFor runs at FinalChampion.Roll (+2 frames, TintSoon) and again in Swap. One pass over scene roots: palette sprites get a PaletteTwin palette, others a Twin sprite (else a Mid tint), meshes a PaletteTwin texture, lights a lerp (once; torch lights every pass). Instance ids are tracked per scene so nothing is recoloured twice. The log line includes the ms it took.
- **Merchant:** InfusionStockPatch first offers one of InfusionUnlocks.Versions(skillID) at UnlockedStockChance (0.5, ×1.1 price), then the old random InfusedStockChance roll.
- **Robe icon:** IntroUI.RobeIcon(row): IconManager.GetWizardSprite takes a pose name (the old call passed an outfit id and got a random raw pose). It takes the private WizardSprites table (Traverse), a front pose, and maps each pixel's red channel to a column of the player's wizard _Palette at the robe's row (mk.RobeColor), cached per row.
- **Copied enemy sprites (ghost, after-images, soul):** EliteGhost.UsePalette gives the copy a new Material(src.material) and tints/fades through _Color; FadeOut sets _Color too.
- **Tint skip list:** ArenaTint.Skip also leaves alone anything under a component named like Npc / Store / Shop / Merchant / Item / Pickup / Loot / Chest / Portal / Pedestal / Price / Gold / Gem / Interact / Prompt / Emote (cached per transform, cleared per scene).
- **Home:** TomiInfusion.AtHome = Hub or PlayerRoom. BountyBoard waits on any other non-run scene (title, loading, PvP), and a queued card is dropped there. ModifiersMenu opens only AtHome and not over the League / Nocturne menus.
- **Team match targeting:** after Release, both rivals get targetGroup = GameController.allies and onlyTargetPlayers = false, so they fight the ally too.
- **Cleanups:** WhiteAlchemist.Monologue calls ChampionSpeech.Monologue; Bounties.Reward is shared (BountyBoard, LeagueMenu); Bounties.ShowUntil is removed.
- **Roadmap:** a "Planned" list: alternate endings per champion, primordial robes from Archons, league rank rewards, choosing your ally, a bounty choice.
- **Primordial robes (PrimordialRobes.cs):** 8 LegendAPI outfits (AlchemicalPrimordial<Prefix>), unlocked by Progress robe.<Prefix>, set by PrimordialRobes.Unlock from FinalChampion.Finish (non-Alchemist) and RivalEvent.Run (an arena Archon beaten). The customMod callback sets the worn prefix. PrimordialRobeWearer (Echo host) recolours the player's palette sprites to the ramp (Prism: aura only), adds an aura in the element colour and a cdStr mod (robe Cooldown) on equipped arcana whose current element (or second element) matches. PrimordialRobeAttackPatch (SetAttackInfo postfix, Priority.Last, after the infusion) applies Damage and Status once per AttackInfo on matching player arcana (looked up in Player.skillsDict).
- **Grandmaster's Robe:** a LegendAPI outfit, unlocked at League.RankIndex >= TopRank (dynamic). GrandmasterRobeHitPatch on Health.TakeDamage raises the AttackInfo damage ×1.3 for that hit and puts it back in the postfix, when the attacker is the Player and the target Entity is a Contestant or Boss. Gold aura through PrimordialRobeWearer. The rank-up card announces it.
- **Epilogues:** ChampionEpilogue.Play runs at the end of FinalChampion.Finish after the victory (playBossDialog = false already skips Sura's victory dialogue in BossOutro): it waits for cards, speaks three last lines (per element, or the Alchemist's), then shows an "Ending: ..." card. The game's Ending scene is unchanged.
- **Hit flair (HitFlair.cs):** HitFlairPatch on Health.TakeDamage (player only). A landed hit means health went down or the method returned true; it returns true only on a killing blow. Attacker = info.entity ?? the Entity argument. It flairs for ElementalNeutral-marked enemies, blobs, ghouls and MovingStatue (golems). The element is info.elementType, else the mark's element, else Enemy.element (Neutral: nothing). A ParticleSystem built in code (Sprites/Default + white texture) bursts per element, at most once every 0.12s.
- **Council board portraits:** ChaosBoardPatch marks corrupted / infused members' portraits with RecolorEngine.Twin(sprite, ramp) (ForElement / ForVariant; Prism falls back to the old 60% blend).
- **README** brought up to 1.1.1 (eleven specials, champions and endings, tournaments, league, robes, merchant, progress file).
- **True form (AlchemistTrueForm.cs):** WhiteAlchemist.Setup first calls RememberPalettes, which keeps each palette sprite's original _Palette, then turns white. At HealthPercentage <= 0.25 with Earned (every robe in Forms(): the 6 base, + Radiant with Arcanos): freeze 3s, a line, a card, heal to 60%, gap ×0.65, then NextForm every 9s: PaletteTwin(original, ramp) per sprite, the aura swapped, the previous form's two arcana dropped (mk.Arcana + availableEnemySkillsDict "Arcana" + id), two of the new element given (EnemyArcana.Give). Alt+7 sets every robe (TestMode: memory only) and jumps to the Alchemist final.
- **Gear reactions (GearReaction.cs):** ForRival / ForArchon / ForAlchemist pick a line from the worn Primordial robe (same element or another), White Mantle, Grandmaster's Robe, or the Grandmaster rank. Rivals: FollowRival 3.6s after the greeting (duel, tournament and team Challenger; not the Echo). Arena Archon: SayLater. Final Archon / Alchemist: a line before the challenge in their intro.
- **Journal:** LeagueMenu page 1 (Tab / Left / Right / A / D / pad Skill3 switch; Up / Down / W / S / pad Skill1 / Skill2 scroll, 10 rows): InfusionUnlocks.UnlockedIds with Versions > 0, sorted by name, each with its icon, the coloured versions, the pair count and Tomi's selection (Label). Page 0 says when the true form is earned.
- **Release:** 1.1.1 is the Thunderstore update (CHANGELOG 1.1.0 is folded into it). The zip holds manifest.json, icon.png, README.md, CHANGELOG.md and WolHybridBosses.dll at the root, like 1.0.0.
- **Tomix (Tomix.cs), v1.1.2:** TomiInfusion.SpawnAt(at, makeTomix = true) returns the SpellBookNpc, and Tomix.Make adds TomixMark (a name label over him), renames him HB_Tomix and does RecolorBody "Radiant" (gold). Runs: TomiAtMerchantPatch spawns him. Hub / house: BountyBoard starts Tomix.BesideTomi on each home scene change (+2.6 x from the first SpellBookNpc). TomixInteractPatch (SpellBookNpc.Interacted prefix) skips Tomi's dialogue for him: a line in RivalSpeech, openedByTomix = true, the "Straighten" anim, PlayerRoomUI.CurrentUI.OpenSpellBook(player, pos). TomiInfusion.Opened consumes the flag into `infusing`; Update / OnGUI do nothing unless infusing, so Tomi's book is vanilla. TomixJournal (on the TomiInfusion object), Hub only: J / pad Interact toggle; Left / Right / pad Skill1-2 for the element page; Up / Down / pad Skill3 to scroll (wraps). Entries are singles of that element and pairs containing it. [I] handling pauses while it's open. The League board journal page is removed.
- **Conversations:** ChampionSpeech.Converse(speaker, lines, colour) pauses players (GameController.PauseAllPlayers(true / false)), says each line with a long timer, and advances on Space / Enter / E / F or pad Confirm / Interact. The first press calls RivalSpeech.ShowAll (finish typing), the next one moves on; it auto-advances after 4.5s + 0.06s per character. RivalSpeech.Skippable draws a blinking "Confirm ▶". Monologue (Archons, Alchemist) freezes the boss with 600s invulnerability for the whole conversation, cleared after. The epilogue uses Converse too.
- **True form fix:** it started during the intro because health right after spawning is still the base wizard's (well under 25% of the real max), and Awaken re-enabled the boss mid-monologue. It now needs ChampionPhase.Shifted (phase 2 finished), owner.enabled, and health seen above 27% first. The card lists the element names (no null-icon squares).
- **Version 1.1.4** (1.1.1 may already be uploaded; 1.1.2 / 1.1.3 were built, maybe not uploaded).
- **Tomix's book filter:** TomiInfusion.BookFilter(p): in a run, OwnedIds; at home with Tomix's book open, or opening (Tomix.Opening: the flag is still set while the game builds the lists inside Activate), only ids with InfusionUnlocks.Versions > 0; otherwise null (Tomi's book is vanilla). It's used by TomiRunListPatch.Filter (eleSpellDict / eleOverdriveDict) and the AddSkillToInfoSkills prefix.
- **Tomix 1.1.2 look and place:** TomixMark keeps his original palettes (at Start) and every 1.6s maps them onto the next ramp (Fire, Water, Earth, Wind, Thunder, Chaos, Radiant). Plain sprites go through RecolorSprites. A HitFlair burst of that element plays when the player is within 14. BesideTomi waits up to 10s for Tomi, then Opposite(): the largest non-entity sprite under Tomi (his planter), then the nearest sprite with the same sprite name 2.5–16 away at a similar height, at Tomi's offset. Fallback: +3 x. Log: `TOMIX placed in <scene> at ...`.
- **Tomix's book text (TomixBookText on the TomiInfusion object):** while TomixOpen at home, it scans the UI Texts under sbRef / the ui each second. "x/y" counts become "N infused": N = eleSpellDict[el] count after the filter, with the element read from a row label FIRE / AIR / EARTH / LIGHTNING / WATER / CHAOS / RADIANCE / PRISM (English only). Any "arcana" becomes "Infusions" / "INFUSIONS". Originals are kept and restored once it's not his book (Tomi shares the screen). Labels drawn as images (the book's "Arcana" title may be one) can't change. The hint bar copy was rewritten.
- **Tomix fixes (1.1.2):** Opposite() skips sprites named like arrow / pickup / shadow / prompt / icon / glow / light / halo / emote / cursor / card, plus entities and UI. It tries the stands under Tomi (area 1.2–30, within 2.5 x, 0.3 above to 4 below) from the largest down, until one has a same-sprite twin 3.5–18 away within 1.5 y. It logs the candidates ("TOMIX: Tomi floats over ..." / "no twin ... (candidates: ...)"). The old run picked ArcanaPickupFront / UpArrow. TomiRunListPatch.Filter now also trims infoEleSkillTupleList (the card view's element tabs, built in LoadEleSkillDict): unrelated basics leaked into Tomix's Earth cards. TomixJournal ignores toggles for 0.6s after his book opens (the opening Interact press popped it open). HubPress refuses when SpellBookUI.currentFocus is Player / Element / Tab, then refreshes the book and calls AnnounceSkillChanged (HUD icon), logging `INFUSION version <id> -> <label> (focus ...)`.
- **1.1.2 hotfixes:** reverted the infoEleSkillTupleList trim (the book keeps indices into those lists, and it crashed HandleInfoSelection with ArgumentOutOfRange). SpellBookEmptyTabGuard is a Finalizer on HandleInfoSelection that swallows ArgumentOutOfRangeException from empty filtered tabs (logged once). The Hub places Tomix at Tomi + (8.5, 0): the matching planter, measured from screenshots; the sprite search found no candidates there. Other scenes use Opposite() and +3 x. His look is now a soft tint on the original sprite colours, fading between element colours (Fire, Water, Earth, Wind, Thunder, Chaos, Ice) every 1.8s with a HitFlair burst per element. The full-ramp twin had flattened his plain sprite into a yellow blob.
- **Hub version switch refresh:** TomiInfusion.Refresh calls LoadEleSkillDict only in runs. At home, rebuilding it after [I] (outside the book's own tab flow) put a basic (EarthSpikeBasic) into the Earth spell grid as an unreachable card; LoadSpellPage / SetEquipSlots / LoadInfoPage redraw the icons. Tomix's Hub spot (Tomi + 8.5 x) is confirmed on the matching planter.
- Tomix book refresh: redraw with LoadSpellPage(currentFocus == Overdrive); the signature page and the normal page list different cards (the normal page includes basics such as EarthSpikeBasic). Shock Assault (TeleStrike) draws its bolt with a pooled SectionedTrailEmitter ("LightningLine"), outside the attack, so `InfusionTrailColourPatch` colours Emit's colour argument (__8) from `Infusion.RecentKey` in the same frame as an infused cast.
- Status mapping: slowChance is the game's Air status (Health.HandleAirStatusEffect); Water and Ice use freezeChance (Infusion.SetStatus, PrimordialRobes). ElementRamps.Map pulls saturated bright yellows (lightning art) into the ramp's middle, except for the Thunder and Radiant ramps. The infused lightning trail swaps its particle texture for RecolorEngine.AtlasTwin and maps its colour over lifetime, then restores both on the next emit that isn't infused (the pool is shared with enemies). Tomix in the Hub breaks the planter Tree he lands on (global::Tree.Break; UnityEngine also has a Tree class) and clones Tomi's TeleportEffect.
- **1.1.3 late fixes:** TomiRunInfoListPatch always lets through the SpellBookUI.currentSkill being opened (the card view indexes infoSkillList at IndexOf(currentSkill); with no infused arcana Tomix's filter left it out and UpdateInfoCards crashed at -1). ChaosStages.Apply skips decorations and lights by ArenaTint.Skip (now internal: entities, UI, NPCs, shop wares, chests, portals; lights carried by the player or NPCs), and keepCache is capped at 20000 entries.
- **MenuInput.cs (1.1.3):** Hold(menu, open) is called every frame by NocturneMenu, ModifiersMenu and LeagueMenu. The first open menu calls GameController.PauseAllPlayers(true); the last one closing calls PauseAllPlayers(false), except while a spellbook is open (TomiInfusion.MenuOpen keeps its own pause). Vertical/Horizontal read ChaosInputDevice.GetMoveVector with a 0.38s / 0.13s repeat. Inside a spellbook the modifiers menu keeps the arcana-button controls, because the stick would also move the book.
- **Chaos look for non-humanoid sheets (1.1.4):** ChaosGiants.EngineLook (SuperCoffin, Cyclops including the Giant Cyclops, Coffin) skips PurpleRow and uses RecolorOrTint("Chaos") plus ElementalNeutrals.TintLaser(cyclops, "Chaos"), in both ChaosGiants.Apply and ChaosEnemies.ArmRegular. A humanoid with no purple row now gets RecolorOrTint instead of a flat Tint.
